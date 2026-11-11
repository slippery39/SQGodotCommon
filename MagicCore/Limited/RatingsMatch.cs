using System.Diagnostics;
using ImmutableGameObjects;
using MagicCore.Ai;
using MagicCore.Sets;

namespace MagicCore.Limited;

/// <summary>
/// Do one ratings table's AI sealed decks beat another's (story DATA-2.6)? Pools in PAIRS, each built on both tables
/// (<see cref="SealedBuilder.Opponent"/>): pool X on the new against pool Y on the old, then X on the old against Y on
/// the new — so each pool and each seat plays both tables, and only the build differs. A pair whose builds the new
/// table leaves unchanged says nothing and is skipped. The search on both sides, one thread a game.
/// </summary>
public static class RatingsMatch
{
	/// <summary>Pool seeds start here — past every pool <see cref="SimulatedRatings"/> rated on (seeds 2..2·games+1).</summary>
	public const int FirstPool = 100_000;

	/// <summary>Pairs tried for each one played before giving up — two tables that build the same decks would loop forever.</summary>
	public const int MaxPairsTried = 20;

	/// <summary>Candidate pairs built at once.</summary>
	const int BuildBatch = 64;

	/// <param name="NewScores">Per pair, the new table's score over its two games (a win 1, a draw half).</param>
	public sealed record Result(
		IReadOnlyList<double> NewScores,
		int PairsSkipped,
		int Unfinished,
		TimeSpan Elapsed,
		int PairsAbandoned = 0
	)
	{
		public double Mean => NewScores.DefaultIfEmpty(0.5).Average(); // every pair dropped: no evidence either way

		/// <summary>One standard error of <see cref="Mean"/>, over pairs.</summary>
		public double Se =>
			NewScores.Count < 2
				? double.NaN
				: Math.Sqrt(EarlyStop.Variance(NewScores) / NewScores.Count);

		/// <summary>The new table's decks won: above half by at least two standard errors (Claude's rule, DATA-2, for review).</summary>
		public bool NewWon => !double.IsNaN(Se) && Mean - 2 * Se > 0.5;

		public string Report(string newName, string oldName) =>
			$"{newName} against {oldName}: {Mean * 100:0.0}% ± {Se * 100:0.0}pp (1 SE) over {NewScores.Count} pairs "
			+ $"({2 * NewScores.Count} games), {PairsSkipped} pairs skipped (no build changed), {Unfinished} unfinished "
			+ $"(draws), {PairsAbandoned} pairs abandoned (a game past {SimulatedRatings.GameBudget.TotalMinutes:0} min or failed), "
			+ $"{Elapsed.TotalMinutes:0.0} min";
	}

