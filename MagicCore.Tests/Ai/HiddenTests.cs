using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Ai;

/// <summary>What the search may know of the libraries (AI-3.3) and the opponent's hand (AI-3.4) — Hidden.Sample at its root.</summary>
public class HiddenTests
{
	GameState s = null!;
	int me;
	int them;

	[SetUp]
	public void OurFirstMain()
	{
		s = TestGames.InFirstMain();
		me = s.GetGame().StartingPlayerId;
		them = s.OpponentOf(me);
	}

	List<int> Library(GameState state, int player) =>
		[.. state.CardsIn(player, ZoneType.Library).Select(c => c.Id)];

	GameState Shuffled(GameState state) => Hidden.Sample(state, me, Hidden.Library.ShuffleUnknown);

	List<int> Hand(GameState state, int player) =>
		[.. state.CardsIn(player, ZoneType.Hand).Select(c => c.Id)];

	GameState UnseenHand(GameState state) =>
		Hidden.Sample(state, me, Hidden.Library.RealOrder, Hidden.Hand.ShuffleUnseen);

	GameState GuessedHand(GameState state) =>
		Hidden.Sample(state, me, Hidden.Library.RealOrder, Hidden.Hand.GuessedFromColors);

	[Test]
	public void RealOrder_IsTheRealPosition()
	{
		Assert.That(Hidden.Sample(s, me, Hidden.Library.RealOrder), Is.SameAs(s));
	}

	// What I know stays put — my surveil's keep, a card I saw go on top of theirs — and the rest moves.
	[Test]
	public void WhatIKnow_StaysWhereItIs_AndTheRestIsShuffled()
	{
		var (mine, theirs) = (Library(s, me), Library(s, them));
		s = s.MakeKnown(mine[0], Knowers.Owner).MakeKnown(mine[5], Knowers.Owner);
		s = s.MakeKnown(theirs[0], Knowers.Opponent);

		var sample = Shuffled(s);
		var (myAfter, theirsAfter) = (Library(sample, me), Library(sample, them));

		Assert.That(
			(myAfter[0], myAfter[5], theirsAfter[0]),
			Is.EqualTo((mine[0], mine[5], theirs[0]))
		);
		Assert.That(myAfter, Is.EquivalentTo(mine).And.Not.EqualTo(mine));
		Assert.That(theirsAfter, Is.EquivalentTo(theirs).And.Not.EqualTo(theirs));
	}

	// Their surveil's keep is THEIR knowledge, not mine: it is shuffled for me.
	[Test]
	public void WhatOnlyTheyKnow_IsShuffledForMe()
	{
		var top = Library(s, them)[0];
		s = s.MakeKnown(top, Knowers.Owner);

		Assert.That(Library(Shuffled(s), them)[0], Is.Not.EqualTo(top));
	}

	// Their hand cards I haven't seen are drawn again from their unseen cards: one I saw stays, and so
	// does a library card whose place I know. Nothing of mine moves.
	[Test]
	public void TheirUnseenHand_IsDrawnAgain_FromTheirUnseenCards()
	{
		var (hand, library) = (Hand(s, them), Library(s, them));
		s = s.MakeKnown(hand[0], Knowers.Opponent).MakeKnown(library[0], Knowers.Opponent);
		var (myHand, myLibrary) = (Hand(s, me), Library(s, me));

		var sample = UnseenHand(s);
		var (handAfter, libraryAfter) = (Hand(sample, them), Library(sample, them));

		Assert.That(handAfter, Has.Count.EqualTo(hand.Count).And.Contains(hand[0]));
		Assert.That(handAfter, Is.Not.EquivalentTo(hand));
		Assert.That(libraryAfter[0], Is.EqualTo(library[0]));
		Assert.That(handAfter.Concat(libraryAfter), Is.EquivalentTo(hand.Concat(library)));
		Assert.That(Hand(sample, me), Is.EqualTo(myHand));
		Assert.That(Library(sample, me), Is.EqualTo(myLibrary));
	}

	// A card drawn from their library swaps places with the hand card it replaced, so every other card
	// keeps its place: the library's own mode is measured apart (Shayne, AI-3.4).
	[Test]
	public void ACardDrawnFromTheLibrary_SwapsPlaces_AndTheRestStays()
	{
		var (hand, library) = (Hand(s, them), Library(s, them));

		var after = Library(UnseenHand(s), them);
		var moved = Enumerable.Range(0, library.Count).Where(i => after[i] != library[i]).ToList();

		Assert.That(moved, Is.Not.Empty);
		Assert.That(moved.Select(i => after[i]), Is.SubsetOf(hand));
	}

	// Their unseen hand is GUESSED: new cards, never theirs, of the colours they have shown — here only
	// a Forest, so green or colourless, and a guessed land is a Forest.
	[Test]
	public void TheirUnseenHand_IsGuessed_InTheColoursTheyHaveShown()
	{
		(s, _) = TestGames.Put(s, them, TestGames.Card("Forest"), ZoneType.Battlefield);
		var hand = Hand(s, them);

		var sample = GuessedHand(s);
		var guessed = sample.CardsIn(them, ZoneType.Hand).ToList();

		Assert.That(guessed.SelectMany(c => c.Colors), Is.All.EqualTo(ManaColor.Green));
		Assert.That(
			guessed.Where(c => c.Is(CardType.Land)).Select(c => c.Name),
			Is.All.EqualTo("Forest")
		);
	}

