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

	// Parallel games (each search on one thread) and one game at a time (each search on every core)
	// play the very same games: the same winners, turns, decisions — and the same cost count. Only the
	// clock differs, which is why strength may come from a parallel run (AI-6.2).
	[Test, Explicit("plays 24 games with the search, twice (~2 min)")]
	public void ParallelGames_PlayTheSameGames_AsOneAtATime()
	{
		// Every field but the clock's — a field added to Game is compared without anyone listing it.
		static AiMatch.Game Played(AiMatch.Game g) =>
			g with
			{
				SecondsA = 0,
				SecondsB = 0,
				SlowestA = 0,
				SlowestB = 0,
			};

		var parallel = AiMatch.Run("beam", "greedy", games: 12);
		var sequential = AiMatch.Run("beam", "greedy", games: 12, sequential: true);

		Assert.That(parallel.Games.Select(Played), Is.EqualTo(sequential.Games.Select(Played)));
		Assert.That(parallel.Games.Sum(g => g.RolloutsA), Is.GreaterThan(0), "the search searched");
		TestContext.Out.WriteLine(
			$"parallel {parallel.Elapsed.TotalSeconds:0} s, sequential {sequential.Elapsed.TotalSeconds:0} s"
		);
	}

	// What makes parallel games fill the cores: every kind of search name takes the thread count, and
	// keeps its own settings beside it.
	[TestCase("beam")]
	[TestCase("beam-plain")]
	[TestCase("beam-casts")]
	[TestCase("beam+shuffle")]
	[TestCase("beam+responds+guess")]
	[TestCase("beam+greedyrollout")]
	[TestCase("beam-held=0.25")]
	public void EverySearch_TakesTheThreadCount(string name)
	{
		var one = (BeamSearchAi)AiMatch.Create(name, 1, 1, searchThreads: 1);
		var every = (BeamSearchAi)AiMatch.Create(name, 1, 1);

		Assert.That((one.Options.Threads, every.Options.Threads), Is.EqualTo((1, (int?)null)));
		// Each search builds its own held-values table: compare the rest, and whether it has one.
		static object Rest(BeamSearchAi.Settings o) =>
			(
				o with
				{
					Threads = null,
					RolloutSettings = null,
				},
				o.RolloutSettings! with
				{
					Held = null,
				},
				o.RolloutSettings!.Held is null
			);
		Assert.That(Rest(one.Options), Is.EqualTo(Rest(every.Options)));
	}

	// The models combine only where each means something: a hand mode needs an opponent that casts from
	// its hand, and one opponent model and one hand mode at a time (AI-3.4–3.6).
	[TestCase("beam+casts+unseen", true)]
	[TestCase("beam+responds+guess", true)]
	[TestCase("beam+unseen", false)]
	[TestCase("beam+guess", false)]
	[TestCase("beam+casts+responds", false)]
	[TestCase("beam+casts+unseen+guess", false)]
	public void TheModels_CombineOnlyWhereTheyMeanSomething(string name, bool known)
	{
		Assert.That(AiMatch.IsKnown(name), Is.EqualTo(known));
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
