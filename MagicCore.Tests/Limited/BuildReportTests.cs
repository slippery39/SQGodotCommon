using MagicCore.Limited;
using MagicCore.Sets;

namespace MagicCore.Tests.Limited;

/// <summary>The sealed-builds report (2026-11-10): play and draw apart, each card's GIH / GNS / IWD, read from the games.</summary>
public class BuildReportTests
{
	static BuildReport.Played Game(bool onPlay, double won, params string[] yours) =>
		new(1, onPlay, won, 14, yours.ToHashSet(), new HashSet<string> { "Their Bomb" });

	[Test]
	public void TheReport_SplitsPlayAndDraw_AndReadsEachCardDrawnAgainstNotDrawn()
	{
		var build = new SealedBuildMatch.Build(
			"Mine",
			[CardRegistry.Get("Targ Wrangler", "TRK"), CardRegistry.Get("Tricorder", "TRK")],
			[]
		);
		var result = new BuildReport.Result(
			[build],
			[
				[
					Game(onPlay: true, 1, "Targ Wrangler"),
					Game(onPlay: false, 0, "Tricorder"),
					Game(onPlay: true, 1, "Targ Wrangler", "Tricorder"),
					Game(onPlay: false, 1),
					null, // a stopped game: counted as stopped, nowhere else
				],
			],
			TimeSpan.Zero
		);

		var report = BuildReport.Report(result, minTheirGames: 1);

		Assert.That(
			report,
			Does.Contain(
				"| Mine | 4 | 75.0% ± 25.0 | 100.0% ± 0.0 (2) | 50.0% ± 50.0 (2) | 14.0 | 1 |"
			)
		);
		// Targ Wrangler: in hand in 2 games, both won; not in 2, one won -> IWD +50
		Assert.That(report, Does.Contain("| Targ Wrangler | 2 | 100.0% | 2 | 50.0% | +50.0 |"));
		Assert.That(report, Does.Contain("| Their Bomb | 4 | 75.0% |"));
	}
}
