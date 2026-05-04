using System.Diagnostics;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Orchestrates multiple simulated games and aggregates results.
/// Each game uses freshly randomised 40-card decks drawn from CardPool.
///
/// AI strategy is configurable — swap DepthLimitedAiStrategy for
/// RandomAiStrategy or any other IAiStrategy implementation.
/// </summary>
public class SimulatorRunner
{
	private readonly int _gameCount;
	private readonly int _aiDepth;
	private readonly int? _seed;

	public SimulatorRunner(int gameCount, int aiDepth = 3, int? seed = null)
	{
		_gameCount = gameCount;
		_aiDepth = aiDepth;
		_seed = seed;
	}

	public void Run()
	{
		var masterSeed = _seed ?? new Random().Next();
		var seedLabel = _seed.HasValue ? $"seed: {masterSeed}" : $"seed: {masterSeed} (random)";
		Console.WriteLine($"Running {_gameCount} games (AI depth: {_aiDepth}, {seedLabel})...");
		Console.WriteLine();

		var results = new List<GameResult>();
		var flaggedGames = new List<(int GameNumber, GameResult Result)>();

		var totalTimer = Stopwatch.StartNew();

		for (int i = 0; i < _gameCount; i++)
		{
			// Each game gets a block of 5 derived seeds: deck1, deck2, shuffle, AI
			var gameSeed = masterSeed + i * 5;
			var (state, ids, cardNames) = SetupGame(gameSeed);

			var aiRng = new Random(gameSeed + 4);
			var player1Strategy = new BeamSearchAiStrategy(ids, _aiDepth, rng: aiRng);
			var player2Strategy = new BeamSearchAiStrategy(ids, _aiDepth, rng: aiRng);

			var runner = new GameRunner(player1Strategy, player2Strategy);
			var (result, finalState) = runner.Run(state, ids, cardNames, shuffleSeed: gameSeed + 2);

			if (result.IsFlagged)
			{
				var savedPath = FlaggedGameSaver.TrySave(
					i + 1,
					result,
					finalState,
					flaggedGames.Count,
					cardNames
				);
				if (savedPath != null)
					result = result with { SavedFilePath = savedPath };

				flaggedGames.Add((i + 1, result));
			}

			results.Add(result);

			if ((i + 1) % 100 == 0)
			{
				var elapsed = totalTimer.Elapsed;
				Console.WriteLine(
					$"  Completed {i + 1} / {_gameCount} games... "
						+ $"({elapsed.TotalSeconds:F1}s elapsed)"
				);
			}
		}

		totalTimer.Stop();

		PrintAggregateReport(results);
		PrintTimingReport(results, totalTimer.ElapsedMilliseconds);
		PrintCardReport(results);
		PrintFlaggedGames(flaggedGames);
	}

	private static void PrintTimingReport(List<GameResult> results, long totalMs)
	{
		if (results.Count == 0)
			return;

		var gameTimes = results.Select(r => r.GameDurationMs).ToList();
		var avgMs = gameTimes.Average();
		var minMs = gameTimes.Min();
		var maxMs = gameTimes.Max();
		var totalSeconds = totalMs / 1000.0;

		Console.WriteLine("  --- Timing ---");
		Console.WriteLine();
		Console.WriteLine($"  Total time:       {totalSeconds:F2}s");
		Console.WriteLine($"  Avg time/game:    {avgMs:F1}ms");
		Console.WriteLine($"  Fastest game:     {minMs}ms");
		Console.WriteLine($"  Slowest game:     {maxMs}ms");
		Console.WriteLine($"  Games/second:     {results.Count / totalSeconds:F1}");
		Console.WriteLine();
	}

