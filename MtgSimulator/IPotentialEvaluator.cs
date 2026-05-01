using ImmutableGameObjects;

namespace MtgSimulator;

/// <summary>
/// Scores a game state for a secondary "potential" signal used during beam pruning.
///
/// Unlike StateEvaluator (concrete board advantage), potential evaluators capture
/// setup value that the main evaluator deliberately ignores — e.g. fast mana, graveyard
/// quality, storm count. States that score well here are preserved in the beam even
/// when their concrete score is low, giving combo lines a chance to reach their payoff.
///
/// SlotCount controls how many candidates this evaluator reserves in each beam level.
/// </summary>
public interface IPotentialEvaluator
{
	float Score(GameState state, int playerId);
	int SlotCount { get; }
}
