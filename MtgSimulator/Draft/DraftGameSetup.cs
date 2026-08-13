using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Builds a playable pre-begin GameState from two drafted pools. Shared by
/// <see cref="DraftRunner"/> and <see cref="DraftTrainer"/> so the deck-loading idiom
/// is not duplicated again.
/// </summary>
public static class DraftGameSetup
{
	/// <summary>
	/// Same shape as SimulatorRunner.SetupGame. Never calls BeginGame — GameRunner.Run does.
	///
	/// BuildDeck stamps OwnerId/ControllerId, so this must run per game: a seat is Player 1
	/// in some pairings and Player 2 in others.
	/// </summary>
	public static (
		GameState State,
		MtgGameIds Ids,
		IReadOnlyDictionary<int, string> CardNames
	) Build(IReadOnlyList<Card> pool1, IReadOnlyList<Card> pool2)
	{
		var (state, ids) = MtgGameFactory.Create();
		var cardNames = new Dictionary<int, string>();

		foreach (var card in Draft.BuildDeck(pool1, ids.Player1Id))
		{
			var (newState, added) = state.AddObject(card, parentId: ids.Player1LibraryId);
			state = newState;
			cardNames[added.Id] = added.Name;
		}

		foreach (var card in Draft.BuildDeck(pool2, ids.Player2Id))
		{
			var (newState, added) = state.AddObject(card, parentId: ids.Player2LibraryId);
			state = newState;
			cardNames[added.Id] = added.Name;
		}

		return (state, ids, cardNames);
	}
}
