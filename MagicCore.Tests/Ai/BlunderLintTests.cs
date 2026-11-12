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

	// AI-19's scan reads a kept TRK self-play game (the scan's first trial, seed 1): Cybernetic Specialist tapped for
	// its ability in its own combat and left tapped (S3a), and a landcycle's search answered "none" (S5).
	[Test]
	public void TheScan_ReadsAKeptGame_AndFindsItsMisplays()
	{
		var found = BlunderLint.Check(KeptRecords.Get("LintTrkSeed1")).Found;

		Assert.That(
			found,
			Has.Some.Matches<BlunderLint.Finding>(f =>
				f.Class == BlunderLint.TappedEarly
				&& f.Card == "Cybernetic Specialist"
				&& f.Move == 285
			)
		);
		Assert.That(
			found,
			Has.Some.Matches<BlunderLint.Finding>(f =>
				f.Class == BlunderLint.MayDeclined
				&& f.Card == "Free Borg Revolutionaries"
				&& f.Move == 48
			)
		);
	}
}
