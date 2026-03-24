using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Deals a fixed amount of damage to each target in TargetIds.
/// Handles both player targets (reduces life) and creature targets (adds damage markers).
///
/// Note: Moving creatures with lethal damage to the graveyard is a state-based effect
/// and will be handled by a separate system. For now it is applied here as a placeholder.
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
			var obj = state.GetObject(targetId);

			var (newState, newEvents) = obj switch
			{
				MtgPlayer player => ApplyToPlayer(state, player, Amount),
				CreatureCard creature => ApplyToCreature(state, creature, Amount),
				_ => (state, ImmutableList<GameEvent>.Empty),
			};

			state = newState;
			events = events.AddRange(newEvents);
		}

		return new ActionResult(state) { Events = events };
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

	private static (GameState, ImmutableList<GameEvent>) ApplyToCreature(
		GameState state,
		CreatureCard creature,
		int amount
	)
	{
		var newDamage = creature.Damage + amount;
		var events = ImmutableList<GameEvent>.Empty;

		if (newDamage >= creature.Toughness)
		{
			// Placeholder: state-based effects will handle this properly later
			var graveyardId = state.GetPlayerZoneId(creature.OwnerId, ZoneType.Graveyard);
			var destroyed = creature with { Damage = newDamage };
			state = state.UpdateObject(creature.Id, destroyed);
			state = state.MoveObject(creature.Id, graveyardId);
			events = events.Add(new CreatureDestroyedEvent { CreatureId = creature.Id });
		}
		else
		{
			var damaged = creature with { Damage = newDamage };
			state = state.UpdateObject(creature.Id, damaged);
			events = events.Add(
				new CreatureDamagedEvent { CreatureId = creature.Id, Amount = amount }
			);
		}

		return (state, events);
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
