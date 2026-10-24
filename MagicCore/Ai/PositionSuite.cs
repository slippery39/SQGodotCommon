using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using ImmutableGameObjects;
using MagicCore.Sets.Fra;

namespace MagicCore.Ai;

/// <summary>
/// The quick check (story AI-6.4): positions from the search's self-play, stored as decks + seed +
/// every move made and replayed in milliseconds. An AI decides in each; the report says which
/// decisions differ from the committed AI's stored answers, and what the decisions cost.
///
/// A move is a <see cref="MoveTokens"/> token — its index among the position's distinct moves, or the
/// ids answering its choice; the format lives there, shared with the table's game records (TBL-5). The generator plays the RECORDED move,
/// not the state the AI returned, so a recording replays exactly by construction. An engine change
/// can make it replay something else: each position stores a fingerprint of what it offers and each
/// game one of how it ended, and a replay that differs from either fails as stale — never silently.
/// </summary>
public static class PositionSuite
{
	const string FileName = "FRA.positions.txt";

	/// <summary>Where the suite is written, from the solution root. GENERATED — never hand-edit it.</summary>
	public static string FilePath => Path.Combine("MagicCore", "Sets", FileName);

	public const string GenerateCommand =
		"dotnet run -c Release --project MagicConsole -- --suite=generate";

	/// <summary>A recorded game: its decks, seed, a fingerprint of how it ended, and every decision as a move token.</summary>
	public sealed record Game(
		string Deck1,
		string Deck2,
		int Seed,
		string End,
		IReadOnlyList<string> Moves
	);

	/// <summary>
	/// The state before move <see cref="Move"/> of game <see cref="GameIndex"/>: how many options it had
	/// and a fingerprint of them (<see cref="Offers"/>), and the committed AI's answer.
	/// </summary>
	public sealed record Position(
		int GameIndex,
		int Move,
		int Options,
		string Offers,
		string Answer
	);

	/// <param name="Oracle">The oracle's reading of each position, by its index in <paramref name="Positions"/> (AI-6.4b); empty until <c>--suite=oracle</c>.</param>
	public sealed record Suite(
		string AnsweredBy,
		IReadOnlyList<Game> Games,
		IReadOnlyList<Position> Positions,
		IReadOnlyList<OracleReading>? Oracle = null
	);

	/// <summary>
	/// What the oracle — a much deeper search, run once — made of a position: its answer, and its score
	/// for each first move it searched (the best line it found starting with that move).
	/// </summary>
	public sealed record OracleReading(
		int Position,
		string Best,
		IReadOnlyDictionary<string, double> Scores
	);

	/// <summary>
	/// The oracle: the search looking FOUR turns ahead instead of two, every first move searched, ten
	/// times the rollout budget, more lines kept and a level deeper (story AI-6.4b). Depth alone barely
	/// mattered — most turns run out of decisions first, and a 2-turn oracle agreed with the default
	/// search 97.6% of the time; looking further ahead, 86.1%. It shares the default's evaluator, held
	/// values, opponent model and real library order, so it ranks versions of the SAME model; whether
	/// another model (casting, shuffling) is better is the overnight match's question, not this one's.
	/// A first move it kept in its beam is scored by the best line below it; one it pruned keeps its
	/// single rollout — so a version that picks a pruned move is judged on a rougher score.
	/// </summary>
	public static BeamSearchAi.Settings OracleSettings =>
		new(
			Depth: 4,
			BeamWidth: 6,
			MaxBranching: int.MaxValue, // every first move
			ExpandBranching: 8,
			RolloutBudget: 2_000,
			CommitPlans: false,
			RolloutSettings: BeamSearchAi.DefaultRollout with
			{
				LookaheadTurns = 4,
			},
			Threads: 1
		);

	public sealed class StaleException(string message)
		: MoveTokens.StaleException(
			$"{message} — the suite is stale; regenerate it: {GenerateCommand}"
		);

	// ===== Moves as tokens: MoveTokens, shared with the table's game records (TBL-5) =====

	public static string Token(GameState s, AiDecision decision) => MoveTokens.Token(s, decision);

