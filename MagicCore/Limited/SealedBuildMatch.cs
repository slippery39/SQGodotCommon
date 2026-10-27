using System.Diagnostics;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Sets.Fra;

namespace MagicCore.Limited;

/// <summary>
/// Candidate sealed builds compared by AI-vs-AI games (story LIM-1.8). Every build plays the SAME
/// opponents: opponent i is the AI's own build of the pool opened from seed i, in a game of seed i
/// (same coin flip, same shuffles of its deck), so two builds differ only in their own 40 and are
/// compared game by game. Both seats are the same AI, so it says which build that AI pilots better —
/// a colour pair's strength, not one card's; ~7pp is the smallest gap 200 games show.
/// </summary>
public static class SealedBuildMatch
{
	/// <param name="Sideboard">The rest of the pool — "outside the game" (Extrapolate the Impossible).</param>
	public sealed record Build(
		string Name,
		IReadOnlyList<CardDefinition> Main,
		IReadOnlyList<CardDefinition> Sideboard
	);

	/// <summary>
	/// An Arena deck export: its Deck section is the build, the rest of what it lists (Arena exports the
	/// pool's unplayed cards as the Sideboard) is outside the game. Throws, naming every problem, if the
	/// deck has a card the engine can't play — a silent substitute would test a different deck.
	/// </summary>
	public static Build FromExport(string name, string text)
	{
		var deck = ArenaDeck.Parse(text);
		if (!deck.Ok)
			throw new ArgumentException($"{name}: {string.Join("; ", deck.Problems)}");
		var rest = ArenaDeck.ReadPool(text).Pool.ToList();
		foreach (var card in deck.Deck)
			rest.Remove(card.Data.Name);
		return new Build(
			name,
			deck.Deck,
			[.. rest.Where(FraCards.IsImplemented).Select(FraCards.Get)]
		);
	}

	/// <summary>Opponents a comparison plays unless told otherwise: ~7pp is then the smallest difference that shows.</summary>
	public const int DefaultOpponents = 200;

	/// <summary>One of the builder's builds as a candidate (LIM-3.4): named by its button, the rest of its pool outside the game.</summary>
	public static Build FromSuggestion(SealedDeck deck) =>
		new(deck.Button, [.. deck.Main.Select(FraCards.Get)], Outside(deck.Sideboard));

	/// <summary>Builds as candidates, a repeated name numbered — the best's greedy and a close pair's can share one.</summary>
	public static IReadOnlyList<Build> FromSuggestions(IEnumerable<SealedDeck> decks)
	{
		var seen = new Dictionary<string, int>();
		return
		[
			.. decks
				.Select(FromSuggestion)
				.Select(b =>
					(seen[b.Name] = seen.GetValueOrDefault(b.Name) + 1) == 1
						? b
						: b with
						{
							Name = $"{b.Name} #{seen[b.Name]}",
						}
				),
		];
	}

	/// <summary>A pool's unplayed cards outside the game, as <see cref="ArenaDeck.ReadPool"/> reads them: playable, and no basics — they're free.</summary>
	static IReadOnlyList<CardDefinition> Outside(IEnumerable<string> cards) =>
		[
			.. cards
				.Where(n => FraCards.IsImplemented(n) && !SealedBuilder.Basics.Contains(n))
				.Select(FraCards.Get),
		];

	/// <summary>Games a minute a core, measured by LIM-1.8 (two builds at 200 in ~15 min on 16 cores) — for an estimate, not a promise.</summary>
	public const double GamesPerMinutePerCore = 1.7;

	/// <summary>Roughly how long <see cref="Run"/> takes on this machine.</summary>
	public static double EstimatedMinutes(int builds, int opponents) =>
		builds * opponents / (GamesPerMinutePerCore * Environment.ProcessorCount);

