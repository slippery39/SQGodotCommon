using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets.Fra;
using MagicCore.Turns;

namespace MagicCore.Tests.Sets;

/// <summary>Uncommons (7e-2b): creatures and a land — each ability fires.</summary>
public class FraUncommonCreatureTests : FraBoardTest
{
	static CardDefinition Heal =>
		TestGames.Inline("Heal", "{W}", "Instant", null, new GainLife(Ref.You, 1));

	void Activate(int card, params int[] targets)
	{
		s = s.Act(Activations(card).First(a => a.Targets.SequenceEqual(targets)));
		Resolve();
	}

	void Tapped(int card) => s = s.UpdateObject(card, s.GetCard(card) with { IsTapped = true }); // TEST SETUP

	IEnumerable<Card> Tokens(int player, string name) =>
		s.CardsIn(player, ZoneType.Battlefield).Where(c => c.IsToken && c.Name == name);

	[Test]
	public void Bloombrute_DrawsOnTheFirstLifeGainEachTurnOnly()
	{
		Field(FraCards.Get("Bloombrute"), me);
		var first = Hand(Heal, me);
		var second = Hand(Heal, me);
		var hand = HandCount(me);
		Cast(first, []);
		Cast(second, []);
		Assert.That(HandCount(me), Is.EqualTo(hand - 2 + 1));
	}

	[Test]
	public void Bloombrute_GivesTrampleAndLifelink()
	{
		var brute = Field(FraCards.Get("Bloombrute"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Activate(brute, bear);
		var card = s.GetCard(bear);
		Assert.That((s.Has(card, "Trample"), s.Has(card, "Lifelink")), Is.EqualTo((true, true)));
	}

	[Test]
	public void Koth_APlainsGainsOneAndPutsACounter()
	{
		Field(FraCards.Get("Koth of the Homestead"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var plains = Hand(TestGames.Card("Plains"), me);
		var life = Life(me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = plains });
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [bear],
				_ => [],
			}
		);
		Assert.That((Life(me), s.GetCard(bear).PlusOneCounters), Is.EqualTo((life + 1, 1)));
	}

	[Test]
	public void Koth_AnotherLandOnlyGainsOne()
	{
		Field(FraCards.Get("Koth of the Homestead"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var forest = Hand(TestGames.Card("Forest"), me);
		var life = Life(me);
		var asked = false;
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = forest });
		Resolve(choice =>
		{
			asked |= choice is ChooseTargetChoice;
			return [];
		});
		Assert.That(
			(Life(me), s.GetCard(bear).PlusOneCounters, asked),
			Is.EqualTo((life + 1, 0, false))
		);
	}

	[Test]
	public void RescueGirl_ReturnsAnotherPermanentYouControl_OnlyDuringYourTurn()
	{
		var girl = Field(FraCards.Get("Rescue Girl, First Responder"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Assert.That(Activations(girl).SelectMany(a => a.Targets), Does.Not.Contain(girl));
		Activate(girl, bear);
		Assert.That(s.IsIn(bear, me, ZoneType.Hand));

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().ActivePlayerId == them && x.GetGame().PriorityPlayerId == me
		);
		s = s.UpdateObject(girl, s.GetCard(girl) with { IsTapped = false }); // TEST SETUP: ready again
		Assert.That(Activations(girl), Is.Empty, "not during their turn");
	}

	[Test]
	public void TeyoLightshield_HexproofAndACounterOnACreature()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		CastWithTrigger(Hand("Teyo, Lightshield Expert", me), [], bear);
		var card = s.GetCard(bear);
		Assert.That((s.Has(card, "Hexproof"), card.PlusOneCounters), Is.EqualTo((true, 1)));
	}

	[Test]
	public void TeyoLightshield_ALoyaltyCounterOnAPlaneswalker()
	{
		var walker = Field(TestGames.Planeswalker("Walker", 3), me);
		s = s.UpdateObject(walker, s.GetCard(walker) with { Loyalty = 3 }); // TEST SETUP: Put skips entering
		CastWithTrigger(Hand("Teyo, Lightshield Expert", me), [], walker);
		Assert.That(
			(s.GetCard(walker).Loyalty, s.GetCard(walker).PlusOneCounters),
			Is.EqualTo((3 + 1, 0))
		);
	}

	[Test]
	public void TeyoDiamondblade_DeathtouchAndACounter()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		CastWithTrigger(Hand("Teyo, Diamondblade Mage", me), [], bear);
		var card = s.GetCard(bear);
		Assert.That((s.Has(card, "Deathtouch"), card.PlusOneCounters), Is.EqualTo((true, 1)));
	}

