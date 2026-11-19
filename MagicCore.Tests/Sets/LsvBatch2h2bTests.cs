using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Targeting;
using MagicCore.Turns;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4h2b, LSVPower — black's graveyard returns: the top creature card of a graveyard (Shallow Grave, Corpse
/// Dance's buyback), each player's own chosen move (Exhume), a chosen move from exile under your control on +1/+1
/// counters (Emperor of Bones), a mill counted as it resolves (Barrowgoyf).
/// </summary>
public class LsvBatch2h2bTests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");
	static CardDefinition Ogre => TestGames.Creature("Ogre", "3/3");

	void ToEndStep()
	{
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.End && !x.StackIsEmpty());
		Resolve();
	}

	[Test]
	public void ShallowGrave_TheTopCreatureCard_HasteThenExiledAtTheEndStep()
	{
		var bear = Put(Bear, me, ZoneType.Graveyard);
		var ogre = Put(Ogre, me, ZoneType.Graveyard);
		Put(TestCards.Fodder, me, ZoneType.Graveyard); // not a creature: the top CREATURE card is the Ogre
		var theirs = Put(Bear, them, ZoneType.Graveyard);
		Cast(Hand(Lsv("Shallow Grave"), me), []);
		Assert.That(Alive(ogre), "the top creature card of your graveyard");
		Assert.That(Alive(bear) || Alive(theirs), Nu.False);
		Assert.That(s.Has(s.GetCard(ogre), "Haste"));
		ToEndStep();
		Assert.That(InExile(ogre), "exiled at the beginning of the next end step");
	}

	[Test]
	public void CorpseDance_Buyback_BackToMyHand_Unbought_ToTheGraveyard()
	{
		var ogre = Put(Ogre, me, ZoneType.Graveyard);
		var dance = Hand(Lsv("Corpse Dance"), me);
		s = s.Act(Casts(dance).Single(c => c.Kicked));
		Resolve();
		Assert.That(Alive(ogre));
		Assert.That(s.IsIn(dance, me, ZoneType.Hand), "buyback: into your hand as it resolves");
		Put(Bear, me, ZoneType.Graveyard);
		s = s.Act(Casts(dance).Single(c => !c.Kicked));
		Resolve();
		Assert.That(InGraveyard(dance));
	}

	[Test]
	public void Exhume_EachPlayerPutsACreatureFromTheirOwnGraveyard()
	{
		var bear = Put(Bear, me, ZoneType.Graveyard);
		var ogre = Put(Ogre, me, ZoneType.Graveyard);
		var theirs = Put(Bear, them, ZoneType.Graveyard);
		var asked = CastAsking(
			Hand(Lsv("Exhume"), me),
			c =>
				c switch
				{
					ChooseAmongChoice a when a.PlayerId == me => [ogre],
					ChooseAmongChoice => [theirs],
					_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
				}
		);
		Assert.That(
			Offered<ChooseAmongChoice>(asked),
			Nu.EquivalentTo(new[] { bear, ogre, theirs }),
			"each from their own graveyard"
		);
		Assert.That(Alive(ogre) && !Alive(bear));
		Assert.That(s.GetCard(theirs).ControllerId, Nu.EqualTo(them), "under its owner's control");
	}

	[Test]
	public void Exhume_OnTheirTurn_TheyChooseFirst_APNAP()
	{
		var mine = Put(Ogre, me, ZoneType.Graveyard);
		var theirs = Put(Bear, them, ZoneType.Graveyard);
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().ActivePlayerId == them
				&& x.GetGame().Step == Step.PreCombatMain
				&& x.GetGame().PriorityPlayerId == them
		);
		var exhume = Hand(Lsv("Exhume"), them);
		s = s.Act(new CastSpellAction { PlayerId = them, CardId = exhume });
		var asked = ResolveAsking(c => c.Options.Take(c.MinChoices).Select(o => o.Id));
		var order = asked.OfType<ChooseAmongChoice>().Select(c => c.PlayerId).ToList();
		Assert.That(order, Nu.EqualTo(new[] { them, me }), "the active player chooses first");
		Assert.That(Alive(mine) && Alive(theirs));
	}

	[Test]
	public void Refused_TopOutsideALibraryOrGraveyard_ACountersTriggerOnLoyalty()
	{
		var top = Ref.Permanents(MagicCore.Filters.Is.Creature) with { Top = true };
		Assert.Throws<InvalidOperationException>(
			() =>
				top.Resolve(
						s,
						ImmutableDictionary<string, object>.Empty.Add(ContextKeys.Controller, me)
					)
					.ToList()
		);
		Assert.Throws<ArgumentException>(
			() => _ = new Trigger(EventKind.CountersPut) { Counters = CounterKinds.Loyalty }
		);
	}

	[Test]
	public void EmperorOfBones_ExilesAtCombat_AdaptReturnsItMine_WithFinality_Haste_Sacrificed()
	{
		var emperor = Field(Lsv("Emperor of Bones"), me);
		var ogre = Put(Ogre, them, ZoneType.Graveyard);
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is ChooseTargetChoice);
		s = s.Choose([ogre]);
		Resolve();
		Assert.That(InExile(ogre), "exile up to one target card from a graveyard");

		s = s.Act(Activations(emperor).Single());
		Resolve();
		Assert.That(s.GetCard(emperor).PlusOneCounters, Nu.EqualTo(2), "adapt 2");
		var back = s.GetCard(ogre);
		Assert.That(Alive(ogre), "a creature card exiled with it, onto the battlefield");
		Assert.That(back.ControllerId, Nu.EqualTo(me), "under your control");
		Assert.That(back.HasFinalityCounter && s.Has(back, "Haste"));

		s = s.Act(Activations(emperor).Single());
		Resolve();
		Assert.That(
			s.GetCard(emperor).PlusOneCounters,
			Nu.EqualTo(2),
			"adapt: none when it has any"
		);

		ToEndStep();
		Assert.That(
			InExile(ogre),
			"sacrificed at the next end step — the finality counter exiles it"
		);
	}

	[Test]
	public void EmperorOfBones_ACounterPutAnotherWay_TriggersToo()
	{
		var emperor = Field(Lsv("Emperor of Bones"), me);
		var ogre = Put(Ogre, them, ZoneType.Graveyard);
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is ChooseTargetChoice);
		s = s.Choose([ogre]);
		Resolve();
		var spell = Hand(
			TestCards.Targeted("Grow", TargetSpec.Creature, new PutCounters(Ref.Target(), 1)),
			me
		);
		Cast(spell, [], emperor);
		Assert.That(Alive(ogre), "one +1/+1 counter is \"one or more\"");
	}

	[Test]
	public void Barrowgoyf_CardTypesInAllGraveyards_MillsThatMany_TakesACreature()
	{
		var goyf = Field(Lsv("Barrowgoyf"), me);
		Put(Bear, them, ZoneType.Graveyard);
		Put(TestCards.Fodder, me, ZoneType.Graveyard); // a sorcery
		Assert.That((s.Power(s.GetCard(goyf)), s.Toughness(s.GetCard(goyf))), Nu.EqualTo((2, 3)));
		var ogre = OnTop(Ogre, me);
		var life = (Life(me), Life(them));
		AttackWith(
			c =>
				c switch
				{
					YesNoChoice => [1],
					ChooseAmongChoice => [ogre],
					_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
				},
			goyf
		);
		Assert.That(
			(Life(me), Life(them)),
			Nu.EqualTo((life.Item1 + 2, life.Item2 - 2)),
			"lifelink"
		);
		Assert.That(
			s.IsIn(ogre, me, ZoneType.Hand),
			"milled that many, a creature card into your hand"
		);
		Assert.That(
			s.CardsIn(me, ZoneType.Graveyard).Count(),
			Nu.EqualTo(2),
			"the Fodder and the other milled card"
		);
	}
}
