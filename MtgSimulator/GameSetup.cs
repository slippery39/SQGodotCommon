using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Loads two decks into a pre-begin <see cref="GameState"/>.
///
/// This is the part every deck source has in common: create the game, add each card to its
/// owner's library zone, and record the id → name map the runners report with. HOW the cards
/// are chosen is not shared and stays with whoever chooses them — <see cref="DraftGameSetup"/>
/// calls <c>Draft.BuildDeck</c>, <see cref="ConstructedGameSetup"/> calls
/// <c>Decklist.Materialize</c>, and both end up here.
///
/// Deliberately NOT an overload on either of those. A single Build that means "pools" or
/// "decklists" depending on the argument type is the kind of signature that gets called with
/// the wrong one; the two callers are named for what they take.
/// </summary>
public static class GameSetup
{
	/// <summary>
	/// Takes deck BUILDERS rather than decks, because every builder stamps OwnerId/ControllerId
	/// and those ids only exist once the game does. The alternative is creating a throwaway
	/// game just to read the ids off it.
	///
	/// A delegate parameter, not a stored one: it is called here and dropped, so the
	/// no-delegates serialization rule does not apply. Same shape and same reasoning as
	/// <c>DeckRegistry.DeckInfo.Builder</c>.
	///
	/// Never calls BeginGame — GameRunner.Run does, and it captures the resulting events.
	/// </summary>
	public static (
		GameState State,
		MtgGameIds Ids,
		IReadOnlyDictionary<int, string> CardNames
	) FromDecks(
		Func<int, IReadOnlyList<Card>> buildDeck1,
		Func<int, IReadOnlyList<Card>> buildDeck2
	)
	{
		var (state, ids) = MtgGameFactory.Create();
		var cardNames = new Dictionary<int, string>();

		state = Load(state, buildDeck1(ids.Player1Id), ids.Player1LibraryId, cardNames);
		state = Load(state, buildDeck2(ids.Player2Id), ids.Player2LibraryId, cardNames);

		return (state, ids, cardNames);
	}

	private static GameState Load(
		GameState state,
		IReadOnlyList<Card> deck,
		int libraryId,
		Dictionary<int, string> cardNames
	)
	{
		foreach (var card in deck)
		{
			var (newState, added) = state.AddObject(card, parentId: libraryId);
			state = newState;
			cardNames[added.Id] = added.Name;
		}
		return state;
	}
}
