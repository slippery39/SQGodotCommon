using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Scores a game state from one player's perspective. Higher is better for that player.
///
/// Exists so two scoring configurations can play each other inside one process — see
/// <c>StrengthHarness</c>. Rebuilding an old commit to get an opponent is not a harness, and an
/// evaluation change that cannot be played against its own absence ships on argument.
///
/// **Terminal states must return exactly <see cref="StateEvaluator.WinScore"/> /
/// <see cref="StateEvaluator.LossScore"/>.** Those constants stay on the static class rather than
/// the interface because the search compares against them directly, and a decayed variant of them
/// has already broken win detection once — see <see cref="StateEvaluator.WinThreshold"/>.
///
/// <see cref="Explain"/> is on the interface, not just the concrete type, so the inspector shows
/// the terms of whichever evaluator is actually running. Reading them off the static default while
/// a different evaluator drives the search would be a panel that quietly describes someone else's
/// arithmetic.
/// </summary>
public interface IStateEvaluator
{
	float Evaluate(GameState state, MtgGameIds ids, int playerId);

	EvaluationBreakdown Explain(GameState state, MtgGameIds ids, int playerId);
}
