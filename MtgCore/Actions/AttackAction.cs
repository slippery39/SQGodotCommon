using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Resolves a creature attacking either an opponent player or one of their creatures.
///
/// Writes to GameState.PendingGameEvents:
///   - CreatureAttackedEvent when the attack is declared
///   - CreatureDestroyedEvent for any creature that dies from combat damage
///
/// These events are consumed by CheckStateBasedEffectsAction after resolution
/// to evaluate triggered abilities.
/// </summary>
public record AttackAction : GameAction
{
	public int AttackerId { get; init; }
	public int TargetId { get; init; }
	public int AttackingPlayerId { get; init; }

	public override ValidationResult ValidateAdd(GameState gameState)
	{
		var attackerResult = ValidateAttacker(gameState);
		if (!attackerResult.IsValid)
			return attackerResult;

		return ValidateTarget(gameState);
	}

	private ValidationResult ValidateAttacker(GameState gameState)
	{
		if (!gameState.HasObject(AttackerId))
			return ValidationResult.Invalid($"Attacker {AttackerId} does not exist");

		var attacker = gameState.GetObject(AttackerId) as Card;
		if (attacker == null)
			return ValidationResult.Invalid("Attacker is not a card");

		if (attacker.ControllerId != AttackingPlayerId)
			return ValidationResult.Invalid("You do not control the attacker");

		if (!attacker.HasComponent<CreatureComponent>())
			return ValidationResult.Invalid("Attacker is not a creature");

		var creature = attacker.GetComponent<CreatureComponent>()!;

		if (creature.HasSummoningSickness && !gameState.GetEffectiveHaste(AttackerId))
			return ValidationResult.Invalid("Creature has summoning sickness and cannot attack");

		if (creature.HasAttacked)
			return ValidationResult.Invalid("Creature has already attacked this turn");

		var attackerZone = gameState.GetCardZone(AttackerId);
		if (attackerZone.ZoneType != ZoneType.Battlefield)
			return ValidationResult.Invalid("Attacker is not on the battlefield");

		return ValidationResult.Valid;
	}

	private ValidationResult ValidateTarget(GameState gameState)
	{
		if (!gameState.HasObject(TargetId))
			return ValidationResult.Invalid($"Target {TargetId} does not exist");

		var targetObj = gameState.GetObject(TargetId);
		int defendingPlayerId;

		if (targetObj is MtgPlayer targetPlayer)
		{
			if (targetPlayer.Id == AttackingPlayerId)
				return ValidationResult.Invalid("Cannot attack yourself");
			defendingPlayerId = targetPlayer.Id;
		}
		else if (targetObj is Card targetCard)
		{
			if (!targetCard.HasComponent<CreatureComponent>())
				return ValidationResult.Invalid("Target card is not a creature");

			var targetZone = gameState.GetCardZone(TargetId);
			if (targetZone.ZoneType != ZoneType.Battlefield)
				return ValidationResult.Invalid("Target creature is not on the battlefield");

			if (targetCard.ControllerId == AttackingPlayerId)
				return ValidationResult.Invalid("Cannot attack your own creature");

			defendingPlayerId = targetCard.ControllerId;
		}
		else
		{
			return ValidationResult.Invalid("Target must be a player or creature");
		}

		return ValidateTauntConstraint(gameState, defendingPlayerId);
	}

	/// <summary>
	/// Enforces the Taunt rule: if the defending player has Taunt creatures, the attacker
	/// must attack one of them — unless the attacker has Flying and none of the Taunt
	/// creatures have Flying or Reach (in which case the aerial attacker flies over).
	/// </summary>
	private ValidationResult ValidateTauntConstraint(GameState gameState, int defendingPlayerId)
	{
		var opponentBattlefieldId = gameState.GetPlayerZoneId(
			defendingPlayerId,
			ZoneType.Battlefield
		);

		// Single pass: collect only taunt creatures — avoids allocating stats for non-taunt creatures
		// and eliminates the three LINQ chains (Where+Select+ToList, two Where+Select+ToHashSet).
		List<(int Id, CreatureStats Stats)>? tauntCreatures = null;
		foreach (var c in gameState.GetCardsInZone(opponentBattlefieldId))
		{
			if (!c.HasComponent<CreatureComponent>())
				continue;
			var stats = gameState.GetEffectiveStats(c.Id);
			if (!stats.HasTaunt)
				continue;
			tauntCreatures ??= [];
			tauntCreatures.Add((c.Id, stats));
		}

		if (tauntCreatures is null)
			return ValidationResult.Valid;

		var attackerStats = gameState.GetEffectiveStats(AttackerId);
		if (attackerStats.HasFlying)
		{
			var hasObstructingTaunt = false;
			var targetIsObstructing = false;
			foreach (var (id, stats) in tauntCreatures)
			{
				if (stats.HasFlying || stats.HasReach)
				{
					hasObstructingTaunt = true;
					if (id == TargetId)
						targetIsObstructing = true;
				}
			}

			if (!hasObstructingTaunt)
				return ValidationResult.Valid;
			if (targetIsObstructing)
				return ValidationResult.Valid;
			return ValidationResult.Invalid(
				"A Taunt creature with Flying or Reach must be attacked first"
			);
		}

		foreach (var (id, _) in tauntCreatures)
			if (id == TargetId)
				return ValidationResult.Valid;

		return ValidationResult.Invalid("A Taunt creature must be attacked first");
	}

