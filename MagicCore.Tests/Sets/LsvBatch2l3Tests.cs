using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4l3a + l3b, LSVPower — colourless' options, each against its printed text. Mana: colours in any combination
/// (Sunken Ruins, Relic of Sauron), a mana ability's rider (Gemstone Mine), mana moves that remove a counter or discard
/// the hand (Pentad Prism, Lion's Eye Diamond), the exiled card's colours (Chrome Mox), X on an activated ability and a
/// colour per unit (Wizard's Rockets, Coalition Relic), a count of your turns (Starting Town), {X}{X} (Walking
/// Ballista). Exile, graveyard and library: Currency Converter, Ghost Vacuum, Memory Jar, Karn, Ugin, Shelldock Isle,
/// Grindstone, Palantír of Orthanc, the Baubles, Shifting Woodland.
/// </summary>
public class LsvBatch2l3Tests : FraBoardTest
{
	/// <summary>An inline card WITH a colour (TestGames' inline cards have none): a 2/2 creature unless said.</summary>
	static CardDefinition Coloured(
		string name,
		string cost,
		string color,
		string typeLine = "Creature — Test"
	)
	{
		var pt = typeLine.StartsWith("Creature") ? "2" : null;
		return new(
			new CardData(name, "normal", cost, 0, typeLine, null, pt, pt, null, [color], [], null),
			[]
		);
	}

	static IEnumerable<int> Min(ChoiceAction c) => c.Options.Take(c.MinChoices).Select(o => o.Id);

	/// <summary>A colour choice's answer (ChooseColorChoice.ColorOf's inverse).</summary>
	static IEnumerable<int> Pick(ManaColor color) => [(int)color + 1];

	IEnumerable<TapForManaAction> ManaMoves(int card) =>
		s.LegalActions().OfType<TapForManaAction>().Where(a => a.CardId == card);

	int Pool(ManaColor color) => s.GetPlayer(me).ManaPool[color];

	/// <summary>TEST SETUP: the permanent untapped again, as though a turn had passed.</summary>
	void Untap(int id) => s = s.UpdateObject(id, s.GetCard(id) with { IsTapped = false });

	void TapFor(int card, ManaColor color) =>
		s = s.Act(
			new TapForManaAction
			{
				PlayerId = me,
				CardId = card,
				Color = color,
			}
		);

