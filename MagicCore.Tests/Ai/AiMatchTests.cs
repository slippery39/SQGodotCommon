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
		// The counters the AI-1.9 land-drop finding rests on: a mirror plays alike from both seats.
		Assert.That(
			result.Games.Sum(g => g.LandsA),
			Is.EqualTo(result.Games.Sum(g => g.LandsB)).And.GreaterThan(0)
		);
		Assert.That(
			result.Games.Sum(g => g.SpellsA),
			Is.EqualTo(result.Games.Sum(g => g.SpellsB)).And.GreaterThan(0)
		);
		// The cost count (AI-6): deterministic, so a mirror costs alike from both seats.
		Assert.That(
			result.Games.Sum(g => g.StepsA),
			Is.EqualTo(result.Games.Sum(g => g.StepsB)).And.GreaterThan(0)
		);
	}

	[Test]
	public void AHeldFraction_InTheName_IsTheSearchAtThatFraction()
	{
		Assert.That(AiMatch.Create("beam-held=0.25", 1, 1), Is.TypeOf<BeamSearchAi>());
		Assert.That(
			new[]
			{
				"beam-held=NaN",
				"beam-held=Infinity",
				"beam-held=-1",
				"beam-held=x",
				"beam-held=",
			}.Where(AiMatch.IsKnown),
			Is.Empty
		);
	}

	[Test]
	public void AnUnknownAi_IsRefusedBeforeAnyGame() =>
		Assert.That(
			() => AiMatch.Run("greedy", "Greedy", games: 12),
			Throws.ArgumentException.With.Message.Contains("known: greedy")
		);
}
