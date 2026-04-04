using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Orchestrates multiple simulated games and aggregates results.
/// Each game uses freshly randomised 20-card decks drawn from CardPool.
/// </summary>
public class SimulatorRunner
{
	private readonly int _gameCount;
	private readonly GameRunner _runner = new();

	public SimulatorRunner(int gameCount)
	{
		_gameCount = gameCount;
	}

	public void Run()
	{
		Console.WriteLine($"Running {_gameCount} games...");
		Console.WriteLine();

		var results = new List<GameResult>();
		var flaggedResults = new List<(int GameNumber, GameResult Result)>();

		for (int i = 0; i < _gameCount; i++)
		{
			var (state, ids, cardNames) = SetupGame();
			var result = _runner.Run(state, ids, cardNames);
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

		// Wire up post-action processor
		state = state with
		{
			PostActionProcessor = new CheckStateBasedEffectsAction
			{
				Player1Id = ids.Player1Id,
				Player2Id = ids.Player2Id,
			},
		};

		var cardNames = new Dictionary<int, string>();

		// Build and load Player 1's deck
		var deck1 = CardPool.BuildRandomDeck(ids.Player1Id);
		foreach (var card in deck1)
		{
			var (newState, added) = state.AddObject(card, parentId: ids.Player1LibraryId);
			state = newState;
			cardNames[added.Id] = added.Name;
		}

		// Build and load Player 2's deck
		var deck2 = CardPool.BuildRandomDeck(ids.Player2Id);
		foreach (var card in deck2)
		{
			var (newState, added) = state.AddObject(card, parentId: ids.Player2LibraryId);
			state = newState;
			cardNames[added.Id] = added.Name;
		}

		(state, _) = state.BeginGame(ids.GameId);

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

		Console.WriteLine("╔══════════════════════════════════════════╗");
		Console.WriteLine("║           SIMULATION RESULTS             ║");
		Console.WriteLine("╚══════════════════════════════════════════╝");
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
		// Build per-card stats: times drawn, times drawn in a winning game
		var stats = new Dictionary<string, (int Drawn, int DrawnInWin)>();

		foreach (var result in results)
		{
			UpdateCardStats(stats, result.Player1DrawnCards, result.IsPlayer1Win);
			UpdateCardStats(stats, result.Player2DrawnCards, result.IsPlayer2Win);
		}

		Console.WriteLine("  --- Card Win Rate When Drawn ---");
		Console.WriteLine();

		var sorted = stats
			.OrderByDescending(kvp => WinRate(kvp.Value.DrawnInWin, kvp.Value.Drawn))
			.ToList();

		Console.WriteLine($"  {"Card", -25} {"Drawn", 6} {"Wins", 6} {"Win%", 7}");
		Console.WriteLine($"  {new string('-', 47)}");

		foreach (var (name, (drawn, wins)) in sorted)
		{
			Console.WriteLine($"  {name, -25} {drawn, 6} {wins, 6} {WinRate(wins, drawn), 6:F1}%");
		}

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
		// Use distinct names — we care whether the card was drawn, not how many times
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
