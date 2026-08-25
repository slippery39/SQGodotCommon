using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// The default evaluator, and the home of the terminal constants the search compares against.
///
/// The scoring itself lives in <see cref="WeightedStateEvaluator"/> — this is a static wrapper
/// over <see cref="WeightedStateEvaluator.Default"/>, kept because roughly 25 call sites across
/// StateEvaluatorTests, AiLandDropTests and LandDiscardCostTests score states directly and none of
/// them care which instance did it.
///
/// **WinScore, LossScore and WinThreshold stay here rather than on IStateEvaluator.** They are
/// compared against by the search, not produced by an evaluator, and making them per-instance
/// would mean two evaluators in one harness run could disagree about what winning is.
/// </summary>
public static class StateEvaluator
{
	public const float WinScore = 10000f;
	public const float LossScore = -10000f;

	/// <summary>
	/// The bar a score must clear to mean "someone won", for scores that have been through a
	/// rollout rather than come straight out of <see cref="Evaluate"/>.
	///
	/// **Never compare a rollout result against <see cref="WinScore"/> directly.**
	/// <c>MultiTurnBeamSearchAiStrategy.DiscountTerminal</c> decays terminals by how many
	/// half-turns they took, so a real win comes back as 9500 or 9025, and <c>>= 10000</c> is
	/// false for every win the search will ever find. That regression shipped: <c>FindWinner</c>
	/// silently stopped returning winners and both <c>ResolveChoice</c> early-outs stopped firing,
	/// which cost 8.8% of run time and, far worse, stopped the AI taking a winning line the moment
	/// it found one.
	///
	/// 2500 sits far above any board score the weights can produce (~150 at the extreme) and far
	/// below the most-decayed win at any sane lookahead (0.95^20 ≈ 3585), so it separates the two
	/// populations with room on both sides.
	/// </summary>
	public const float WinThreshold = WinScore * 0.25f;

	/// <summary>True if this score — raw or discounted — means the player won.</summary>
	public static bool IsWin(float score) => score >= WinThreshold;

	/// <summary>True if this score — raw or discounted — means the game ended either way.</summary>
	public static bool IsDecisive(float score) => MathF.Abs(score) >= WinThreshold;

	/// <summary>
	/// The hot path — one line over <see cref="WeightedStateEvaluator.Explain"/>, so there is
	/// exactly one copy of the weighted sum and a display path cannot drift from the scoring path.
	///
	/// **This delegation was measured, and the measurement is worth knowing about, because the
	/// first three attempts said the opposite.** Delegating appeared to cost +11.8% wall time over
	/// 300 games; sharing only the battlefield walk still showed +8.0%; restoring the original body
	/// byte-for-byte showed +8.8%. Against a +3% budget that read as damning, and the sum was
	/// duplicated to work around it.
	///
	/// All three numbers were measuring a different bug. The terminal discount had broken every
	/// win-detection comparison in the search (see <see cref="WinThreshold"/>), so the beam stopped
	/// short-circuiting on a found win and burned its entire rollout budget on every move. With
	/// that fixed, delegation costs **~0%** (57.75s vs 58.78s over three interleaved rounds).
	///
	/// The tell was <c>Avg actions/game</c>, not the clock: actions went DOWN while time went UP,
	/// which is impossible for a per-call cost and pointed straight at the search doing more work
	/// per decision. **Wall time alone would have shipped duplicated evaluator logic to work around
	/// a bug two commits earlier** — the third time in this project a clock reading has accused the
	/// wrong code, after the stale-binary trap and the parallel-batch draw disaster.
	///
	/// If you measure this again, read the action count beside the time.
	/// </summary>
	public static float Evaluate(GameState state, MtgGameIds ids, int playerId) =>
		WeightedStateEvaluator.Default.Evaluate(state, ids, playerId);

	/// <summary>
	/// The default evaluator's score split into its terms — for the AI inspector and the scenario
	/// viewer. When a custom evaluator drives the search, call <see cref="IStateEvaluator.Explain"/>
	/// on that one instead, or the panel describes arithmetic nobody used.
	/// </summary>
	public static EvaluationBreakdown Explain(GameState state, MtgGameIds ids, int playerId) =>
		WeightedStateEvaluator.Default.Explain(state, ids, playerId);
}