	public override ActionResult Execute(GameState gameState)
	{
		var attacker = (Card)gameState.GetObject(AttackerId);
		var attackerCreature = attacker.GetComponent<CreatureComponent>()!;
		var targetObj = gameState.GetObject(TargetId);
		var strikeCount = attackerCreature.HasDoubleStrike ? 2 : 1;

		var state = gameState.UpdateObject(
			AttackerId,
			attacker.WithComponentReplaced(attackerCreature with { HasAttacked = true })
		);

		var attackedEvent = new CreatureAttackedEvent
		{
			CreatureId = AttackerId,
			AttackingPlayerId = AttackingPlayerId,
		};
		state = state with { PendingGameEvents = state.PendingGameEvents.Add(attackedEvent) };
		var events = ImmutableList.Create<GameEvent>(attackedEvent);

		var attackerStats = state.GetEffectiveStats(AttackerId);
		var power = attackerStats.Power;

		if (targetObj is MtgPlayer targetPlayer)
		{
			var totalDamage = power * strikeCount;
			var (s, e) = ApplyDamageToPlayerStrikes(state, targetPlayer.Id, power, strikeCount);
			state = s;
			events = events.AddRange(e);

			if (attackerStats.HasLifelink)
				state = state.SpawnAction(
					new GainLifeAction
					{
						Amount = totalDamage,
						TargetIds = ImmutableList.Create(AttackingPlayerId),
					}
				);

			return new ActionResult(state) { Events = events };
		}

		if (targetObj is Card targetCard)
		{
			var targetStats = state.GetEffectiveStats(targetCard.Id);

			var (s, e) = ApplyCreatureVsCreature(state, targetCard, power, strikeCount);
			state = s;
			events = events.AddRange(e);

			if (attackerStats.HasLifelink)
				state = state.SpawnAction(
					new GainLifeAction
					{
						Amount = power * strikeCount,
						TargetIds = ImmutableList.Create(AttackingPlayerId),
					}
				);

			if (targetStats.HasLifelink)
				state = state.SpawnAction(
					new GainLifeAction
					{
						Amount = targetStats.Power,
						TargetIds = ImmutableList.Create(targetCard.ControllerId),
					}
				);

			if (attackerStats.HasTrample)
			{
				var excess = Math.Max(0, power * strikeCount - targetStats.Toughness);
				if (excess > 0)
				{
					var (s2, e2) = ApplyDamageToPlayer(
						state,
						AttackerId,
						(MtgPlayer)state.GetObject(targetCard.ControllerId),
						excess
					);
					state = s2;
					events = events.AddRange(e2);
				}
			}

			return new ActionResult(state) { Events = events };
		}

		return new ActionResult(state) { Events = events };
	}

	private (GameState, ImmutableList<GameEvent>) ApplyDamageToPlayerStrikes(
		GameState state,
		int targetPlayerId,
		int power,
		int strikeCount
	)
	{
		var events = ImmutableList<GameEvent>.Empty;
		for (int i = 0; i < strikeCount; i++)
		{
			if (!state.HasObject(targetPlayerId))
				break;
			var (newState, newEvents) = ApplyDamageToPlayer(
				state,
				AttackerId,
				(MtgPlayer)state.GetObject(targetPlayerId),
				power
			);
			state = newState;
			events = events.AddRange(newEvents);
		}
		return (state, events);
	}

	private (GameState, ImmutableList<GameEvent>) ApplyCreatureVsCreature(
		GameState state,
		Card targetCard,
		int power,
		int strikeCount
	)
	{
		var (state2, targetEvents) = ApplyDamageToCreature(state, targetCard, power * strikeCount);
		var events = targetEvents;

		var currentAttacker = state2.HasObject(AttackerId)
			? (Card)state2.GetObject(AttackerId)
			: null;

		if (currentAttacker == null)
			return (state2, events);

		var (state3, attackerEvents) = ApplyDamageToCreature(
			state2,
			currentAttacker,
			state2.GetEffectivePower(targetCard.Id)
		);
		return (state3, events.AddRange(attackerEvents));
	}

	private static (GameState, ImmutableList<GameEvent>) ApplyDamageToPlayer(
		GameState state,
		int attackerId,
		MtgPlayer player,
		int amount
	)
	{
		var updated = player with { Life = player.Life - amount };
		var newState = state.UpdateObject(player.Id, updated);

		var combatEvent = new CombatDamageDealtToPlayerEvent
		{
			AttackerId = attackerId,
			DefendingPlayerId = player.Id,
			Amount = amount,
		};
		newState = newState with
		{
			PendingGameEvents = newState.PendingGameEvents.Add(combatEvent),
		};

		var events = ImmutableList.Create<GameEvent>(
			new PlayerDamagedEvent { PlayerId = player.Id, Amount = amount },
			combatEvent
		);
		return (newState, events);
	}

	private static (GameState, ImmutableList<GameEvent>) ApplyDamageToCreature(
		GameState state,
		Card card,
		int amount
	)
	{
		var creature = card.GetComponent<CreatureComponent>()!;
		var newDamage = creature.Damage + amount;
		var events = ImmutableList<GameEvent>.Empty;

		// Use effective toughness — accounts for modifiers
		var updatedCard = card.WithComponentReplaced(creature with { Damage = newDamage });
		state = state.UpdateObject(card.Id, updatedCard);

		if (state.HasLethalDamage(card.Id))
		{
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
		else
		{
			events = events.Add(new CreatureDamagedEvent { CreatureId = card.Id, Amount = amount });
		}

		return (state, events);
	}
}
