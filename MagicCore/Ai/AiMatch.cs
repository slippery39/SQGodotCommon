using System.Diagnostics;
using ImmutableGameObjects;
using MagicCore.Sets.Fra;

namespace MagicCore.Ai;

/// <summary>
/// Two AIs against each other over every ordered FRA deck pairing, every game played twice with the
/// AIs swapping seats — the same decks, seats, seed and coin flip, so only the AI differs. Every
/// strength claim about a MagicCore AI comes from here, recorded in docs/findings/magic-ai.md.
///
/// Results are deterministic for the seeds; only the timings depend on the machine. Games run in
/// parallel unless <c>sequential</c> — parallel games share the CPU unevenly, which inflates time per
/// decision by an amount that varies with what else ran: only <c>sequential</c> timings are evidence.
/// </summary>
public static class AiMatch
{
	/// <summary>Past this many decisions a game is unfinished — it counts as a draw and is reported.</summary>
	public const int DecisionLimit = 20_000;

	/// <summary>The AIs a match can name — the one list; <see cref="Names"/> reads it.</summary>
	static readonly Dictionary<string, Func<int, int, IAi>> Makers =
		new()
		{
			["greedy"] = (player, _) => new GreedyAi(player),
			["random"] = (player, seed) => new RandomAi(player, seed * 31 + player),
			["beam"] = (player, _) => new BeamSearchAi(player),
		};

	public static IReadOnlyCollection<string> Names => Makers.Keys;

	public static IAi Create(string name, int playerId, int seed) =>
		Makers.TryGetValue(name, out var make)
			? make(playerId, seed)
			: throw new ArgumentException(
				$"Unknown AI '{name}' — known: {string.Join(", ", Names)}"
			);

	/// <summary>One game, from A's side. Winner: 1 = A, 2 = B, 0 = a draw or unfinished.</summary>
	public sealed record Game(
		string Deck1,
		string Deck2,
		int Seed,
		bool AIsPlayer1,
		int Winner,
		bool Finished,
		int Turns,
		int DecisionsA,
		int DecisionsB,
		double SecondsA,
		double SecondsB,
		double SlowestA,
		double SlowestB
	)
	{
		public string DeckOfA => AIsPlayer1 ? Deck1 : Deck2;
		public string DeckOfB => AIsPlayer1 ? Deck2 : Deck1;
	}

	/// <summary>
	/// At least <paramref name="games"/> games, rounded up to whole schedules: every ordered deck
	/// pairing, both seatings, per seed.
	/// </summary>
	public static Result Run(string a, string b, int games, bool sequential = false)
	{
		var decks = FraDecks.All.Keys.ToList();
		var pairs = decks.SelectMany(x => decks.Where(y => y != x).Select(y => (x, y))).ToList();
		if (pairs.Count == 0)
			throw new InvalidOperationException("A match needs at least two FRA decks to pair.");
		foreach (var name in new[] { a, b }.Where(n => !Makers.ContainsKey(n)))
			throw new ArgumentException($"Unknown AI '{name}' — known: {string.Join(", ", Names)}");
		var seeds = Math.Max(1, (int)Math.Ceiling(games / (pairs.Count * 2.0)));
		var schedule = (
			from seed in Enumerable.Range(1, seeds)
			from pair in pairs
			from aIsPlayer1 in new[] { true, false }
			select (pair.x, pair.y, seed, aIsPlayer1)
		).ToList();

		var clock = Stopwatch.StartNew();
		var results = new Game[schedule.Count];
		void PlayAt(int i) =>
			results[i] = Play(
				a,
				b,
				schedule[i].x,
				schedule[i].y,
				schedule[i].seed,
				schedule[i].aIsPlayer1
			);
		if (sequential)
			for (var i = 0; i < schedule.Count; i++)
				PlayAt(i);
		else
			Parallel.For(0, schedule.Count, PlayAt);
		return new Result(a, b, pairs.Count, seeds, results, clock.Elapsed);
	}