	[Test]
	public void Kiora_ADragonOnEntering_AndOneForEight()
	{
		var kiora = Hand("Kiora of Fire and Ashes", me);
		Cast(kiora, []);
		var dragon = Tokens(me, "Dragon").Single();
		Assert.That((s.Power(dragon), s.Has(dragon, "Flying")), Is.EqualTo((5, true)));

		Activate(kiora);
		Assert.That(Tokens(me, "Dragon").Count(), Is.EqualTo(2));
	}

	[Test]
	public void PiaRebuilder_AThopter_ThenPlusOneForEachArtifact()
	{
		var pia = Hand("Pia, Determined Rebuilder", me);
		Cast(pia, []);
		Assert.That(Tokens(me, "Thopter").Count(), Is.EqualTo(1));

		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Activate(pia, bear);
		Assert.That(s.Power(s.GetCard(bear)), Is.EqualTo(2 + 1));
	}

	[Test]
	public void HapatraDesertFrost_TapsAndStunsTheirCreature()
	{
		var theirs = Field(TestGames.Creature("Theirs", "2/2"), them);
		CastWithTrigger(Hand("Hapatra, the Desert Frost", me), [], theirs);
		var card = s.GetCard(theirs);
		Assert.That((card.IsTapped, card.StunCounters), Is.EqualTo((true, 1)));
	}

