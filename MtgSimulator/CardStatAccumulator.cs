namespace MtgSimulator;

/// <summary>
/// Accumulates games-in-hand counts per card and per card pair, producing a
/// <see cref="DraftTrainingData"/>.
///
/// A card is credited once per game in which it was drawn regardless of how many copies were
/// drawn, and a pair only when both halves were drawn in that same game.
///
/// Extracted from DraftTrainer, where it was a private class, because the constructed
/// evolution mode counts exactly the same thing over its own games. The counting rule is
/// subtle enough (games-in-hand, not games-in-deck; DeckGames recorded whether drawn or not)
/// that a second copy would drift, and the two tables would stop being comparable — which is
/// the entire point of keeping them in the same format.
/// </summary>
public sealed class CardStatAccumulator
{
	private readonly Dictionary<string, (int Games, int Wins, int DeckGames)> _cards =
		new(StringComparer.Ordinal);
	private readonly Dictionary<(string A, string B), (int Games, int Wins, int DeckGames)> _pairs =
		[];
	private int _perspectives;
	private int _wins;

	/// <param name="deckSpells">
	/// The non-land cards actually in this deck. Recorded whether drawn or not — the
	/// drawn/in-deck ratio is what lets a scorer weigh a pair term (needs both cards drawn)
	/// against a card term (needs only one) on a common per-game scale.
	/// </param>
	public void Add(IReadOnlyList<string> drawnCards, IReadOnlyList<string> deckSpells, bool won)
	{
		_perspectives++;
		if (won)
			_wins++;

		// Sorted so every pair key is (A <= B).
		var deck = deckSpells
			.Distinct(StringComparer.Ordinal)
			.OrderBy(n => n, StringComparer.Ordinal)
			.ToList();
		var inDeck = deck.ToHashSet(StringComparer.Ordinal);
		var wasDrawn = drawnCards.Where(inDeck.Contains).ToHashSet(StringComparer.Ordinal);

		foreach (var name in deck)
		{
			var c = _cards.GetValueOrDefault(name);
			var hit = wasDrawn.Contains(name);
			_cards[name] = (
				c.Games + (hit ? 1 : 0),
				c.Wins + (hit && won ? 1 : 0),
				c.DeckGames + 1
			);
		}

		for (var i = 0; i < deck.Count; i++)
		{
			for (var j = i + 1; j < deck.Count; j++)
			{
				var key = (deck[i], deck[j]);
				var p = _pairs.GetValueOrDefault(key);
				var hit = wasDrawn.Contains(deck[i]) && wasDrawn.Contains(deck[j]);
				_pairs[key] = (
					p.Games + (hit ? 1 : 0),
					p.Wins + (hit && won ? 1 : 0),
					p.DeckGames + 1
				);
			}
		}
	}

	public DraftTrainingData ToData() =>
		new(
			_perspectives,
			_wins,
			_cards
				.OrderBy(kv => kv.Key, StringComparer.Ordinal)
				.Select(kv => new CardStat(
					kv.Key,
					kv.Value.Games,
					kv.Value.Wins,
					kv.Value.DeckGames
				))
				.ToList(),
			_pairs
				.OrderBy(kv => kv.Key.A, StringComparer.Ordinal)
				.ThenBy(kv => kv.Key.B, StringComparer.Ordinal)
				.Select(kv => new PairStat(
					kv.Key.A,
					kv.Key.B,
					kv.Value.Games,
					kv.Value.Wins,
					kv.Value.DeckGames
				))
				.ToList()
		);
}
