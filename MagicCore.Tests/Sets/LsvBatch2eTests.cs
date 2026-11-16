using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
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
}
