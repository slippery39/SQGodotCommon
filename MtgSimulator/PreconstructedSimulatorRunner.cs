using System.Diagnostics;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

public class PreconstructedSimulatorRunner
{
	private readonly int _n;
	private readonly int _aiDepth;

	/// <param name="n">Games per side per matchup. Each unique pair plays 2N total games.</param>
	public PreconstructedSimulatorRunner(int n, int aiDepth = 3)
	{
		_n = n;
		_aiDepth = aiDepth;
	}

	public void Run()
	{
		var decks = DeckRegistry.All;
		var schedule = BuildSchedule(decks, _n);
		var totalGames = schedule.Count;

		Console.WriteLine(
			$"Running {totalGames} games "
				+ $"({decks.Count} decks · N={_n} · AI depth: {_aiDepth})..."
		);
		Console.WriteLine();

		var stats = new PreconstructedStats();
		var flaggedGames = new List<(int GameNumber, GameResult Result)>();
		var totalTimer = Stopwatch.StartNew();
		var progressInterval = Math.Max(1, totalGames / 10);

		for (var i = 0; i < schedule.Count; i++)
		{
			var (deck1Name, deck2Name) = schedule[i];
			var (state, ids, cardNames) = SetupGame(deck1Name, deck2Name);

			var rng = new Random();
			var p1Strategy = new MultiTurnBeamSearchAiStrategy(ids, _aiDepth, rng: rng);
			var p2Strategy = new MultiTurnBeamSearchAiStrategy(ids, _aiDepth, rng: rng);

			var (gameResult, finalState) = new GameRunner(p1Strategy, p2Strategy).Run(
				state,
				ids,
				cardNames
			);

			if (gameResult.IsFlagged)
			{
				var savedPath = FlaggedGameSaver.TrySave(
					i + 1,
					gameResult,
					finalState,
					flaggedGames.Count,
					cardNames
				);
				if (savedPath != null)
					gameResult = gameResult with { SavedFilePath = savedPath };

				flaggedGames.Add((i + 1, gameResult));
			}

			stats.AddResult(
				new PreconstructedGameResult
				{
					GameResult = gameResult,
					Player1DeckName = deck1Name,
					Player2DeckName = deck2Name,
					Player1IsOnPlay = true, // Player 1 always goes first in this engine
				}
			);

			if ((i + 1) % progressInterval == 0)
			{
				Console.WriteLine(
					$"  Completed {i + 1} / {totalGames} "
						+ $"({totalTimer.Elapsed.TotalSeconds:F1}s elapsed)"
				);
			}
		}

		totalTimer.Stop();

		PrintSummary(stats, totalGames, _n, decks.Count, _aiDepth, totalTimer.ElapsedMilliseconds);
		PrintFlaggedGames(flaggedGames);

		var csvPath = PreconstructedCsvExporter.Export(stats);
		Console.WriteLine($"  Card stats written to: {csvPath}");
		Console.WriteLine();
	}

	// Each unique pair (A, B) yields N games with A as Player 1 (on play)
	// and N games with B as Player 1 (on play).
	// Schedule is shuffled so matchups are interleaved during the run.
	private static List<(string Deck1, string Deck2)> BuildSchedule(
		IReadOnlyList<DeckInfo> decks,
		int n
	)
	{
		var schedule = new List<(string, string)>();
		for (var i = 0; i < decks.Count; i++)
		{
			for (var j = i + 1; j < decks.Count; j++)
			{
				for (var k = 0; k < n; k++)
					schedule.Add((decks[i].Name, decks[j].Name));
				for (var k = 0; k < n; k++)
					schedule.Add((decks[j].Name, decks[i].Name));
			}
		}

		var rng = new Random();
		schedule = schedule.OrderBy(_ => rng.Next()).ToList();
		return schedule;
	}

