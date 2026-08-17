using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Creates one token per point of the source creature's effective power, plus an offset.
///
/// This is how "X = the number of +1/+1 counters on this creature" is answered without a counter
/// subsystem (Chasm Skulker). A permanent AddModifierAction IS this engine's +1/+1 counter, and a
/// creature that starts at 1/1 and grows by +1/+1 per counter has power == 1 + counters — so
/// power minus one is the counter count exactly, with nothing new to maintain.
///
/// Reads power BEFORE the creature left the battlefield where possible; on a death trigger the
/// card is already in the graveyard but its modifiers travel with it, so the count still holds.
/// </summary>
public record CreateTokensPerPowerAction : GameAction
{
	public Card? CardTemplate { get; init; }

	/// <summary>Added to the measured power. -1 for a 1/1 base whose counters start at zero.</summary>
	public int Offset { get; init; } = 0;

	public override ActionResult Execute(GameState gameState)
	{
		if (CardTemplate == null)
			return new ActionResult(gameState);

		var sourceId = GetInput<int>(ContextKeys.SourceCardId, 0);
		var controllerId = GetInput<int>(ContextKeys.CastingPlayerId, 0);

		if (sourceId == 0 || controllerId == 0)
			return new ActionResult(gameState);

		var count = gameState.GetEffectivePower(sourceId) + Offset;
		if (count <= 0)
			return new ActionResult(gameState);

		return new ActionResult(
			gameState.SpawnAction(
				new CreateCardAction
				{
					CardTemplate = CardTemplate,
					ControllerId = controllerId,
					Count = count,
				}
			)
		);
	}
}