	private static (
		GameState State,
		MtgGameIds Ids,
		IReadOnlyDictionary<int, string> CardNames
	) SetupGame(int gameSeed)
	{
		var (state, ids) = MtgGameFactory.Create();

		var cardNames = new Dictionary<int, string>();

		// Krenko and Siege-Gang Commander excluded: token flood causes search blowup.
		// See DesignNotes.md — symmetry reduction needed before re-enabling.
		var excludedCards = new HashSet<string> { };
		var pool = CardLibrary.All.Where(c => !excludedCards.Contains(c.Name)).ToList();

		var deck1 = CardPool.BuildRandomDeck(ids.Player1Id, pool, rng: new Random(gameSeed));
		foreach (var card in deck1)
		{
			var (newState, added) = state.AddObject(card, parentId: ids.Player1LibraryId);
			state = newState;
			cardNames[added.Id] = added.Name;
		}

		var deck2 = CardPool.BuildRandomDeck(ids.Player2Id, pool, rng: new Random(gameSeed + 1));
		foreach (var card in deck2)
		{
			var (newState, added) = state.AddObject(card, parentId: ids.Player2LibraryId);
			state = newState;
			cardNames[added.Id] = added.Name;
		}

		return (state, ids, cardNames);
	}

	private static void PrintAggregateReport(List<GameResult> results)
	{
		var total = results.Count;
		var p1Wins = results.Count(r => r.IsPlayer1Win);
		var p2Wins = results.Count(r => r.IsPlayer2Win);
		var draws = results.Count(r => r.IsDraw);
		var avgTurns = results.Average(r => r.TurnCount);
		var avgActions = results.Average(r => r.TotalActions);

		var damageWins = results.Count(r => r.EndReason == GameEndReason.Damage);
		var libraryWins = results.Count(r => r.EndReason == GameEndReason.LibraryEmpty);
		var turnLimits = results.Count(r => r.EndReason == GameEndReason.TurnLimitReached);
		var actionLimits = results.Count(r => r.EndReason == GameEndReason.ActionLimitReached);
		var timeLimits = results.Count(r => r.EndReason == GameEndReason.TimeLimitReached);
		var exceptions = results.Count(r => r.EndReason == GameEndReason.UnhandledException);
		var warnings = results.Count(r => r.HadActionWarning);

		Console.WriteLine("╔═══════════════════════════════════════════════╗");
		Console.WriteLine("║           SIMULATION RESULTS                  ║");
		Console.WriteLine("╚═══════════════════════════════════════════════╝");
		Console.WriteLine();
		Console.WriteLine($"  Total games:      {total}");
		Console.WriteLine($"  Player 1 wins:    {p1Wins} ({Pct(p1Wins, total)})");
		Console.WriteLine($"  Player 2 wins:    {p2Wins} ({Pct(p2Wins, total)})");
		Console.WriteLine($"  Draws:            {draws} ({Pct(draws, total)})");
		Console.WriteLine();
		Console.WriteLine($"  Avg turn count:   {avgTurns:F1}");
		Console.WriteLine($"  Avg actions/game: {avgActions:F1}");
		Console.WriteLine();
		Console.WriteLine($"  End by damage:    {damageWins} ({Pct(damageWins, total)})");
		Console.WriteLine($"  End by library:   {libraryWins} ({Pct(libraryWins, total)})");
		Console.WriteLine($"  Turn limit hit:   {turnLimits} ({Pct(turnLimits, total)})");
		Console.WriteLine($"  Action limit hit: {actionLimits} ({Pct(actionLimits, total)})");
		Console.WriteLine($"  Time limit hit:   {timeLimits} ({Pct(timeLimits, total)})");
		Console.WriteLine($"  Exceptions:       {exceptions} ({Pct(exceptions, total)})");
		Console.WriteLine($"  Action warnings:  {warnings} ({Pct(warnings, total)})");
		Console.WriteLine();
	}

