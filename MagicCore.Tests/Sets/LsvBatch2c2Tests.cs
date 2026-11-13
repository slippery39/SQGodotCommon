using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4c2, LSVPower — red's last 16, each the nearest piece with an option (MC-6's rule): each card's ability
/// FIRES, against its PRINTED text, through real casting and the real stack.
/// </summary>
public class LsvBatch2c2Tests : FraBoardTest
{
	static CardDefinition Lsv(string name) => CardRegistry.Get(name, "LSV");

	static CardDefinition Boon =>
		TestGames.Inline("Boon", "{1}", "Instant", null, new GainLife(Ref.You, 1));

	/// <summary>A 2/2 of a creature type, costing {1}.</summary>
	static CardDefinition Typed(string type) =>
		new(
			new CardData(
				type,
				"normal",
				"{1}",
				1,
				$"Creature — {type}",
				null,
				"2",
				"2",
				null,
				null,
				[],
				null
			),
			[]
		);

	static CardDefinition TreasureCard =>
		TestGames.Inline("Treasure", "{0}", "Artifact — Treasure");

	int Tokens(int player, string name) =>
		s.CardsIn(player, ZoneType.Battlefield).Count(c => c.IsToken && c.Name == name);

	bool InExile(int card) => s.IsIn(card, s.GetCard(card).OwnerId, ZoneType.Exile);

	int? PlayableUntil(int card) => s.GetCard(card).GetComponent<PlayableFromExile>()?.UntilTurn;

