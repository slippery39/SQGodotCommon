using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// A compact end-of-game board reading, captured for every game so draw games can be
/// compared against decided games rather than described in isolation.
///
/// Deliberately not a <see cref="GameStateSnapshot"/>: that carries the full event log and
/// is written per file, which is far too much to hold for every game of a training run.
/// </summary>
public readonly record struct BoardSnapshot(
	int Life1,
	int Life2,
	int Library1,
	int Library2,
	int Hand1,
	int Hand2,
	IReadOnlyList<string> Battlefield1,
	IReadOnlyList<string> Battlefield2
)
{
	public bool AnyLibraryEmpty => Library1 == 0 || Library2 == 0;
	public int Creatures => Battlefield1.Count + Battlefield2.Count;

	public static readonly BoardSnapshot Unknown = new(0, 0, 0, 0, 0, 0, [], []);
}

/// <summary>
/// Explains a run's draws: whether they are gameplay draws or the harness giving up, and
/// what the drawing games have in common.
///
/// A draw is only a real draw when both players lost simultaneously — everything else is a
/// limit in <see cref="GameRunner"/> firing, which is a harness or AI-speed problem, not a
/// rules outcome. Without the split, "500 draws" says nothing about which one you have.
/// </summary>
public static class DrawDiagnostics
{
	private const int MinCardSample = 30;
	private const int TopCards = 12;

	/// Reads the four numbers and two card lists we compare on. Never throws: an
	/// UnhandledException game can leave a state these lookups do not expect, and losing
	/// diagnostics must not lose the training run.
	public static BoardSnapshot Capture(GameState state, MtgGameIds ids)
	{
		try
		{
			return new BoardSnapshot(
				state.GetPlayer(ids.Player1Id).Life,
				state.GetPlayer(ids.Player2Id).Life,
				state.GetCardsInZone(ids.Player1LibraryId).Count(),
				state.GetCardsInZone(ids.Player2LibraryId).Count(),
				state.GetCardsInZone(ids.Player1HandId).Count(),
				state.GetCardsInZone(ids.Player2HandId).Count(),
				[.. state.GetCardsInZone(ids.Player1BattlefieldId).Select(c => c.Name)],
				[.. state.GetCardsInZone(ids.Player2BattlefieldId).Select(c => c.Name)]
			);
		}
		catch
		{
			return BoardSnapshot.Unknown;
		}
	}

	/// <param name="boards">Parallel to <paramref name="results"/>; may be empty if the
	/// caller did not capture board state, in which case only the reason split is printed.</param>
	public static void Print(IReadOnlyList<GameResult> results, IReadOnlyList<BoardSnapshot> boards)
	{
		var draws = results.Where(r => r.IsDraw).ToList();
		Console.WriteLine("  --- Draw Diagnostics ---");
		Console.WriteLine();
		if (draws.Count == 0)
		{
			Console.WriteLine("  No draws.");
			Console.WriteLine();
			return;
		}

		var drawRate = (double)draws.Count / results.Count;
		Console.WriteLine($"  Draws: {draws.Count} / {results.Count} ({drawRate:P1})");
		Console.WriteLine();

		// 1. Gameplay draw or harness giving up?
		var gameplay = draws.Count(IsGameplayDraw);
		Console.WriteLine("  By end reason:");
		foreach (var g in draws.GroupBy(r => r.EndReason).OrderByDescending(g => g.Count()))
		{
			var label = IsGameplayDraw(g.First()) ? "gameplay draw" : "HARNESS GAVE UP";
			var medianTurn = Median(g.Select(r => (double)r.TurnCount));
			var medianMs = Median(g.Select(r => (double)r.GameDurationMs));
			Console.WriteLine(
				$"    {g.Key, -22} {g.Count(), 6}  ({(double)g.Count() / results.Count:P1} of games)"
					+ $"  median turn {medianTurn:F0}, {medianMs:F0}ms   [{label}]"
			);
		}
		Console.WriteLine($"    -> {gameplay} genuine, {draws.Count - gameplay} unfinished");
		Console.WriteLine();

		PrintByRunPosition(results);

		if (boards.Count == results.Count)
			PrintBoardComparison(results, boards, draws.Count);

		PrintCardLift(results, drawRate);

		if (boards.Count == results.Count)
			PrintBattlefieldLift(results, boards, drawRate);
	}

	// Both players lost at once — the only way MtgCore itself reports a draw. Every other
	// reason is a GameRunner limit.
	private static bool IsGameplayDraw(GameResult r) =>
		r.EndReason is GameEndReason.Damage or GameEndReason.LibraryEmpty;

