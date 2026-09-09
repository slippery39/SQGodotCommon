namespace MtgSimulator;

/// <summary>
/// What a card is worth INSIDE a given colour identity — the second of the three win-rate tables.
///
/// The first says what a card is worth in a random deck of its colours; this says what it is worth
/// in mono-red as opposed to red-white. Same idea as a limited stat site publishing a card's rate
/// per colour pair, and it captures something the other tables structurally cannot: the pooled
/// table averages over every identity a card is legal in, so a card that is excellent in one and
/// mediocre in four reads as the average of five things nobody plays.
///
/// **The identity rate is shrunk TOWARD the card's pooled rate, never added to it.** These are one
/// population viewed two ways — every game in an identity's table is also in the pooled table — so
/// summing them would count the same games twice and over-weight exactly the cards that already
/// have the most data. Shrinking toward the pooled rate makes the answer a delta by construction:
/// with no identity games it IS the pooled rate, and it moves away only as evidence arrives.
///
/// **Presim only.** Like the pooled table, nothing that came out of an evolved deck may enter this
/// one, or a card's identity rating starts reflecting the decks the builder happened to make. See
/// <see cref="ConstructedValuesStore.EvolvedPathFor"/> for what that costs when it happens.
/// </summary>
public sealed class IdentityValues
{
	/// <summary>
	/// Shrinkage for a (card, identity) cell, in games.
	///
	/// **Provisional and deliberately equal to <see cref="ConstructedValues.CardShrinkK"/> until a
	/// real run reports the distribution to calibrate it against.** Setting a games constant by
	/// analogy is precisely how `MinPairGames` came to be 200 against a busiest-pair of 166,
	/// silently disabling every synergy path while the tests passed. `PreSimulation.Run` now prints
	/// the median and p10 games-per-cell for exactly this purpose — read it, then set this.
	///
	/// It should almost certainly end up HIGHER than the pooled constant, not equal: a cell holds a
	/// fraction of a card's games, so it needs proportionally more shrinkage to say the same thing.
	/// </summary>
	public const int IdentityShrinkK = 25;

	private readonly IReadOnlyDictionary<string, DraftTrainingData> _data;
	private readonly Dictionary<string, Dictionary<string, CardStat>> _cards;

	public IdentityValues(IReadOnlyDictionary<string, DraftTrainingData> data)
	{
		_data = data;
		_cards = data.ToDictionary(
			kv => kv.Key,
			kv => kv.Value.Cards.ToDictionary(c => c.Name, StringComparer.Ordinal),
			StringComparer.Ordinal
		);
	}

	public static IdentityValues Empty { get; } =
		new(new Dictionary<string, DraftTrainingData>(StringComparer.Ordinal));

	public IReadOnlyDictionary<string, DraftTrainingData> Data => _data;

	public IReadOnlyCollection<string> Identities => _cards.Keys;

	/// <summary>Games behind one (card, identity) cell. The honesty check on any rate it returns.</summary>
	public int GamesFor(string identity, string card) =>
		_cards.TryGetValue(identity, out var byName) && byName.TryGetValue(card, out var stat)
			? stat.Games
			: 0;

	/// <summary>
	/// The card's win rate in this identity, shrunk toward <paramref name="pooledRate"/>.
	///
	/// Returns <paramref name="pooledRate"/> unchanged when the identity is unknown or the card was
	/// never played in it, which is the correct fallback rather than a special case: no evidence
	/// means no reason to differ from the pooled answer.
	/// </summary>
	public double RateOf(string identity, string card, double pooledRate) =>
		_cards.TryGetValue(identity, out var byName) && byName.TryGetValue(card, out var stat)
			? DraftTrainingData.Shrink(stat.Wins, stat.Games, pooledRate, IdentityShrinkK)
			: pooledRate;

	/// <summary>Accumulates another run's counts, per identity. Safe for presim data only.</summary>
	public static IReadOnlyDictionary<string, DraftTrainingData> Merge(
		IReadOnlyDictionary<string, DraftTrainingData> a,
		IReadOnlyDictionary<string, DraftTrainingData> b
	)
	{
		var merged = new Dictionary<string, DraftTrainingData>(a, StringComparer.Ordinal);
		foreach (var (identity, data) in b)
			merged[identity] = merged.TryGetValue(identity, out var existing)
				? DraftTrainingData.Merge(existing, data)
				: data;
		return merged;
	}
}
