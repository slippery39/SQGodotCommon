using System.Collections.Immutable;
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
		var flaggedResults = new List<(int GameNumber, GameResult Result)>();

		for (int i = 0; i < _gameCount; i++)
		{
			var (state, ids, cardNames) = SetupGame();

			// Both players use depth-limited AI — swap to RandomAiStrategy for baseline
			var rng = new Random();
			var player1Strategy = new DepthLimitedAiStrategy(ids, _aiDepth, rng);
			var player2Strategy = new DepthLimitedAiStrategy(ids, _aiDepth, rng);

			var runner = new GameRunner(player1Strategy, player2Strategy);
			var result = runner.Run(state, ids, cardNames);
			results.Add(result);

			if (result.IsFlagged)
				flaggedResults.Add((i + 1, result));

			if ((i + 1) % 100 == 0)
				Console.WriteLine($"  Completed {i + 1} / {_gameCount} games...");
		}

		PrintAggregateReport(results);
		PrintCardReport(results);
		PrintFlaggedGames(flaggedResults);
	}

	private static (
		GameState State,
		MtgGameIds Ids,
		IReadOnlyDictionary<int, string> CardNames
	) SetupGame()
	{
		var (state, ids) = MtgGameFactory.Create();

		var cardNames = new Dictionary<int, string>();

		var deck1 = CardPool.BuildRandomDeck(ids.Player1Id);
		foreach (var card in deck1)
		{
			var (newState, added) = state.AddObject(card, parentId: ids.Player1LibraryId);
			state = newState;
			cardNames[added.Id] = added.Name;
		}

		var deck2 = CardPool.BuildRandomDeck(ids.Player2Id);
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
		Console.WriteLine($"  Action warnings:  {warnings} ({Pct(warnings, total)})");
		Console.WriteLine();
	}

	private static void PrintCardReport(List<GameResult> results)
	{
		// Per-card stats tracked separately for each player position
		var p1Stats = new Dictionary<string, (int Drawn, int DrawnInWin)>();
		var p2Stats = new Dictionary<string, (int Drawn, int DrawnInWin)>();

		foreach (var result in results)
		{
			UpdateCardStats(p1Stats, result.Player1DrawnCards, result.IsPlayer1Win);
			UpdateCardStats(p2Stats, result.Player2DrawnCards, result.IsPlayer2Win);
		}

		// Combine into a unified view per card name
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

		// Print baseline win rates for context
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
		if (!flaggedGames.Any())
		{
			Console.WriteLine("  No flagged games.");
			return;
		}

		Console.WriteLine($"  --- Flagged Games ({flaggedGames.Count}) ---");
		Console.WriteLine();

		foreach (var (gameNumber, result) in flaggedGames)
		{
			var winner =
				result.IsDraw ? "Draw"
				: result.IsPlayer1Win ? "Player 1"
				: "Player 2";

			var flags = new List<string>();
			if (result.EndReason == GameEndReason.TurnLimitReached)
				flags.Add("TURN LIMIT");
			if (result.EndReason == GameEndReason.ActionLimitReached)
				flags.Add("ACTION LIMIT");
			if (result.HadActionWarning)
				flags.Add("action warning");

			Console.WriteLine(
				$"  Game {gameNumber, 4}: {winner, -10} | "
					+ $"Turns: {result.TurnCount, 3} | "
					+ $"Actions: {result.TotalActions, 5} | "
					+ $"[{string.Join(", ", flags)}]"
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
}
