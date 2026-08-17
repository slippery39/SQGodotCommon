using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Destroys each target permanent of any type — Disenchant's "destroy target artifact or
/// enchantment".
///
/// DestroyCreatureAction only ever touched creatures, so artifact and enchantment removal had no
/// action at all. This handles every permanent, including creatures, and emits
/// CreatureDestroyedEvent only for actual creatures so death triggers stay honest.
///
/// Indestructible is respected, matching DestroyCreatureAction — the two must not disagree about
/// what "destroy" means.
/// </summary>
public record DestroyPermanentAction : EffectAction
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

			if (!card.HasComponent<PermanentComponent>() && !card.HasComponent<CreatureComponent>())
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

			// Only a creature dying is a creature death. Firing this for an artifact would make
			// every "whenever a creature dies" payoff trigger on Disenchant.
			if (card.HasComponent<CreatureComponent>())
			{
				var destroyedEvent = new CreatureDestroyedEvent { CreatureId = card.Id };
				events = events.Add(destroyedEvent);
				state = state with
				{
					PendingGameEvents = state.PendingGameEvents.Add(destroyedEvent),
				};
			}
		}

		return new ActionResult(state) { Events = events };
	}
}
