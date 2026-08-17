using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Destroys each target creature outright, ignoring damage and toughness.
///
/// Indestructible creatures are skipped — "destroy" is exactly what indestructible answers.
/// Damage-based death is decided separately in CreatureEvaluator.IsLethalDamage, which checks
/// indestructible too, so the keyword cannot work against one and silently not the other.
/// </summary>
public record DestroyCreatureAction : EffectAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		foreach (var targetId in ResolveTargetIds())
		{
			if (!state.HasObject(targetId))
				continue;

			var obj = state.GetObject(targetId);
			if (obj is not Card card || !card.HasComponent<CreatureComponent>())
				continue;

			if (state.GetEffectiveIndestructible(card.Id))
				continue;

			var leftEvent = new PermanentLeftBattlefieldEvent
			{
				CardId = card.Id,
				OwnerId = card.OwnerId,
			};
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(leftEvent) };

			var graveyardId = state.GetPlayerZoneId(card.OwnerId, ZoneType.Graveyard);
			state = state.MoveCardTracked(card.Id, graveyardId);

			var destroyedEvent = new CreatureDestroyedEvent { CreatureId = card.Id };
			events = events.Add(destroyedEvent);
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(destroyedEvent) };
		}

		return new ActionResult(state) { Events = events };
	}
}
