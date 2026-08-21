using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Drawing from an empty library loses the game.
///
/// Until this existed the engine had exactly one loss condition — life reaching zero — and an
/// empty library was a silent no-op: `LibraryEmptyEvent` went to the caller-visible log and
/// nothing read it. Decked games therefore could not end. Flagged training games sat at turn 101
/// with both libraries at zero and both players alive, having fired 332 of those events.
/// </summary>
[TestFixture]
public class DeckingTests
{
	private GameState _state;
	private MtgGameIds _ids;

	// Create(), not CreateForTesting() — the latter disables the very rule under test.
	// None of these need mana.
	[SetUp]
	public void Setup() => (_state, _ids) = MtgGameFactory.Create();

	private (GameState State, System.Collections.Immutable.ImmutableList<GameEvent> Events) Draw(
		GameState state,
		int playerId,
		int amount = 1
	) =>
		state
			.AddAction(new DrawCardsAction { TargetIds = [playerId], Amount = amount })
			.ProcessAllActions();

	[Test]
	public void DrawingFromAnEmptyLibraryLosesTheGame()
	{
		var (state, events) = Draw(_state, _ids.Player1Id);

		Assert.Multiple(() =>
		{
			Assert.That(state.GetPlayer(_ids.Player1Id).HasLost, Is.True);
			Assert.That(state.GetPlayer(_ids.Player2Id).HasLost, Is.False);
			Assert.That(
				events.OfType<PlayerLostEvent>().Single().Reason,
				Does.Contain("library"),
				"GameRunner reads this string to report GameEndReason.LibraryEmpty"
			);
			Assert.That(
				events.OfType<GameOverEvent>().Single().WinnerPlayerId,
				Is.EqualTo(_ids.Player2Id)
			);
		});
	}

	/// The rule is about the attempt to draw, not the count — an empty library on its own is
	/// survivable right up until something asks you to draw from it.
	[Test]
	public void AnEmptyLibraryAloneIsNotALoss()
	{
		var (state, _) = _state
			.AddAction(new GainLifeAction { TargetIds = [_ids.Player1Id], Amount = 1 })
			.ProcessAllActions();

		Assert.That(state.GetCardsInZone(_ids.Player1LibraryId).Count(), Is.Zero);
		Assert.That(state.GetPlayer(_ids.Player1Id).HasLost, Is.False);
	}

	/// Milling out is not decking. Only a draw kills, which is why the flag lives in
	/// DrawCardsAction rather than being derived from library size.
	[Test]
	public void MillingAnEmptyLibraryIsNotALoss()
	{
		var (state, _) = _state
			.AddAction(new MillAction { TargetIds = [_ids.Player1Id], Amount = 3 })
			.ProcessAllActions();

		Assert.That(state.GetPlayer(_ids.Player1Id).HasLost, Is.False);
	}

	/// Drawing the last card is fine; it is the next draw that kills.
	[Test]
	public void DrawingTheLastCardIsSafe()
	{
		var stocked = TestCardFactory.AddCardsToLibrary(_state, _ids.Player1Id, "Only Card");

		var (afterLast, _) = Draw(stocked, _ids.Player1Id);
		Assert.That(
			afterLast.GetPlayer(_ids.Player1Id).HasLost,
			Is.False,
			"The last card is a legal draw"
		);

		var (afterEmpty, _) = Draw(afterLast, _ids.Player1Id);
		Assert.That(afterEmpty.GetPlayer(_ids.Player1Id).HasLost, Is.True);
	}

	/// Both decking at once is a draw, the same as simultaneous lethal damage.
	///
	/// "At once" means one symmetric effect ("each player draws a card") resolving into a single
	/// state-based check. Drawing them one after the other is NOT this case and must not be
	/// written as though it were: the first player to deck has already lost, so the game is over
	/// and the survivor has won before the second draw happens.
	[Test]
	public void BothPlayersDeckingOnTheSameEffectIsADraw()
	{
		var (state, events) = _state
			.AddAction(
				new DrawCardsAction { TargetIds = [_ids.Player1Id, _ids.Player2Id], Amount = 1 }
			)
			.ProcessAllActions();

		Assert.Multiple(() =>
		{
			Assert.That(state.GetPlayer(_ids.Player1Id).HasLost, Is.True);
			Assert.That(state.GetPlayer(_ids.Player2Id).HasLost, Is.True);
			Assert.That(events.OfType<GameOverEvent>().Last().WinnerPlayerId, Is.EqualTo(-1));
		});
	}

	/// The sequential case, spelled out because it is the one that looks like a draw and is not.
	[Test]
	public void TheFirstPlayerToDeckLosesOutright()
	{
		var (afterP1, _) = Draw(_state, _ids.Player1Id);

		Assert.That(afterP1.GetPlayer(_ids.Player1Id).HasLost, Is.True);
		Assert.That(afterP1.GetPlayer(_ids.Player2Id).HasLost, Is.False);
	}
}
