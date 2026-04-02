using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Resolves a creature attacking either an opponent player or one of their creatures.
///
/// The attacker must be on the attacking player's battlefield, must not have
/// summoning sickness, and must not have already attacked this turn.
///
/// When attacking a player, only the attacker deals damage.
/// When attacking a creature, both deal their power simultaneously.
/// Creatures with lethal damage are moved to their owner's graveyard.
///
/// State-based effects (win/loss) are handled automatically by
/// GameState.PostActionProcessor after this action resolves.
/// </summary>
public record AttackAction : GameAction
{
	public int AttackerId { get; init; }
	public int TargetId { get; init; }
	public int AttackingPlayerId { get; init; }

	public override ValidationResult ValidateAdd(GameState gameState)
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

		if (creature.HasSummoningSickness)
			return ValidationResult.Invalid("Creature has summoning sickness and cannot attack");

		if (creature.HasAttacked)
			return ValidationResult.Invalid("Creature has already attacked this turn");

		var attackerZone = gameState.GetCardZone(AttackerId);
		if (attackerZone.ZoneType != ZoneType.Battlefield)
			return ValidationResult.Invalid("Attacker is not on the battlefield");

		if (!gameState.HasObject(TargetId))
			return ValidationResult.Invalid($"Target {TargetId} does not exist");

		var targetObj = gameState.GetObject(TargetId);

		if (targetObj is MtgPlayer targetPlayer)
		{
			if (targetPlayer.Id == AttackingPlayerId)
				return ValidationResult.Invalid("Cannot attack yourself");
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
		}
		else
		{
			return ValidationResult.Invalid("Target must be a player or creature");
		}

		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState gameState)
	{
		var attacker = (Card)gameState.GetObject(AttackerId);
		var attackerCreature = attacker.GetComponent<CreatureComponent>()!;
		var targetObj = gameState.GetObject(TargetId);

		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		// Mark attacker as having attacked this turn
		var updatedAttacker = attacker.WithComponentReplaced(
			attackerCreature with
			{
				HasAttacked = true,
			}
		);
		state = state.UpdateObject(AttackerId, updatedAttacker);

		if (targetObj is MtgPlayer targetPlayer)
		{
			var (newState, newEvents) = ApplyDamageToPlayer(
				state,
				targetPlayer,
				attackerCreature.Power
			);
			state = newState;
			events = events.AddRange(newEvents);
		}
		else if (targetObj is Card targetCard)
		{
			var (stateAfterTargetDamage, targetEvents) = ApplyDamageToCreature(
				state,
				targetCard,
				attackerCreature.Power
			);
			state = stateAfterTargetDamage;
			events = events.AddRange(targetEvents);

			// Target retaliates — re-fetch attacker in case it was already destroyed
			var targetCreature = targetCard.GetComponent<CreatureComponent>()!;
			var currentAttacker = state.HasObject(AttackerId)
				? (Card)state.GetObject(AttackerId)
				: null;

			if (currentAttacker != null)
			{
				var (stateAfterAttackerDamage, attackerEvents) = ApplyDamageToCreature(
					state,
					currentAttacker,
					targetCreature.Power
				);
				state = stateAfterAttackerDamage;
				events = events.AddRange(attackerEvents);
			}
		}

		return new ActionResult(state) { Events = events };
	}

	private static (GameState, ImmutableList<GameEvent>) ApplyDamageToPlayer(
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

	private static (GameState, ImmutableList<GameEvent>) ApplyDamageToCreature(
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
			var updatedCreature = creature with { Damage = newDamage };
			var updatedCard = card.WithComponentReplaced(updatedCreature);
			state = state.UpdateObject(card.Id, updatedCard);
			state = state.MoveObject(card.Id, graveyardId);
			events = events.Add(new CreatureDestroyedEvent { CreatureId = card.Id });
		}
		else
		{
			var updatedCreature = creature with { Damage = newDamage };
			var updatedCard = card.WithComponentReplaced(updatedCreature);
			state = state.UpdateObject(card.Id, updatedCard);
			events = events.Add(new CreatureDamagedEvent { CreatureId = card.Id, Amount = amount });
		}

		return (state, events);
	}
}
