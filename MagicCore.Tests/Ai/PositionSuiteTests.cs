using MagicCore.Ai;

namespace MagicCore.Tests.Ai;

/// <summary>The quick check (story AI-6.4): recorded positions, replayed, decided again, compared.</summary>
public class PositionSuiteTests
{
	// A small suite of GreedyAi's own games: fast, and the same machinery as the search's.
	static readonly Lazy<PositionSuite.Suite> Greedy =
		new(() => PositionSuite.Generate("greedy", seeds: 1, perGame: 3));

	// The tracked suite still replays: an engine change that reorders legal moves, or changes what a
	// position offers, fails here — regenerate it (the message says how).
	[Test]
	public void TheTrackedSuite_StillReplays()
	{
		var tracked = PositionSuite.Tracked();

		Assert.That(
			tracked,
			Is.Not.Null,
			$"no {PositionSuite.FilePath} — {PositionSuite.GenerateCommand}"
		);
		Assert.That(PositionSuite.States(tracked!), Has.Count.EqualTo(tracked!.Positions.Count));
	}

	[Test]
	public void ASuite_ReadsBackAsWritten()
	{
		var text = PositionSuite.ToText(Greedy.Value);
		Assert.That(PositionSuite.ToText(PositionSuite.Parse(text)), Is.EqualTo(text));
		Assert.That(Greedy.Value.Positions, Has.Count.GreaterThan(0));
	}

	// The AI that answered changes nothing — the recording replays exactly — and another AI changes
	// some: a check that can't see a difference measures nothing.
	[Test]
	public void ItsOwnAnswers_ChangeNothing_AndAnotherAiChangesSome()
	{
		Assert.That(PositionSuite.Run(Greedy.Value, "greedy").Changed, Is.Empty);
		Assert.That(PositionSuite.Run(Greedy.Value, "random").Changed, Is.Not.Empty);
	}

	[Test]
	public void AMoveThatNoLongerApplies_IsStale()
	{
		var game = Greedy.Value.Games[0];
		var broken = Greedy.Value with
		{
			Games =
			[
				game with
				{
					Moves = ["a999", .. game.Moves.Skip(1)],
				},
				.. Greedy.Value.Games.Skip(1),
			],
		};
		Assert.Throws<PositionSuite.StaleException>(() => PositionSuite.States(broken));
	}

	[Test]
	public void APositionThatOffersSomethingElse_IsStale()
	{
		var p = Greedy.Value.Positions[0];
		var broken = Greedy.Value with
		{
			Positions = [p with { Offers = "00000000" }, .. Greedy.Value.Positions.Skip(1)],
		};
		Assert.Throws<PositionSuite.StaleException>(() => PositionSuite.States(broken));
	}

	// A divergence that leaves every recorded position's offer alike still changes how the game ends.
	[Test]
	public void AGameThatEndsDifferently_IsStale()
	{
		var game = Greedy.Value.Games[0];
		var broken = Greedy.Value with
		{
			Games = [game with { End = "00000000" }, .. Greedy.Value.Games.Skip(1)],
		};
		Assert.Throws<PositionSuite.StaleException>(() => PositionSuite.States(broken));
	}

	[Test]
	public void AGameWithNoMoves_ReadsBackWithNone()
	{
		var empty = new PositionSuite.Suite(
			"greedy",
			[new("White-Black", "Red-Green", 1, "x", [])],
			[]
		);
		Assert.That(PositionSuite.Parse(PositionSuite.ToText(empty)).Games[0].Moves, Is.Empty);
	}

	// ===== The oracle (AI-6.4b), cheap here: the machinery, not its strength =====

	static readonly Lazy<PositionSuite.Suite> WithOracle =
		new(
			() =>
				PositionSuite.WithOracle(
					Greedy.Value,
					new BeamSearchAi.Settings(
						Depth: 2,
						BeamWidth: 3,
						MaxBranching: 64,
						ExpandBranching: 4,
						RolloutBudget: 40,
						CommitPlans: false,
						Threads: 1
					)
				)
		);

	// What it scores is each first move it searched, its answer among them — so "score given up" can
	// be read for any version's answer, the oracle's own scoring nothing.
	[Test]
	public void TheOracle_ScoresItsOwnAnswer_AmongEveryFirstMove()
	{
		var readings = WithOracle.Value.Oracle!;

		Assert.That(readings, Has.Count.EqualTo(Greedy.Value.Positions.Count));
		Assert.That(readings.Count(r => r.Scores.Count >= 2), Is.GreaterThan(0), "it searched");
		Assert.That(
			readings.Where(r => r.Scores.Count > 0).All(r => r.Scores.ContainsKey(r.Best)),
			"its answer is among the scored first moves"
		);
	}

	[Test]
	public void TheOracle_ReadsBackAsWritten()
	{
		var text = PositionSuite.ToText(WithOracle.Value);
		Assert.That(PositionSuite.ToText(PositionSuite.Parse(text)), Is.EqualTo(text));
	}

	[Test]
	public void ACheck_ReportsAgreementAndScoreGivenUp()
	{
		var verdict = PositionSuite.Run(WithOracle.Value, "greedy").Oracle!;

		Assert.That(verdict.Positions, Is.EqualTo(Greedy.Value.Positions.Count));
		Assert.That(verdict.Scored, Is.GreaterThan(0));
	}

	// The judging on hand-built readings: given up measured from the oracle's own answer and never
	// below 0, a choice's ids in any order the same answer, decided games counted apart.
	[Test]
	public void Judging_MeasuresFromTheOraclesAnswer_AndCountsDecidedGamesApart()
	{
		static PositionSuite.OracleReading At(
			int i,
			string best,
			params (string, double)[] scores
		) => new(i, best, scores.ToDictionary(x => x.Item1, x => x.Item2));
		PositionSuite.OracleReading[] oracle =
		[
			At(0, "a0", ("a0", 10), ("a1", 7)), // gives up 3
			At(1, "a0", ("a0", 10), ("a1", 12)), // scores above the oracle's own answer: 0, not -2
			At(2, "a0", ("a0", 9_000), ("a1", 5)), // a win it misses
			At(3, "a0", ("a0", 5), ("a1", -9_000)), // a loss it walks into
			At(4, "c1,2", ("c1,2", 4)), // the same ids in another order: agrees, gives up 0
		];

		var verdict = PositionSuite.Judge(
			oracle,
			["a1", "a1", "a1", "a1", "c2,1"],
			["a0", "a0", "a0", "a0", "c1,2"]
		);

		Assert.That(
			(verdict.Agree, verdict.CommittedAgree, verdict.Scored, verdict.MeanGivenUp),
			Is.EqualTo((1, 5, 3, 1.0))
		);
		Assert.That(
			(verdict.MissedWins, verdict.WalkedIntoLosses, verdict.Unscored),
			Is.EqualTo((1, 1, 0))
		);
	}

	[Test]
	public void AnOracleThatDoesntMatchThePositions_IsStale()
	{
		var missingOne = WithOracle.Value with { Oracle = [.. WithOracle.Value.Oracle!.Skip(1)] };
		Assert.Throws<PositionSuite.StaleException>(() => PositionSuite.Run(missingOne, "greedy"));
	}
}
