using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
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

	// ===== MC-4.4e2c: blue's options, mana and costs =====

	/// <summary>TEST SETUP: every land of mine tapped — what pays is the rest of the board.</summary>
	void TapMyLands()
	{
		foreach (
			var land in s.CardsIn(me, ZoneType.Battlefield).Where(c => c.Is(CardType.Land)).ToList()
		)
			TapIt(land.Id);
	}

	bool KnownToMe(int theirCard) =>
		(s.GetCard(theirCard).KnownTo & Knowers.Opponent) == Knowers.Opponent;

	[Test]
	public void GitaxianProbe_UPaidWithTwoLife_LookAtTargetPlayersHand_DrawACard()
	{
		var probe = Hand(Lsv("Gitaxian Probe"), me);
		var secret = Hand(Bear, them);
		Assert.That(
			Casts(probe).Select(c => (c.Targets[0], c.LifeForPhyrexian)),
			Nu.EquivalentTo(new[] { (me, 0), (me, 1), (them, 0), (them, 1) }),
			"one cast per target and number of {U/P} paid with life"
		);
		Assert.That(
			s.DistinctMoves(s.LegalActions())
				.OfType<CastSpellAction>()
				.Count(c => c.CardId == probe),
			Nu.EqualTo(4),
			"the AI tells them apart (MoveKey)"
		);
		var (lands, life, hand) = (UntappedLands(), Life(me), HandCount(me));
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = probe,
				Targets = [them],
				LifeForPhyrexian = 1,
			}
		);
		Resolve();
		Assert.That(Life(me), Nu.EqualTo(life - 2), "{U/P} paid with 2 life");
		Assert.That(UntappedLands(), Nu.EqualTo(lands), "no mana");
		Assert.That(HandCount(me), Nu.EqualTo(hand), "the Probe left, a card was drawn");
		Assert.That(KnownToMe(secret), "look at target player's hand");
	}

	[Test]
	public void GitaxianProbe_UPaidWithBlue_AndNoLifeOptionWithoutTwoLife()
	{
		var probe = Hand(Lsv("Gitaxian Probe"), me);
		var (lands, life) = (UntappedLands(), Life(me));
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = probe,
				Targets = [them],
			}
		);
		Resolve();
		Assert.That((Life(me), UntappedLands()), Nu.EqualTo((life, lands - 1)));
		var again = Hand(Lsv("Gitaxian Probe"), me);
		s = s.WithPlayer(s.GetPlayer(me) with { Life = 1 });
		Assert.That(Casts(again).Select(c => c.LifeForPhyrexian), Nu.All.EqualTo(0));
	}

	[Test]
	public void PhyrexianMetamorph_EntersAsACopy_AnArtifactInAddition()
	{
		var bear = Field(Bear, them);
		var trinket = Field(TestCards.Trinket, them);
		var metamorph = Hand(Lsv("Phyrexian Metamorph"), me);
		Assert.That(
			Casts(metamorph).Select(c => c.LifeForPhyrexian),
			Nu.EquivalentTo(new[] { 0, 1 })
		);
		var life = Life(me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = metamorph,
				LifeForPhyrexian = 1,
			}
		);
		var asked = ResolveAsking(On<ChooseCreatureToCopyChoice>(bear));
		Assert.That(
			Offered<ChooseCreatureToCopyChoice>(asked),
			Nu.SupersetOf(new[] { bear, trinket }),
			"any artifact or creature"
		);
		var copy = s.GetCard(metamorph);
		Assert.That(copy.Name, Nu.EqualTo("Bear"));
		Assert.That(
			copy.Is(CardType.Artifact) && copy.Is(CardType.Creature),
			"an artifact in addition"
		);
		Assert.That(Life(me), Nu.EqualTo(life - 2));
	}

	[Test]
	public void Urza_EntersWithAConstruct_PlusOnePerArtifactYouControl()
	{
		Field(TestCards.Trinket, me);
		Field(TestCards.Trinket, them);
		Cast(Hand(Lsv("Urza, Lord High Artificer"), me), []);
		var construct = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Construct");
		Assert.That(construct.Is(CardType.Artifact) && construct.Is(CardType.Creature));
		Assert.That(
			(s.Power(construct), s.Toughness(construct)),
			Nu.EqualTo((2, 2)),
			"the Trinket and itself"
		);
	}

	[TestCase(2, true)]
	[TestCase(1, false)]
	public void Urza_TapAnUntappedArtifactYouControl_AddU(int artifacts, bool castable)
	{
		var urza = Field(Lsv("Urza, Lord High Artificer"), me);
		var tapped = Enumerable
			.Range(0, artifacts)
			.Select(_ => Field(TestCards.Trinket, me))
			.ToList();
		TapMyLands();
		var spell = Hand(TestGames.Inline("Twin Sorcery", "{U}{U}", "Sorcery"), me);
		Assert.That(s.LegalActions().OfType<TapForManaAction>(), Nu.Empty, "no move per artifact");
		Assert.That(Casts(spell).Any(), Nu.EqualTo(castable));
		if (!castable)
			return;
		Cast(spell, []);
		Assert.That(tapped.All(id => s.GetCard(id).IsTapped), "each artifact tapped for a {U}");
		Assert.That(s.GetCard(urza).IsTapped, Nu.False, "Urza himself isn't tapped");
	}

	[Test]
	public void Urza_Five_ShuffleExileTheTop_PlayItFreeThisTurn()
	{
		var urza = Field(Lsv("Urza, Lord High Artificer"), me);
		var big = OnTop(TestGames.Inline("Big Sorcery", "{7}", "Sorcery"), me);
		LibraryOf(1);
		s = s.Act(Activations(urza).Single());
		Resolve();
		Assert.That(s.IsIn(big, me, ZoneType.Exile));
		var casts = Casts(big).ToList();
		Assert.That(casts, Nu.Not.Empty);
		Assert.That(casts.All(c => c.WithoutPayingManaCost), "without paying its mana cost");
		var lands = UntappedLands();
		s = s.Act(casts[0]);
		Resolve();
		Assert.That(s.IsIn(big, me, ZoneType.Graveyard));
		Assert.That(UntappedLands(), Nu.EqualTo(lands));
	}

	[Test]
	public void Urza_ALotusPetalBesideHimIsStillSacrificedForAnyColour()
	{
		Field(Lsv("Urza, Lord High Artificer"), me);
		Field(Lsv("Lotus Petal"), me);
		TapMyLands();
		Assert.That(
			Casts(Hand(TestGames.Inline("Red Sorcery", "{R}", "Sorcery"), me)),
			Nu.Not.Empty
		);
	}

	/// <summary>Urza's {5} activated with only this card in my library — it is exiled, free this turn.</summary>
	int UrzaExiles(CardDefinition card)
	{
		var urza = Field(Lsv("Urza, Lord High Artificer"), me);
		LibraryOf(0);
		var id = Put(card, me, ZoneType.Library);
		s = s.Act(Activations(urza).Single());
		Resolve();
		return id;
	}

	[Test]
	public void Urza_AFreeCastFromHisExileIsNoAlternativeCostOfTheCards()
	{
		var bear = Field(Bear, them);
		var mastery = UrzaExiles(Lsv("Baleful Mastery"));
		var hand = HandCount(them);
		s = s.Act(Casts(mastery).Single(c => c.Targets.Contains(bear)));
		Resolve();
		Assert.That(Alive(bear), Nu.False);
		Assert.That(HandCount(them), Nu.EqualTo(hand), "the {1}{B} wasn't paid: no card for them");
	}

	[Test]
	public void ThievingSkydiver_CastFreeFromUrzasExile_CanStillBeKickedForX()
	{
		var diver = UrzaExiles(Lsv("Thieving Skydiver"));
		Assert.That(
			Casts(diver).Any(c => c is { Kicked: true, WithoutPayingManaCost: true, X: 1 }),
			"kicker {X} is an additional cost"
		);
	}

	[TestCase(1)]
	[TestCase(2)] // two reductions, but never below one mana
	public void ForensicGadgeteer_AnArtifactSpellInvestigates_ItsAbilitiesCostOneLess(
		int gadgeteers
	)
	{
		for (var i = 0; i < gadgeteers; i++)
			Field(Lsv("Forensic Gadgeteer"), me);
		Cast(Hand(TestCards.Trinket, me), []);
		var clues = s.CardsIn(me, ZoneType.Battlefield).Where(c => c.Name == "Clue").ToList();
		Assert.That(clues, Has.Count.EqualTo(gadgeteers), "investigate");
		var (lands, hand) = (UntappedLands(), HandCount(me));
		s = s.Act(Activations(clues[0].Id).Single());
		Resolve();
		Assert.That(UntappedLands(), Nu.EqualTo(lands - 1), "the Clue's {2} is {1}");
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1));
	}

	[Test]
	public void ThievingSkydiver_KickerX_XCantBeZero()
	{
		var diver = Hand(Lsv("Thieving Skydiver"), me);
		var casts = Casts(diver).ToList();
		Assert.That(casts.Where(c => !c.Kicked).Select(c => c.X), Nu.EquivalentTo(new[] { 0 }));
		Assert.That(casts.Where(c => c.Kicked).Select(c => c.X), Has.Member(1).And.No.Member(0));
		Assert.That(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = diver,
				Kicked = true,
			}
				.ValidateAdd(s)
				.IsValid,
			Nu.False,
			"X can't be 0"
		);
	}

	[TestCase(true)]
	[TestCase(false)]
	public void ThievingSkydiver_Kicked_GainControlOfAnArtifactOfManaValueXOrLess_AnEquipmentAttached(
		bool equipment
	)
	{
		var cheap = Field(
			TestGames.Inline("Blade", "{2}", equipment ? "Artifact — Equipment" : "Artifact"),
			them
		);
		var dear = Field(TestGames.Inline("Relic", "{3}", "Artifact"), them);
		var diver = Hand(Lsv("Thieving Skydiver"), me);
		var lands = UntappedLands();
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = diver,
				Kicked = true,
				X = 2,
			}
		);
		var asked = ResolveAsking(On<ChooseTargetChoice>(cheap));
		Assert.That(UntappedLands(), Nu.EqualTo(lands - 4), "{1}{U} and an X of 2");
		Assert.That(Offered<ChooseTargetChoice>(asked), Has.Member(cheap).And.No.Member(dear));
		Assert.That(s.IsIn(cheap, me, ZoneType.Battlefield), "gain control of it");
		Assert.That(s.GetCard(cheap).AttachedToId, Nu.EqualTo(equipment ? diver : 0));
	}

	[Test]
	public void ThievingSkydiver_NotKicked_TakesNothing()
	{
		var relic = Field(TestGames.Inline("Zero Relic", "{0}", "Artifact"), them); // mana value 0: X is 0 unkicked
		var asked = CastAsking(Hand(Lsv("Thieving Skydiver"), me), On<ChooseTargetChoice>(relic));
		Assert.That(asked.OfType<ChooseTargetChoice>(), Nu.Empty);
		Assert.That(s.IsIn(relic, them, ZoneType.Battlefield));
	}

	// ===== MC-4.4e2d: casting off the hand =====

	bool InExile(int card) => s.IsIn(card, s.GetCard(card).OwnerId, ZoneType.Exile);

	bool InGraveyard(int card) => s.IsIn(card, s.GetCard(card).OwnerId, ZoneType.Graveyard);

	/// <summary>These attack; every choice to the end of combat answered as told (blocks: none).</summary>
	void AttackWith(Func<ChoiceAction, IEnumerable<int>> answer, params int[] attackers)
	{
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareAttackerChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice d)
			s = s.Choose([attackers.Contains(d.CreatureId) ? them : 0]);
		for (var i = 0; i < 200 && s.GetGame().Step != Step.PostCombatMain; i++)
			s = s.GetPendingChoice() is { } choice
				? s.Choose(answer(choice))
				: s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });
	}

	[Test]
	public void Bilbo_Attacks_CastAnArtifactFromYourGraveyard_ItEnters_ForOneLess()
	{
		var bilbo = Field(Lsv("Bilbo, Thief in the Night"), me);
		var trinket = Put(TestGames.Inline("Trinket", "{2}", "Artifact"), me, ZoneType.Graveyard);
		var lands = UntappedLands();
		AttackWith(On<ChooseAmongChoice>(trinket), bilbo);
		Assert.That(
			Alive(trinket),
			"an artifact cast from the graveyard resolves onto the battlefield"
		);
		Assert.That(
			UntappedLands(),
			Nu.EqualTo(lands - 1),
			"{2}, one less: not cast from the hand"
		);
	}

	[Test]
	public void Bilbo_ASorceryCastThisWayIsExiled_AndOneFromTheHandCostsInFull()
	{
		var bilbo = Field(Lsv("Bilbo, Thief in the Night"), me);
		var lands = UntappedLands();
		Cast(Hand(TestCards.Fodder, me), []);
		Assert.That(UntappedLands(), Nu.EqualTo(lands - 1), "{1} from the hand: no less");
		var sorcery = Put(TestCards.Fodder, me, ZoneType.Graveyard);
		lands = UntappedLands();
		AttackWith(On<ChooseAmongChoice>(sorcery), bilbo);
		Assert.That(UntappedLands(), Nu.EqualTo(lands), "{1}, one less");
		Assert.That(InExile(sorcery), "exile it instead of the graveyard");
	}

	[Test]
	public void Bilbo_AttacksWithNothingCastable_AsksNothing()
	{
		var bilbo = Field(Lsv("Bilbo, Thief in the Night"), me);
		Put(Bear, me, ZoneType.Graveyard); // a creature card: not an artifact, instant or sorcery
		var asked = new List<ChoiceAction>();
		AttackWith(
			c =>
			{
				asked.Add(c);
				return c.Options.Take(c.MinChoices).Select(o => o.Id);
			},
			bilbo
		);
		Assert.That(asked.OfType<ChooseAmongChoice>(), Nu.Empty);
	}

	[TestCase(3, true)]
	[TestCase(2, false)]
	public void Malcolm_CombatDamage_AChorusCounter_Loot_AtFourCastTheDiscardedCardFree(
		int counters,
		bool casts
	)
	{
		var malcolm = Field(Lsv("Malcolm, Alluring Scoundrel"), me);
		s = s.PutCounters(malcolm, CounterKinds.Chorus, counters); // TEST SETUP
		var elk = Hand(TestGames.CreatureCosting("{3}{G}", "Elk", "3/3"), me);
		var lands = UntappedLands();
		var life = Life(them);
		AttackWith(
			c =>
				c switch
				{
					ChooseCardsFromHandChoice => [elk],
					ChooseAmongChoice => [elk],
					_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
				},
			malcolm
		);
		Assert.That(Life(them), Nu.EqualTo(life - 2));
		Assert.That(s.GetCard(malcolm).CountersOf(CounterKinds.Chorus), Nu.EqualTo(counters + 1));
		Assert.That(Alive(elk), Nu.EqualTo(casts), "cast from the graveyard, it enters");
		Assert.That(InGraveyard(elk), Nu.EqualTo(!casts));
		Assert.That(UntappedLands(), Nu.EqualTo(lands), "without paying its mana cost");
	}

	[TestCase(true)]
	[TestCase(false)]
	public void Flash_PutACreatureFromYourHand_SacrificeItUnlessYouPayItsCostLessTwo(bool pay)
	{
		var elk = Hand(TestGames.CreatureCosting("{3}{G}", "Elk", "3/3"), me);
		var lands = UntappedLands();
		var asked = CastAsking(
			Hand(Lsv("Flash"), me),
			c =>
				c switch
				{
					ChooseCardsFromHandChoice => [elk],
					YesNoChoice when pay => [1],
					_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
				}
		);
		Assert.That(asked.OfType<YesNoChoice>().Count(), Nu.EqualTo(1));
		Assert.That(Alive(elk), Nu.EqualTo(pay));
		Assert.That(InGraveyard(elk), Nu.EqualTo(!pay), "sacrificed");
		Assert.That(UntappedLands(), Nu.EqualTo(lands - 2 - (pay ? 2 : 0)), "{1}{U}, then {1}{G}");
	}

	[Test]
	public void Flash_APhyrexianSymbolInItsCost_IsPaidWithItsColour()
	{
		var metamorph = Hand(Lsv("Phyrexian Metamorph"), me); // {3}{U/P}: reduced by {2}, {1}{U/P}
		var lands = UntappedLands();
		CastAsking(
			Hand(Lsv("Flash"), me),
			c =>
				c switch
				{
					ChooseCardsFromHandChoice => [metamorph],
					YesNoChoice => [1],
					_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
				}
		);
		Assert.That(UntappedLands(), Nu.EqualTo(lands - 2 - 2), "{1}{U}, then {1}{U}");
	}

	[Test]
	public void Bilbo_AFlashbackCardCastThisWay_PaysItsManaCost_NotItsFlashbackCost()
	{
		var bilbo = Field(Lsv("Bilbo, Thief in the Night"), me);
		var echo = Put(
			new CardDefinition(
				TestGames.Inline("Echo", "{3}", "Sorcery").Data,
				[new FlashbackComponent(ManaCost.Parse("{1}"))]
			),
			me,
			ZoneType.Graveyard
		);
		var lands = UntappedLands();
		AttackWith(On<ChooseAmongChoice>(echo), bilbo);
		Assert.That(
			UntappedLands(),
			Nu.EqualTo(lands - 2),
			"{3}, one less — never flashback's {1}"
		);
		Assert.That(InExile(echo));
	}

	[Test]
	public void Flash_ACostThatCantBePaid_IsSacrificedUnasked()
	{
		// {G}{G}{G}{G}{G}: no generic to reduce, and four Forests can't pay it.
		var hydra = Hand(TestGames.CreatureCosting("{G}{G}{G}{G}{G}", "Hydra", "5/5"), me);
		var asked = CastAsking(Hand(Lsv("Flash"), me), On<ChooseCardsFromHandChoice>(hydra));
		Assert.That(asked.OfType<YesNoChoice>(), Nu.Empty);
		Assert.That(InGraveyard(hydra), "sacrifice it unless you pay");
	}

	[TestCase("{2}", false, true, 0)] // less than or equal: onto the battlefield, nothing to pay
	[TestCase("{3}", false, true, 0)]
	[TestCase("{5}", true, true, 2)] // greater: pay the difference
	[TestCase("{5}", false, false, 0)] // or into its owner's graveyard
	public void TransmuteArtifact_SacrificeAnArtifact_SearchForOne_PayTheDifference(
		string cost,
		bool pay,
		bool enters,
		int difference
	)
	{
		var relic = Field(TestGames.Inline("Relic", "{3}", "Artifact"), me);
		var found = Put(TestGames.Inline("Found", cost, "Artifact"), me, ZoneType.Library);
		var lands = UntappedLands();
		var asked = CastAsking(
			Hand(Lsv("Transmute Artifact"), me),
			c =>
				c switch
				{
					ChoosePermanentChoice => [relic],
					ChooseFromLibraryChoice => [found],
					YesNoChoice when pay => [1],
					_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
				}
		);
		Assert.That(InGraveyard(relic), "sacrificed");
		Assert.That(
			asked.OfType<YesNoChoice>().Count(),
			Nu.EqualTo(difference > 0 || !enters ? 1 : 0)
		);
		Assert.That(Alive(found), Nu.EqualTo(enters));
		Assert.That(InGraveyard(found), Nu.EqualTo(!enters));
		Assert.That(UntappedLands(), Nu.EqualTo(lands - 2 - difference), "{U}{U}, then {X}");
	}

	[Test]
	public void TransmuteArtifact_NoArtifactToSacrifice_NoSearch()
	{
		var found = Put(TestGames.Inline("Found", "{0}", "Artifact"), me, ZoneType.Library);
		var asked = CastAsking(
			Hand(Lsv("Transmute Artifact"), me),
			On<ChooseFromLibraryChoice>(found)
		);
		Assert.That(asked.OfType<ChooseFromLibraryChoice>(), Nu.Empty);
		Assert.That(s.IsIn(found, me, ZoneType.Library));
	}
}