	/// <summary>Each build against opponents 1..<paramref name="opponents"/>, games in parallel, each search on one thread.</summary>
	public static Result Run(IReadOnlyList<Build> builds, int opponents, string ai = "beam")
	{
		if (!AiMatch.IsKnown(ai))
			throw new ArgumentException($"Unknown AI '{ai}' — known: {AiMatch.Usage}");
		if (opponents < 2)
			throw new ArgumentException("At least 2 opponents — a standard error needs two games.");
		var clock = Stopwatch.StartNew();
		var rivals = Enumerable.Range(1, opponents).Select(SealedBuilder.Opponent).ToList();
		var games = new AiMatch.Game[builds.Count * opponents];
		// Capped at the cores, as AiMatch.PlayAll: uncapped, the pool grows past them and games fight over them.
		Parallel.For(
			0,
			games.Length,
			new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
			i =>
			{
				var (build, rival, seed) = (
					builds[i / opponents],
					rivals[i % opponents],
					i % opponents + 1
				);
				var start = MagicRules.NewGame(
					build.Main,
					rival.Main.Select(FraCards.Get),
					seed,
					sideboard1: build.Sideboard,
					sideboard2: Outside(rival.Sideboard)
				);
				games[i] = AiMatch.Play(ai, ai, start, build.Name, $"pool {seed}", seed, true, 1)!;
			}
		);
		return new Result(
			builds,
			ai,
			[.. builds.Select((_, b) => games[(b * opponents)..((b + 1) * opponents)])],
			clock.Elapsed
		);
	}

	/// <param name="Games">Per build, its game against each opponent in order — build A's game i and build B's share an opponent and seed.</param>
	public sealed record Result(
		IReadOnlyList<Build> Builds,
		string Ai,
		IReadOnlyList<AiMatch.Game[]> Games,
		TimeSpan Elapsed
	)
	{
		static double[] Scores(AiMatch.Game[] games) => [.. games.Select(AiMatch.Result.ScoreOfA)];

		/// <summary>Mean and one standard error.</summary>
		static (double Mean, double Se) Estimate(IReadOnlyList<double> xs) =>
			(xs.Average(), Math.Sqrt(EarlyStop.Variance(xs) / xs.Count));

		public double WinRate(int build) => Scores(Games[build]).Average();

		/// <summary>Build x's score minus build y's, paired by opponent — the comparison the shared opponents make sharp.</summary>
		public (double Mean, double Se) Difference(int x, int y)
		{
			var (sx, sy) = (Scores(Games[x]), Scores(Games[y]));
			return Estimate([.. sx.Zip(sy, (a, b) => a - b)]);
		}

		public string Report()
		{
			var n = Games[0].Length;
			var lines = new List<string>
			{
				$"{Builds.Count} sealed builds x {n} AI sealed opponents ({Ai} on both sides), {Elapsed.TotalMinutes:0.0} min",
			};
			for (var b = 0; b < Builds.Count; b++)
			{
				var (mean, se) = Estimate(Scores(Games[b]));
				var g = Games[b];
				var unfinished = g.Count(x => !x.Finished);
				lines.Add(
					$"  {Builds[b].Name}: {mean * 100:0.0}% ± {se * 100:0.0}pp (1 SE) — W-L-D {g.Count(x => x.Winner == 1)}-{g.Count(x => x.Winner == 2)}-{g.Count(x => x.Winner == 0)}, turns {g.Average(x => x.Turns):0.0}"
						+ (unfinished > 0 ? $", {unfinished} UNFINISHED (draws)" : "")
				);
			}
			// many builds (a pool's whole offer, LIM-3.4): each against the FIRST — the best pair's two
			// colours, the screen's one to beat — not every pair, and never against whichever won most,
			// which would measure every gap from a lucky draw
			var each = Builds.Count > 3;
			if (each)
				lines.Add(
					$"  each against {Builds[0].Name} ({Builds.Count - 1} comparisons: at 2 SE, about one in 20 shows a gap by chance)"
				);
			for (var x = 0; x < Builds.Count; x++)
			for (var y = x + 1; y < Builds.Count; y++)
			{
				if (each && x != 0)
					continue;
				var (mean, se) = Difference(x, y);
				lines.Add(
					$"  {Builds[x].Name} − {Builds[y].Name}: {mean * 100:+0.0;-0.0;0.0}pp ± {se * 100:0.0}pp (paired by opponent; within 2 SE, no difference is shown)"
				);
			}
			return string.Join("\n", lines);
		}
	}
}
