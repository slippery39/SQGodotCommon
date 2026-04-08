using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Deals a fixed amount of damage to each target in TargetIds.
/// Handles both player targets (reduces life) and creature targets (adds damage markers).
///
/// When a creature takes lethal damage it is moved to the graveyard and a
/// CreatureDestroyedEvent is appended to GameState.PendingGameEvents for
/// the PostActionProcessor to evaluate triggered abilities after the scope closes.
/// </summary>
public record DealDamageAction : GameAction, ITargetedAction
{
	public int Amount { get; init; }
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

			var (newState, newEvents) = obj switch
			{
				MtgPlayer player => ApplyToPlayer(state, player, Amount),
				Card card when card.HasComponent<CreatureComponent>() => ApplyToCreature(
					state,
					card,
					Amount
				),
				_ => (state, ImmutableList<GameEvent>.Empty),
			};

			state = newState;
			events = events.AddRange(newEvents);
		}

		return new ActionResult(state) { Events = events };
	}

	private static (GameState, ImmutableList<GameEvent>) ApplyToCreature(
		GameState state,
		Card card,
		int amount
	)
	{
		var creature = card.GetComponent<CreatureComponent>()!;
		var newDamage = creature.Damage + amount;
		var events = ImmutableList<GameEvent>.Empty;

		if (newDamage >= creature.Toughness)
		{
			var graveyardId = state.GetPlayerZoneId(card.OwnerId, ZoneType.Graveyard);
			state = state.UpdateObject(
				card.Id,
				card.WithComponentReplaced(creature with { Damage = newDamage })
			);
			state = state.MoveObject(card.Id, graveyardId);

			var destroyedEvent = new CreatureDestroyedEvent { CreatureId = card.Id };
			events = events.Add(destroyedEvent);

			// Stage for trigger evaluation after the resolution scope closes
			state = state with
			{
				PendingGameEvents = state.PendingGameEvents.Add(destroyedEvent),
			};
		}
		else
		{
			state = state.UpdateObject(
				card.Id,
				card.WithComponentReplaced(creature with { Damage = newDamage })
			);
			events = events.Add(new CreatureDamagedEvent { CreatureId = card.Id, Amount = amount });
		}

		return (state, events);
	}

	private static (GameState, ImmutableList<GameEvent>) ApplyToPlayer(
		GameState state,
		MtgPlayer player,
		int amount
	)
	{
		var updated = player with { Life = player.Life - amount };
		var newState = state.UpdateObject(player.Id, updated);
		var events = ImmutableList.Create<GameEvent>(
			new PlayerDamagedEvent { PlayerId = player.Id, Amount = amount }
		);
		return (newState, events);
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
