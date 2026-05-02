using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Generates all legal actions for the active player in the current game state.
///
/// This is the single shared source of legal action generation used by both
/// the console and the simulator. Never duplicate this logic in presentation layers.
///
/// Performance notes:
///   - Hand is scanned once, categorizing each card in a single pass
///   - Zone IDs are resolved once at the top and reused throughout
///   - TryAddAction is still called for creature/spell actions since their
///     ValidateAdd checks mana and targeting which are non-trivial to inline
///   - Attack validation is inlined since the checks are simple flag reads
/// </summary>
public static class MtgActionGenerator
{
	public static List<GameAction> GetLegalActions(GameState state, MtgGameIds ids, int playerId)
	{
		var actions = new List<GameAction>();
		var opponentId = playerId == ids.Player1Id ? ids.Player2Id : ids.Player1Id;

		var handId = playerId == ids.Player1Id ? ids.Player1HandId : ids.Player2HandId;
		var battlefieldId =
			playerId == ids.Player1Id ? ids.Player1BattlefieldId : ids.Player2BattlefieldId;
		var opponentBattlefieldId =
			playerId == ids.Player1Id ? ids.Player2BattlefieldId : ids.Player1BattlefieldId;

		AddHandActions(state, playerId, handId, actions);
		AddAttackActions(
			state,
			playerId,
			opponentId,
			battlefieldId,
			opponentBattlefieldId,
			actions
		);
		AddAbilityActions(state, playerId, battlefieldId, actions);

		return actions;
	}

	// ===== HAND =====

	private static void AddHandActions(
		GameState state,
		int playerId,
		int handId,
		List<GameAction> actions
	)
	{
		foreach (var card in state.GetCardsInZone(handId))
		{
			var costPayments = BuildAdditionalCostPayments(
				state,
				playerId,
				card.Id,
				card.AdditionalCastCosts
			);
			if (costPayments == null)
				continue;

			if (card.HasComponent<CreatureComponent>())
			{
				AddCreatureAction(state, playerId, card, costPayments, actions);
			}
			else if (card.HasComponent<PermanentComponent>())
			{
				AddPermanentAction(state, playerId, card, costPayments, actions);
			}
			else
			{
				AddSpellAction(state, playerId, card, costPayments, actions);
			}
		}
	}

	private static void AddCreatureAction(
		GameState state,
		int playerId,
		Card card,
		ImmutableDictionary<int, ImmutableList<int>> costPayments,
		List<GameAction> actions
	)
	{
		var action = new CastCreatureAction
		{
			CardId = card.Id,
			CastingPlayerId = playerId,
			AdditionalCostPayments = costPayments,
		};
		if (state.TryAddAction(action).Success)
			actions.Add(action);
	}

	private static void AddPermanentAction(
		GameState state,
		int playerId,
		Card card,
		ImmutableDictionary<int, ImmutableList<int>> costPayments,
		List<GameAction> actions
	)
	{
		var action = new CastPermanentAction
		{
			CardId = card.Id,
			CastingPlayerId = playerId,
			AdditionalCostPayments = costPayments,
		};
		if (state.TryAddAction(action).Success)
			actions.Add(action);
	}

	private static void AddSpellAction(
		GameState state,
		int playerId,
		Card card,
		ImmutableDictionary<int, ImmutableList<int>> costPayments,
		List<GameAction> actions
	)
	{
		var spell = card.GetComponent<SpellComponent>();
		if (spell == null)
			return;

		var targetedEffect = spell.Effects.FirstOrDefault(e =>
			e.TargetingStrategy.RequiresUserSelection
		);
		if (targetedEffect != null)
		{
			AddTargetedSpellAction(state, playerId, card, costPayments, targetedEffect, actions);
		}
		else
		{
			var castAction = new CastSpellAction
			{
				CardId = card.Id,
				CastingPlayerId = playerId,
				AdditionalCostPayments = costPayments,
				TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty,
			};
			if (state.TryAddAction(castAction).Success)
				actions.Add(castAction);
		}
	}

