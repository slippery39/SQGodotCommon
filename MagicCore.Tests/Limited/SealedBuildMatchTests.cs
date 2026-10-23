using MagicCore.Limited;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Limited;

/// <summary>Candidate sealed builds compared by sims (story LIM-1.8): read from an Arena export, every build against the same opponents.</summary>
public class SealedBuildMatchTests
{
	/// <summary>A built pool as Arena exports it: the deck, then the rest of the pool as the sideboard.</summary>
	static string Export(SealedDeck deck) =>
		string.Join(
			"\n",
			[
				"Deck",
				.. deck.Main.Select(n => $"1 {n} (FRA) 1"),
				"",
				"Sideboard",
				.. deck.Sideboard.Select(n => $"1 {n} (FRA) 1"),
			]
		);

	[Test]
	public void FromExport_TheDeckIsTheBuild_TheRestOfThePoolIsOutsideTheGame()
	{
		var deck = SealedBuilder.Opponent(5);

		var build = SealedBuildMatch.FromExport("mine", Export(deck));

		Assert.That(build.Main.Select(c => c.Data.Name), Is.EquivalentTo(deck.Main));
		Assert.That(
			build.Sideboard.Select(c => c.Data.Name),
			Is.EquivalentTo(
				deck.Sideboard.Where(n =>
					FraCards.IsImplemented(n) && !SealedBuilder.Basics.Contains(n)
				)
			),
			"basics are free, so the pool's own never reach the sideboard"
		);
	}

	[Test]
	public void TheSameBuildTwice_PlaysTheSameGames_SoBuildsDifferOnlyByTheirCards()
	{
		var build = SealedBuildMatch.FromExport("a", Export(SealedBuilder.Opponent(5)));

		var result = SealedBuildMatch.Run([build, build with { Name = "b" }], 3, "greedy");

		Assert.That(result.Games[0].Select(g => g.Seed), Is.EqualTo(new[] { 1, 2, 3 }));
		Assert.That(
			result.Games[1].Select(g => (g.Winner, g.Turns)),
			Is.EqualTo(result.Games[0].Select(g => (g.Winner, g.Turns)))
		);
		Assert.That(result.Difference(0, 1).Mean, Is.Zero);
	}
}