	/// <summary><paramref name="pairs"/> pairs whose builds differ, decks on <paramref name="newRatings"/> against <paramref name="oldRatings"/> (the set's own table if null).</summary>
	public static Result Run(
		string set,
		IReadOnlyDictionary<string, double> newRatings,
		int pairs,
		string ai = "beam",
		IReadOnlyDictionary<string, double>? oldRatings = null,
		Action<string>? failed = null,
		Action<string>? abandoned = null
	)
	{
		if (!AiMatch.IsKnown(ai))
			throw new ArgumentException($"Unknown AI '{ai}' — known: {AiMatch.Usage}");
		if (pairs < 2)
			throw new ArgumentException("At least 2 pairs (4 games) — a standard error needs two.");
		var clock = Stopwatch.StartNew();
		var chosen =
			new List<(SealedDeck Xn, SealedDeck Xo, SealedDeck Yn, SealedDeck Yo, int Seed)>();
		var skipped = 0;
		// candidate pairs a batch at a time, each built on both tables in parallel — four builds a pair, each
		// measuring castability, are minutes on one core
		// the total is the most it may try; it stops as soon as enough pairs differ
		var building = new RunProgress(
			$"{set} candidate pairs built (at most)",
			MaxPairsTried * pairs
		);
		for (var k = 0; chosen.Count < pairs; k += BuildBatch)
		{
			if (k >= MaxPairsTried * pairs)
				throw new InvalidOperationException(
					$"{chosen.Count} of {k} pairs changed a build: the two tables build (nearly) the same decks"
				);
			var batch = new (SealedDeck, SealedDeck, SealedDeck, SealedDeck, int)[BuildBatch];
			Parallel.For(
				0,
				BuildBatch,
				b =>
				{
					var (x, y) = (FirstPool + 2 * (k + b), FirstPool + 2 * (k + b) + 1);
					batch[b] = (
						SealedBuilder.Opponent(set, x, newRatings),
						SealedBuilder.Opponent(set, x, oldRatings),
						SealedBuilder.Opponent(set, y, newRatings),
						SealedBuilder.Opponent(set, y, oldRatings),
						k + b + 1
					);
					building.Tick(() => $"{chosen.Count} changed so far");
				}
			);
			foreach (var d in batch.TakeWhile(_ => chosen.Count < pairs))
				if (
					SealedBuilder.SameDeck(d.Item1, d.Item2)
					&& SealedBuilder.SameDeck(d.Item3, d.Item4)
				)
					skipped++;
				else
					chosen.Add(d);
		}
		// game 2i: X new (player 1) against Y old; game 2i+1: X old (player 1) against Y new
		var scores = new double[2 * pairs];
		var finished = new bool[2 * pairs];
		var played = new bool[2 * pairs];
		var progress = new RunProgress($"{set} ratings match ({ai})", 2 * pairs);
		Parallel.For(
			0,
			2 * pairs,
			new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
			i =>
			{
				var (xn, xo, yn, yo, seed) = chosen[i / 2];
				var (first, second) = i % 2 == 0 ? (xn, yo) : (xo, yn);
				var start = MagicRules.NewGame(
					first.Main.Select(n => CardRegistry.Get(n, set)),
					second.Main.Select(n => CardRegistry.Get(n, set)),
					seed,
					sideboard1: SealedBuildMatch.Outside(first.Sideboard),
					sideboard2: SealedBuildMatch.Outside(second.Sideboard)
				);
				AiMatch.Game? game;
				using var budget = new CancellationTokenSource(SimulatedRatings.GameBudget);
				GameState? last = null;
				try
				{
					game = AiMatch.Play(
						ai,
						ai,
						start,
						"first",
						"second",
						seed,
						true,
						1,
						budget.Token,
						seen: x => last = x
					);
				}
				catch (Exception e) when (e is InvalidOperationException or ArgumentException)
				{
					var where =
						$"{set} ratings match, pair {seed} (pools {FirstPool + 2 * (seed - 1)} and {FirstPool + 2 * seed - 1}), "
						+ $"game {i % 2 + 1} of 2 ({ai})";
					if (failed is null)
						throw new InvalidOperationException($"{where}: {e.Message}", e);
					failed($"{where}: {e}"); // with failed given, its pair is dropped, as one past the budget
					game = null;
				}
				if (game is null) // past the budget: its pair is dropped
				{
					if (last is not null && budget.IsCancellationRequested) // past the budget, not a failure
						abandoned?.Invoke(
							$"pair {seed} game {i % 2 + 1}: {SimulatedRatings.Board(last)}"
						);
					scores[i] = double.NaN;
					Volatile.Write(ref played[i], true);
					progress.Tick(() => $"pair {seed} game {i % 2 + 1} ABANDONED");
					return;
				}
				var firstScore = AiMatch.Result.ScoreOfA(game);
				scores[i] = i % 2 == 0 ? firstScore : 1 - firstScore;
				finished[i] = game.Finished;
				Volatile.Write(ref played[i], true);
				progress.Tick(() =>
				{
					var done = scores.Where((x, j) => played[j] && !double.IsNaN(x)).ToList();
					return $"the new table's decks {done.Average() * 100:0.0}% so far";
				});
			}
		);
		var kept = Enumerable
			.Range(0, pairs)
			.Where(p => !double.IsNaN(scores[2 * p]) && !double.IsNaN(scores[2 * p + 1]))
			.ToList();
		return new Result(
			[.. kept.Select(p => (scores[2 * p] + scores[2 * p + 1]) / 2)],
			skipped,
			kept.Sum(p => (finished[2 * p] ? 0 : 1) + (finished[2 * p + 1] ? 0 : 1)),
			clock.Elapsed,
			pairs - kept.Count
		);
	}
}
