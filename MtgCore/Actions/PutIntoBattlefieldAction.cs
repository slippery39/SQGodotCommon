using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Places one or more creature cards directly onto the battlefield without paying mana.
/// Used by Goblin Lackey, Warren Instigator, and similar "put into play" effects.
///
/// Unlike PlayCreatureAction, this bypasses mana cost validation and does not emit
/// CreaturePlayedEvent. It stamps HasSummoningSickness and emits CreatureEnteredBattlefieldEvent
/// so ETB triggers fire the same way as for normally cast creatures.
/// </summary>
public record PutIntoBattlefieldAction : GameAction, ITargetedAction
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

			if (state.GetObject(targetId) is not Card card)
				continue;

			if (!card.HasComponent<CreatureComponent>())
				continue;

			var battlefieldId = state.GetPlayerZoneId(card.ControllerId, ZoneType.Battlefield);

			var creature = card.GetComponent<CreatureComponent>()!;
			var updatedCard = card.WithComponentReplaced(
				creature with
				{
					HasSummoningSickness = !creature.HasHaste,
				}
			);
			state = state.UpdateObject(targetId, updatedCard).MoveObject(targetId, battlefieldId);

			var enteredEvent = new CreatureEnteredBattlefieldEvent
			{
				CardId = targetId,
				PlayerId = card.ControllerId,
			};
			events = events.Add(enteredEvent);
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(enteredEvent) };
		}

		return new ActionResult(state) { Events = events };
	}

	public override ValidationResult ValidateResolve(GameState gameState)
	{
		foreach (var targetId in TargetIds)
		{
			if (!gameState.HasObject(targetId))
				return ValidationResult.Invalid($"Target {targetId} no longer exists");
		}
		return ValidationResult.Valid;
	}
}