	static string Token(GameState s, GameAction? action, IReadOnlyList<int>? answer) =>
		MoveTokens.Token(s, action, answer);

	public static GameState Apply(GameState s, string token)
	{
		try
		{
			return MoveTokens.Apply(s, token);
		}
		catch (MoveTokens.StaleException e)
		{
			throw new StaleException(e.Message);
		}
	}

	static string Describe(GameState s, string token)
	{
		try
		{
			return MoveTokens.Describe(s, token);
		}
		catch (MoveTokens.StaleException e)
		{
			throw new StaleException(e.Message);
		}
	}

	static bool Same(string a, string b) => MoveTokens.Same(a, b);

	// ===== What a position offers =====

	static int Options(GameState s) =>
		s.GetPendingChoice() is { } choice
			? choice.Options.Count
			: s.DistinctMoves(s.LegalActions()).Count;

	/// <summary>
	/// A decision an AI could get wrong: two moves or more, or a choice with more than one answer — not
	/// one where every enabled option must be taken (those would inflate any agreement).
	/// </summary>
	internal static bool IsReal(GameState s)
	{
		if (s.GetGame().Step == Step.PreGame)
			return false;
		if (s.GetPendingChoice() is not { } choice)
			return Options(s) >= 2;
		var enabled = choice.Options.Count(o => o.IsEnabled);
		return enabled >= 2 && enabled > choice.MinChoices;
	}

	static string Offers(GameState s) => MoveTokens.Offers(s);

	static string Ending(GameState s) => MoveTokens.Ending(s);

	// ===== Replay =====

	/// <summary>Plays a recorded game: its states before <paramref name="moves"/>, and where it ended.</summary>
	public static (Dictionary<int, GameState> At, GameState End) Replay(
		Game game,
		IReadOnlySet<int> moves
	)
	{
		var s = MagicRules.NewGame(FraDecks.All[game.Deck1], FraDecks.All[game.Deck2], game.Seed);
		var at = new Dictionary<int, GameState>();
		for (var i = 0; i < game.Moves.Count; i++)
		{
			s = s.AutoPass();
			if (moves.Contains(i))
				at[i] = s;
			s = Apply(s, game.Moves[i]);
		}
		return (at, s.AutoPass());
	}

	/// <summary>Every position, replayed — and checked against what was recorded: what it offers, and how its game ended.</summary>
	public static List<(Position Position, GameState State)> States(Suite suite) =>
		Replay(suite, check: true);

	/// <summary>
	/// The positions' states, by replaying each game once. Checking, a position whose offer or a game
	/// whose ending differs is stale; not checking (a new suite), the fingerprints are filled in.
	/// </summary>
	static List<(Position Position, GameState State)> Replay(Suite suite, bool check)
	{
		var at = new Dictionary<(int Game, int Move), GameState>();
		foreach (var (game, g) in suite.Games.Select((game, g) => (game, g)))
		{
			var moves = suite
				.Positions.Where(p => p.GameIndex == g)
				.Select(p => p.Move)
				.ToHashSet();
			var (states, end) = Replay(game, moves);
			if (check && Ending(end) != game.End)
				throw new StaleException(
					$"game {g} ({game.Deck1} vs {game.Deck2}, seed {game.Seed}) ends differently"
				);
			foreach (var (move, s) in states)
				at[(g, move)] = s;
		}
		// In the suite's own order: an answer, a stored answer and an oracle reading all share an index.
		return
		[
			.. suite.Positions.Select(p =>
			{
				var s = at[(p.GameIndex, p.Move)];
				if (check && Offers(s) != p.Offers)
					throw new StaleException(
						$"game {p.GameIndex} move {p.Move} offers something else"
					);
				return (check ? p : p with { Options = Options(s), Offers = Offers(s) }, s);
			}),
		];
	}

	// ===== Generate and check =====

