using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Exhausts each target creature — this engine's equivalent of "tap target creature".
///
/// An exhausted creature cannot attack and cannot activate abilities that require tapping.
/// StartTurnAction clears the flag for the active player only, so exhausting a creature during
/// your own turn costs its controller exactly one attack, the same way tapping a creature down
/// on your turn does in real MTG.
///
/// Emits CreatureExhaustedEvent into PendingGameEvents (not just the Events log) so tapper
/// payoffs such as Gideon's Avenger actually fire.
/// </summary>
public record ExhaustCreatureAction : EffectAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		foreach (var targetId in ResolveTargetIds())
		{
			if (!state.HasObject(targetId))
				continue;

			if (state.GetObject(targetId) is not Card card)
				continue;

			var creature = card.GetComponent<CreatureComponent>();
			if (creature == null)
				continue;

			// Already exhausted — no state change, and no second event for payoffs to
			// double-count.
			if (creature.IsExhausted)
				continue;

			state = state.UpdateObject(
				targetId,
				card.WithComponentReplaced(creature with { IsExhausted = true })
			);

			var exhaustedEvent = new CreatureExhaustedEvent { CreatureId = targetId };
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(exhaustedEvent) };
			events = events.Add(exhaustedEvent);
		}

		return new ActionResult(state) { Events = events };
	}
}