	/// <summary>
	/// Draw rate over the course of the run. A card-pool or rules problem is flat across
	/// quarters; a rate that climbs is the harness degrading — the games are the same games,
	/// so the only thing that changed is the machine running them (GC pressure from retained
	/// results, thermal throttling, another process taking cores).
	///
	/// Index order is not exactly completion order under Parallel.For, but chunks are handed
	/// out in increasing index order, so a quarter-of-the-run trend is readable. The median
	/// game duration per quarter is the load-bearing column.
	/// </summary>
	private static void PrintByRunPosition(IReadOnlyList<GameResult> results)
	{
		if (results.Count < 40)
			return;

		Console.WriteLine(
			"  By position in the run (flat = card pool, rising = harness degrading):"
		);
		var size = results.Count / 4;
		for (var q = 0; q < 4; q++)
		{
			var slice = results
				.Skip(q * size)
				.Take(q == 3 ? results.Count - 3 * size : size)
				.ToList();
			var rate = slice.Count(r => r.IsDraw) / (double)slice.Count;
			var medianMs = Median(slice.Select(r => (double)r.GameDurationMs));
			Console.WriteLine($"    Q{q + 1}  {rate, 7:P1} draws   median game {medianMs, 6:F0}ms");
		}
		Console.WriteLine();
	}

	private static void PrintBoardComparison(
		IReadOnlyList<GameResult> results,
		IReadOnlyList<BoardSnapshot> boards,
		int drawCount
	)
	{
		var drawn = new List<BoardSnapshot>(drawCount);
		var decided = new List<BoardSnapshot>(results.Count - drawCount);
		for (var i = 0; i < results.Count; i++)
			(results[i].IsDraw ? drawn : decided).Add(boards[i]);

		Console.WriteLine("  Final board — median at draw vs median when decided:");
		Row("Life (lower)", b => Math.Min(b.Life1, b.Life2));
		Row("Life (higher)", b => Math.Max(b.Life1, b.Life2));
		Row("Library (lower)", b => Math.Min(b.Library1, b.Library2));
		Row("Hand (total)", b => b.Hand1 + b.Hand2);
		Row("Permanents", b => b.Creatures);

		var emptyLib = drawn.Count(b => b.AnyLibraryEmpty) / (double)Math.Max(1, drawn.Count);
		Console.WriteLine($"    Someone decked at draw: {emptyLib:P1} of draws");
		Console.WriteLine();

		void Row(string label, Func<BoardSnapshot, int> f)
		{
			var d = Median(drawn.Select(b => (double)f(b)));
			var w = Median(decided.Select(b => (double)f(b)));
			Console.WriteLine($"    {label, -18} draw {d, 6:F0}   decided {w, 6:F0}");
		}
	}

	/// P(draw | card was drawn) against the overall draw rate. A card that stalls games out
	/// shows a positive lift over a large sample; noise shows a big lift over a small one,
	/// which is what MinCardSample is for.
	private static void PrintCardLift(IReadOnlyList<GameResult> results, double drawRate)
	{
		var counts = new Dictionary<string, (int Games, int Draws)>(StringComparer.Ordinal);
		foreach (var r in results)
		{
			foreach (
				var name in r
					.Player1DrawnCards.Concat(r.Player2DrawnCards)
					.Distinct(StringComparer.Ordinal)
			)
			{
				var c = counts.GetValueOrDefault(name);
				counts[name] = (c.Games + 1, c.Draws + (r.IsDraw ? 1 : 0));
			}
		}

		PrintLiftTable(
			"Cards drawn in drawing games (P(draw | drawn) vs base rate)",
			counts,
			drawRate
		);
	}

	/// Same idea on the end-of-game battlefield: a card that is *present when time runs out*
	/// is a better stall suspect than one that was merely drawn at some point.
	private static void PrintBattlefieldLift(
		IReadOnlyList<GameResult> results,
		IReadOnlyList<BoardSnapshot> boards,
		double drawRate
	)
	{
		var counts = new Dictionary<string, (int Games, int Draws)>(StringComparer.Ordinal);
		for (var i = 0; i < results.Count; i++)
		{
			var isDraw = results[i].IsDraw;
			foreach (
				var name in boards[i]
					.Battlefield1.Concat(boards[i].Battlefield2)
					.Distinct(StringComparer.Ordinal)
			)
			{
				var c = counts.GetValueOrDefault(name);
				counts[name] = (c.Games + 1, c.Draws + (isDraw ? 1 : 0));
			}
		}

		PrintLiftTable(
			"Permanents on the board at game end (P(draw | on board) vs base rate)",
			counts,
			drawRate
		);
	}

	private static void PrintLiftTable(
		string title,
		Dictionary<string, (int Games, int Draws)> counts,
		double drawRate
	)
	{
		var rows = counts
			.Where(kv => kv.Value.Games >= MinCardSample)
			.Select(kv =>
				(Name: kv.Key, kv.Value.Games, Rate: (double)kv.Value.Draws / kv.Value.Games)
			)
			.OrderByDescending(r => r.Rate)
			.Take(TopCards)
			.ToList();
		if (rows.Count == 0)
			return;

		Console.WriteLine($"  {title}:");
		Console.WriteLine($"    (base rate {drawRate:P1}, min {MinCardSample} games)");
		foreach (var r in rows)
			Console.WriteLine(
				$"    {r.Name, -30} {r.Rate, 7:P1}  ({r.Rate - drawRate:+0.0%;-0.0%})  n={r.Games}"
			);
		Console.WriteLine();
	}

	private static double Median(IEnumerable<double> values)
	{
		var sorted = values.OrderBy(v => v).ToList();
		return sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
	}
}
