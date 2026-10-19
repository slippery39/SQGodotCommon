using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Ai;

/// <summary>What the search may know of the libraries (story AI-3.3) — Hidden.Sample at its root.</summary>
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
		string Play(Hidden.Library mode)
		{
			var game = MagicRules.NewGame(
				FraDecks.All["White-Blue Jace"],
				FraDecks.All["White-Black"],
				seed: 3
			);
			var options = new BeamSearchAi.Settings(Library: mode);
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

		Assert.That(
			Play(Hidden.Library.ShuffleUnknown),
			Is.Not.EqualTo(Play(Hidden.Library.RealOrder))
		);
	}
}