	/// <summary>This turn's step, a player holding priority — what triggers there is on the stack, unresolved.</summary>
	void To(Step step) =>
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().Step == step && x.GetGame().PriorityPlayerId != 0
		);

	/// <summary>That step of the NEXT turn.</summary>
	void ToNext(Step step)
	{
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().TurnNumber > turn
				&& x.GetGame().Step == step
				&& x.GetGame().PriorityPlayerId != 0
		);
	}

	void Activate(int card, int ability, params int[] targets)
	{
		s = s.Act(
			Activations(card)
				.First(a => a.AbilityIndex == ability && a.Targets.SequenceEqual(targets))
		);
	}

	void Loyalty(int walker, int ability) =>
		s = s.Act(
			new ActivateLoyaltyAction
			{
				PlayerId = me,
				CardId = walker,
				AbilityIndex = ability,
			}
		);

	// ===== l3a: mana =====

	[Test]
	public void SunkenRuins_AHybridManaBecomesTwoOfBlueAndOrBlack()
	{
		EmptyBoard();
		var ruins = Field(Lsv("Sunken Ruins"), me);
		Assert.That(CanTapFor(ruins, ManaColor.Colorless), "{T}: Add {C}");
		var both = Hand(Coloured("Agent", "{U}{B}", "U"), me);
		Assert.That(Casts(both), Nu.Empty, "its own {C} never pays its {U/B}");

		Field(TestGames.Card("Island"), me);
		var black = Hand(Coloured("Shade", "{B}{B}", "B"), me);
		var red = Hand(Coloured("Goblin", "{R}", "R"), me);
		Assert.That(Casts(black), Nu.Not.Empty, "{U/B} from the Island: {B}{B}");
		Assert.That(Casts(red), Nu.Empty, "never another colour");

		Cast(both, []);
		Assert.That(s.IsOnBattlefield(both), "… or {U}{B}");
		Assert.That(s.GetCard(ruins).IsTapped);
	}

	[TestCase("{U}{U}")]
	[TestCase("{U}{B}")]
	[TestCase("{B}{R}")]
	[TestCase("{R}{R}")]
	public void RelicOfSauron_TwoManaInAnyCombinationOfBlueBlackAndRed(string cost)
	{
		EmptyBoard();
		Field(Lsv("Relic of Sauron"), me);
		var spell = Hand(Coloured("Wraith", cost, "B"), me);

		Cast(spell, []);

		Assert.That(s.IsOnBattlefield(spell));
	}

	[Test]
	public void RelicOfSauron_NeverAnotherColour_AndByHandTwoOfTheOneChosen()
	{
		EmptyBoard();
		var relic = Field(Lsv("Relic of Sauron"), me);
		Assert.That(CanTapFor(relic, ManaColor.Green), Nu.False);
		Assert.That(CanTapFor(relic, ManaColor.Colorless), Nu.False);

		TapFor(relic, ManaColor.Red);

		Assert.That(Pool(ManaColor.Red), Nu.EqualTo(2));
	}

	[Test]
	public void PentadPrism_ACounterPerColourSpent_EachRemovedForManaOfAnyColour_WithoutTapping()
	{
		var prism = Hand(Lsv("Pentad Prism"), me);
		Cast(prism, []);
		Assert.That(s.GetCard(prism).ChargeCounters, Nu.EqualTo(2), "sunburst: two colours on {2}");
		Assert.That(
			ManaMoves(prism).Select(a => a.Color),
			Nu.EquivalentTo(ManaAbilities.AnyColor),
			"a move per colour"
		);

		s = s.Act(ManaMoves(prism).Single(a => a.Color == ManaColor.Red));
		s = s.Act(ManaMoves(prism).Single(a => a.Color == ManaColor.Green));

		Assert.That(Pool(ManaColor.Red), Nu.EqualTo(1));
		Assert.That(Pool(ManaColor.Green), Nu.EqualTo(1));
		Assert.That(s.GetCard(prism).IsTapped, Nu.False, "no {T} in its cost");
		Assert.That(s.GetCard(prism).ChargeCounters, Nu.EqualTo(0));
		Assert.That(ManaMoves(prism), Nu.Empty, "no counter left to remove");
	}

	[Test]
	public void GemstoneMine_ThreeMiningCounters_SacrificedAsTheLastMakesItsMana()
	{
		EmptyBoard();
		var mine = Play("Gemstone Mine");
		Assert.That(s.GetCard(mine).CountersOf(CounterKinds.Mining), Nu.EqualTo(3));

		foreach (var left in new[] { 2, 1 })
		{
			TapFor(mine, ManaColor.Green);
			Assert.That(s.GetCard(mine).CountersOf(CounterKinds.Mining), Nu.EqualTo(left));
			Assert.That(s.IsOnBattlefield(mine), "mining counters left");
			Untap(mine);
		}
		Assert.That(Pool(ManaColor.Green), Nu.EqualTo(2), "one mana of any color each");

		// the last counter, removed as it pays for a spell
		var spell = Hand(Coloured("Goblin", "{R}", "R"), me);
		Cast(spell, []);

		Assert.That(s.IsOnBattlefield(spell), "it made the {R}");
		Assert.That(s.IsIn(mine, me, ZoneType.Graveyard), "no mining counters on it: sacrificed");
	}

	[Test]
	public void ChromeMox_ImprintsANonartifactNonlandCard_AndTapsForItsColours()
	{
		var goblin = Hand(Coloured("Goblin", "{R}", "R"), me);
		var land = Hand(TestGames.Card("Island"), me);
		var rock = Hand(TestGames.Inline("Rock", "{1}", "Artifact"), me);
		var mox = Hand(Lsv("Chrome Mox"), me);

		var asked = CastAsking(mox, On<ChooseCardsFromHandChoice>(goblin));

		var offered = Offered<ChooseCardsFromHandChoice>(asked).ToList();
		Assert.That(offered, Does.Contain(goblin));
		Assert.That(offered, Does.Not.Contain(land), "a nonland card");
		Assert.That(offered, Does.Not.Contain(rock), "a nonartifact card");
		Assert.That(InExile(goblin));
		Assert.That(CanTapFor(mox, ManaColor.Red), "one mana of any of the exiled card's colors");
		Assert.That(CanTapFor(mox, ManaColor.Blue), Nu.False);
	}

	[Test]
	public void ChromeMox_WithNoCardToImprint_AsksNothing()
	{
		s = TestGames.EmptyHands(s);
		Hand(TestGames.Card("Island"), me);
		var mox = Hand(Lsv("Chrome Mox"), me);

		var asked = CastAsking(mox, Min);

		Assert.That(asked.OfType<ChooseCardsFromHandChoice>(), Nu.Empty);
		Assert.That(s.IsOnBattlefield(mox));
	}

	[Test]
	public void ChromeMox_WithNothingImprinted_MakesNoMana()
	{
		var mox = Hand(Lsv("Chrome Mox"), me);
		Cast(mox, []); // "you may": none
		Assert.That(ManaAbilities.AnyColor.Any(c => CanTapFor(mox, c)), Nu.False);
	}

	[Test]
	public void LionsEyeDiamond_DiscardsYourHand_IsSacrificed_ForThreeOfOneColour_NeverByThePayment()
	{
		TapMyLands();
		var led = Field(Lsv("Lion's Eye Diamond"), me);
		var spell = Hand(Coloured("Shade", "{B}", "B"), me);
		Assert.That(Casts(spell), Nu.Empty, "the payment never discards a hand by itself");
		Assert.That(
			ManaMoves(led).Select(a => a.Color),
			Nu.EquivalentTo(ManaAbilities.AnyColor),
			"a move per colour"
		);

		s = s.Act(ManaMoves(led).Single(a => a.Color == ManaColor.Black));

		Assert.That(Pool(ManaColor.Black), Nu.EqualTo(3), "three mana of any one color");
		Assert.That(HandCount(me), Nu.EqualTo(0), "Discard your hand");
		Assert.That(InGraveyard(spell));
		Assert.That(InGraveyard(led), "Sacrifice this artifact");
		Assert.That(s.StackIsEmpty(), "a mana ability: no stack");
	}

	[Test]
	public void WizardsRockets_XManaInAnyCombinationOfColours_ThenDraws()
	{
		var rockets = Field(Lsv("Wizard's Rockets"), me);
		var top = OnTop(TestGames.Creature("Bear", "2/2"));
		var untapped = UntappedLands();

		s = s.Act(Activations(rockets).Single(a => a.X == 2));
		var colours = new Queue<ManaColor>([ManaColor.Red, ManaColor.Green]);
		Resolve(c => c is ChooseColorChoice ? Pick(colours.Dequeue()) : Min(c));

		Assert.That(untapped - UntappedLands(), Nu.EqualTo(2), "{X}");
		Assert.That(Pool(ManaColor.Red), Nu.EqualTo(1), "a colour for each");
		Assert.That(Pool(ManaColor.Green), Nu.EqualTo(1));
		Assert.That(InGraveyard(rockets));
		Assert.That(
			s.CardsIn(me, ZoneType.Hand).Select(c => c.Id),
			Does.Contain(top),
			"put into a graveyard from the battlefield: draw a card"
		);
	}

	[Test]
	public void CoalitionRelic_ItsChargeCountersBecomeManaOfAnyColoursAtYourFirstMainPhase()
	{
		var relic = Field(Lsv("Coalition Relic"), me);
		Assert.That(CanTapFor(relic, ManaColor.Green), "{T}: Add one mana of any color");
		s = s.Act(Activations(relic).Single());
		Resolve();
		Assert.That(s.GetCard(relic).ChargeCounters, Nu.EqualTo(1), "{T}: a charge counter");
		s = s.PutCounters(relic, CounterKinds.Charge, 1); // TEST SETUP: a second, as though charged a turn ago too

		ToNext(Step.PreCombatMain); // theirs
		ToNext(Step.PreCombatMain); // mine: the trigger is on the stack
		var colours = new Queue<ManaColor>([ManaColor.Blue, ManaColor.Red]);
		Resolve(c => c is ChooseColorChoice ? Pick(colours.Dequeue()) : Min(c));

		Assert.That(s.GetGame().ActivePlayerId, Nu.EqualTo(me));
		Assert.That(s.GetCard(relic).ChargeCounters, Nu.EqualTo(0), "remove all charge counters");
		Assert.That(Pool(ManaColor.Blue), Nu.EqualTo(1), "one mana of any color for each");
		Assert.That(Pool(ManaColor.Red), Nu.EqualTo(1));
	}

	[Test]
	public void StartingTown_UntappedOnYourFirstThreeTurns_TappedFromYourFourth()
	{
		EmptyBoard();
		Assert.That(s.GetPlayer(me).TurnsBegun, Nu.EqualTo(1), "my first turn");
		var first = Play("Starting Town");
		Assert.That(s.GetCard(first).IsTapped, Nu.False, "your first turn");
		var life = Life(me);
		var bear = Hand(TestGames.Creature("Bear", "2/2"), me);
		Cast(bear, []);
		Assert.That(s.IsOnBattlefield(bear), "{T}: Add {C}");
		Assert.That(Life(me), Nu.EqualTo(life), "generic is paid by its free {C}, never with life");

		ToNext(Step.PreCombatMain); // theirs
		ToNext(Step.PreCombatMain); // mine
		Assert.That(
			s.GetPlayer(me).TurnsBegun,
			Nu.EqualTo(2),
			"counted as each turn of mine begins"
		);
		Assert.That(s.GetPlayer(them).TurnsBegun, Nu.EqualTo(1));

		s = s.WithPlayer(s.GetPlayer(me) with { TurnsBegun = 3 }); // TEST SETUP: my third turn
		var third = Play("Starting Town");
		Assert.That(s.GetCard(third).IsTapped, Nu.False, "your third turn");
		TapFor(third, ManaColor.Red);
		Assert.That(Life(me), Nu.EqualTo(life - 1), "{T}, Pay 1 life: one mana of any color");

		ToNext(Step.PreCombatMain);
		ToNext(Step.PreCombatMain);
		Assert.That(s.GetPlayer(me).TurnsBegun, Nu.EqualTo(4));
		var fourth = Play("Starting Town");
		Assert.That(s.GetCard(fourth).IsTapped, "your fourth turn");
	}

	[Test]
	public void WalkingBallista_EntersWithXCountersForXX_AndRemovesOneToDealOneDamage()
	{
		var ballista = Hand(Lsv("Walking Ballista"), me);
		var untapped = UntappedLands();
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = ballista,
				X = 2,
			}
		);
		Resolve();
		Assert.That(untapped - UntappedLands(), Nu.EqualTo(4), "{X}{X}");
		Assert.That(s.GetCard(ballista).PlusOneCounters, Nu.EqualTo(2), "X +1/+1 counters");

		var life = Life(them);
		Activate(ballista, 1, them);
		Resolve();

		Assert.That(Life(them), Nu.EqualTo(life - 1));
		Assert.That(s.GetCard(ballista).PlusOneCounters, Nu.EqualTo(1));
	}

	// ===== l3b: exile, graveyard and library =====

	/// <summary>Currency Converter's "{2}, {T}: Draw a card, then discard a card" — discarding that card, and exiling it.</summary>
	void LootAway(int converter, int card)
	{
		Untap(converter);
		Activate(converter, 0);
		Resolve(c =>
			c switch
			{
				ChooseCardsFromHandChoice => [card],
				YesNoChoice => [1],
				_ => Min(c),
			}
		);
		Untap(converter);
	}

	[Test]
	public void CurrencyConverter_ExilesWhatYouDiscard_ThenATreasureForALand_ARogueForANonland()
	{
		var converter = Field(Lsv("Currency Converter"), me);
		Assert.That(
			Activations(converter).Select(a => a.AbilityIndex),
			Nu.EquivalentTo(new[] { 0 }),
			"nothing exiled with it yet"
		);
		var land = Hand(TestGames.Card("Island"), me);
		LootAway(converter, land);
		Assert.That(InExile(land), "you may exile that card from your graveyard");

		Activate(converter, 1);
		Resolve();
		Assert.That(InGraveyard(land), "into its owner's graveyard");
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Treasure"),
			Nu.EqualTo(1),
			"a land card: a Treasure"
		);

		var bear = Hand(TestGames.Creature("Bear", "2/2"), me);
		LootAway(converter, bear);
		Activate(converter, 1);
		Resolve();
		Assert.That(InGraveyard(bear));
		var rogue = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Rogue");
		Assert.That((s.Power(rogue), s.Toughness(rogue)), Nu.EqualTo((2, 2)), "a 2/2 black Rogue");
	}

	[Test]
	public void GhostVacuum_EveryCreatureCardItExiledReturnsUnderYourControl_AFlyingOneOneSpirit()
	{
		var ogre = Put(TestGames.Creature("Ogre", "4/4"), them, ZoneType.Graveyard);
		var bolt = Put(TestGames.Inline("Bolt", "{R}", "Instant"), them, ZoneType.Graveyard);
		var vacuum = Field(Lsv("Ghost Vacuum"), me);
		foreach (var card in new[] { ogre, bolt })
		{
			Activate(vacuum, 0, card);
			Resolve();
			Untap(vacuum);
		}
		Assert.That(InExile(ogre) && InExile(bolt), "{T}: Exile target card from a graveyard");

		Activate(vacuum, 1);
		Resolve();

		Assert.That(InGraveyard(vacuum), "sacrificed for it");
		var spirit = s.GetCard(ogre);
		Assert.That(s.IsOnBattlefield(ogre), "each creature card exiled with it");
		Assert.That(spirit.ControllerId, Nu.EqualTo(me), "under your control");
		Assert.That(s.Has(spirit, "Flying"), "with a flying counter on it");
		Assert.That((s.Power(spirit), s.Toughness(spirit)), Nu.EqualTo((1, 1)), "a 1/1");
		Assert.That(s.HasSubtype(spirit, "Spirit"), "a Spirit in addition to its other types");
		Assert.That(InExile(bolt), "not a creature card: it stays");
	}

	[Test]
	public void MemoryJar_HandsExiledFaceDown_SevenDrawn_ThenDiscardedAndTheOldHandsReturned()
	{
		var mine = s.CardsIn(me, ZoneType.Hand).Select(c => c.Id).ToList();
		var theirs = s.CardsIn(them, ZoneType.Hand).Select(c => c.Id).ToList();
		var jar = Field(Lsv("Memory Jar"), me);

		Activate(jar, 0);
		Resolve();

		Assert.That(
			mine.Concat(theirs).All(InExile),
			"each player exiles all cards from their hand"
		);
		Assert.That(s.GetCard(theirs[0]).IsHiddenFrom(me), "face down");
		Assert.That(s.GetCard(theirs[0]).IsHiddenFrom(them), Nu.False, "its owner knows it");
		Assert.That((HandCount(me), HandCount(them)), Nu.EqualTo((7, 7)), "and draws seven cards");
		var drawn = s.CardsIn(me, ZoneType.Hand).Select(c => c.Id).ToList();

		To(Step.End);
		Resolve();

		Assert.That(drawn.All(InGraveyard), "each player discards their hand");
		Assert.That(
			s.CardsIn(me, ZoneType.Hand).Select(c => c.Id),
			Nu.EquivalentTo(mine),
			"and returns to their hand each card they exiled this way"
		);
		Assert.That(s.CardsIn(them, ZoneType.Hand).Select(c => c.Id), Nu.EquivalentTo(theirs));
		Assert.That(
			s.GetCard(theirs[0]).KnownTo & Knowers.Opponent,
			Nu.EqualTo(Knowers.None),
			"never revealed: I still don't know their hand"
		);
	}

	int CastKarn()
	{
		var karn = Hand(Lsv("Karn, Scion of Urza"), me);
		Cast(karn, []);
		return karn;
	}

	[Test]
	public void Karn_PlusOne_AnOpponentChoosesTheCardForYourHand_TheOtherExiledWithASilverCounter()
	{
		var karn = CastKarn();
		var second = OnTop(TestGames.Creature("Ogre", "4/4"));
		var first = OnTop(TestGames.Creature("Bear", "2/2"));

		Loyalty(karn, 0);
		var deciders = new List<int>();
		Resolve(c =>
		{
			if (c is not ChooseAmongChoice)
				return Min(c);
			deciders.Add(s.DecidingPlayerId());
			return [second];
		});

		Assert.That(deciders, Nu.EqualTo(new[] { them }), "an opponent chooses one of them");
		Assert.That(s.IsIn(second, me, ZoneType.Hand), "put that card into your hand");
		Assert.That(InExile(first), "exile the other");
		Assert.That(s.GetCard(first).CountersOf(CounterKinds.Silver), Nu.EqualTo(1));
	}

	[Test]
	public void Karn_MinusOne_ACardYouOwnWithASilverCounterFromExileToYourHand()
	{
		var karn = CastKarn();
		var silver = Put(TestGames.Creature("Bear", "2/2"), me, ZoneType.Exile);
		s = s.PutCounters(silver, CounterKinds.Silver, 1); // TEST SETUP: as his +1 exiled it
		var plain = Put(TestGames.Creature("Ogre", "4/4"), me, ZoneType.Exile);

		Loyalty(karn, 1);
		var asked = ResolveAsking(Min);

		Assert.That(Offered<ChooseAmongChoice>(asked), Nu.EquivalentTo(new[] { silver }));
		Assert.That(s.IsIn(silver, me, ZoneType.Hand));
		Assert.That(InExile(plain));
	}

	[Test]
	public void Karn_MinusTwo_AConstructWithPlusOnePlusOneForEachArtifactYouControl()
	{
		var karn = CastKarn();
		Loyalty(karn, 2);
		Resolve();

		var construct = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Construct");
		Assert.That((s.Power(construct), s.Toughness(construct)), Nu.EqualTo((1, 1)), "itself");
		Field(TestGames.Inline("Rock", "{1}", "Artifact"), me);
		Assert.That(s.Power(s.GetCard(construct.Id)), Nu.EqualTo(2));
	}

	[Test]
	public void Ugin_CastingItOrAColourlessSpell_ExilesAPermanentThatIsOneOrMoreColours()
	{
		var red = Field(Coloured("Goblin", "{R}", "R"), them);
		var blue = Field(Coloured("Merfolk", "{U}", "U"), them);
		var rock = Field(TestGames.Inline("Rock", "{1}", "Artifact"), them);
		var ugin = Hand(Lsv("Ugin, Eye of the Storms"), me);

		var asked = CastAsking(ugin, On<ChooseTargetChoice>(red));
		Assert.That(
			Offered<ChooseTargetChoice>(asked),
			Does.Not.Contain(rock),
			"a colourless permanent is no target"
		);
		Assert.That(InExile(red), "when you cast this spell");
		Assert.That(s.IsOnBattlefield(ugin));

		var elf = Hand(Coloured("Elf", "{G}", "G"), me);
		asked = CastAsking(elf, Min);
		Assert.That(asked.OfType<ChooseTargetChoice>(), Nu.Empty, "a green spell: no trigger");

		var golem = Hand(TestGames.Creature("Golem", "3/3"), me);
		CastAsking(golem, On<ChooseTargetChoice>(blue));
		Assert.That(InExile(blue), "whenever you cast a colorless spell");
	}

	[Test]
	public void Ugin_MinusEleven_AnyNumberOfColourlessNonlandCards_CastFreeThisTurn()
	{
		var ugin = Field(Lsv("Ugin, Eye of the Storms"), me);
		s = s.UpdateObject(ugin, s.GetCard(ugin) with { Loyalty = 11 }); // TEST SETUP: Put skips entering
		var golem = Put(TestGames.Creature("Golem", "3/3"), me, ZoneType.Library);
		var rock = Put(TestGames.Inline("Rock", "{1}", "Artifact"), me, ZoneType.Library);
		var elf = Put(Coloured("Elf", "{G}", "G"), me, ZoneType.Library);

		Loyalty(ugin, 2);
		var asked = ResolveAsking(On<ChooseFromLibraryChoice>(golem, rock));

		var offered = Offered<ChooseFromLibraryChoice>(asked).ToList();
		Assert.That(offered, Does.Not.Contain(elf), "colorless cards only");
		Assert.That(
			offered.Any(id => s.GetCard(id).Is(CardType.Land)),
			Nu.False,
			"nonland cards only"
		);
		Assert.That(InExile(golem) && InExile(rock), "any number of them, exiled");
		Assert.That(Casts(golem).Any(c => c.WithoutPayingManaCost), "cast without paying");
		Assert.That(Casts(rock).Any(c => c.WithoutPayingManaCost));
	}

	[Test]
	public void ShelldockIsle_Hideaway_ThenTheExiledCardIsPlayedFreeOnceALibraryIsDownToTwenty()
	{
		EmptyBoard();
		Field(TestGames.Card("Island"), me);
		var titan = OnTop(TestGames.CreatureCosting("{6}", "Titan", "6/6"));
		var library = s.CardsIn(me, ZoneType.Library).Count();

		var isle = Play("Shelldock Isle");
		Resolve(On<ChooseAmongChoice>(titan));

		Assert.That(s.GetCard(isle).IsTapped, "enters tapped");
		Assert.That(InExile(titan), "exile one");
		Assert.That(s.GetCard(titan).IsHiddenFrom(them), "face down");
		Assert.That(s.GetCard(titan).IsHiddenFrom(me), Nu.False, "I looked at it");
		Assert.That(
			s.CardsIn(me, ZoneType.Library).Count(),
			Nu.EqualTo(library - 1),
			"the rest on the bottom"
		);

		Untap(isle);
		Assert.That(Activations(isle), Nu.Empty, "no library has twenty or fewer cards");
		foreach (var card in s.CardsIn(them, ZoneType.Library).Skip(20).ToList())
			s = s.MoveCard(card.Id, s.ZoneId(them, ZoneType.Graveyard)); // TEST SETUP: "a library" — theirs
		Activate(isle, 0);
		Resolve();

		Assert.That(
			Casts(titan).Any(c => c.WithoutPayingManaCost),
			"you may play the exiled card without paying its mana cost"
		);
	}

	// CR 607.2a: the card is linked to the object the hideaway ability came from — an Isle that left and came back
	// is a new one, and never plays the first Isle's card.
	[Test]
	public void ShelldockIsle_ThatLeftBeforeItsHideawayResolved_IsNotLinkedToTheCard()
	{
		EmptyBoard();
		var isle = Play("Shelldock Isle"); // its hideaway is on the stack
		s = s.MoveCard(isle, s.ZoneId(me, ZoneType.Hand)); // TEST SETUP: bounced in response …
		s = s.EnterBattlefield(isle, me); // … and back: a new object, with a hideaway of its own

		var picks = new List<int>();
		Resolve(c =>
		{
			if (c is not ChooseAmongChoice)
				return Min(c);
			picks.Add(c.Options[0].Id);
			return [picks.Last()];
		});

		Assert.That(picks, Has.Count.EqualTo(2), "both hideaways resolved");
		Assert.That(
			picks.Count(id =>
				s.GetCard(id).GetComponent<ExiledBy>()!.SourceIncarnation
				== s.GetCard(isle).Incarnation
			),
			Nu.EqualTo(1),
			"only its own hideaway's card is the new Isle's exiled card"
		);
	}

	[Test]
	public void Grindstone_MillsTwo_AndAgainWhileTheTwoMilledShareAColour()
	{
		var grindstone = Field(Lsv("Grindstone"), me);
		// from the top: red, red (they share: again) — red, blue (they don't: it stops) — and a fifth that stays
		var stays = OnTop(Coloured("E", "{R}", "R"), them);
		var d = OnTop(Coloured("D", "{U}", "U"), them);
		var c = OnTop(Coloured("C", "{R}", "R"), them);
		var b = OnTop(Coloured("B", "{R}", "R"), them);
		var a = OnTop(Coloured("A", "{R}", "R"), them);

		Activate(grindstone, 0, them);
		Resolve();

		Assert.That(new[] { a, b, c, d }.All(InGraveyard), "two, then two more");
		Assert.That(s.IsIn(stays, them, ZoneType.Library), "red and blue share no colour");
	}

	[Test]
	public void Grindstone_TwoColourlessCardsShareNoColour()
	{
		var grindstone = Field(Lsv("Grindstone"), me);
		var stays = OnTop(Coloured("C", "{R}", "R"), them);
		var b = OnTop(TestGames.Creature("Golem", "3/3"), them);
		var a = OnTop(TestGames.Creature("Automaton", "2/2"), them);

		Activate(grindstone, 0, them);
		Resolve();

		Assert.That(InGraveyard(a) && InGraveyard(b));
		Assert.That(s.IsIn(stays, them, ZoneType.Library));
	}

	[Test]
	public void Palantir_TheyDecline_YouMillItsInfluenceCounters_TheyLoseTheMilledManaValue()
	{
		var palantir = Field(Lsv("Palantír of Orthanc"), me);
		var trolls = new[]
		{
			OnTop(TestGames.CreatureCosting("{3}", "Troll", "3/3")),
			OnTop(TestGames.CreatureCosting("{3}", "Troll", "3/3")),
		};
		var life = Life(them);

		To(Step.End);
		Resolve(); // the scry keeps both; they don't have me draw (a "may": its minimum)

		Assert.That(s.GetCard(palantir).CountersOf(CounterKinds.Influence), Nu.EqualTo(1));
		Assert.That(trolls.Count(InGraveyard), Nu.EqualTo(1), "mill X: one influence counter");
		Assert.That(Life(them), Nu.EqualTo(life - 3), "the total mana value of those cards");
	}

	[Test]
	public void Palantir_TheOpponentMayHaveYouDrawInstead()
	{
		Field(Lsv("Palantír of Orthanc"), me);
		var (hand, life, graveyard) = (
			HandCount(me),
			Life(them),
			s.CardsIn(me, ZoneType.Graveyard).Count()
		);

		To(Step.End);
		var deciders = new List<int>();
		Resolve(c =>
		{
			if (c is not YesNoChoice)
				return Min(c);
			deciders.Add(s.DecidingPlayerId());
			return [1];
		});

		Assert.That(
			deciders,
			Nu.EqualTo(new[] { them }),
			"target opponent may have you draw a card"
		);
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1));
		Assert.That(Life(them), Nu.EqualTo(life));
		Assert.That(s.CardsIn(me, ZoneType.Graveyard).Count(), Nu.EqualTo(graveyard), "no mill");
	}

	[Test]
	public void MishrasBauble_LooksAtTheTopOfTheirLibrary_AndDrawsAtTheNextTurnsUpkeep()
	{
		var bauble = Field(Lsv("Mishra's Bauble"), me);
		var library = s.CardsIn(them, ZoneType.Library).Select(c => c.Id).Take(2).ToList();

		Activate(bauble, 0, them);
		Resolve();

		Assert.That(
			s.GetCard(library[0]).KnownTo.HasFlag(Knowers.Opponent),
			"I looked at their top"
		);
		Assert.That(
			s.GetCard(library[1]).KnownTo.HasFlag(Knowers.Opponent),
			Nu.False,
			"only the top"
		);
		Assert.That(InGraveyard(bauble));

		var hand = HandCount(me);
		To(Step.End);
		Resolve();
		Assert.That(HandCount(me), Nu.EqualTo(hand), "not at this turn's end step");
		ToNext(Step.Upkeep);
		Resolve();
		Assert.That(s.GetGame().ActivePlayerId, Nu.EqualTo(them), "the next turn's upkeep");
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1), "draw a card");
	}

	[Test]
	public void MishrasBauble_OnYourOwnLibrary_OnlyYouKnowYourTopCard()
	{
		var bauble = Field(Lsv("Mishra's Bauble"), me);
		var top = s.CardsIn(me, ZoneType.Library).First().Id;

		Activate(bauble, 0, me);
		Resolve();

		Assert.That(s.GetCard(top).KnownTo, Nu.EqualTo(Knowers.Owner));
	}

	[Test]
	public void UrzasBauble_LooksAtOneCardAtRandomInTheirHand()
	{
		var bauble = Field(Lsv("Urza's Bauble"), me);

		Activate(bauble, 0, them);
		Resolve();

		Assert.That(
			s.CardsIn(them, ZoneType.Hand).Count(c => c.KnownTo.HasFlag(Knowers.Opponent)),
			Nu.EqualTo(1),
			"a card at random — not the hand"
		);
	}

	[Test]
	public void ShiftingWoodland_WithDelirium_BecomesACopyOfAPermanentCardInYourGraveyard()
	{
		var woodland = Field(Lsv("Shifting Woodland"), me);
		var ogre = Put(TestGames.Creature("Ogre", "4/4"), me, ZoneType.Graveyard);
		Put(TestGames.Inline("Bolt", "{R}", "Instant"), me, ZoneType.Graveyard);
		Put(TestGames.Inline("Rite", "{B}", "Sorcery"), me, ZoneType.Graveyard);
		Assert.That(Activations(woodland), Nu.Empty, "three card types: no delirium");
		var island = Put(TestGames.Card("Island"), me, ZoneType.Graveyard);
		Assert.That(
			Activations(woodland).Select(a => a.Targets.Single()),
			Nu.EquivalentTo(new[] { ogre, island }),
			"a PERMANENT card in your graveyard"
		);

		Activate(woodland, 0, ogre);
		Resolve();

		var copy = s.GetCard(woodland);
		Assert.That(copy.Name, Nu.EqualTo("Ogre"));
		Assert.That(copy.Is(CardType.Creature));
		Assert.That(s.Power(copy), Nu.EqualTo(4));
		Assert.That(InGraveyard(ogre), "the card stays where it is");
	}

	[Test]
	public void ShiftingWoodland_EntersTappedUnlessYouControlAForest()
	{
		EmptyBoard();
		var alone = Play("Shifting Woodland");
		Assert.That(s.GetCard(alone).IsTapped);

		EmptyBoard();
		Field(TestGames.Card("Forest"), me);
		var withForest = Play("Shifting Woodland");
		Assert.That(s.GetCard(withForest).IsTapped, Nu.False);
	}
}