	/// <summary>
	/// Self-play by <paramref name="ai"/> over every ordered FRA pairing for each seed, recorded;
	/// <paramref name="perGame"/> positions a game, evenly spaced among its real decisions
	/// (<see cref="IsReal"/>); answered by <paramref name="ai"/>, fresh at each.
	/// </summary>
	public static Suite Generate(string ai = "beam", int seeds = 4, int perGame = 12)
	{
		var decks = FraDecks.All.Keys.ToList();
		var schedule = (
			from seed in Enumerable.Range(1, seeds)
			from x in decks
			from y in decks
			where x != y
			select (x, y, seed)
		).ToList();
		var games = new Game[schedule.Count];
		var picked = new List<int>[schedule.Count];
		Parallel.For(
			0,
			schedule.Count,
			new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
			i =>
			{
				var (x, y, seed) = schedule[i];
				var s = MagicRules.NewGame(FraDecks.All[x], FraDecks.All[y], seed);
				var ais = new[] { s.Player1Id(), s.Player2Id() }.ToDictionary(
					p => p,
					p => AiMatch.Create(ai, p, seed, searchThreads: 1)
				);
				var (moves, real) = (new List<string>(), new List<int>());
				for (var n = 0; n < AiMatch.DecisionLimit && !s.IsOver(); n++)
				{
					s = s.AutoPass();
					if (s.IsOver())
						break;
					if (IsReal(s))
						real.Add(moves.Count);
					var token = Token(s, ais[s.DecidingPlayerId()].Decide(s));
					moves.Add(token);
					s = Apply(s, token);
				}
				games[i] = new Game(x, y, seed, Ending(s.AutoPass()), moves);
				var take = Math.Min(perGame, real.Count);
				picked[i] = [.. Enumerable.Range(0, take).Select(k => real[k * real.Count / take])];
			}
		);
		var unanswered = picked
			.SelectMany((moves, g) => moves.Select(m => new Position(g, m, 0, "", "")))
			.ToList();
		var suite = new Suite(ai, games, unanswered);
		var states = Replay(suite, check: false);
		return suite with { Positions = [.. Answer(ai, states).Select(a => a.Position)] };
	}

	/// <summary>A fresh <paramref name="ai"/> decides at each position, one thread each, positions in parallel.</summary>
	static (Position Position, string Token, long Steps, long Rollouts)[] Answer(
		string ai,
		IReadOnlyList<(Position Position, GameState State)> states
	)
	{
		var answers = new (Position, string, long, long)[states.Count];
		Parallel.For(
			0,
			states.Count,
			new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
			i =>
			{
				var (position, s) = states[i];
				var player = AiMatch.Create(ai, s.DecidingPlayerId(), 1, searchThreads: 1);
				var (decision, steps) = MagicRules.CountingSteps(() => player.Decide(s));
				var token = Token(s, decision);
				var rollouts = player is BeamSearchAi search ? search.Rollouts : 0;
				answers[i] = (position with { Answer = token }, token, steps, rollouts);
			}
		);
		return answers;
	}

	/// <summary>The suite with the oracle's reading of every position (<see cref="OracleSettings"/> unless given) — run once, then stored.</summary>
	public static Suite WithOracle(Suite suite, BeamSearchAi.Settings? settings = null)
	{
		var states = States(suite);
		var readings = new OracleReading[states.Count];
		Parallel.For(
			0,
			states.Count,
			new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
			i => readings[i] = Read(states[i].State, i, settings)
		);
		return suite with { Oracle = readings };
	}

	/// <summary>The oracle's reading of one position (<see cref="OracleSettings"/> unless given) — also the game records' comparison (TBL-5.6).</summary>
	public static OracleReading Read(
		GameState s,
		int position,
		BeamSearchAi.Settings? settings = null
	)
	{
		var oracle = new BeamSearchAi(s.DecidingPlayerId(), settings ?? OracleSettings);
		var best = Token(s, oracle.Decide(s));
		var scores = oracle
			.FirstMoveScores.GroupBy(f => Token(s, f.Action, f.Answer))
			.ToDictionary(g => g.Key, g => Math.Round(g.Max(f => f.Score), 2));
		return new OracleReading(position, best, scores);
	}

