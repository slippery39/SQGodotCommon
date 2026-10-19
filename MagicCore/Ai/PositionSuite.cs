using System.Diagnostics;
using System.Reflection;
using ImmutableGameObjects;
using MagicCore.Sets.Fra;

namespace MagicCore.Ai;

/// <summary>
/// The quick check (story AI-6.4): positions from the search's self-play, stored as decks + seed +
/// every move made and replayed in milliseconds. An AI decides in each; the report says which
/// decisions differ from the committed AI's stored answers, and what the decisions cost.
///
/// A move is stored as its index among the position's distinct moves (<c>a3</c>), or the ids
/// answering its choice (<c>c12,15</c>; <c>c</c> for none). The generator plays the RECORDED move,
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

	public sealed record Suite(
		string AnsweredBy,
		IReadOnlyList<Game> Games,
		IReadOnlyList<Position> Positions
	);

	public sealed class StaleException(string message)
		: Exception($"{message} — the suite is stale; regenerate it: {GenerateCommand}");

	// ===== Moves as tokens =====

	public static string Token(GameState s, AiDecision decision)
	{
		if (decision.Answer is { } ids)
			return "c" + string.Join(",", ids);
		var key = s.MoveKey(decision.Action!);
		var index = s.DistinctMoves(s.LegalActions()).FindIndex(a => Equals(s.MoveKey(a), key));
		return index >= 0
			? $"a{index}"
			: throw new InvalidOperationException($"{decision.What}: not among the distinct moves");
	}

	/// <summary>A token against the state it applies to: the action, or the choice's ids — the one parser.</summary>
	static (GameAction? Action, int[]? Ids) Resolve(GameState s, string token)
	{
		try
		{
			return token switch
			{
				"c" => (null, []),
				['c', .. var ids] => (null, [.. ids.Split(',').Select(int.Parse)]),
				['a', .. var index] => (s.DistinctMoves(s.LegalActions())[int.Parse(index)], null),
				_ => throw new FormatException($"not a move token: '{token}'"),
			};
		}
		catch (Exception e)
			when (e is FormatException or OverflowException or ArgumentOutOfRangeException)
		{
			throw new StaleException($"move '{token}' no longer applies ({e.Message})");
		}
	}

	public static GameState Apply(GameState s, string token)
	{
		var (action, ids) = Resolve(s, token);
		try
		{
			return action is not null ? s.Act(action) : s.Choose(ids!);
		}
		catch (InvalidOperationException e)
		{
			throw new StaleException($"move '{token}' no longer applies ({e.Message})");
		}
	}

	static string Describe(GameState s, string token) =>
		Resolve(s, token) switch
		{
			({ } action, _) => MagicCore.Describe.Action(s, action),
			(_, var ids) => MagicCore.Describe.Answer(s, s.GetPendingChoice()!, ids!),
		};

	/// <summary>Two answers are the same if they pick the same move, or the same ids in any order.</summary>
	static bool Same(string a, string b) =>
		a == b
		|| (
			a[0] == 'c'
			&& b[0] == 'c'
			&& a[1..].Split(',').Order().SequenceEqual(b[1..].Split(',').Order())
		);

	// ===== What a position offers =====

	static int Options(GameState s) =>
		s.GetPendingChoice() is { } choice
			? choice.Options.Count
			: s.DistinctMoves(s.LegalActions()).Count;

	/// <summary>
	/// A decision an AI could get wrong: two moves or more, or a choice with more than one answer — not
	/// one where every enabled option must be taken (those would inflate any agreement).
	/// </summary>
	static bool IsReal(GameState s)
	{
		if (s.GetGame().Step == Step.PreGame)
			return false;
		if (s.GetPendingChoice() is not { } choice)
			return Options(s) >= 2;
		var enabled = choice.Options.Count(o => o.IsEnabled);
		return enabled >= 2 && enabled > choice.MinChoices;
	}

	/// <summary>What a position offers, as a stable hash: its distinct moves, or its choice's options.</summary>
	static string Offers(GameState s) =>
		Fnv(
			s.GetPendingChoice() is { } choice
				? choice.Options.Select(o => $"{o.Id}:{o.DisplayText}:{o.IsEnabled}")
				: s.DistinctMoves(s.LegalActions()).Select(a => s.MoveKey(a).ToString()!)
		);

	/// <summary>How a game ended, as a stable hash: the turn, and each player's life and zones.</summary>
	static string Ending(GameState s) =>
		Fnv(
			new[] { s.Player1Id(), s.Player2Id() }.Select(p =>
				$"{s.GetGame().TurnNumber}:{s.GetPlayer(p).Life}:{string.Join(",", PlayerZones.Select(z => s.CardsIn(p, z).Count()))}"
			)
		);

	static readonly ZoneType[] PlayerZones =
	[
		ZoneType.Library,
		ZoneType.Hand,
		ZoneType.Battlefield,
		ZoneType.Graveyard,
		ZoneType.Exile,
	];

	/// <summary>FNV-1a — stable across processes, where string.GetHashCode is not.</summary>
	static string Fnv(IEnumerable<string> parts)
	{
		var hash = 2166136261u;
		foreach (var c in string.Join("|", parts))
			hash = unchecked((hash ^ c) * 16777619u);
		return hash.ToString("x8");
	}

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
		var states = new List<(Position, GameState)>();
		foreach (var (game, g) in suite.Games.Select((game, g) => (game, g)))
		{
			var positions = suite.Positions.Where(p => p.GameIndex == g).ToList();
			var (at, end) = Replay(game, positions.Select(p => p.Move).ToHashSet());
			if (check && Ending(end) != game.End)
				throw new StaleException(
					$"game {g} ({game.Deck1} vs {game.Deck2}, seed {game.Seed}) ends differently"
				);
			foreach (var p in positions)
			{
				var s = at[p.Move];
				if (check && Offers(s) != p.Offers)
					throw new StaleException($"game {g} move {p.Move} offers something else");
				states.Add((check ? p : p with { Options = Options(s), Offers = Offers(s) }, s));
			}
		}
		return states;
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
			}
		);
	}

	/// <param name="Accepted">The suite with <paramref name="Ai"/>'s answers as the committed ones (<c>--accept</c>).</param>
	public sealed record Check(
		Suite Suite,
		string Ai,
		IReadOnlyList<(Position Position, string Was, string Now)> Changed,
		long Steps,
		long Rollouts,
		TimeSpan Elapsed,
		Suite Accepted
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
			]
		) + "\n";

	public static Suite Parse(string text)
	{
		var (by, games, positions) = ("", new List<Game>(), new List<Position>());
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
			}
		}
		return new Suite(by, games, positions);
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
