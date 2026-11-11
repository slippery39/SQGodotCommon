using MagicCore.Limited;

namespace MagicCore.Tests.Limited;

/// <summary>DATA-2.6: one ratings table's AI sealed decks against another's, pools in pairs built on both.</summary>
public class RatingsMatchTests
{
	[Test]
	public void TwoTablesThatBuildTheSameDecks_StopWithAReason_NeverLoop()
	{
		Assert.That(
			() => RatingsMatch.Run("TRK", CardRatings.Of("TRK"), 2, "greedy"),
			Throws.InvalidOperationException.With.Message.Contains("same decks")
		);
	}

	[Test]
	public void OnePair_OrAnUnknownAi_IsRefusedBeforeAnyGame()
	{
		Assert.That(
			() => RatingsMatch.Run("TRK", CardRatings.Of("TRK"), 1, "greedy"),
			Throws.ArgumentException
		);
		Assert.That(
			() => RatingsMatch.Run("TRK", CardRatings.Of("TRK"), 2, "bean"),
			Throws.ArgumentException
		);
	}

	[Test]
	public void APairPlaysTwoGames_TheNewTablesScoreBetweenNothingAndAll()
	{
		var round = SimulatedRatings.Rate(
			SimulatedRatings.Measure("TRK", 2, firstSeed: 90001),
			SimulatedRatings.Rateable("TRK")
		);
		var result = RatingsMatch.Run("TRK", round, 2, "greedy");
		Assert.That(result.NewScores, Has.Count.EqualTo(2));
		Assert.That(result.NewScores, Is.All.InRange(0.0, 1.0));
		Assert.That(result.Report("new", "old"), Does.Contain("over 2 pairs (4 games)"));
	}
}