	[Test]
	public void HapatraDesertFrost_UntapsACreature()
	{
		var hapatra = Field(FraCards.Get("Hapatra, the Desert Frost"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Tapped(bear);
		Activate(hapatra, bear);
		Assert.That(s.GetCard(bear).IsTapped, Is.False);
	}

	[Test]
	public void GeistOfSaintThalia_YourNoncreatureSpellsCostOneLess()
	{
		Field(FraCards.Get("Geist of Saint Thalia"), me);
		var spell = Hand(TestGames.Inline("Spell", "{2}{U}", "Instant"), me);
		var creature = Hand(TestGames.CreatureCosting("{2}{U}", "Guy", "2/2"), me);
		var theirs = Hand(TestGames.Inline("Theirs", "{2}{U}", "Instant"), them);
		Assert.That(Costs.Of(s, me, s.GetCard(spell)).ManaValue, Is.EqualTo(3 - 1));
		Assert.That(Costs.Of(s, me, s.GetCard(creature)).ManaValue, Is.EqualTo(3));
		Assert.That(Costs.Of(s, them, s.GetCard(theirs)).ManaValue, Is.EqualTo(3), "yours only");
	}

	[Test]
	public void GeistOfSaintThalia_TheDiscountAppliesToX()
	{
		Field(FraCards.Get("Geist of Saint Thalia"), me);
		var blast = Hand(
			TestGames.Inline("Blast", "{X}{R}", "Instant", null, new GainLife(Ref.You, 1)),
			me
		);
		int Untapped() =>
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Is(CardType.Land) && !c.IsTapped);
		var lands = Untapped();
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = blast,
				X = 2,
			}
		);
		Assert.That(
			Untapped(),
			Is.EqualTo(lands - (2 + 1 - 1)),
			"{X}{R} with X=2, one less: 2 lands"
		);
	}

	[Test]
	public void Arni_UntapsWhenANontokenCreatureEnters_NotAToken()
	{
		var arni = Field(FraCards.Get("Arni, Humble Scribe"), me);
		Tapped(arni);
		var thopters = Hand(
			TestGames.Inline(
				"Thopters",
				"{1}",
				"Instant",
				null,
				new CreateTokens(FraCards.Thopter, 1)
			),
			me
		);
		Cast(thopters, []);
		Assert.That(s.GetCard(arni).IsTapped, "a token entering doesn't untap him");

		Cast(Hand(TestGames.Creature("Bear", "2/2"), me), []);
		Assert.That(s.GetCard(arni).IsTapped, Is.False);
	}

	[Test]
	public void Arni_LootsForTap()
	{
		var arni = Field(FraCards.Get("Arni, Humble Scribe"), me);
		var hand = HandCount(me);
		var graveyard = s.CardsIn(me, ZoneType.Graveyard).Count();
		Activate(arni);
		Assert.That(
			(HandCount(me), s.CardsIn(me, ZoneType.Graveyard).Count()),
			Is.EqualTo((hand, graveyard + 1))
		);
	}

	[Test]
	public void Traxos_StaysTappedThroughYourUntapStep_ButACreatureSpellUntapsIt()
	{
		var traxos = Field(FraCards.Get("Traxos, Scourge Eternal"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Tapped(traxos);
		Tapped(bear);
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == turn + 2 && x.GetGame().Step == Step.PreCombatMain
		);
		Assert.That(
			(s.GetCard(traxos).IsTapped, s.GetCard(bear).IsTapped),
			Is.EqualTo((true, false))
		);

		Cast(Hand(TestGames.Creature("Guy", "2/2"), me), []);
		Assert.That(s.GetCard(traxos).IsTapped, Is.False);

		Tapped(traxos);
		Cast(Hand(TestGames.Inline("Trinket", "{1}", "Artifact"), me), []);
		Assert.That(s.GetCard(traxos).IsTapped, Is.False, "an artifact spell untaps it too");
	}

	[Test]
	public void Saheeli_ThoptersHaveHaste_AndANoncreatureSpellMakesOne()
	{
		Field(FraCards.Get("Saheeli, Jewel of Avishkar"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Cast(Hand(Heal, me), []);
		var thopter = Tokens(me, "Thopter").Single();
		Assert.That(
			(s.Has(thopter, "Haste"), s.Has(s.GetCard(bear), "Haste")),
			Is.EqualTo((true, false))
		);
	}

	[Test]
	public void HexhavenArena_PreparesForFour()
	{
		var arena = Field(FraCards.Get("Hexhaven Dueling Arena"), me);
		var foreseer = Field(FraCards.Get("Semester Foreseer // Peer Review"), me);
		Activate(arena, foreseer);
		Assert.That(s.GetCard(foreseer).IsPrepared);
	}

	[Test]
	public void HexhavenArena_ForTwo_OnlyACreatureThatAttackedThisTurn()
	{
		var arena = Field(FraCards.Get("Hexhaven Dueling Arena"), me);
		var attacker = Field(FraCards.Get("Semester Foreseer // Peer Review"), me);
		var stayedHome = Field(TestGames.Creature("Homebody", "2/2"), me);
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice declare)
			s = s.Choose([declare.CreatureId == attacker ? them : CombatOption.None]);
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PostCombatMain);

		var forTwo = Activations(arena).Where(a => a.AbilityIndex == 0); // "{2}, {T}:" is printed first
		Assert.That(
			forTwo.Select(a => a.Targets[0]),
			Does.Contain(attacker).And.Not.Contain(stayedHome)
		);
		var trick = Hand(
			TestGames.Inline("Trick", "{G}", "Instant", null, new GainLife(Ref.You, 1)),
			me
		);
		var withTrick = s.Act(new CastSpellAction { PlayerId = me, CardId = trick });
		Assert.That(
			withTrick
				.LegalActions()
				.OfType<ActivateAbilityAction>()
				.Where(a => a.CardId == arena)
				.Select(a => a.AbilityIndex)
				.Distinct(),
			Is.EqualTo(new[] { 1 }),
			"with a spell on the stack: the {4} only — the {2} is sorcery speed"
		);

		s = s.Act(forTwo.First(a => a.Targets[0] == attacker));
		Resolve();
		Assert.That(s.GetCard(attacker).IsPrepared);
	}
}