	private static (
		GameState State,
		MtgGameIds Ids,
		IReadOnlyDictionary<int, string> CardNames
	) SetupGame(string deck1Name, string deck2Name)
	{
		var (state, ids) = MtgGameFactory.Create();
		var cardNames = new Dictionary<int, string>();
		var rng = new Random();

		var deck1 = DeckRegistry.Build(deck1Name, ids.Player1Id).OrderBy(_ => rng.Next()).ToList();
		foreach (var card in deck1)
		{
			var (newState, added) = state.AddObject(card, parentId: ids.Player1LibraryId);
			state = newState;
			cardNames[added.Id] = added.Name;
		}

		var deck2 = DeckRegistry.Build(deck2Name, ids.Player2Id).OrderBy(_ => rng.Next()).ToList();
		foreach (var card in deck2)
		{
			var (newState, added) = state.AddObject(card, parentId: ids.Player2LibraryId);
			state = newState;
			cardNames[added.Id] = added.Name;
		}

		return (state, ids, cardNames);
	}

	private static void PrintSummary(
		PreconstructedStats stats,
		int totalGames,
		int n,
		int deckCount,
		int aiDepth,
		long totalMs
	)
	{
		Console.WriteLine();
		Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
		Console.WriteLine("║        PRECONSTRUCTED DECK SIMULATION RESULTS            ║");
		Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
		Console.WriteLine();
		Console.WriteLine(
			$"  {deckCount} decks · N={n} · {totalGames} total games · AI depth: {aiDepth}"
		);
		Console.WriteLine($"  Total time: {totalMs / 1000.0:F2}s");
		Console.WriteLine();

		PrintDeckStats(stats.GetDeckStats());
		PrintMatchupStats(stats.GetMatchupStats());
	}

	private static void PrintDeckStats(IReadOnlyList<DeckWinRateRow> rows)
	{
		Console.WriteLine("  --- Overall Win Rates ---");
		Console.WriteLine();
		Console.WriteLine(
			$"  {"Deck", -18} {"Games", 6}  {"Wins", 5}  {"Win%", 7}  {"OnPlay%", 8}  {"OnDraw%", 8}  {"AvgWinT", 8}  {"MinT", 5}  {"MaxT", 5}"
		);
		Console.WriteLine($"  {new string('-', 85)}");

		foreach (var r in rows)
		{
			var avgWinTurn = r.Wins > 0 ? $"{r.AvgWinTurn:F1}" : "—";
			var minTurn = r.Wins > 0 ? r.MinWinTurn.ToString() : "—";
			var maxTurn = r.Wins > 0 ? r.MaxWinTurn.ToString() : "—";
			Console.WriteLine(
				$"  {r.DeckName, -18} {r.TotalGames, 6}  {r.Wins, 5}  {Pct(r.WinRate), 7}  "
					+ $"{Pct(r.OnPlayWinRate), 8}  {Pct(r.OnDrawWinRate), 8}  "
					+ $"{avgWinTurn, 8}  {minTurn, 5}  {maxTurn, 5}"
			);
		}

		Console.WriteLine();
	}

	private static void PrintMatchupStats(IReadOnlyList<MatchupWinRateRow> rows)
	{
		Console.WriteLine("  --- Matchup Win Rates ---");
		Console.WriteLine();
		Console.WriteLine(
			$"  {"Deck", -18} {"vs Opponent", -18} {"Games", 6}  {"Wins", 5}  {"Win%", 7}  {"OnPlay%", 8}  {"OnDraw%", 8}"
		);
		Console.WriteLine($"  {new string('-', 80)}");

		foreach (var r in rows)
		{
			Console.WriteLine(
				$"  {r.DeckName, -18} {"vs " + r.OpponentDeckName, -18} {r.TotalGames, 6}  {r.Wins, 5}  {Pct(r.WinRate), 7}  "
					+ $"{Pct(r.OnPlayWinRate), 8}  {Pct(r.OnDrawWinRate), 8}"
			);
		}

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

	private static string Pct(double rate) => $"{rate * 100:F1}%";

	private const string SaveDirectory = "flagged_games";
}