	static Game Play(string a, string b, string deck1, string deck2, int seed, bool aIsPlayer1)
	{
		var s = MagicRules.NewGame(FraDecks.All[deck1], FraDecks.All[deck2], seed);
		var (aSeat, bSeat) = aIsPlayer1
			? (s.Player1Id(), s.Player2Id())
			: (s.Player2Id(), s.Player1Id());
		var ais = new Dictionary<int, IAi>
		{
			[aSeat] = Create(a, aSeat, seed),
			[bSeat] = Create(b, bSeat, seed),
		};
		var decisions = new Dictionary<int, int> { [aSeat] = 0, [bSeat] = 0 };
		var ticks = new Dictionary<int, long> { [aSeat] = 0, [bSeat] = 0 };
		var slowest = new Dictionary<int, long> { [aSeat] = 0, [bSeat] = 0 };

		for (var i = 0; i < DecisionLimit && !s.IsOver(); i++)
		{
			s = s.AutoPass();
			if (s.IsOver())
				break;
			var who = s.DecidingPlayerId();
			var start = Stopwatch.GetTimestamp();
			s = ais[who].Decide(s).State;
			var took = Stopwatch.GetTimestamp() - start;
			ticks[who] += took;
			slowest[who] = Math.Max(slowest[who], took);
			decisions[who]++;
		}

		var winner = s.WinnerId();
		return new Game(
			deck1,
			deck2,
			seed,
			aIsPlayer1,
			winner == aSeat ? 1
				: winner == bSeat ? 2
				: 0,
			s.IsOver(),
			s.GetGame().TurnNumber,
			decisions[aSeat],
			decisions[bSeat],
			(double)ticks[aSeat] / Stopwatch.Frequency,
			(double)ticks[bSeat] / Stopwatch.Frequency,
			(double)slowest[aSeat] / Stopwatch.Frequency,
			(double)slowest[bSeat] / Stopwatch.Frequency
		);
	}

	public sealed record Result(
		string A,
		string B,
		int Pairings,
		int Seeds,
		IReadOnlyList<Game> Games,
		TimeSpan Elapsed
	)
	{
		/// <summary>A's score: a win is 1, a draw or unfinished game half.</summary>
		public double WinRateA => Games.Average(ScoreOfA);

		public static double ScoreOfA(Game g) =>
			g.Winner switch
			{
				1 => 1.0,
				0 => 0.5,
				_ => 0.0,
			};

		/// <summary>
		/// One standard error of <see cref="WinRateA"/>, over MIRROR PAIRS, not games: the two games of
		/// a pair share decks, seed and coin flip, so they are not independent trials. An AI against
		/// itself splits every pair — zero spread, zero error — where a per-game binomial would claim
		/// ±5.9pp at 72 games.
		/// </summary>
		public double StandardError
		{
			get
			{
				var pairScores = Games
					.GroupBy(g => (g.Deck1, g.Deck2, g.Seed))
					.Select(p => p.Average(ScoreOfA))
					.ToList();
				if (pairScores.Count < 2)
					return double.NaN;
				var mean = pairScores.Average();
				var variance =
					pairScores.Sum(x => (x - mean) * (x - mean)) / (pairScores.Count - 1);
				return Math.Sqrt(variance / pairScores.Count);
			}
		}

		public string Report()
		{
			// The same AI on both sides needs telling apart in its own report.
			var (a, b) = A == B ? ($"{A} (A)", $"{B} (B)") : (A, B);
			static string Ms(double seconds, int decisions) =>
				decisions == 0 ? "-" : $"{seconds * 1000 / decisions:0.0} ms";
			var (secA, decA) = (Games.Sum(g => g.SecondsA), Games.Sum(g => g.DecisionsA));
			var (secB, decB) = (Games.Sum(g => g.SecondsB), Games.Sum(g => g.DecisionsB));
			var byDeck = Games
				.GroupBy(g => g.DeckOfA)
				.OrderBy(g => g.Key)
				.Select(g =>
					$"{g.Key} {g.Count(x => x.Winner == 1)}-{g.Count(x => x.Winner == 2)}-{g.Count(x => x.Winner == 0)}"
				);
			var unfinished = Games.Count(g => !g.Finished);
			return string.Join(
				"\n",
				$"{A} vs {B}: {Games.Count} games ({Pairings} deck pairings x {Seeds} seeds x both seats), {Elapsed.TotalSeconds:0.0} s",
				$"  {a} {WinRateA * 100:0.0}% ± {StandardError * 100:0.0}pp (1 SE) — wins {a} {Games.Count(g => g.Winner == 1)}, {b} {Games.Count(g => g.Winner == 2)}, draws {Games.Count(g => g.Winner == 0)}",
				$"  time per decision: {a} {Ms(secA, decA)} (slowest {Games.Max(g => g.SlowestA) * 1000:0} ms), {b} {Ms(secB, decB)} (slowest {Games.Max(g => g.SlowestB) * 1000:0} ms)",
				$"  per game: decisions {a} {(double)decA / Games.Count:0}, {b} {(double)decB / Games.Count:0}; turns {Games.Average(g => g.Turns):0.0}",
				$"  {a}'s record by its deck (W-L-D): {string.Join(", ", byDeck)}",
				unfinished > 0
					? $"  UNFINISHED at {DecisionLimit} decisions: {unfinished} (counted as draws)"
					: "  every game finished"
			);
		}
	}
}
