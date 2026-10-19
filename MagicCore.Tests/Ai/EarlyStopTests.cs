using MagicCore.Ai;

namespace MagicCore.Tests.Ai;

/// <summary>When a match has said enough (story AI-6.3) — the verdict on pair scores, then real runs.</summary>
public class EarlyStopTests
{
	static List<double> Repeat(int times, params double[] pattern) =>
		[.. Enumerable.Repeat(pattern, times).SelectMany(p => p)];

	[TestCase(1.0, EarlyStop.Verdict.Better)]
	[TestCase(0.0, EarlyStop.Verdict.Worse)]
	[TestCase(0.5, EarlyStop.Verdict.NoDifference)] // every pair split: an AI against itself
	public void APlainRun_IsJudgedAtTheFirstChance(double everyPair, EarlyStop.Verdict verdict)
	{
		Assert.That(
			EarlyStop.Judge(Repeat(EarlyStop.MinPairs, everyPair)).Verdict,
			Is.EqualTo(verdict)
		);
	}

	[Test]
	public void BeforeTheMinimum_NothingIsDecided()
	{
		Assert.That(
			EarlyStop.Judge(Repeat(EarlyStop.MinPairs - 1, 1.0)).Verdict,
			Is.EqualTo(EarlyStop.Verdict.Undecided)
		);
	}

	// Wins and losses alike, no draws: even, and with enough pairs that is settled.
	[Test]
	public void AnEvenMatch_IsUndecidedAtFirst_ThenNoDifference()
	{
		Assert.That(
			EarlyStop.Judge(Repeat(EarlyStop.MinPairs / 2, 1, 0)).Verdict,
			Is.EqualTo(EarlyStop.Verdict.Undecided)
		);
		Assert.That(
			EarlyStop.Judge(Repeat(600, 1, 0)).Verdict,
			Is.EqualTo(EarlyStop.Verdict.NoDifference)
		);
	}

	// A 60% side — twice the margin — is found better well before the cap.
	[Test]
	public void ATenPointEdge_IsFoundBetter()
	{
		Assert.That(
			EarlyStop.Judge(Repeat(60, 1, 1, 1, 0, 0)).Verdict,
			Is.EqualTo(EarlyStop.Verdict.Better)
		);
	}

	// A direction once ruled out stays out: left running, later noise could cross its other bound.
	[Test]
	public void ARuledOutDirection_StaysRuledOut()
	{
		var bound = EarlyStop.Judge(Repeat(EarlyStop.MinPairs, 1.0)).Bound;
		var notBetter = new EarlyStop.Reading(
			EarlyStop.Verdict.Undecided,
			-bound,
			0,
			bound,
			BetterRuledOut: true
		);

		var reading = EarlyStop.Judge(Repeat(EarlyStop.MinPairs, 1.0), notBetter);

		Assert.That(reading.Verdict, Is.Not.EqualTo(EarlyStop.Verdict.Better));
		Assert.That(reading.BetterRuledOut);
	}

	// One real early-stopped match, shared: GreedyAi against random play.
	static readonly Lazy<AiMatch.Result> GreedyVsRandom =
		new(() => AiMatch.RunUntilClear("greedy", "random"));

	static int GamesPerBatch(AiMatch.Result r) => AiMatch.SeedsPerBatch * r.Pairings * 2;

	[Test]
	public void GreedyAgainstRandom_StopsEarly_Better()
	{
		var result = GreedyVsRandom.Value;

		Assert.That(result.Stop!.Verdict, Is.EqualTo(EarlyStop.Verdict.Better), result.Report());
		Assert.That(result.Games, Has.Count.LessThan(600), "it stopped before the cap");
		Assert.That(result.Games.Count % GamesPerBatch(result), Is.Zero, "whole batches");
	}

	[Test]
	public void AnAiAgainstItself_StopsAtOnce_NoDifference()
	{
		var result = AiMatch.RunUntilClear("greedy", "greedy");

		Assert.That(
			result.Stop!.Verdict,
			Is.EqualTo(EarlyStop.Verdict.NoDifference),
			result.Report()
		);
		Assert.That(result.Games, Has.Count.EqualTo(GamesPerBatch(result)));
	}

	// Its games are the first a fixed run of that size plays — the same seeds, in order.
	[Test]
	public void AnEarlyStop_PlaysTheFixedRunsFirstGames()
	{
		static AiMatch.Game Played(AiMatch.Game g) =>
			g with
			{
				SecondsA = 0,
				SecondsB = 0,
				SlowestA = 0,
				SlowestB = 0,
			};
		var early = GreedyVsRandom.Value;
		var fixedRun = AiMatch.Run("greedy", "random", games: early.Games.Count);

		Assert.That(early.Games.Select(Played), Is.EqualTo(fixedRun.Games.Select(Played)));
	}
}
