using MagicCore.Ai;

namespace MagicCore.Tests.Ai;

/// <summary>
/// The strength harness's self-checks — run these before trusting any number from it. A harness with
/// a seating bias fakes an effect everywhere; one that cannot see a difference reports "no effect"
/// for everything, which is the answer most comparisons are hoping for. The second check is
/// <c>GreedyAiTests.BeatsRandomPlay_InNearlyEveryGame</c>, which runs through this harness.
/// </summary>
public class AiMatchTests
{
	// GreedyAi is deterministic and the two games of a mirror pair are the same game with the AIs'
	// names swapped — so an AI against itself scores EXACTLY half, with zero error over pairs.
	// Anything else is a bookkeeping bug: a seat credited to the wrong AI, or a mirror that isn't one.
	[Test]
	public void AnAiAgainstItself_ScoresExactlyHalf()
	{
		var result = AiMatch.Run("greedy", "greedy", games: 12);

		Assert.That(result.Games, Has.Count.EqualTo(12), result.Report());
		Assert.That(result.Games.All(g => g.Finished), result.Report());
		Assert.That(result.WinRateA, Is.EqualTo(0.5), result.Report());
		Assert.That(result.StandardError, Is.EqualTo(0), result.Report());
	}

	[Test]
	public void AnUnknownAi_IsRefusedBeforeAnyGame() =>
		Assert.That(
			() => AiMatch.Run("greedy", "Greedy", games: 12),
			Throws.ArgumentException.With.Message.Contains("known: greedy")
		);
}
