using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

public record DestroyCreatureAction : GameAction, ITargetedAction
{
	public ImmutableList<int> TargetIds { get; init; } = ImmutableList<int>.Empty;

	public GameAction WithTargets(ImmutableList<int> targetIds) =>
		this with
		{
			TargetIds = targetIds,
		};

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		foreach (var targetId in TargetIds)
		{
			if (!state.HasObject(targetId))
				continue;

			var obj = state.GetObject(targetId);
			if (obj is not Card card || !card.HasComponent<CreatureComponent>())
				continue;

			var leftEvent = new PermanentLeftBattlefieldEvent
			{
				CardId = card.Id,
				OwnerId = card.OwnerId,
			};
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(leftEvent) };

			var graveyardId = state.GetPlayerZoneId(card.OwnerId, ZoneType.Graveyard);
			state = state.MoveObject(card.Id, graveyardId);

			var destroyedEvent = new CreatureDestroyedEvent { CreatureId = card.Id };
			events = events.Add(destroyedEvent);
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(destroyedEvent) };
		}

		return new ActionResult(state) { Events = events };
	}
}