	private static void PrintCardReport(List<GameResult> results)
	{
		var p1DrawnStats = new Dictionary<string, (int Count, int Wins)>();
		var p2DrawnStats = new Dictionary<string, (int Count, int Wins)>();
		var p1PlayedStats = new Dictionary<string, (int Count, int Wins)>();
		var p2PlayedStats = new Dictionary<string, (int Count, int Wins)>();

		foreach (var result in results)
		{
			UpdateCardStats(p1DrawnStats, result.Player1DrawnCards, result.IsPlayer1Win);
			UpdateCardStats(p2DrawnStats, result.Player2DrawnCards, result.IsPlayer2Win);
			UpdateCardStats(p1PlayedStats, result.Player1PlayedCards, result.IsPlayer1Win);
			UpdateCardStats(p2PlayedStats, result.Player2PlayedCards, result.IsPlayer2Win);
		}

		var allNames = p1DrawnStats.Keys.Union(p2DrawnStats.Keys).OrderBy(n => n).ToList();

		// --- Drawn table ---
		Console.WriteLine("  --- Card Win Rate When Drawn ---");
		Console.WriteLine();
		Console.WriteLine(
			$"  {"Card", -25} {"P1 Drawn", 9} {"P1 Win%", 8} {"P2 Drawn", 9} {"P2 Win%", 8} {"Combined", 9}"
		);
		Console.WriteLine($"  {new string('-', 72)}");

		var drawnRows = allNames
			.Select(name =>
			{
				p1DrawnStats.TryGetValue(name, out var p1);
				p2DrawnStats.TryGetValue(name, out var p2);
				var combined = WinRate(p1.Wins + p2.Wins, p1.Count + p2.Count);
				return (name, p1, p2, combined);
			})
			.OrderByDescending(r => r.combined)
			.ToList();

		foreach (var (name, p1, p2, combined) in drawnRows)
		{
			var p1WinPct = p1.Count > 0 ? $"{WinRate(p1.Wins, p1.Count):F1}%" : NotAvailable;
			var p2WinPct = p2.Count > 0 ? $"{WinRate(p2.Wins, p2.Count):F1}%" : NotAvailable;

			Console.WriteLine(
				$"  {name, -25} {p1.Count, 9} {p1WinPct, 8} {p2.Count, 9} {p2WinPct, 8} {combined, 8:F1}%"
			);
		}

		Console.WriteLine();

		// --- Played table ---
		// Sorted by play rate ascending so "dead" cards (drawn but rarely played) appear first.
		Console.WriteLine("  --- Card Win Rate When Played ---");
		Console.WriteLine();
		Console.WriteLine(
			$"  {"Card", -25} {"Drawn", 7} {"Played", 8} {"Play%", 7} {"P1 Win%", 8} {"P2 Win%", 8} {"Combined", 9}"
		);
		Console.WriteLine($"  {new string('-', 76)}");

		var playedRows = allNames
			.Select(name =>
			{
				p1DrawnStats.TryGetValue(name, out var p1Drawn);
				p2DrawnStats.TryGetValue(name, out var p2Drawn);
				p1PlayedStats.TryGetValue(name, out var p1Played);
				p2PlayedStats.TryGetValue(name, out var p2Played);
				var totalDrawn = p1Drawn.Count + p2Drawn.Count;
				var totalPlayed = p1Played.Count + p2Played.Count;
				var playRate = totalDrawn > 0 ? 100.0 * totalPlayed / totalDrawn : 0;
				var combined = WinRate(
					p1Played.Wins + p2Played.Wins,
					p1Played.Count + p2Played.Count
				);
				return (name, p1Played, p2Played, totalDrawn, totalPlayed, playRate, combined);
			})
			.OrderBy(r => r.playRate)
			.ToList();

		foreach (
			var (
				name,
				p1Played,
				p2Played,
				totalDrawn,
				totalPlayed,
				playRate,
				combined
			) in playedRows
		)
		{
			var p1WinPct =
				p1Played.Count > 0 ? $"{WinRate(p1Played.Wins, p1Played.Count):F1}%" : NotAvailable;
			var p2WinPct =
				p2Played.Count > 0 ? $"{WinRate(p2Played.Wins, p2Played.Count):F1}%" : NotAvailable;
			var combinedStr = totalPlayed > 0 ? $"{combined:F1}%" : NotAvailable;

			Console.WriteLine(
				$"  {name, -25} {totalDrawn, 7} {totalPlayed, 8} {playRate, 6:F1}% {p1WinPct, 8} {p2WinPct, 8} {combinedStr, 9}"
			);
		}

		Console.WriteLine();

		var p1BaseWins = results.Count(r => r.IsPlayer1Win);
		var p2BaseWins = results.Count(r => r.IsPlayer2Win);
		var total = results.Count;
		Console.WriteLine(
			$"  Baseline: P1 wins {Pct(p1BaseWins, total)}, P2 wins {Pct(p2BaseWins, total)}"
		);
		Console.WriteLine();
	}

