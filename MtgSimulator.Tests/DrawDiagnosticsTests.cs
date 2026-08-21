using MtgSimulator;

namespace MtgSimulator.Tests;

/// The report is print-only, so it is checked by reading what it prints — the whole point
/// of the class is that the text at the end of a training run is correct.
[TestFixture]
public class DrawDiagnosticsTests
{
	private static GameResult Game(int winner, GameEndReason reason, params string[] drawnCards) =>
		new()
		{
			WinnerPlayerId = winner,
			Player1Id = 1,
			Player2Id = 2,
			EndReason = reason,
			TurnCount = 20,
			Player1DrawnCards = drawnCards,
		};

	private static string Report(
		IReadOnlyList<GameResult> results,
		IReadOnlyList<BoardSnapshot> boards
	)
	{
		var original = Console.Out;
		var writer = new StringWriter();
		Console.SetOut(writer);
		try
		{
			DrawDiagnostics.Print(results, boards);
		}
		finally
		{
			Console.SetOut(original);
		}
		return writer.ToString();
	}

	[Test]
	public void SeparatesHarnessDrawsFromGameplayDraws()
	{
		var results = new List<GameResult>
		{
			Game(1, GameEndReason.Damage),
			Game(2, GameEndReason.Damage),
			Game(-1, GameEndReason.TimeLimitReached),
			Game(-1, GameEndReason.Damage), // both players died at once
		};

		var text = Report(results, []);

		Assert.That(text, Does.Contain("Draws: 2 / 4"));
		Assert.That(text, Does.Contain("1 genuine, 1 unfinished"));
		Assert.That(text, Does.Contain("HARNESS GAVE UP"));
	}

	[Test]
	public void RanksTheCardThatShowsUpInDraws()
	{
		// Staller is drawn in every draw and never in a decided game; Filler is everywhere.
		var results = new List<GameResult>();
		for (var i = 0; i < 40; i++)
			results.Add(Game(-1, GameEndReason.TimeLimitReached, "Staller", "Filler"));
		for (var i = 0; i < 40; i++)
			results.Add(Game(1, GameEndReason.Damage, "Filler"));

		var text = Report(results, []);
		var staller = text.IndexOf("Staller", StringComparison.Ordinal);
		var filler = text.IndexOf("Filler", StringComparison.Ordinal);

		Assert.That(staller, Is.GreaterThan(-1), "Staller missing from the lift table");
		Assert.That(staller, Is.LessThan(filler), "Staller should outrank Filler");
		Assert.That(text, Does.Contain("100.0%"));
	}

	[Test]
	public void ReportsEmptyLibrariesAndBoardMedians()
	{
		var results = new List<GameResult>
		{
			Game(-1, GameEndReason.TurnLimitReached),
			Game(1, GameEndReason.Damage),
		};
		var boards = new List<BoardSnapshot>
		{
			new(5, 5, 0, 4, 3, 3, ["Wall of Frost"], []),
			new(20, 0, 9, 9, 2, 2, [], []),
		};

		var text = Report(results, boards);

		Assert.That(text, Does.Contain("Someone decked at draw: 100.0%"));
		Assert.That(text, Does.Contain("Permanents"));
	}

	[Test]
	public void SaysNothingIsWrongWhenThereAreNoDraws()
	{
		var text = Report([Game(1, GameEndReason.Damage)], []);
		Assert.That(text, Does.Contain("No draws."));
	}
}
