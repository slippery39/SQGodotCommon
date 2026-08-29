using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Builds a playable pre-begin GameState from two drafted pools. Shared by
/// <see cref="DraftRunner"/> and <see cref="DraftTrainer"/> so the deck-loading idiom
/// is not duplicated again.
///
/// Owns only the draft-specific step — turning a 45-card pool into a 40-card deck. Loading
/// those cards into a GameState is <see cref="GameSetup.FromDecks"/>, shared with constructed.
/// </summary>
public static class DraftGameSetup
{
	/// <summary>
	/// Same shape as SimulatorRunner.SetupGame. Never calls BeginGame — GameRunner.Run does.
	///
	/// BuildDeck stamps OwnerId/ControllerId, so this must run per game: a seat is Player 1
	/// in some pairings and Player 2 in others. Passing BuildDeck as a builder rather than
	/// calling it here is what guarantees that.
	/// </summary>
	public static (
		GameState State,
		MtgGameIds Ids,
		IReadOnlyDictionary<int, string> CardNames
	) Build(IReadOnlyList<Card> pool1, IReadOnlyList<Card> pool2) =>
		GameSetup.FromDecks(
			ownerId => Draft.BuildDeck(pool1, ownerId),
			ownerId => Draft.BuildDeck(pool2, ownerId)
		);
}
