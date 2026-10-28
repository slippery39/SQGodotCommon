using MagicCore.Ai;

namespace MagicCore.Tests.Ai;

/// <summary>Evaluator.Explain is the score term by term — never a second copy that drifts (the decision report reads it).</summary>
public class EvaluatorExplainTests
{
	[Test]
	public void Explain_SumsToEvaluate_AtEveryRecordedPosition()
	{
		var suite = PositionSuite.Tracked()!;
		var held = BeamSearchAi.DefaultRollout.Held;
		foreach (var (_, s) in PositionSuite.States(suite))
		foreach (var player in new[] { s.DecidingPlayerId(), s.OpponentOf(s.DecidingPlayerId()) })
			Assert.That(
				Evaluator.Explain(s, player, held).Sum(t => t.Value),
				Is.EqualTo(Evaluator.Evaluate(s, player, held)).Within(1e-9)
			);
	}
}