	/// <summary>What <paramref name="ai"/> decides at every position, against the stored answers.</summary>
	public static Check Run(Suite suite, string ai)
	{
		var clock = Stopwatch.StartNew();
		var states = States(suite);
		var answers = Answer(ai, states);
		var changed = answers
			.Select((a, i) => (a, states[i].State, Was: states[i].Position.Answer))
			.Where(x => !Same(x.a.Token, x.Was))
			.Select(x =>
				(x.a.Position, Was: Describe(x.State, x.Was), Now: Describe(x.State, x.a.Token))
			)
			.ToList();
		return new Check(
			suite,
			ai,
			changed,
			answers.Sum(a => a.Steps),
			answers.Sum(a => a.Rollouts),
			clock.Elapsed,
			suite with
			{
				AnsweredBy = ai,
				Positions = [.. answers.Select(a => a.Position)],
			},
			suite.Oracle is { Count: > 0 } oracle
				? Judge(
					Aligned(oracle, suite.Positions.Count),
					[.. answers.Select(a => a.Token)],
					[.. suite.Positions.Select(p => p.Answer)]
				)
				: null
		);
	}

	/// <summary>The oracle's readings, one per position and in its order — anything else is stale.</summary>
	static IReadOnlyList<OracleReading> Aligned(
		IReadOnlyList<OracleReading> oracle,
		int positions
	) =>
		oracle.Count == positions && oracle.Select((r, i) => r.Position == i).All(x => x)
			? oracle
			: throw new StaleException(
				"the oracle's readings don't match the positions — re-run: dotnet run -c Release --project MagicConsole -- --suite=oracle"
			);

	/// <summary>
	/// The answers against the oracle (one reading per answer, in order): how often they agree with it,
	/// and the score each gives up — the score of the oracle's answer less the answer's, never below 0,
	/// so agreeing gives up nothing. Decided games score in the thousands and would swamp the average,
	/// so a win the oracle found and the answer misses, and a loss the answer walks into that the
	/// oracle's answer avoids, are counted apart; positions the oracle saw already decided are left out.
	/// </summary>
	public static OracleVerdict Judge(
		IReadOnlyList<OracleReading> oracle,
		IReadOnlyList<string> answers,
		IReadOnlyList<string> committedAnswers
	)
	{
		const double win = BeamSearchAi.WinThreshold;
		static double? Score(OracleReading r, string token) =>
			r
				.Scores.Where(kv => Same(kv.Key, token))
				.Select(kv => (double?)kv.Value)
				.FirstOrDefault();
		var (agree, committed, missedWins, losses, unscored) = (0, 0, 0, 0, 0);
		var givenUp = new List<double>();
		for (var i = 0; i < oracle.Count; i++)
		{
			var r = oracle[i];
			agree += Same(answers[i], r.Best) ? 1 : 0;
			committed += Same(committedAnswers[i], r.Best) ? 1 : 0;
			if (Score(r, r.Best) is not { } reference)
				continue; // a decision the oracle didn't search
			if (Score(r, answers[i]) is not { } mine)
				unscored++;
			else if (reference >= win && mine < win)
				missedWins++;
			else if (mine <= -win && reference > -win)
				losses++;
			else if (Math.Abs(reference) < win)
				givenUp.Add(Math.Max(0, reference - mine));
		}
		return new(
			oracle.Count,
			agree,
			committed,
			givenUp.Count,
			givenUp.DefaultIfEmpty(0).Average(),
			missedWins,
			losses,
			unscored
		);
	}

	public sealed record OracleVerdict(
		int Positions,
		int Agree,
		int CommittedAgree,
		int Scored,
		double MeanGivenUp,
		int MissedWins,
		int WalkedIntoLosses,
		int Unscored
	);

