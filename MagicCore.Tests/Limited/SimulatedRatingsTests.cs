using MagicCore.Limited;

namespace MagicCore.Tests.Limited;

/// <summary>DATA-2: a set's cards rated by their games-in-hand win rate in AI-vs-AI sealed games.</summary>
public class SimulatedRatingsTests
{
	[Test]
	public void ACardShortOfMinGames_IsPulledToTheMean_OneWithEnough_IsItsOwnRate()
	{
		var n = CardRatings.MinGames;
		var rated = SimulatedRatings.Rate(
			new Dictionary<string, SimulatedRatings.Tally>
			{
				["seen often"] = new(n, 0.7 * n),
				["seen once"] = new(1, 1),
				["the rest"] = new(3 * n, 1.2 * n),
			}
		);
		var mean = (0.7 * n + 1 + 1.2 * n) / (4 * n + 1);
		Assert.That(rated["seen often"], Is.EqualTo(0.7).Within(1e-12));
		Assert.That(rated["seen once"], Is.EqualTo((1 + mean * (n - 1)) / n).Within(1e-12));
	}

	[Test]
	public void AFewGames_TallyTheCardsDrawn_TwoPlayersAGame()
	{
		const int games = 4;
		var tallies = SimulatedRatings.Measure("FRA", games);
		Assert.That(tallies, Is.Not.Empty);
		Assert.That(tallies.Values.Select(t => t.Games), Is.All.InRange(1, 2 * games));
		// every player sees at least their opening hand's nonbasics: well over one card a game
		Assert.That(tallies.Values.Sum(t => t.Games), Is.GreaterThan(2 * games));
		Assert.That(
			tallies.Values.Sum(t => t.Wins),
			Is.LessThanOrEqualTo(tallies.Values.Sum(t => t.Games))
		);
	}
}
