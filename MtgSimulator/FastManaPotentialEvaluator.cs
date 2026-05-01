using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Potential evaluator that scores states by the player's total current available mana.
///
/// This preserves fast-mana lines (Seething Song, Lotus Petal, etc.) in the beam even
/// though the main StateEvaluator ignores temporary mana. States with more mana in pool
/// score higher and are more likely to survive pruning, giving them the chance to reach
/// payoff spells like Dragonstorm at deeper search levels.
/// </summary>
public class FastManaPotentialEvaluator : IPotentialEvaluator
{
	public int SlotCount { get; }

	public FastManaPotentialEvaluator(int slotCount = 5)
	{
		SlotCount = slotCount;
	}

	public float Score(GameState state, int playerId) => state.GetPlayer(playerId).CurrentMana;
}
