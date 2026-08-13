using System.Text.Json;

namespace MtgSimulator;

/// <summary>
/// Counts for one card. <see cref="Games"/>/<see cref="Wins"/> are games-in-hand (drawn).
/// <see cref="DeckGames"/> is how often it was in the deck at all, drawn or not — the two
/// together give P(drawn | in deck), which converts a conditional win rate into an
/// expected contribution per game. Defaults to 0 so pre-existing files still load.
/// </summary>
public sealed record CardStat(string Name, int Games, int Wins, int DeckGames = 0);

/// <summary>
/// Counts for one unordered card pair; <see cref="A"/> sorts before <see cref="B"/>.
/// <see cref="Games"/> counts games where BOTH were drawn, <see cref="DeckGames"/> games
/// where both were in the deck. Their ratio is what stops the synergy term from being
/// compared against card rates measured on a much more frequent event.
/// </summary>
public sealed record PairStat(string A, string B, int Games, int Wins, int DeckGames = 0);

/// <summary>
/// Trained draft knowledge: raw win/loss counts, never rates.
///
/// Counts rather than rates on purpose — they can be merged across training runs and
/// the scoring formula can be retuned without re-simulating anything.
///
/// "Games" is games-in-hand: a card is only credited for a game in which it was actually
/// drawn, and a pair only when both halves were drawn in the same game.
/// <see cref="Perspectives"/> counts deck-games recorded (2 per game) and gives the base
/// win rate that <see cref="Prior"/> shrinks toward.
/// </summary>
public sealed record DraftTrainingData(
	int Perspectives,
	int Wins,
	IReadOnlyList<CardStat> Cards,
	IReadOnlyList<PairStat> Pairs
)
{
	public static readonly DraftTrainingData Empty = new(0, 0, [], []);

	/// Base win rate across every recorded deck-game. Near 0.5; below it when games draw.
	public double Prior => Perspectives == 0 ? 0.5 : (double)Wins / Perspectives;

	/// <summary>
	/// Bayesian shrink toward <paramref name="prior"/>. Without this a pair seen twice and
	/// won twice reads as 100% and dominates every pick — which is the single thing that
	/// makes an untuned synergy term worse than no synergy term at all.
	/// </summary>
	public static double Shrink(int wins, int games, double prior, int k) =>
		(wins + k * prior) / (games + k);

	/// <summary>
	/// The win rate a pair would be expected to post if the two cards did not interact at
	/// all — each card's effect added in log-odds space, which is the correct way to
	/// combine independent effects on a win/lose outcome.
	///
	/// This is the baseline real synergy must be measured against. Measuring a pair against
	/// the global prior instead just re-reports card quality: pair a bomb with anything and
	/// the pair looks great, so every pair containing that bomb reads as synergy.
	/// </summary>
	public static double ExpectedPairRate(double rateA, double rateB, double prior)
	{
		var logitPrior = Logit(prior);
		return Sigmoid(logitPrior + (Logit(rateA) - logitPrior) + (Logit(rateB) - logitPrior));
	}

	/// Clamped so a 0% or 100% rate cannot produce an infinite log-odds.
	public static double Logit(double p)
	{
		var c = Math.Clamp(p, 1e-6, 1 - 1e-6);
		return Math.Log(c / (1 - c));
	}

	public static double Sigmoid(double x) => 1.0 / (1.0 + Math.Exp(-x));

	/// <summary>
	/// Standard deviation of shrunk card win rates, in percentage points — the health check
	/// for generational training. It should stay roughly flat across generations.
	/// Exploding means card rates are absorbing the strength of whichever picker drafted
	/// them; collapsing toward 0 means decks have converged and cards no longer separate.
	/// </summary>
	public double CardRateSpread(int k = 25)
	{
		if (Cards.Count == 0)
			return 0;
		var rates = Cards.Select(c => 100.0 * Shrink(c.Wins, c.Games, Prior, k)).ToList();
		var mean = rates.Average();
		return Math.Sqrt(rates.Sum(r => (r - mean) * (r - mean)) / rates.Count);
	}

	public static DraftTrainingData Merge(DraftTrainingData a, DraftTrainingData b)
	{
		var cards = a
			.Cards.Concat(b.Cards)
			.GroupBy(c => c.Name, StringComparer.Ordinal)
			.Select(g => new CardStat(
				g.Key,
				g.Sum(c => c.Games),
				g.Sum(c => c.Wins),
				g.Sum(c => c.DeckGames)
			))
			.ToList();

		var pairs = a
			.Pairs.Concat(b.Pairs)
			.GroupBy(p => (p.A, p.B))
			.Select(g => new PairStat(
				g.Key.A,
				g.Key.B,
				g.Sum(p => p.Games),
				g.Sum(p => p.Wins),
				g.Sum(p => p.DeckGames)
			))
			.ToList();

		return new DraftTrainingData(
			a.Perspectives + b.Perspectives,
			a.Wins + b.Wins,
			cards,
			pairs
		);
	}
}

/// <summary>
/// Reads and writes <see cref="DraftTrainingData"/>. Plain System.Text.Json — the DTOs are
/// flat records of strings and ints, so no converters or polymorphism are involved.
/// </summary>
public static class DraftTrainingStore
{
	public const string DefaultPath = "sim_results/draft_training.json";

	private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

	/// Returns null when the file does not exist or cannot be parsed — callers fall back
	/// to an untrained picker rather than failing a whole run over a bad cache file.
	public static DraftTrainingData? Load(string path = DefaultPath)
	{
		if (!File.Exists(path))
			return null;
		try
		{
			return JsonSerializer.Deserialize<DraftTrainingData>(File.ReadAllText(path), Options);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	public static void Save(DraftTrainingData data, string path = DefaultPath)
	{
		var dir = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(dir))
			Directory.CreateDirectory(dir);
		File.WriteAllText(path, JsonSerializer.Serialize(data, Options));
	}

	/// Merges into whatever is already on disk, so repeated training runs accumulate.
	public static DraftTrainingData SaveMerged(DraftTrainingData fresh, string path = DefaultPath)
	{
		var merged = DraftTrainingData.Merge(Load(path) ?? DraftTrainingData.Empty, fresh);
		Save(merged, path);
		return merged;
	}
}