	void AttackWith(params int[] attackers)
	{
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareAttackerChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice d)
			s = s.Choose([attackers.Contains(d.CreatureId) ? them : 0]);
	}

	/// <summary>Puts a fresh card on top of a library.</summary>
	int OnTop(CardDefinition card, int player)
	{
		int id;
		(s, id) = TestGames.Put(s, player, card, ZoneType.Library);
		s = s.MoveObjectToFront(id, s.ZoneId(player, ZoneType.Library));
		return id;
	}

	// ===== MC-4.4c2a =====

	[Test]
	public void BroadsideBombardiers_Boast_TwoPlusTheSacrificedManaValue_OnlyAfterAttacking_OncePerTurn()
	{
		var bombardiers = Field(Lsv("Broadside Bombardiers"), me);
		var monolith = Field(Lsv("Basalt Monolith"), me); // {3}
		Field(Lsv("Basalt Monolith"), me);
		Assert.That(Activations(bombardiers), Nu.Empty, "only if it attacked this turn");
		s = s.UpdateObject(
			bombardiers,
			s.GetCard(bombardiers) with
			{
				AttackedOnTurn = s.GetGame().TurnNumber,
			}
		);
		var life = Life(them);
		s = s.Act(
			Activations(bombardiers).First(a => a.SacrificeId == monolith && a.Targets[0] == them)
		);
		Resolve();
		Assert.That(Life(them), Nu.EqualTo(life - (2 + 3)));
		Assert.That(Activations(bombardiers), Nu.Empty, "only once each turn");
	}

	[Test]
	public void CoriSteelCutter_Flurry_SecondSpellMakesAMonk_WhichItMayEquip()
	{
		var cutter = Field(Lsv("Cori-Steel Cutter"), me);
		Cast(Hand(Boon, me), []);
		Assert.That(Tokens(me, "Monk"), Nu.EqualTo(0), "the first spell");
		var second = Hand(Boon, me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = second });
		Resolve(On<YesNoChoice>(1));
		var monk = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Monk");
		Assert.That(s.GetCard(cutter).AttachedToId, Nu.EqualTo(monk.Id));
		Assert.That(
			(s.Power(monk), s.Has(monk, "Trample"), s.Has(monk, "Haste")),
			Nu.EqualTo((2, true, true))
		);
		Cast(Hand(Boon, me), []);
		Assert.That(s.Power(s.GetCard(monk.Id)), Nu.EqualTo(3), "prowess");
		Assert.That(Tokens(me, "Monk"), Nu.EqualTo(1), "only the second spell");
	}

	[Test]
	public void BonehoardDracosaur_Upkeep_ExilesTwoPlayable_ADinosaurForALand_ATreasureForANonland()
	{
		Field(Lsv("Bonehoard Dracosaur"), me);
		var land = OnTop(TestGames.Card("Mountain"), me);
		var spell = OnTop(Boon, me);
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().ActivePlayerId == me
				&& x.GetGame().Step == Step.Upkeep
				&& !x.StackIsEmpty()
		);
		Resolve();
		Assert.That(InExile(land) && InExile(spell), Nu.True);
		Assert.That(PlayableUntil(spell), Nu.EqualTo(s.GetGame().TurnNumber), "this turn");
		Assert.That((Tokens(me, "Dinosaur"), Tokens(me, "Treasure")), Nu.EqualTo((1, 1)));
	}

	[Test]
	public void Inti_AttackDiscardCountersAnAttacker_AndADiscardExilesThePlayableTop()
	{
		var inti = Field(Lsv("Inti, Seneschal of the Sun"), me);
		var boon = Hand(Boon, me);
		var top = OnTop(Boon, me);
		AttackWith(inti);
		Resolve(c =>
			c switch
			{
				ChooseCardsFromHandChoice => [boon],
				ChooseTargetChoice => [inti],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(s.GetCard(inti).PlusOneCounters, Nu.EqualTo(1));
		Assert.That(s.Has(s.GetCard(inti), "Trample"), Nu.True);
		Assert.That(InExile(top), Nu.True, "whenever you discard one or more cards");
		Assert.That(
			PlayableUntil(top),
			Nu.EqualTo(s.GetGame().TurnNumber),
			"my end step is still to come"
		);
	}

	[Test]
	public void Inti_DiscardOnTheirTurn_PlayableUntilMyNextEndStep()
	{
		Field(Lsv("Inti, Seneschal of the Sun"), me);
		var top = OnTop(Boon, me);
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().ActivePlayerId == them && x.GetGame().Step == Step.PreCombatMain
		);
		var turn = s.GetGame().TurnNumber;
		s = s.Act(new PassPriorityAction { PlayerId = them });
		Hand(Boon, me);
		var toss = Hand(
			TestGames.Inline("Toss", "{0}", "Instant", null, new Discard(Ref.You, 1)),
			me
		);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = toss });
		Resolve();
		Assert.That(PlayableUntil(top), Nu.EqualTo(turn + 1), "my next turn");
	}

	[Test]
	public void Magda_FiveTreasuresSearchAnArtifact_DwarvesGetOne_TappedDwarfMakesATreasure()
	{
		var magda = Field(Lsv("Magda, Brazen Outlaw"), me);
		var treasures = Enumerable.Range(0, 4).Select(_ => Field(TreasureCard, me)).ToList();
		Assert.That(Activations(magda), Nu.Empty, "four Treasures can't pay");
		treasures.Add(Field(TreasureCard, me));
		Field(TreasureCard, me);
		var monolith = OnTop(Lsv("Basalt Monolith"), me);
		s = s.Act(Activations(magda).Single());
		Resolve(c =>
			c switch
			{
				ChoosePermanentChoice => treasures,
				ChooseFromLibraryChoice => [monolith],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(treasures.Any(Alive), Nu.False, "the five chosen sacrificed");
		Assert.That(Alive(monolith), Nu.True, "an artifact card onto the battlefield");

		var dwarf = Field(Typed("Dwarf"), me);
		Assert.That(
			(s.Power(s.GetCard(dwarf)), s.Power(s.GetCard(magda))),
			Nu.EqualTo((3, 2)),
			"other Dwarves"
		);
		AttackWith(dwarf);
		Resolve();
		Assert.That(
			Tokens(me, "Treasure"),
			Nu.EqualTo(1),
			"whenever a Dwarf you control becomes tapped"
		);
	}

	[Test]
	public void Tersa_DiscardUpToTwoDrawThatMany_AttackExilesARandomGraveyardCardPlayable()
	{
		s = TestGames.EmptyHands(s);
		Hand(Boon, me);
		var toss = Hand(Boon, me);
		var tersa = Hand(Lsv("Tersa Lightshatter"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = tersa });
		Resolve(On<ChooseCardsFromHandChoice>(toss));
		Assert.That(s.IsIn(toss, me, ZoneType.Graveyard), Nu.True);
		Assert.That(HandCount(me), Nu.EqualTo(2), "one discarded, one drawn");

		for (var i = 0; i < 6; i++)
			(s, _) = TestGames.Put(s, me, Boon, ZoneType.Graveyard);
		var graveyard = s.CardsIn(me, ZoneType.Graveyard).Select(c => c.Id).ToList();
		Assert.That(graveyard, Has.Count.EqualTo(7));
		AttackWith(tersa);
		Resolve();
		var exiled = graveyard.Where(InExile).ToList();
		Assert.That(exiled, Has.Count.EqualTo(1), "a card at random from your graveyard");
		Assert.That(
			exiled[0],
			Nu.Not.EqualTo(graveyard[0]),
			"at random (this seed), not the first in the zone"
		);
		Assert.That(PlayableUntil(exiled[0]), Nu.EqualTo(s.GetGame().TurnNumber), "this turn");
		Assert.That(
			s.GetCard(exiled[0]).GetComponent<PlayableFromExile>()!.Blind,
			Nu.False,
			"a graveyard card was public: the AI knows what it is"
		);
	}
}