	private static void PrintFlaggedGames(List<(int GameNumber, GameResult Result)> flaggedGames)
	{
		if (flaggedGames.Count == 0)
		{
			Console.WriteLine("  No flagged games.");
			return;
		}

		var savedCount = flaggedGames.Count(g => g.Result.SavedFilePath != null);
		Console.WriteLine($"  --- Flagged Games ({flaggedGames.Count}) ---");
		if (savedCount > 0)
			Console.WriteLine(
				$"  Snapshots saved to: ./{SaveDirectory}/  ({savedCount} of {flaggedGames.Count})"
			);
		if (flaggedGames.Count > FlaggedGameSaver.MaxSaves)
			Console.WriteLine(
				$"  (save cap of {FlaggedGameSaver.MaxSaves} reached — remaining games not saved)"
			);
		Console.WriteLine();

		foreach (var (gameNumber, result) in flaggedGames)
		{
			string winner;
			if (result.IsDraw)
				winner = "Draw";
			else if (result.IsPlayer1Win)
				winner = "Player 1";
			else
				winner = "Player 2";

			var flags = new List<string>();
			if (result.EndReason == GameEndReason.TurnLimitReached)
				flags.Add("TURN LIMIT");
			if (result.EndReason == GameEndReason.ActionLimitReached)
				flags.Add("ACTION LIMIT");
			if (result.EndReason == GameEndReason.TimeLimitReached)
				flags.Add("TIME LIMIT");
			if (result.EndReason == GameEndReason.UnhandledException)
			{
				var firstLine =
					result
						.ExceptionMessage?.Split('\n', StringSplitOptions.RemoveEmptyEntries)
						.FirstOrDefault() ?? "unknown";
				flags.Add($"EXCEPTION: {firstLine}");
			}
			if (result.HadActionWarning)
				flags.Add("action warning");

			var savedMark = result.SavedFilePath != null ? " [saved]" : "";

			Console.WriteLine(
				$"  Game {gameNumber, 4}: {winner, -10} | "
					+ $"Turns: {result.TurnCount, 3} | "
					+ $"Actions: {result.TotalActions, 5} | "
					+ $"Time: {result.GameDurationMs, 6}ms | "
					+ $"[{string.Join(", ", flags)}]{savedMark}"
			);
		}

		Console.WriteLine();
	}

	private static void UpdateCardStats(
		Dictionary<string, (int Drawn, int DrawnInWin)> stats,
		IReadOnlyList<string> drawnCards,
		bool playerWon
	)
	{
		foreach (var name in drawnCards.Distinct())
		{
			if (!stats.ContainsKey(name))
				stats[name] = (0, 0);

			var (drawn, wins) = stats[name];
			stats[name] = (drawn + 1, wins + (playerWon ? 1 : 0));
		}
	}

	private static string Pct(int value, int total) =>
		total == 0 ? "0%" : $"{100.0 * value / total:F1}%";

	private static double WinRate(int wins, int drawn) => drawn == 0 ? 0 : 100.0 * wins / drawn;

	private const string SaveDirectory = "flagged_games";
	private const string NotAvailable = "  n/a";
}
