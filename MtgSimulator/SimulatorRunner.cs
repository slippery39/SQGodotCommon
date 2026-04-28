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

	public SimulatorRunner(int gameCount, int aiDepth = 3)
	{
		_gameCount = gameCount;
		_aiDepth = aiDepth;
	}

	public void Run()
	{
		Console.WriteLine($"Running {_gameCount} games (AI depth: {_aiDepth})...");
		Console.WriteLine();

		var results = new List<GameResult>();
		var flaggedGames = new List<(int GameNumber, GameResult Result)>();

		var totalTimer = Stopwatch.StartNew();

		for (int i = 0; i < _gameCount; i++)
		{
			var (state, ids, cardNames) = SetupGame();

			var rng = new Random();
			var player1Strategy = new DepthLimitedAiStrategy(ids, _aiDepth, rng);
			var player2Strategy = new DepthLimitedAiStrategy(ids, _aiDepth, rng);

			var runner = new GameRunner(player1Strategy, player2Strategy);
			var (result, finalState) = runner.Run(state, ids, cardNames);

			if (result.IsFlagged)
			{
				var savedPath = FlaggedGameSaver.TrySave(
					i + 1,
					result,
					finalState,
					ids,
					flaggedGames.Count
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
	) SetupGame()
	{
		var (state, ids) = MtgGameFactory.Create();

		var cardNames = new Dictionary<int, string>();

		// Krenko and Siege-Gang Commander excluded: token flood causes search blowup.
		// See DesignNotes.md — symmetry reduction needed before re-enabling.
		var excludedCards = new HashSet<string> { "Krenko, Mob Boss", "Siege-Gang Commander" };
		var pool = CardPool
			.All.Concat(CardLibrary.All)
			.Where(c => !excludedCards.Contains(c.Name))
			.ToList();

		var deck1 = CardPool.BuildRandomDeck(ids.Player1Id, pool);
		foreach (var card in deck1)
		{
			var (newState, added) = state.AddObject(card, parentId: ids.Player1LibraryId);
			state = newState;
			cardNames[added.Id] = added.Name;
		}

		var deck2 = CardPool.BuildRandomDeck(ids.Player2Id, pool);
		foreach (var card in deck2)
		{
			var (newState, added) = state.AddObject(card, parentId: ids.Player2LibraryId);
			state = newState;
			cardNames[added.Id] = added.Name;
		}

		(state, _) = state.BeginGame(ids.GameId, ids.Player1Id, ids.Player2Id);

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
		Console.WriteLine($"  Action warnings:  {warnings} ({Pct(warnings, total)})");
		Console.WriteLine();
	}

	private static void PrintCardReport(List<GameResult> results)
	{
		var p1Stats = new Dictionary<string, (int Drawn, int DrawnInWin)>();
		var p2Stats = new Dictionary<string, (int Drawn, int DrawnInWin)>();

		foreach (var result in results)
		{
			UpdateCardStats(p1Stats, result.Player1DrawnCards, result.IsPlayer1Win);
			UpdateCardStats(p2Stats, result.Player2DrawnCards, result.IsPlayer2Win);
		}

		var allNames = p1Stats.Keys.Union(p2Stats.Keys).OrderBy(n => n).ToList();

		Console.WriteLine("  --- Card Win Rate When Drawn ---");
		Console.WriteLine();
		Console.WriteLine(
			$"  {"Card", -25} {"P1 Drawn", 9} {"P1 Win%", 8} {"P2 Drawn", 9} {"P2 Win%", 8} {"Combined", 9}"
		);
		Console.WriteLine($"  {new string('-', 72)}");

		var rows = allNames
			.Select(name =>
			{
				p1Stats.TryGetValue(name, out var p1);
				p2Stats.TryGetValue(name, out var p2);
				var combined = WinRate(p1.DrawnInWin + p2.DrawnInWin, p1.Drawn + p2.Drawn);
				return (name, p1, p2, combined);
			})
			.OrderByDescending(r => r.combined)
			.ToList();

		foreach (var (name, p1, p2, combined) in rows)
		{
			var p1WinPct = p1.Drawn > 0 ? $"{WinRate(p1.DrawnInWin, p1.Drawn):F1}%" : "  n/a";
			var p2WinPct = p2.Drawn > 0 ? $"{WinRate(p2.DrawnInWin, p2.Drawn):F1}%" : "  n/a";

			Console.WriteLine(
				$"  {name, -25} {p1.Drawn, 9} {p1WinPct, 8} {p2.Drawn, 9} {p2WinPct, 8} {combined, 8:F1}%"
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
}
