using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Ai;

/// <summary>A played game's choices beside the AI's (story TBL-5.6).</summary>
public class RecordComparisonTests
{
	// One level, every first move scored, a one-turn look-ahead: cheap, and deterministic per state.
	static readonly BeamSearchAi.Settings Cheap =
		new(
			Depth: 1,
			MaxBranching: int.MaxValue,
			RolloutBudget: 24,
			CommitPlans: false,
			RolloutSettings: BeamSearchAi.DefaultRollout with
			{
				LookaheadTurns = 1,
			},
			Threads: 1
		);

	// A "you" seat that plays exactly as the oracle would must agree with it at every decision and
	// rank first — read at the position BEFORE each move, so an off-by-one or a rank that counts the
	// oracle's own pick against it fails here.
	[Test]
	public void APlayerWhoPlaysAsTheOracle_AgreesEverywhere_AndRanksFirst()
	{
		var record = GameRecord.New(
			3,
			FraDecks.All["White-Black"],
			FraDecks.All["Red-Green"],
			("You", "AI")
		);
		var s = record.Start();
		var (you, ai) = (s.Player1Id(), new GreedyAi(s.Player2Id()));
		while (!s.IsOver() && s.GetGame().TurnNumber <= 12)
		{
			var (token, kind) =
				s.LegalActions() is [PassPriorityAction pass]
					? (MoveTokens.Token(s, pass, null), "rule")
				: s.DecidingPlayerId() == you
					? (MoveTokens.Token(s, new BeamSearchAi(you, Cheap).Decide(s)), "you")
				: (MoveTokens.Token(s, ai.Decide(s)), "ai");
			record = record.Add(s, token, kind);
			s = MoveTokens.Apply(s, token);
		}

		var report = RecordComparison.Compare(record, search: Cheap, oracle: Cheap);

		Assert.That(report.Decisions, Has.Count.GreaterThan(5), "the game reached real decisions");
		Assert.Multiple(() =>
		{
			foreach (var d in report.Decisions)
			{
				Assert.That(
					MoveTokens.Same(d.You, d.Oracle.Best) && MoveTokens.Same(d.You, d.Search),
					$"#{d.Move}: you {d.YouSaid}, oracle {d.OracleSaid}, search {d.SearchSaid}"
				);
				if (d.Oracle.Scores.Count > 0) // a pass the search makes without searching scores nothing
					Assert.That(d.Rank, Is.EqualTo(1), $"#{d.Move}");
			}
			Assert.That(report.Verdict.Agree, Is.EqualTo(report.Decisions.Count));
			Assert.That(report.Decisions.Any(d => d.Differs), Is.False);
		});
	}

	// Your pick's rank among every score the oracle gave — ties share a place, and its own pick can
	// rank below a move it pruned (a4). A pick scored as well as the oracle's own, or better, is no
	// mistake; a pick it never scored has no rank.
	[TestCase("a1", 2, false)]
	[TestCase("a2", 2, true)]
	[TestCase("a3", 4, false)]
	[TestCase("a4", 1, true)]
	[TestCase("a9", null, false)]
	public void YourPick_IsRankedAmongTheOraclesScores(string you, int? rank, bool ties)
	{
		var oracle = new PositionSuite.OracleReading(
			0,
			"a1",
			new Dictionary<string, double>
			{
				["a1"] = 2.0,
				["a2"] = 2.0,
				["a3"] = 1.0,
				["a4"] = 3.0,
			}
		);
		var d = new RecordComparison.Decision(1, 1, you, "a1", oracle, "", "", "");
		Assert.Multiple(() =>
		{
			Assert.That(d.Rank, Is.EqualTo(rank));
			Assert.That(d.Ties, Is.EqualTo(ties));
		});
	}
}
