using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Builds a playable pre-begin GameState from two decklists. The constructed sibling of
/// <see cref="DraftGameSetup"/>: it owns only the constructed-specific step — turning a
/// Decklist into cards — and hands the loading to <see cref="GameSetup.FromDecks"/>.
/// </summary>
public static class ConstructedGameSetup
{
	/// <summary>
	/// Never calls BeginGame — GameRunner.Run does.
	///
	/// Materialize stamps OwnerId/ControllerId, so it is passed as a builder rather than
	/// called here: a deck is Player 1 in some pairings and Player 2 in others.
	/// </summary>
	public static (
		GameState State,
		MtgGameIds Ids,
		IReadOnlyDictionary<int, string> CardNames
	) Build(Decklist deck1, Decklist deck2, IReadOnlyDictionary<string, Card> pool) =>
		GameSetup.FromDecks(
			ownerId => deck1.Materialize(ownerId, pool),
			ownerId => deck2.Materialize(ownerId, pool)
		);

	/// Name → template, the lookup Materialize needs. Built once per run, not per game.
	public static IReadOnlyDictionary<string, Card> PoolIndex(IReadOnlyList<Card> pool)
	{
		var index = new Dictionary<string, Card>(StringComparer.Ordinal);
		foreach (var card in pool)
			index[card.Name] = card;
		return index;
	}
}
