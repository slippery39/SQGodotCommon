using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Turns;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4e1, LSVPower — blue's tier 1 made of tested pieces: each card's ability FIRES, against its PRINTED
/// text, through real casting and the real stack.
/// </summary>
public class LsvBatch2eTests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	/// <summary>My untapped lands — what a cast or activation tapped is the difference.</summary>
	int UntappedLands() =>
		s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Is(CardType.Land) && !c.IsTapped);

	/// <summary>"Reveal it": its owner's opponent knows the card, wherever it went.</summary>
	bool Revealed(int card) => (s.GetCard(card).KnownTo & Knowers.Opponent) == Knowers.Opponent;

	int[] TopSeven(int player) =>
		[.. s.CardsIn(player, ZoneType.Library).Take(7).Select(c => c.Id)];

	IEnumerable<int> HandOf(int player) => s.CardsIn(player, ZoneType.Hand).Select(c => c.Id);

	/// <summary>Answers a target choice with this target and a "you may" as told; any other with its minimum.</summary>
	static Func<ChoiceAction, IEnumerable<int>> Targeting(int target, bool yes) =>
		c =>
			c switch
			{
				ChooseTargetChoice => [target],
				YesNoChoice when yes => [1],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			};

	[Test]
	public void MysticalTutor_AnInstantOrSorceryOnTopOfTheLibrary()
	{
		var sorcery = Put(TestCards.Fodder, me, ZoneType.Library);
		var instant = Put(TestCards.Kill(), me, ZoneType.Library);
		var bear = Put(Bear, me, ZoneType.Library);
		var asked = CastAsking(
			Hand(Lsv("Mystical Tutor"), me),
			On<ChooseFromLibraryChoice>(instant)
		);
		var offered = Offered<ChooseFromLibraryChoice>(asked).ToList();
		Assert.That(offered, Nu.SupersetOf(new[] { sorcery, instant })); // and the deck's own
		Assert.That(offered, Nu.Not.Contains(bear));
		Assert.That(s.CardsIn(me, ZoneType.Library).First().Id, Nu.EqualTo(instant));
		Assert.That(Revealed(instant), "reveal it");
	}

	[Test]
	public void Spellseeker_EntersSearchingForAnInstantOrSorceryOfManaValueTwoOrLess()
	{
		var cheap = Put(TestCards.Fodder, me, ZoneType.Library);
		var dear = Put(TestGames.Inline("Dear", "{3}", "Sorcery"), me, ZoneType.Library);
		var trinket = Put(TestCards.Trinket, me, ZoneType.Library);
		var asked = CastAsking(Hand(Lsv("Spellseeker"), me), On<ChooseFromLibraryChoice>(cheap));
		var offered = Offered<ChooseFromLibraryChoice>(asked).ToList();
		Assert.That(offered, Nu.SupersetOf(new[] { cheap }));
		Assert.That(offered, Nu.Not.Contains(dear), "mana value 2 or less");
		Assert.That(offered, Nu.Not.Contains(trinket), "an instant or sorcery");
		Assert.That(s.IsIn(cheap, me, ZoneType.Hand));
		Assert.That(Revealed(cheap), "reveal it");
	}

	[Test]
	public void TrinketMage_EntersSearchingForAnArtifactOfManaValueOneOrLess()
	{
		var cheap = Put(TestCards.Trinket, me, ZoneType.Library);
		var dear = Put(TestGames.Inline("Relic", "{2}", "Artifact"), me, ZoneType.Library);
		var sorcery = Put(TestCards.Fodder, me, ZoneType.Library);
		var asked = CastAsking(Hand(Lsv("Trinket Mage"), me), On<ChooseFromLibraryChoice>(cheap));
		var offered = Offered<ChooseFromLibraryChoice>(asked).ToList();
		Assert.That(offered, Nu.SupersetOf(new[] { cheap }));
		Assert.That(offered, Nu.Not.Contains(dear), "mana value 1 or less");
		Assert.That(offered, Nu.Not.Contains(sorcery), "an artifact");
		Assert.That(s.IsIn(cheap, me, ZoneType.Hand));
		Assert.That(Revealed(cheap), "reveal that card");
	}

	[Test]
	public void Tinker_SacrificesAnArtifact_AndPutsOneFromTheLibraryOntoTheBattlefield()
	{
		var colossus = Put(TestGames.Inline("Colossus", "{7}", "Artifact"), me, ZoneType.Library);
		var tinker = Hand(Lsv("Tinker"), me);
		Assert.That(Casts(tinker), Nu.Empty, "no artifact to sacrifice: it can't be cast");
		var trinket = Field(TestCards.Trinket, me);
		Field(Bear, me);
		s = s.Act(Casts(tinker).Single()); // one cast per artifact, never the Bear
		Assert.That(Alive(trinket), Nu.False, "sacrificed as it is cast");
		Resolve(On<ChooseFromLibraryChoice>(colossus));
		Assert.That(Alive(colossus));
	}

	[Test]
	public void SnapcasterMage_AnInstantInMyGraveyardGainsFlashback_ForItsManaCost()
	{
		var kill = Put(TestCards.Kill(), me, ZoneType.Graveyard);
		var bear = Field(Bear, them);
		Assert.That(Casts(kill), Nu.Empty, "not from the graveyard yet");
		CastWithTrigger(Hand(Lsv("Snapcaster Mage"), me), [], kill);
		var lands = UntappedLands();
		Cast(kill, [], bear);
		Assert.That(lands - UntappedLands(), Nu.EqualTo(1), "its mana cost: Kill's {1}");
		Assert.That(Alive(bear), Nu.False);
		Assert.That(s.IsIn(kill, me, ZoneType.Exile), "flashback exiles it");
	}

	[Test]
	public void DisplacerKitten_ANoncreatureSpell_BlinksUpToOneNonlandPermanentOfMine()
	{
		var kitten = Field(Lsv("Displacer Kitten"), me);
		var bear = Field(Bear, me);
		var theirs = Field(Bear, them);
		TapIt(bear);
		var before = s.GetCard(bear).Incarnation;
		var asked = CastAsking(Hand(Bear, me), On<ChooseTargetChoice>(bear));
		Assert.That(asked, Nu.Empty, "a creature spell doesn't trigger it");
		asked = CastAsking(Hand(TestCards.Fodder, me), On<ChooseTargetChoice>(bear));
		var offered = Offered<ChooseTargetChoice>(asked).ToList();
		Assert.That(offered, Nu.SupersetOf(new[] { bear, kitten }));
		Assert.That(offered, Nu.Not.Contains(theirs), "you control");
		Assert.That(offered.Select(s.GetCard).Any(c => c.Is(CardType.Land)), Nu.False, "nonland");
		Assert.That(
			asked.OfType<ChooseTargetChoice>().Single().MinChoices,
			Nu.EqualTo(0),
			"up to one: none is an answer"
		);
		Assert.That(Alive(bear));
		Assert.That(s.GetCard(bear).Incarnation, Nu.Not.EqualTo(before), "a new object");
		Assert.That(s.GetCard(bear).IsTapped, Nu.False);
	}

	[Test]
	public void BoomerangBasics_ReturnsANonlandPermanent_AndDrawsOnlyWhenItWasMine()
	{
		s = TestGames.EmptyHands(s);
		var theirs = Field(Bear, them);
		var mine = Field(Bear, me);
		var first = Hand(Lsv("Boomerang Basics"), me);
		var second = Hand(Lsv("Boomerang Basics"), me);
		Assert.That(
			Casts(first).Select(a => a.Targets[0]),
			Nu.EquivalentTo(new[] { theirs, mine }),
			"a nonland permanent"
		);
		Cast(first, [], theirs);
		Assert.That(s.IsIn(theirs, them, ZoneType.Hand));
		Assert.That(HandCount(me), Nu.EqualTo(1), "theirs: no card — only the second Basics");
		Cast(second, [], mine);
		Assert.That(s.IsIn(mine, me, ZoneType.Hand));
		Assert.That(HandCount(me), Nu.EqualTo(2), "mine: the Bear and a card drawn");
	}

	[Test]
	public void Upheaval_ReturnsAllPermanentsToTheirOwnersHands()
	{
		s = TestGames.EmptyHands(s);
		var mine = Field(Bear, me);
		var theirs = Field(TestCards.Trinket, them);
		var myLands = s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Is(CardType.Land));
		var theirLands = s.CardsIn(them, ZoneType.Battlefield).Count(c => c.Is(CardType.Land));
		Cast(Hand(Lsv("Upheaval"), me), []);
		Assert.That(s.CardsIn(me, ZoneType.Battlefield), Nu.Empty);
		Assert.That(s.CardsIn(them, ZoneType.Battlefield), Nu.Empty);
		Assert.That(s.IsIn(mine, me, ZoneType.Hand));
		Assert.That(s.IsIn(theirs, them, ZoneType.Hand));
		Assert.That(HandCount(me), Nu.EqualTo(myLands + 1), "lands too");
		Assert.That(HandCount(them), Nu.EqualTo(theirLands + 1));
	}

	/// <summary>TEST SETUP: empty hands, then this many cards in each player's hand and graveyard; the library sizes.</summary>
	(int Mine, int Theirs) HandsAndGraveyards(int myHand, int myYard, int theirHand, int theirYard)
	{
		s = TestGames.EmptyHands(s);
		foreach (
			var (player, zone, n) in new[]
			{
				(me, ZoneType.Hand, myHand),
				(me, ZoneType.Graveyard, myYard),
				(them, ZoneType.Hand, theirHand),
				(them, ZoneType.Graveyard, theirYard),
			}
		)
			for (var i = 0; i < n; i++)
				Put(TestCards.Fodder, player, zone);
		return (s.CardsIn(me, ZoneType.Library).Count(), s.CardsIn(them, ZoneType.Library).Count());
	}

	[Test]
	public void Timetwister_EachPlayerShufflesHandAndGraveyardIn_ThenDrawsSeven()
	{
		var (mine, theirs) = HandsAndGraveyards(2, 3, 0, 2);
		var theirTop = TopSeven(them);
		var twister = Hand(Lsv("Timetwister"), me);
		Cast(twister, []);
		Assert.That(HandCount(me), Nu.EqualTo(7));
		Assert.That(HandCount(them), Nu.EqualTo(7));
		Assert.That(HandOf(them), Nu.Not.EquivalentTo(theirTop), "a graveyard shuffled in");
		Assert.That(
			s.CardsIn(me, ZoneType.Graveyard).Select(c => c.Id),
			Nu.EqualTo(new[] { twister }),
			"only Timetwister itself, put there after"
		);
		Assert.That(s.CardsIn(them, ZoneType.Graveyard), Nu.Empty);
		Assert.That(s.CardsIn(me, ZoneType.Library).Count(), Nu.EqualTo(mine + 2 + 3 - 7));
		Assert.That(s.CardsIn(them, ZoneType.Library).Count(), Nu.EqualTo(theirs + 2 - 7));
	}

	[Test]
	public void EchoOfEons_ByFlashback_EachPlayerShufflesInAndDrawsSeven_AndItIsExiled()
	{
		var (mine, theirs) = HandsAndGraveyards(1, 2, 3, 0);
		var echo = Put(Lsv("Echo of Eons"), me, ZoneType.Graveyard);
		var theirTop = TopSeven(them);
		var lands = UntappedLands();
		Cast(echo, []); // from the graveyard
		Assert.That(lands - UntappedLands(), Nu.EqualTo(3), "flashback {2}{U}");
		Assert.That(s.IsIn(echo, me, ZoneType.Exile), "flashback exiles it");
		Assert.That(HandCount(me), Nu.EqualTo(7));
		Assert.That(HandCount(them), Nu.EqualTo(7));
		Assert.That(HandOf(them), Nu.Not.EquivalentTo(theirTop), "a hand shuffled in");
		Assert.That(s.CardsIn(me, ZoneType.Graveyard), Nu.Empty);
		Assert.That(s.CardsIn(me, ZoneType.Library).Count(), Nu.EqualTo(mine + 1 + 2 - 7));
		Assert.That(s.CardsIn(them, ZoneType.Library).Count(), Nu.EqualTo(theirs + 3 - 7));
	}

	[Test]
	public void CryogenRelic_DrawsEnteringAndLeaving_AndStunsUpToOneTappedCreature()
	{
		s = TestGames.EmptyHands(s);
		var tapped = Field(Bear, them);
		Field(Bear, them); // untapped: never a target
		TapIt(tapped);
		var relic = Hand(Lsv("Cryogen Relic"), me);
		Cast(relic, []);
		Assert.That(HandCount(me), Nu.EqualTo(1), "a card as it enters");
		Assert.That(
			Activations(relic).Select(a => a.Targets[0]),
			Nu.EquivalentTo(new[] { tapped, 0 }),
			"up to one tapped creature — never the untapped one"
		);
		var lands = UntappedLands();
		s = s.Act(Activations(relic).Single(a => a.Targets[0] == tapped));
		Resolve();
		Assert.That(lands - UntappedLands(), Nu.EqualTo(2), "{1}{U}");
		Assert.That(Alive(relic), Nu.False, "sacrificed as the cost");
		Assert.That(s.GetCard(tapped).StunCounters, Nu.EqualTo(1));
		Assert.That(HandCount(me), Nu.EqualTo(2), "and a card as it leaves");
	}

	[Test]
	public void SewerVeillanceCam_EnteringOrLeaving_MayTapOrUntapTargetCreature_AndDrawsTwo()
	{
		s = TestGames.EmptyHands(s);
		var bear = Field(Bear, them);
		CastAsking(Hand(Lsv("Sewer-veillance Cam"), me), Targeting(bear, yes: false));
		Assert.That(s.GetCard(bear).IsTapped, Nu.False, "you may: declined");
		var cam = Hand(Lsv("Sewer-veillance Cam"), me);
		CastAsking(cam, Targeting(bear, yes: true));
		Assert.That(s.GetCard(bear).IsTapped, "entering: an untapped creature is tapped");
		var lands = UntappedLands();
		s = s.Act(Activations(cam).Single());
		ResolveAsking(Targeting(bear, yes: true));
		Assert.That(lands - UntappedLands(), Nu.EqualTo(4), "{3}{U}");
		Assert.That(Alive(cam), Nu.False, "sacrificed as the cost");
		Assert.That(s.GetCard(bear).IsTapped, Nu.False, "leaving: a tapped creature is untapped");
		Assert.That(HandCount(me), Nu.EqualTo(2), "{3}{U}, sacrifice: draw two cards");
	}

	// ===== MC-4.4e2a: blue's options, the library's top =====

	/// <summary>A library, top first — unchanged after an effect, it was not shuffled.</summary>
	List<int> Library(int player) => [.. s.CardsIn(player, ZoneType.Library).Select(c => c.Id)];

	/// <summary>Its owner looked at it and knows where in the library it is.</summary>
	bool OwnerKnows(int card) => (s.GetCard(card).KnownTo & Knowers.Owner) == Knowers.Owner;

	/// <summary>TEST SETUP: my library cut down to its top n cards.</summary>
	void LibraryOf(int n)
	{
		foreach (var id in Library(me).Skip(n))
			s = s.MoveCard(id, s.ZoneId(me, ZoneType.Exile));
	}

	static CardDefinition BlueSprite => TestGames.CreatureCosting("{U}", "Sprite", "1/1");

	[Test]
	public void Ponder_LooksAtTheTopThree_PutsThemBackInAnyOrder_AndDrawsACard()
	{
		s = TestGames.EmptyHands(s);
		var third = OnTop(Bear);
		var second = OnTop(TestCards.Fodder);
		var first = OnTop(TestCards.Trinket);
		var below = Library(me).Skip(3).ToList();
		var lands = UntappedLands();
		var asked = CastAsking(
			Hand(Lsv("Ponder"), me),
			On<OrderLibraryTopChoice>(third, first, second)
		);
		Assert.That(lands - UntappedLands(), Nu.EqualTo(1), "{U}");
		Assert.That(asked.OfType<ChooseAmongChoice>(), Nu.Empty, "none of them is taken");
		var order = asked.OfType<OrderLibraryTopChoice>().Single();
		Assert.That(
			order.Options.Select(o => o.Id),
			Nu.EquivalentTo(new[] { first, second, third }),
			"the top three"
		);
		Assert.That(order.MinChoices, Nu.EqualTo(3), "put them back: every one");
		Assert.That(asked.OfType<YesNoChoice>().Count(), Nu.EqualTo(1), "you may shuffle");
		Assert.That(HandOf(me), Nu.EqualTo(new[] { third }), "draw a card: the one put on top");
		Assert.That(
			Library(me),
			Nu.EqualTo(new[] { first, second }.Concat(below)),
			"in the order chosen — and, the shuffle declined, not shuffled"
		);
		Assert.That(OwnerKnows(first) && OwnerKnows(second), "looked at: you know where they are");
	}

	[Test]
	public void Ponder_YouMayShuffle()
	{
		s = TestGames.EmptyHands(s);
		var third = OnTop(Bear);
		var second = OnTop(TestCards.Fodder);
		var first = OnTop(TestCards.Trinket);
		var before = Library(me);
		CastAsking(Hand(Lsv("Ponder"), me), Targeting(0, yes: true)); // put back as they were; shuffle
		var after = HandOf(me).Concat(Library(me)).ToList(); // the card drawn was on top
		Assert.That(after, Nu.EquivalentTo(before));
		Assert.That(after, Nu.Not.EqualTo(before), "shuffled, then the draw");
		Assert.That(
			new[] { first, second, third }.Where(id => s.IsIn(id, me, ZoneType.Library)),
			Has.None.Matches<int>(OwnerKnows),
			"a shuffle forgets what was looked at"
		);
	}

	[Test]
	public void StockUp_TwoOfTheTopFiveIntoYourHand_TheRestOnTheBottom()
	{
		s = TestGames.EmptyHands(s);
		var five = new List<int>();
		foreach (var card in new[] { Bear, TestCards.Fodder, TestCards.Trinket, Bear, Bear })
			five.Add(OnTop(card));
		var lands = UntappedLands();
		var asked = CastAsking(Hand(Lsv("Stock Up"), me), On<ChooseAmongChoice>(five[0], five[3]));
		Assert.That(lands - UntappedLands(), Nu.EqualTo(3), "{2}{U}");
		var take = asked.OfType<ChooseAmongChoice>().Single();
		Assert.That(take.Options.Select(o => o.Id), Nu.EquivalentTo(five), "the top five");
		Assert.That(
			(take.MinChoices, take.MaxChoices),
			Nu.EqualTo((2, 2)),
			"put two of them — not 'up to', not 'may'"
		);
		Assert.That(HandOf(me), Nu.EquivalentTo(new[] { five[0], five[3] }));
		var rest = new[] { five[1], five[2], five[4] };
		Assert.That(Library(me).TakeLast(3), Nu.EquivalentTo(rest), "the rest on the bottom");
		Assert.That(rest, Has.None.Matches<int>(OwnerKnows), "a random order is not known");
	}

	[Test]
	public void StockUp_AfterPonder_WhatGoesToTheBottomIsNoLongerKnown()
	{
		s = TestGames.EmptyHands(s);
		var third = OnTop(Bear);
		var second = OnTop(TestCards.Fodder);
		OnTop(TestCards.Trinket);
		Cast(Hand(Lsv("Ponder"), me), []); // looks at the three, draws the top one
		Assert.That(OwnerKnows(second) && OwnerKnows(third), "TEST SETUP: Ponder's look is known");
		CastAsking(
			Hand(Lsv("Stock Up"), me),
			c =>
				c switch
				{
					ChooseAmongChoice => c
						.Options.Select(o => o.Id)
						.Except([second, third])
						.Take(2),
					_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
				}
		);
		Assert.That(Library(me).TakeLast(3), Nu.SupersetOf(new[] { second, third }));
		Assert.That(
			new[] { second, third },
			Has.None.Matches<int>(OwnerKnows),
			"a random order is not known — whatever was known before"
		);
	}

	[Test]
	public void LookAndTake_ToAnyOtherZoneOfYours()
	{
		s = TestGames.EmptyHands(s);
		var second = OnTop(Bear);
		var first = OnTop(TestCards.Fodder);
		var bury = TestGames.Inline(
			"Bury",
			"{1}",
			"Sorcery",
			null,
			new MagicCore.Effects.LookAndTake(2) { Must = true, To = ZoneType.Graveyard }
		);
		CastAsking(Hand(bury, me), On<ChooseAmongChoice>(second));
		Assert.That(s.IsIn(second, me, ZoneType.Graveyard), "the zone it names — not the hand");
		Assert.That(Library(me)[^1], Nu.EqualTo(first), "the rest on the bottom");
	}

	[Test]
	public void ConsultTheStarCharts_OneOfTheTopXIntoYourHand_TwoIfKicked()
	{
		s = TestGames.EmptyHands(s);
		var x = s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Is(CardType.Land));
		Assert.That(
			Library(me),
			Has.Count.GreaterThan(x + 2),
			"TEST SETUP: X is not the whole library"
		);
		var consult = Hand(Lsv("Consult the Star Charts"), me);
		Assert.That(
			Casts(consult).Select(a => a.Kicked),
			Nu.EquivalentTo(new[] { false, true }),
			"one cast, and one kicked — the cards are picked after"
		);
		var top = Library(me).Take(x).ToList();
		var lands = UntappedLands();
		s = s.Act(Casts(consult).Single(a => !a.Kicked));
		var asked = ResolveAsking(On<ChooseAmongChoice>(top[1]));
		Assert.That(lands - UntappedLands(), Nu.EqualTo(2), "{1}{U}");
		var take = asked.OfType<ChooseAmongChoice>().Single();
		Assert.That(
			take.Options.Select(o => o.Id),
			Nu.EqualTo(top),
			"the top X cards, X the lands you control"
		);
		Assert.That(
			(take.MinChoices, take.MaxChoices),
			Nu.EqualTo((1, 1)),
			"put one of those cards"
		);
		Assert.That(HandOf(me), Nu.EqualTo(new[] { top[1] }));
		Assert.That(
			Library(me).TakeLast(x - 1),
			Nu.EquivalentTo(top.Where(id => id != top[1])),
			"the rest on the bottom"
		);

		var taken = top[1];
		var again = Hand(Lsv("Consult the Star Charts"), me);
		top = Library(me).Take(x).ToList();
		lands = UntappedLands();
		s = s.Act(Casts(again).Single(a => a.Kicked));
		asked = ResolveAsking(On<ChooseAmongChoice>(top[0], top[2]));
		Assert.That(lands - UntappedLands(), Nu.EqualTo(4), "kicked: {1}{U} more");
		take = asked.OfType<ChooseAmongChoice>().Single();
		Assert.That(
			(take.MinChoices, take.MaxChoices),
			Nu.EqualTo((2, 2)),
			"if this spell was kicked, put two of those cards into your hand instead"
		);
		Assert.That(HandOf(me), Nu.EquivalentTo(new[] { taken, top[0], top[2] }));
		Assert.That(
			Library(me).TakeLast(x - 2),
			Nu.EquivalentTo(top.Where(id => id != top[0] && id != top[2])),
			"the rest on the bottom"
		);
	}

	[Test]
	public void ThassasOracle_LooksAtTheTopX_UpToOneOnTop_TheRestOnTheBottom()
	{
		s = TestGames.EmptyHands(s);
		Field(BlueSprite, me); // devotion to blue: its {U}, and the Oracle's own {U}{U}
		Field(BlueSprite, them); // "your devotion": never theirs
		var c = OnTop(Bear);
		var b = OnTop(TestCards.Fodder);
		var a = OnTop(TestCards.Trinket);
		var lands = UntappedLands();
		var asked = CastAsking(Hand(Lsv("Thassa's Oracle"), me), On<ChooseAmongChoice>(c));
		Assert.That(lands - UntappedLands(), Nu.EqualTo(2), "{U}{U}");
		var take = asked.OfType<ChooseAmongChoice>().Single();
		Assert.That(
			take.Options.Select(o => o.Id),
			Nu.EquivalentTo(new[] { a, b, c }),
			"X is your devotion to blue: three"
		);
		Assert.That((take.MinChoices, take.MaxChoices), Nu.EqualTo((0, 1)), "up to one of them");
		Assert.That(Library(me)[0], Nu.EqualTo(c), "on top of your library");
		Assert.That(OwnerKnows(c), "you looked at it, and it is where you put it");
		Assert.That(
			Library(me).TakeLast(2),
			Nu.EquivalentTo(new[] { a, b }),
			"the rest on the bottom"
		);
		Assert.That(
			new[] { a, b },
			Has.None.Matches<int>(OwnerKnows),
			"a random order is not known"
		);
		Assert.That(HandCount(me), Nu.EqualTo(0), "nothing into your hand");
		Assert.That(s.IsOver(), Nu.False, "X is less than the number of cards in your library");
	}

	[TestCase(2, true)] // X is 2: the Oracle's own {U}{U}
	[TestCase(3, false)]
	public void ThassasOracle_YouWinIfXIsAtLeastTheCardsInYourLibrary(int cards, bool wins)
	{
		s = TestGames.EmptyHands(s);
		LibraryOf(cards);
		CastAsking(Hand(Lsv("Thassa's Oracle"), me), On<ChooseAmongChoice>()); // up to one: none
		Assert.That(
			Library(me),
			Has.Count.EqualTo(cards),
			"they stay in the library, top or bottom"
		);
		Assert.That(s.IsOver() && s.WinnerId() == me, Nu.EqualTo(wins));
	}

	[Test]
	public void Timetwister_APlayerWithNoHandAndNoGraveyardStillShuffles()
	{
		HandsAndGraveyards(2, 3, 0, 0);
		var theirTop = TopSeven(them);
		Cast(Hand(Lsv("Timetwister"), me), []);
		Assert.That(HandCount(them), Nu.EqualTo(7));
		Assert.That(
			HandOf(them),
			Nu.Not.EquivalentTo(theirTop),
			"each player shuffles — with nothing to shuffle in too"
		);
	}

	[Test]
	public void ShuffleLibrary_ANameThatIsNoPlayerShufflesNothing()
	{
		var bear = Field(Bear, them);
		var mine = Library(me);
		var theirs = Library(them);
		var riffle = TestCards.Targeted(
			"Riffle",
			MagicCore.Targeting.TargetSpec.Creature,
			new MagicCore.Effects.ShuffleLibrary(MagicCore.Effects.Ref.Target())
		);
		Cast(Hand(riffle, me), [], bear);
		Assert.That(Library(me), Nu.EqualTo(mine), "a creature is no player: nobody shuffles");
		Assert.That(Library(them), Nu.EqualTo(theirs));
	}

	// ----- "you MAY search … then shuffle": no card taken, no shuffle (SearchLibrary { May }) -----

	[Test]
	public void Spellseeker_YouMaySearch_TakingNothingShufflesNothing_TakingACardShuffles()
	{
		var cheap = Put(TestCards.Fodder, me, ZoneType.Library);
		var before = Library(me);
		var asked = CastAsking(Hand(Lsv("Spellseeker"), me), On<ChooseFromLibraryChoice>());
		Assert.That(
			Offered<ChooseFromLibraryChoice>(asked),
			Has.Member(cheap),
			"there was one to take"
		);
		Assert.That(Library(me), Nu.EqualTo(before), "declined: the library is as it was");
		CastAsking(Hand(Lsv("Spellseeker"), me), On<ChooseFromLibraryChoice>(cheap));
		Assert.That(s.IsIn(cheap, me, ZoneType.Hand));
		Assert.That(
			Library(me),
			Nu.Not.EqualTo(before.Where(id => id != cheap)),
			"a card taken: then shuffle"
		);
	}

	[Test]
	public void MysticalTutor_NotAMaySearch_ShufflesWithNothingFound()
	{
		var before = Library(me);
		CastAsking(Hand(Lsv("Mystical Tutor"), me), On<ChooseFromLibraryChoice>());
		Assert.That(Library(me), Nu.EquivalentTo(before), "nothing taken");
		Assert.That(Library(me), Nu.Not.EqualTo(before), "search …, then shuffle — found or not");
	}

	[TestCase("Stoneforge Mystic")]
	[TestCase("Trinket Mage")]
	public void YouMaySearchAsItEnters_DeclinedShufflesNothing(string name)
	{
		var before = Library(me);
		var card = Hand(name, me);
		Cast(card, []); // every choice answered with its fewest: no card
		Assert.That(Alive(card), "it entered, and its trigger resolved");
		Assert.That(Library(me), Nu.EqualTo(before));
	}

	[TestCase("Path to Exile")]
	[TestCase("Erode")]
	public void ItsControllerMaySearch_DeclinedShufflesNothing(string name)
	{
		var bear = Field(Bear, them);
		var before = Library(them);
		Cast(Hand(name, me), [], bear);
		Assert.That(Alive(bear), Nu.False, "the spell resolved");
		Assert.That(Library(them), Nu.EqualTo(before));
	}

	// ===== MC-4.4e2b: draws =====

	int Treasures(int player) =>
		s.CardsIn(player, ZoneType.Battlefield).Count(c => c.Name == "Treasure");

	/// <summary>Ancestral Recall, cast by its owner: "Target player draws three cards."</summary>
	void DrawsThree(int caster, int target) =>
		Cast(Hand(Lsv("Ancestral Recall"), caster), [], target);

	/// <summary>They hold priority in their draw step: the turn's card is drawn.</summary>
	void ToTheirDrawStep() =>
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().Step == Step.Draw && x.GetGame().ActivePlayerId == them
		);

	/// <summary>I activate the planeswalker's loyalty ability (by its place among them) and it resolves.</summary>
	void Activate(int walker, int ability, params int[] targets)
	{
		s = s.Act(
			new ActivateLoyaltyAction
			{
				PlayerId = me,
				CardId = walker,
				AbilityIndex = ability,
				Targets = [.. targets],
			}
		);
		Resolve();
	}

	[Test]
	public void FaerieMastermind_AnOpponentsSecondCardEachTurn_YouDrawACard()
	{
		Cast(Hand(Lsv("Faerie Mastermind"), me), []);
		var (mine, theirs) = (HandCount(me), HandCount(them));
		DrawsThree(me, them);
		Assert.That(HandCount(them) - theirs, Nu.EqualTo(3));
		Assert.That(
			HandCount(me) - mine,
			Nu.EqualTo(1),
			"their second card — once, not their first nor their third"
		);
	}

	[Test]
	public void FaerieMastermind_YourOwnSecondCardIsNoTrigger()
	{
		Cast(Hand(Lsv("Faerie Mastermind"), me), []);
		var mine = HandCount(me);
		DrawsThree(me, me);
		Assert.That(HandCount(me) - mine, Nu.EqualTo(3), "an opponent draws");
	}

	[Test]
	public void FaerieMastermind_EachPlayerDrawsACard_AndTheirSecondIsYourCard()
	{
		var mastermind = Hand(Lsv("Faerie Mastermind"), me);
		Cast(mastermind, []);
		var lands = UntappedLands();
		var (mine, theirs) = (HandCount(me), HandCount(them));
		s = s.Act(Activations(mastermind).Single());
		Resolve();
		Assert.That(lands - UntappedLands(), Nu.EqualTo(4), "{3}{U}");
		Assert.That(
			(HandCount(me) - mine, HandCount(them) - theirs),
			Nu.EqualTo((1, 1)),
			"each player draws a card — their first this turn: no trigger"
		);
		s = s.Act(Activations(mastermind).Single());
		Resolve();
		Assert.That(
			(HandCount(me) - mine, HandCount(them) - theirs),
			Nu.EqualTo((3, 2)),
			"their second card this turn, drawn by a later ability, is counted from the turn's first"
		);
	}

	[Test]
	public void Hullbreacher_AnOpponentsDrawIsATreasureForYou()
	{
		Cast(Hand(Lsv("Hullbreacher"), me), []);
		var (mine, theirs, library) = (HandCount(me), HandCount(them), Library(them));
		DrawsThree(me, them);
		Assert.That(HandCount(them), Nu.EqualTo(theirs), "instead: no card drawn");
		Assert.That(Library(them), Nu.EqualTo(library));
		Assert.That((Treasures(me), Treasures(them)), Nu.EqualTo((3, 0)), "you create a Treasure");
		DrawsThree(me, me);
		Assert.That(HandCount(me) - mine, Nu.EqualTo(3), "an opponent would draw — not you");
		Assert.That(Treasures(me), Nu.EqualTo(3));
	}

	[Test]
	public void Hullbreacher_ExceptTheFirstCardTheyDrawInTheirDrawStep()
	{
		Cast(Hand(Lsv("Hullbreacher"), me), []);
		var theirs = HandCount(them);
		ToTheirDrawStep();
		Assert.That(HandCount(them) - theirs, Nu.EqualTo(1), "the first one of their draw step");
		Assert.That(Treasures(me), Nu.EqualTo(0));
		DrawsThree(them, them);
		Assert.That(HandCount(them) - theirs, Nu.EqualTo(1), "any other card in that draw step");
		Assert.That(Treasures(me), Nu.EqualTo(3));
	}

	[Test]
	public void Narset_AnOpponentCantDrawMoreThanOneCardEachTurn()
	{
		Cast(Hand(Lsv("Narset, Parter of Veils"), me), []);
		foreach (var id in Library(them).Skip(2)) // TEST SETUP: two cards left in their library
			s = s.MoveCard(id, s.ZoneId(them, ZoneType.Exile));
		var (mine, theirs) = (HandCount(me), HandCount(them));
		DrawsThree(me, them);
		Assert.That(HandCount(them) - theirs, Nu.EqualTo(1), "one card each turn");
		Assert.That(Library(them), Has.Count.EqualTo(1));
		DrawsThree(me, me);
		Assert.That(HandCount(me) - mine, Nu.EqualTo(3), "each opponent — not you");
		ToTheirDrawStep();
		Assert.That(HandCount(them) - theirs, Nu.EqualTo(2), "a new turn: one card again");
		DrawsThree(them, them);
		Assert.That(HandCount(them) - theirs, Nu.EqualTo(2));
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PreCombatMain || x.IsOver());
		Assert.That(
			s.IsOver(),
			Nu.False,
			"a draw that can't happen is no draw from an empty library"
		);
	}

	[Test]
	public void Narset_MinusTwo_ANoncreatureNonlandCardOfTheTopFour()
	{
		var narset = Hand(Lsv("Narset, Parter of Veils"), me);
		Cast(narset, []);
		var below = OnTop(TestCards.Fodder); // fifth from the top: not looked at
		var land = OnTop(TestGames.Card("Island"));
		var instant = OnTop(TestCards.Kill());
		var creature = OnTop(Bear);
		var sorcery = OnTop(TestCards.Fodder);
		s = s.Act(
			new ActivateLoyaltyAction
			{
				PlayerId = me,
				CardId = narset,
				AbilityIndex = 0,
			}
		);
		var asked = ResolveAsking(On<ChooseAmongChoice>(instant));
		Assert.That(
			Offered<ChooseAmongChoice>(asked),
			Nu.EquivalentTo(new[] { instant, sorcery }),
			"a noncreature, nonland card from among the top four"
		);
		Assert.That(s.GetCard(narset).Loyalty, Nu.EqualTo(3), "loyalty 5, −2");
		Assert.That(HandOf(me), Does.Contain(instant));
		Assert.That(Revealed(instant), "you may reveal …");
		Assert.That(Library(me)[0], Nu.EqualTo(below), "the rest on the bottom");
		Assert.That(Library(me).TakeLast(3), Nu.EquivalentTo(new[] { land, creature, sorcery }));
	}

	[Test]
	public void NarsetBesideHullbreacher_ADrawThatCantHappenMakesNoTreasure()
	{
		Cast(Hand(Lsv("Hullbreacher"), me), []);
		Cast(Hand(Lsv("Narset, Parter of Veils"), me), []);
		var theirs = HandCount(them);
		ToTheirDrawStep();
		DrawsThree(them, them);
		Assert.That(HandCount(them) - theirs, Nu.EqualTo(1), "the turn's card, and no more");
		Assert.That(Treasures(me), Nu.EqualTo(0), "they can't draw: nothing is replaced");
	}

	[Test]
	public void Hullbreacher_TheirDrawStepsCardDoubled_OnlyTheFirstIsExcepted()
	{
		Cast(Hand(Lsv("Hullbreacher"), me), []);
		Field(global::MagicCore.Sets.CardRegistry.Get("Mr. Spock, Science Officer"), them); // "draw two cards instead"
		var theirs = HandCount(them);
		ToTheirDrawStep();
		Assert.That(HandCount(them) - theirs, Nu.EqualTo(1), "the first one they draw");
		Assert.That(Treasures(me), Nu.EqualTo(1), "the second is any other draw");
	}

	/// <summary>Jace, Wielder of Mysteries, cast — with this much loyalty (TEST SETUP beyond his printed 4).</summary>
	int JaceWielder(int loyalty = 4)
	{
		var jace = Hand(Lsv("Jace, Wielder of Mysteries"), me);
		Cast(jace, []);
		Assert.That(s.GetCard(jace).Loyalty, Nu.EqualTo(4));
		s = s.UpdateObject(jace, s.GetCard(jace) with { Loyalty = loyalty });
		return jace;
	}

	[Test]
	public void Jace_PlusOne_TargetPlayerMillsTwo_YouDrawACard()
	{
		var jace = JaceWielder();
		Assert.That(
			s.LegalActions()
				.OfType<ActivateLoyaltyAction>()
				.Where(a => a.CardId == jace)
				.Select(a => (a.AbilityIndex, a.Targets.Single())),
			Nu.EquivalentTo(new[] { (0, me), (0, them) }),
			"target player: either — and no −8 at loyalty 4"
		);
		var (mine, theirs) = (HandCount(me), Library(them).Count);
		Activate(jace, 0, them);
		Assert.That(theirs - Library(them).Count, Nu.EqualTo(2), "target player mills two cards");
		Assert.That(s.CardsIn(them, ZoneType.Graveyard).Count(), Nu.EqualTo(2));
		Assert.That(HandCount(me) - mine, Nu.EqualTo(1), "draw a card");
		Assert.That(s.GetCard(jace).Loyalty, Nu.EqualTo(5));
	}

	[Test]
	public void Jace_ADrawFromYourEmptyLibrary_YouWinTheGameInstead()
	{
		var jace = JaceWielder();
		LibraryOf(0);
		Activate(jace, 0, them);
		Assert.That(s.IsOver() && s.WinnerId() == me);
	}

	[Test]
	public void Jace_YourDrawStepWithNoLibrary_YouWinTheGameInstead()
	{
		JaceWielder();
		LibraryOf(0);
		s = TestGames.PassUntil(s, x => x.IsOver());
		Assert.That(s.IsOver() && s.WinnerId() == me, "the turn's draw is a draw too");
	}

	[Test]
	public void Jace_AnOpponentsDrawWhileYourLibraryIsEmpty_IsADraw()
	{
		JaceWielder();
		LibraryOf(0);
		var theirs = HandCount(them);
		DrawsThree(me, them);
		Assert.That(HandCount(them) - theirs, Nu.EqualTo(3), "if YOU would draw a card");
		Assert.That(s.IsOver(), Nu.False);
	}

	[TestCase(7, true)]
	[TestCase(8, false)]
	public void Jace_MinusEight_DrawSeven_ThenYouWinIfYourLibraryIsEmpty(int cards, bool wins)
	{
		var jace = JaceWielder(8);
		LibraryOf(cards);
		var mine = HandCount(me);
		Activate(jace, 1);
		Assert.That(HandCount(me) - mine, Nu.EqualTo(7), "each of the seven had a card to draw");
		Assert.That(s.IsOver() && s.WinnerId() == me, Nu.EqualTo(wins));
	}
}