	/// <param name="Accepted">The suite with <paramref name="Ai"/>'s answers as the committed ones (<c>--accept</c>).</param>
	public sealed record Check(
		Suite Suite,
		string Ai,
		IReadOnlyList<(Position Position, string Was, string Now)> Changed,
		long Steps,
		long Rollouts,
		TimeSpan Elapsed,
		Suite Accepted,
		OracleVerdict? Oracle
	)
	{
		public string Report(int shown = 10)
		{
			var n = Suite.Positions.Count;
			if (n == 0)
				return $"suite: no positions to check — {GenerateCommand}";
			var lines = new List<string>
			{
				$"suite: {Ai} on {n} positions ({Suite.Games.Count} games), answers by {Suite.AnsweredBy}, {Elapsed.TotalSeconds:0.0} s",
				$"  changed from the committed answers: {Changed.Count} ({100.0 * Changed.Count / n:0.0}%)",
				$"  cost per position (deterministic): {(double)Steps / n:0.0} engine steps, {(double)Rollouts / n:0.0} rollouts",
				Oracle is { } o
					? $"  agrees with the oracle: {100.0 * o.Agree / o.Positions:0.0}% (the committed answers: {100.0 * o.CommittedAgree / o.Positions:0.0}%); "
						+ $"score given up: {o.MeanGivenUp:0.00} a position over {o.Scored}; misses a win it found: {o.MissedWins}, walks into a loss it avoids: {o.WalkedIntoLosses}, unscored: {o.Unscored}"
					: "  no oracle yet — dotnet run -c Release --project MagicConsole -- --suite=oracle",
			};
			foreach (var (p, was, now) in Changed.Take(shown))
			{
				var g = Suite.Games[p.GameIndex];
				lines.Add(
					$"    {g.Deck1} vs {g.Deck2} seed {g.Seed}, move {p.Move}: was \"{was}\", now \"{now}\""
				);
			}
			if (Changed.Count > shown)
				lines.Add($"    … and {Changed.Count - shown} more");
			return string.Join("\n", lines);
		}
	}

	// ===== The file =====

	public static string ToText(Suite suite) =>
		string.Join(
			"\n",
			[
				$"# GENERATED by: {GenerateCommand} — never hand-edit (story AI-6.4)",
				$"answered-by|{suite.AnsweredBy}",
				.. suite.Games.Select(g =>
					$"game|{g.Deck1}|{g.Deck2}|{g.Seed}|{g.End}|{string.Join(" ", g.Moves)}"
				),
				.. suite.Positions.Select(p =>
					$"pos|{p.GameIndex}|{p.Move}|{p.Options}|{p.Offers}|{p.Answer}"
				),
				.. (suite.Oracle ?? []).Select(r =>
					$"oracle|{r.Position}|{r.Best}|{string.Join(" ", r.Scores.Select(kv => $"{kv.Key}={kv.Value.ToString("0.00", CultureInfo.InvariantCulture)}"))}"
				),
			]
		) + "\n";

	public static Suite Parse(string text)
	{
		var (by, games, positions, oracle) = (
			"",
			new List<Game>(),
			new List<Position>(),
			new List<OracleReading>()
		);
		foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
		{
			var f = line.Split('|');
			switch (f[0])
			{
				case "answered-by":
					by = f[1];
					break;
				case "game":
					games.Add(
						new Game(
							f[1],
							f[2],
							int.Parse(f[3]),
							f[4],
							f[5].Length == 0 ? [] : f[5].Split(' ')
						)
					);
					break;
				case "pos":
					positions.Add(
						new Position(int.Parse(f[1]), int.Parse(f[2]), int.Parse(f[3]), f[4], f[5])
					);
					break;
				case "oracle":
					oracle.Add(
						new OracleReading(
							int.Parse(f[1]),
							f[2],
							(f[3].Length == 0 ? [] : f[3].Split(' '))
								.Select(x => x.Split('='))
								.ToDictionary(
									x => x[0],
									x => double.Parse(x[1], CultureInfo.InvariantCulture)
								)
						)
					);
					break;
			}
		}
		return new Suite(by, games, positions, oracle);
	}

	/// <summary>The tracked suite, embedded in MagicCore (no working-directory trap); null if none was generated.</summary>
	public static Suite? Tracked()
	{
		using var stream = Assembly
			.GetExecutingAssembly()
			.GetManifestResourceStream($"MagicCore.Sets.{FileName}");
		return stream is null ? null : Parse(new StreamReader(stream).ReadToEnd());
	}
}
