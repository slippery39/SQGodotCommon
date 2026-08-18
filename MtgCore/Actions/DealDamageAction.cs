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
public record DealDamageAction : EffectAction
{
	public int Amount { get; init; }
	public string PlayerOutputKey { get; init; } = "";
	public string CreatureOutputKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;
		var amount = ResolveAmount(Amount);
		var damagedPlayers = ImmutableList<int>.Empty;
		var damagedCreatures = ImmutableList<int>.Empty;

		foreach (var targetId in ResolveTargetIds())
		{
			if (!state.HasObject(targetId))
				continue;

			var obj = state.GetObject(targetId);

			var (newState, newEvents) = obj switch
			{
				MtgPlayer player => ApplyToPlayer(state, player, amount),
				Card card when card.HasComponent<CreatureComponent>() => ApplyToCreature(
					state,
					card,
					amount
				),
				// Loyalty absorbs the damage, exactly as it does in combat. Without this arm a
				// planeswalker fell through to the discard case and silently took nothing —
				// no error, no event, the walker simply shrugged off every burn spell.
				// The walker's death is left to CheckStateBasedEffectsAction's zero-loyalty
				// pass, which is the same route combat damage takes.
				Card walker when walker.HasComponent<PlaneswalkerComponent>() => (
					state.DamagePlaneswalker(walker.Id, amount),
					ImmutableList<GameEvent>.Empty
				),
				_ => (state, ImmutableList<GameEvent>.Empty),
			};

			state = newState;
			events = events.AddRange(newEvents);

			if (obj is MtgPlayer)
				damagedPlayers = damagedPlayers.Add(targetId);
			else if (obj is Card c && c.HasComponent<CreatureComponent>())
				damagedCreatures = damagedCreatures.Add(targetId);
		}

		var result = new ActionResult(state) { Events = events };
		if (!string.IsNullOrEmpty(PlayerOutputKey))
			result = result.WithOutput(PlayerOutputKey, damagedPlayers);
		if (!string.IsNullOrEmpty(CreatureOutputKey))
			result = result.WithOutput(CreatureOutputKey, damagedCreatures);
		return result;
	}

	private (GameState, ImmutableList<GameEvent>) ApplyToCreature(
		GameState state,
		Card card,
		int rawAmount
	)
	{
		// Protection from the source's creature type prevents the damage entirely.
		var sourceId = GetInput<int>(ContextKeys.SourceCardId, 0);
		if (state.IsProtectedFrom(card.Id, sourceId))
			return (state, ImmutableList<GameEvent>.Empty);

		var amount = state.ApplyReplacements(
			ReplaceableEvent.DamageToCreature,
			card.ControllerId,
			rawAmount
		);
		if (amount <= 0)
			return (state, ImmutableList<GameEvent>.Empty);

		var creature = card.GetComponent<CreatureComponent>()!;
		var newDamage = creature.Damage + amount;
		var events = ImmutableList<GameEvent>.Empty;

		// Effect damage has no deathtouch source today — combat is the only deathtouch path.
		if (state.IsLethalDamage(card.Id, newDamage, fromDeathtouch: false))
		{
			var leftEvent = new PermanentLeftBattlefieldEvent
			{
				CardId = card.Id,
				OwnerId = card.OwnerId,
			};
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(leftEvent) };

			var graveyardId = state.GetPlayerZoneId(card.OwnerId, ZoneType.Graveyard);
			state = state.UpdateObject(
				card.Id,
				card.WithComponentReplaced(creature with { Damage = newDamage })
			);
			state = state.MoveCardTracked(card.Id, graveyardId);

			var destroyedEvent = new CreatureDestroyedEvent { CreatureId = card.Id };
			events = events.Add(destroyedEvent);
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(destroyedEvent) };
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
		int rawAmount
	)
	{
		// Damage prevention lives here, on the DAMAGED player's own permanents.
		var amount = state.ApplyReplacements(ReplaceableEvent.DamageToPlayer, player.Id, rawAmount);
		if (amount <= 0)
			return (state, ImmutableList<GameEvent>.Empty);

		var updated = player with
		{
			Life = player.Life - amount,
			LifeLostThisTurn = player.LifeLostThisTurn + amount,
		};
		var newState = state.UpdateObject(player.Id, updated);
		var events = ImmutableList.Create<GameEvent>(
			new PlayerDamagedEvent { PlayerId = player.Id, Amount = amount }
		);
		return (newState, events);
	}
}
