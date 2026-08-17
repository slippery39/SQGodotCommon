using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Stamps a damage-prevention replacement onto each target player for the rest of the turn —
/// Safe Passage, Harm's Way.
///
/// The component goes on the PLAYER, not a permanent: a one-shot instant has no permanent to
/// live on. ReplacementEngine.ApplyReplacements scans player components for exactly this reason,
/// and EndTurnAction strips UntilEndOfTurn ones so "this turn" really means this turn.
///
/// PreventsCreatureDamage covers the "and creatures you control" half of Safe Passage: it stamps
/// a second component for DamageToCreature, since the two are separate ReplaceableEvents.
/// </summary>
public record PreventDamageAction : EffectAction
{
	public bool PreventAll { get; init; } = true;
	public int Amount { get; init; } = 2;
	public bool PreventsCreatureDamage { get; init; } = true;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;

		foreach (var targetId in ResolveTargetIds())
		{
			if (state.GetObject(targetId) is not MtgPlayer player)
				continue;

			var added = player.Components.Add(
				new DamagePreventionComponent
				{
					Target = ReplaceableEvent.DamageToPlayer,
					PreventAll = PreventAll,
					Amount = Amount,
					Duration = ModifierDuration.UntilEndOfTurn,
				}
			);

			if (PreventsCreatureDamage)
				added = added.Add(
					new DamagePreventionComponent
					{
						Target = ReplaceableEvent.DamageToCreature,
						PreventAll = PreventAll,
						Amount = Amount,
						Duration = ModifierDuration.UntilEndOfTurn,
					}
				);

			state = state.UpdateObject(targetId, player with { Components = added });
		}

		return new ActionResult(state);
	}
}