	// A guess takes the id and place of the card it replaces, and makes no new id: a searched line's
	// new objects get the ids the real game will give them, so a committed plan can name them.
	[Test]
	public void AGuess_KeepsTheIdsOfTheHand_AndMakesNoNewOne()
	{
		var sample = GuessedHand(s);

		Assert.That(Hand(sample, them), Is.EqualTo(Hand(s, them)));
		Assert.That(sample.NextId, Is.EqualTo(s.NextId));
		Assert.That(
			sample.CardsIn(them, ZoneType.Hand).Select(c => c.Name),
			Is.Not.EqualTo(s.CardsIn(them, ZoneType.Hand).Select(c => c.Name))
		);
	}

	// A hand card I saw stays; their library and everything of mine are untouched.
	[Test]
	public void AGuess_KeepsWhatISaw_AndTouchesNothingElse()
	{
		var hand = Hand(s, them);
		s = s.MakeKnown(hand[0], Knowers.Opponent);
		var (library, myHand, myLibrary) = (Library(s, them), Hand(s, me), Library(s, me));

		var sample = GuessedHand(s);

		Assert.That(Hand(sample, them), Has.Count.EqualTo(hand.Count).And.Contains(hand[0]));
		Assert.That(Library(sample, them), Is.EqualTo(library));
		Assert.That(Hand(sample, me), Is.EqualTo(myHand));
		Assert.That(Library(sample, me), Is.EqualTo(myLibrary));
	}

	// Lands from what's left of a 17-land deck: with all 17 shown, no guess is a land.
	[Test]
	public void AGuess_HasNoLand_OnceAllSeventeenAreShown()
	{
		for (var i = 0; i < 17; i++)
			(s, _) = TestGames.Put(s, them, TestGames.Card("Forest"), ZoneType.Battlefield);

		var guessed = GuessedHand(s).CardsIn(them, ZoneType.Hand).ToList();

		Assert.That(guessed, Is.Not.Empty.And.None.Matches<Card>(c => c.Is(CardType.Land)));
	}

	// The sample's own randomness is not the game's next: a shuffle inside a rollout can't predict it.
	[Test]
	public void TheSample_HasItsOwnSeed()
	{
		Assert.That(Shuffled(s).RngSeed, Is.Not.EqualTo(s.RngSeed));
	}

	[Test]
	public void TheSamePosition_GetsTheSameSample()
	{
		Assert.That(Library(Shuffled(s), me), Is.EqualTo(Library(Shuffled(s), me)));
	}

	// A search lists one copy per name, the first in library order: whatever the sample, the choice it
	// offers the search is the real choice's, so the pick can be played.
	[Test]
	public void AChoiceOfLibraryCards_OffersTheRealChoicesIds()
	{
		var tutor = TestGames.Inline(
			"Tutor",
			"{G}",
			"Sorcery",
			null,
			new Effects.SearchLibrary(CardType.Land, ZoneType.Hand)
		);
		(s, _) = TestGames.Put(s, me, TestGames.Card("Forest"), ZoneType.Battlefield);
		int card;
		(s, card) = TestGames.Put(s, me, tutor, ZoneType.Hand);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = card });
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is ChooseFromLibraryChoice);

		Assert.That(s.GetPendingChoice(), Is.TypeOf<ChooseFromLibraryChoice>());
		Assert.That(
			Shuffled(s).GetPendingChoice()!.Options.Select(o => o.Id),
			Is.EqualTo(s.GetPendingChoice()!.Options.Select(o => o.Id))
		);
	}

	// The search in this mode plays a real FRA game — every decision it takes in its sample is one the
	// real game accepts (PlayAis fails on a refused one) — and plays it differently from the real
	// order: the same seed, a different game, so the search really decides from the sample.
	[Test]
	public void TheSearch_PlaysARealGame_FromItsSamples()
	{
		Assert.That(
			Play(new(Library: Hidden.Library.ShuffleUnknown)),
			Is.Not.EqualTo(Play(new(Library: Hidden.Library.RealOrder)))
		);
	}

	// The same for the opponent's hand, with an opponent that casts from it — the only one it matters to.
	[Test]
	public void TheSearch_PlaysARealGame_FromItsSampledHands()
	{
		var casts = BeamSearchAi.DefaultRollout with
		{
			Opponent = Rollout.OpponentModel.CastsFromHand,
		};
		Assert.That(
			Play(new(RolloutSettings: casts, Hand: Hidden.Hand.ShuffleUnseen)),
			Is.Not.EqualTo(Play(new(RolloutSettings: casts)))
		);
	}

	[Test]
	public void TheSearch_PlaysARealGame_FromItsGuessedHands()
	{
		var casts = BeamSearchAi.DefaultRollout with
		{
			Opponent = Rollout.OpponentModel.CastsFromHand,
		};
		Assert.That(
			Play(new(RolloutSettings: casts, Hand: Hidden.Hand.GuessedFromColors)),
			Is.Not.EqualTo(Play(new(RolloutSettings: casts)))
		);
	}

	static string Play(BeamSearchAi.Settings options)
	{
		var game = MagicRules.NewGame(
			FraDecks.All["White-Blue Jace"],
			FraDecks.All["White-Black"],
			seed: 3
		);
		game = TestGames.PlayAis(
			game,
			6,
			new BeamSearchAi(game.Player1Id(), options),
			new BeamSearchAi(game.Player2Id(), options)
		);
		return string.Join(
			";",
			new[] { game.Player1Id(), game.Player2Id() }.Select(p =>
				$"{game.GetPlayer(p).Life}:{string.Join(",", game.CardsIn(p, ZoneType.Battlefield).Select(c => c.Name).Order())}:{game.CardsIn(p, ZoneType.Graveyard).Count()}"
			)
		);
	}
}
