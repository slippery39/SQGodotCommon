using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Turns;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4h7–h9, LSVPower — one batch of three pieces: saddle (Caustic Bronco), emerge (Crabomination), miracle
/// (Metamorphosis Fanatic), FIRING against their printed text through real casting and the real stack.
/// </summary>
public class LsvBatch2h7Tests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");
	static CardDefinition Bird => TestGames.Creature("Bird", "1/1");

	/// <summary>A {2}{B}{B} creature on top of my library: mana value 4.</summary>
	static CardDefinition Four => TestGames.CreatureCosting("{2}{B}{B}", "Four", "4/4");

	static IEnumerable<int> Defaults(ChoiceAction c) =>
		c.Options.Take(c.MinChoices).Select(o => o.Id);

	// ===== h7: saddle =====

	[Test]
	public void CausticBronco_Saddle3_TapsOtherCreaturesToPowerThree_NotItself()
	{
		var bronco = Field(Lsv("Caustic Bronco"), me);
		var bear = Field(Bear, me);
		var bird = Field(Bird, me);
		s = s.Act(Activations(bronco).Single());
		ResolveAsking(Defaults);
		Assert.That(
			(s.GetCard(bear).IsTapped, s.GetCard(bird).IsTapped, s.GetCard(bronco).IsTapped),
			Nu.EqualTo((true, true, false)),
			"the others, to a total of 3 — the Mount untapped"
		);
		Assert.That(s.Has(s.GetCard(bronco), "Saddled"), "saddled until end of turn");
	}

	[Test]
	public void CausticBronco_Saddle_NotWithLessThanThreePowerBesideIt()
	{
		var bronco = Field(Lsv("Caustic Bronco"), me);
		Field(Bear, me);
		Assert.That(Activations(bronco), Nu.Empty, "2 power — itself never counts");
	}

	[Test]
	public void CausticBronco_AttacksUnsaddled_TheCardToHand_YouLoseItsManaValue()
	{
		var bronco = Field(Lsv("Caustic Bronco"), me);
		var four = OnTop(Four);
		AttackWith(Defaults, bronco);
		Assert.That(s.IsIn(four, me, ZoneType.Hand), "revealed and put into your hand");
		Assert.That(
			(Life(me), Life(them)),
			Nu.EqualTo((20 - 4, 20 - 2)),
			"you lose 4; they take 2 combat"
		);
	}

	[Test]
	public void CausticBronco_AttacksSaddled_EachOpponentLosesItsManaValue()
	{
		var bronco = Field(Lsv("Caustic Bronco"), me);
		Field(Bear, me);
		Field(Bird, me);
		s = s.Act(Activations(bronco).Single());
		ResolveAsking(Defaults);
		OnTop(Four);
		AttackWith(Defaults, bronco);
		Assert.That(
			(Life(me), Life(them)),
			Nu.EqualTo((20, 20 - 4 - 2)),
			"they lose 4, and take 2 combat"
		);
	}

	// ===== h8: emerge =====

	/// <summary>A {3} artifact: mana value 3.</summary>
	static CardDefinition Relic => TestGames.Inline("Relic", "{3}", "Artifact");

	[Test]
	public void Crabomination_Emerge_SacrificesAnArtifact_PaysSevenLessItsManaValue()
	{
		var relic = Field(Relic, me);
		var crab = Hand(Lsv("Crabomination"), me);
		var emerge = Casts(crab).Single(c => c.SacrificeId == relic);
		var lands = UntappedLands();
		s = s.Act(emerge);
		Resolve();
		Assert.That(lands - UntappedLands(), Nu.EqualTo(4), "{5}{B}{B} less 3: {2}{B}{B}");
		Assert.That(InGraveyard(relic) && Alive(crab), "the artifact sacrificed, the Crab cast");
	}

	[Test]
	public void Crabomination_Enters_TheyExileTopGraveyardAndHandCards_YouCastOneFree()
	{
		foreach (var card in s.CardsIn(them, ZoneType.Hand).ToList())
			s = s.MoveCard(card.Id, s.ZoneId(them, ZoneType.Library)); // their hand: only the card below
		var top = OnTop(Four, them);
		var dead = Put(Bird, them, ZoneType.Graveyard);
		var held = Put(Bear, them, ZoneType.Hand);
		var crab = Hand(Lsv("Crabomination"), me);
		var lands = UntappedLands();
		s = s.Act(Casts(crab).Single(c => !c.WithoutPayingManaCost));
		var asked = ResolveAsking(c =>
			c switch
			{
				ChooseAmongChoice => [top],
				_ => Defaults(c),
			}
		);
		Assert.That(
			Offered<ChooseAmongChoice>(asked),
			Nu.EquivalentTo(new[] { top, dead, held }),
			"the three exiled this way"
		);
		Assert.That(
			s.IsOnBattlefield(top) && s.GetCard(top).ControllerId == me,
			"cast from their exile, under your control"
		);
		Assert.That(InExile(dead) && InExile(held), "the rest stay exiled");
		Assert.That(
			lands - UntappedLands(),
			Nu.EqualTo(6),
			"the Crab's {4}{B}{B} — the Four was free"
		);
	}

	// ===== h9: miracle =====

	/// <summary>A {1} instant: "Draw N cards."</summary>
	static CardDefinition Draw(int n) =>
		new(
			TestGames.Inline($"Draw {n}", "{1}", "Instant").Data,
			[new SpellEffectsComponent([new DrawCards(Ref.You, n)])]
		);

	[Test]
	public void MetamorphosisFanatic_Miracle_TheFirstCardDrawn_CastForOneB()
	{
		Assume.That(
			s.GetPlayer(me).ThisTurn.CardsDrawn,
			Nu.EqualTo(0),
			"no card drawn yet this turn"
		);
		var fanatic = OnTop(Lsv("Metamorphosis Fanatic"));
		var draw = Hand(Draw(1), me);
		var lands = UntappedLands();
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = draw });
		var asked = ResolveAsking(c =>
			c switch
			{
				ChooseAmongChoice => [fanatic],
				_ => Defaults(c),
			}
		);
		Assert.That(
			Offered<ChooseAmongChoice>(asked),
			Nu.EqualTo(new[] { fanatic }),
			"you may cast it"
		);
		Assert.That(Alive(fanatic), "cast from the hand, at instant speed, and resolved");
		Assert.That(
			lands - UntappedLands(),
			Nu.EqualTo(1 + 2),
			"the Draw's {1}, then {1}{B} — not {4}{B}{B}"
		);
	}

	[Test]
	public void MetamorphosisFanatic_Miracle_RevealedAsItTriggers_DeclinedStaysInHand()
	{
		var fanatic = OnTop(Lsv("Metamorphosis Fanatic"));
		var draw = Hand(Draw(1), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = draw });
		var asked = ResolveAsking(c =>
			c switch
			{
				ChooseAmongChoice => [],
				_ => Defaults(c),
			}
		);
		Assert.That(
			asked.OfType<ChooseAmongChoice>().Single().Prompt,
			Does.Contain("{1}{B}"),
			"the miracle cost is named, never \"free\""
		);
		Assert.That(s.IsIn(fanatic, me, ZoneType.Hand), "declined: it stays in the hand");
		Assert.That(
			s.GetCard(fanatic).KnownTo,
			Nu.EqualTo(Knowers.Everyone),
			"revealed as it triggered"
		);
	}

	[Test]
	public void CastNow_Instead_PaysThatCost_EvenNotFree()
	{
		var fanatic = Hand(Lsv("Metamorphosis Fanatic"), me);
		var options = CastNow.Options(
			s,
			me,
			fanatic,
			free: false,
			instead: ManaCost.Parse("{1}{B}")
		);
		Assert.That(
			options.All(c => c.WithoutPayingManaCost && c.ManaInstead?.ToString() == "{1}{B}"),
			"the cost instead of its mana cost, never both"
		);
	}

	[Test]
	public void MetamorphosisFanatic_TheSecondCardDrawn_NoMiracle()
	{
		var fanatic = OnTop(Lsv("Metamorphosis Fanatic"));
		OnTop(Bear); // drawn first
		var draw = Hand(Draw(2), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = draw });
		var asked = ResolveAsking(c =>
			c switch
			{
				ChooseAmongChoice => [fanatic],
				_ => Defaults(c),
			}
		);
		Assert.That(
			asked.OfType<ChooseAmongChoice>(),
			Nu.Empty,
			"drawn second this turn: nothing triggers"
		);
		Assert.That(s.IsIn(fanatic, me, ZoneType.Hand));
	}

	[Test]
	public void MetamorphosisFanatic_Enters_ReturnsACreatureCard_WithALifelinkCounter()
	{
		var bear = Put(Bear, me, ZoneType.Graveyard);
		var fanatic = Hand(Lsv("Metamorphosis Fanatic"), me);
		CastWithTrigger(fanatic, [], bear);
		Assert.That(Alive(bear), "returned to the battlefield");
		Assert.That(s.Has(s.GetCard(bear), "Lifelink"), "a lifelink counter: it has lifelink");
	}

	[Test]
	public void OpeningHands_AreDrawnBeforeTheFirstTurn_NothingTriggers()
	{
		var game = TestGames.New(smoothing: false); // the smoothed hand is moved, never drawn; a mulligan draws as this does
		Assert.That(
			game.PendingGameEvents.OfType<DrewCardEvent>(),
			Nu.Empty,
			"no miracle from an opening hand"
		);
	}
}
