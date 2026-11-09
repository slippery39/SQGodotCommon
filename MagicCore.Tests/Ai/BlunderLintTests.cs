using MagicCore.Ai;
using NUnit.Framework;

namespace MagicCore.Tests.Ai;

public class BlunderLintTests
{
	// AI-17.7: the lint plays whole games and reports every class, even at zero. The checkers' own test is a run
	// with an older build's switch (`beam+nolanddrop`), which must bring pass-with-land back - see the story.
	[Test]
	public void TheLint_PlaysGames_AndReportsEveryClass()
	{
		var (report, _) = BlunderLint.Run(3, 1, "greedy");

		foreach (
			var c in new[]
			{
				BlunderLint.PassedWithLand,
				BlunderLint.AttackIntoBigger,
				BlunderLint.WastedRemoval,
				BlunderLint.FaceBurn,
				BlunderLint.FlashOnOwnTurn,
			}
		)
			Assert.That(report, Does.Contain($"| {c} |"));
	}
}
