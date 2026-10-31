using MagicCore.Limited;
using MagicCore.Sets;

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
		var deck = SealedBuilder.Opponent("FRA", 5);

		var build = SealedBuildMatch.FromExport("mine", Export(deck));

		Assert.That(build.Main.Select(c => c.Data.Name), Is.EquivalentTo(deck.Main));
		Assert.That(
			build.Sideboard.Select(c => c.Data.Name),
			Is.EquivalentTo(
				deck.Sideboard.Where(n =>
					CardRegistry.IsImplemented(n) && !SealedBuilder.Basics.Contains(n)
				)
			),
			"basics are free, so the pool's own never reach the sideboard"
		);
	}

	[Test]
	public void TheSameBuildTwice_PlaysTheSameGames_SoBuildsDifferOnlyByTheirCards()
	{
		var build = SealedBuildMatch.FromExport("a", Export(SealedBuilder.Opponent("FRA", 5)));

		var result = SealedBuildMatch.Run([build, build with { Name = "b" }], 3, "greedy");

		Assert.That(result.Games[0].Select(g => g.Seed), Is.EqualTo(new[] { 1, 2, 3 }));
		Assert.That(
			result.Games[1].Select(g => (g.Winner, g.Turns)),
			Is.EqualTo(result.Games[0].Select(g => (g.Winner, g.Turns)))
		);
		Assert.That(result.Difference(0, 1).Mean, Is.Zero);
		Assert.That(result.Report(), Does.Contain("within 2 SE: no difference shown"));
	}

	// LIM-3.4: a builder's build as a candidate — named by its button, its pool's other playable cards
	// outside the game, never a basic (they're free).
	[Test]
	public void FromSuggestion_IsTheBuild_NamedByItsButton()
	{
		var deck = SealedBuilder.Suggestions(Boosters.SealedPool("FRA", 3), CardRatings.Of("FRA"))[
			0
		];
		var build = SealedBuildMatch.FromSuggestion(deck);

		Assert.That(build.Name, Is.EqualTo(deck.Button));
		Assert.That(build.Main.Select(c => c.Data.Name), Is.EqualTo(deck.Main));
		Assert.That(
			build.Sideboard.Select(c => c.Data.Name),
			Is.EquivalentTo(
				deck.Sideboard.Where(n =>
					CardRegistry.IsImplemented(n) && !SealedBuilder.Basics.Contains(n)
				)
			)
		);
	}

	// The best's greedy and a close pair's greedy can share a button name; the report needs one each.
	[Test]
	public void FromSuggestions_NumbersARepeatedName()
	{
		var deck = SealedBuilder.Suggestions(Boosters.SealedPool("FRA", 3), CardRatings.Of("FRA"))[
			0
		];
		var names = SealedBuildMatch.FromSuggestions([deck, deck, deck]).Select(b => b.Name);

		Assert.That(
			names,
			Is.EqualTo(new[] { deck.Button, $"{deck.Button} #2", $"{deck.Button} #3" })
		);
	}
}