	private static void AddTargetedSpellAction(
		GameState state,
		int playerId,
		Card card,
		ImmutableDictionary<int, ImmutableList<int>> costPayments,
		CardEffect targetedEffect,
		List<GameAction> actions
	)
	{
		var context = new TargetingContext
		{
			GameState = state,
			SourceCardId = card.Id,
			CastingPlayerId = playerId,
		};
		var validTargets = targetedEffect.TargetingStrategy.GetValidTargets(context);

		foreach (var target in validTargets)
		{
			var castAction = new CastSpellAction
			{
				CardId = card.Id,
				CastingPlayerId = playerId,
				AdditionalCostPayments = costPayments,
				TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
					0,
					ImmutableList.Create(target)
				),
			};
			if (state.TryAddAction(castAction).Success)
			{
				actions.Add(castAction);
			}
		}
	}

	// ===== ATTACKS =====

	private static void AddAttackActions(
		GameState state,
		int playerId,
		int opponentId,
		int battlefieldId,
		int opponentBattlefieldId,
		List<GameAction> actions
	)
	{
		var attackTargets = state
			.GetCardsInZone(opponentBattlefieldId)
			.Where(c => c.HasComponent<CreatureComponent>())
			.Select(c => c.Id)
			.Prepend(opponentId)
			.ToList();

		foreach (var attacker in state.GetCardsInZone(battlefieldId))
		{
			var creature = attacker.GetComponent<CreatureComponent>();
			if (creature == null || creature.HasAttacked)
				continue;
			if (creature.HasSummoningSickness && !state.GetEffectiveHaste(attacker.Id))
				continue;

			foreach (var targetId in attackTargets)
			{
				var attack = new AttackAction
				{
					AttackerId = attacker.Id,
					TargetId = targetId,
					AttackingPlayerId = playerId,
				};
				if (state.TryAddAction(attack).Success)
					actions.Add(attack);
			}
		}
	}

	// ===== ACTIVATED ABILITIES =====

	private static void AddAbilityActions(
		GameState state,
		int playerId,
		int battlefieldId,
		List<GameAction> actions
	)
	{
		foreach (var card in state.GetCardsInZone(battlefieldId))
		{
			var abilities = card.GetComponents<ActivatedAbilityComponent>().ToList();
			for (int i = 0; i < abilities.Count; i++)
			{
				if (abilities[i].HasActivated)
					continue;

				var costPayments = BuildAdditionalCostPayments(
					state,
					playerId,
					card.Id,
					abilities[i].AdditionalCosts
				);
				if (costPayments == null)
					continue;

				var abilityAction = BuildAbilityAction(
					state,
					playerId,
					card.Id,
					i,
					abilities[i],
					costPayments
				);
				if (abilityAction != null && state.TryAddAction(abilityAction).Success)
					actions.Add(abilityAction);
			}
		}
	}

	private static ActivateAbilityAction? BuildAbilityAction(
		GameState state,
		int playerId,
		int cardId,
		int abilityIndex,
		ActivatedAbilityComponent ability,
		ImmutableDictionary<int, ImmutableList<int>> costPayments
	)
	{
		var needsTarget = ability.Effect.TargetingStrategy.RequiresUserSelection;
		if (needsTarget)
		{
			var context = new TargetingContext
			{
				GameState = state,
				SourceCardId = cardId,
				CastingPlayerId = playerId,
			};
			var validTargets = ability.Effect.TargetingStrategy.GetValidTargets(context);
			if (validTargets.Count == 0)
				return null;

			return new ActivateAbilityAction
			{
				CardId = cardId,
				ActivatingPlayerId = playerId,
				AbilityIndex = abilityIndex,
				AdditionalCostPayments = costPayments,
				TargetIds = ImmutableList.Create(validTargets[0]),
			};
		}

		return new ActivateAbilityAction
		{
			CardId = cardId,
			ActivatingPlayerId = playerId,
			AbilityIndex = abilityIndex,
			AdditionalCostPayments = costPayments,
			TargetIds = ImmutableList<int>.Empty,
		};
	}

	// ===== ADDITIONAL COST HELPERS =====

	/// <summary>
	/// Builds the AdditionalCostPayments dictionary for a cast/activate action.
	/// Returns null if any selection cost has no valid payments (card cannot be played).
	/// Picks the first valid payment for each selection cost — sufficient for AI use.
	/// </summary>
	private static ImmutableDictionary<int, ImmutableList<int>>? BuildAdditionalCostPayments(
		GameState state,
		int playerId,
		int sourceCardId,
		ImmutableList<AdditionalCost> costs
	)
	{
		var payments = ImmutableDictionary<int, ImmutableList<int>>.Empty;
		for (int i = 0; i < costs.Count; i++)
		{
			if (!costs[i].RequiresSelection)
				continue;

			var validPayments = costs[i].GetValidPayments(state, playerId, sourceCardId);
			if (validPayments.IsEmpty)
				return null;

			payments = payments.Add(i, ImmutableList.Create(validPayments[0]));
		}
		return payments;
	}
}
