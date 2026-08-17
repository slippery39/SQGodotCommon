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
	/// <summary>
	/// Extra untap steps the target must sit out — "doesn't untap during its controller's next
	/// untap step" is 1. Zero is a plain tapper that untaps as normal next turn.
	/// </summary>
	public int FreezeTurns { get; init; } = 0;

	/// <summary>
	/// When true the freeze lasts as long as the effect's source stays on the battlefield
	/// (Dungeon Geists), rather than for a fixed number of turns.
	/// </summary>
	public bool FreezeWhileSourceRemains { get; init; } = false;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;
		var sourceId = FreezeWhileSourceRemains ? GetInput<int>(ContextKeys.SourceCardId, 0) : 0;

		foreach (var targetId in ResolveTargetIds())
		{
			if (!state.HasObject(targetId))
				continue;

			if (state.GetObject(targetId) is not Card card)
				continue;

			var creature = card.GetComponent<CreatureComponent>();
			if (creature == null)
				continue;

			// Already exhausted AND already frozen at least this long — nothing to add, and no
			// second event for payoffs to double-count. A longer freeze on an exhausted creature
			// still applies, which is why this is not a bare IsExhausted check.
			if (
				creature.IsExhausted
				&& creature.FrozenTurns >= FreezeTurns
				&& (sourceId == 0 || creature.FrozenBySourceId != 0)
			)
				continue;

			state = state.UpdateObject(
				targetId,
				card.WithComponentReplaced(
					creature with
					{
						IsExhausted = true,
						FrozenTurns = Math.Max(creature.FrozenTurns, FreezeTurns),
						FrozenBySourceId = sourceId != 0 ? sourceId : creature.FrozenBySourceId,
					}
				)
			);

			var exhaustedEvent = new CreatureExhaustedEvent { CreatureId = targetId };
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(exhaustedEvent) };
			events = events.Add(exhaustedEvent);
		}

		return new ActionResult(state) { Events = events };
	}
}
