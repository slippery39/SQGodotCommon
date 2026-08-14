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
	/// <param name="deduplicateAttackers">
	/// Collapses strategically-identical attackers to one representative action. Correct for AI
	/// search, where it prevents exponential blow-up with many identical tokens; wrong for a
	/// human, who needs every creature to be individually attackable. Presentation layers pass
	/// false.
	/// </param>
	public static List<GameAction> GetLegalActions(
		GameState state,
		MtgGameIds ids,
		int playerId,
		bool deduplicateAttackers = true
	)
	{
		var actions = new List<GameAction>();
		var opponentId = playerId == ids.Player1Id ? ids.Player2Id : ids.Player1Id;

		var handId = playerId == ids.Player1Id ? ids.Player1HandId : ids.Player2HandId;
		var graveyardId =
			playerId == ids.Player1Id ? ids.Player1GraveyardId : ids.Player2GraveyardId;
		var battlefieldId =
			playerId == ids.Player1Id ? ids.Player1BattlefieldId : ids.Player2BattlefieldId;
		var opponentBattlefieldId =
			playerId == ids.Player1Id ? ids.Player2BattlefieldId : ids.Player1BattlefieldId;

		AddHandActions(state, playerId, handId, actions);
		AddGraveyardFlashbackActions(state, playerId, graveyardId, actions);
		AddAttackActions(
			state,
			playerId,
			opponentId,
			battlefieldId,
			opponentBattlefieldId,
			actions,
			deduplicateAttackers
		);
		AddAbilityActions(state, playerId, battlefieldId, actions);
		actions.Add(
			new EndTurnAction
			{
				GameId = ids.GameId,
				Player1Id = ids.Player1Id,
				Player2Id = ids.Player2Id,
			}
		);

		return actions;
	}

	/// <summary>
	/// Generates all legal actions for the given player using well-known IDs from GameState,
	/// without needing the obsolete MtgGameIds struct.
	/// </summary>
	/// <param name="deduplicateAttackers">See the overload above — pass false for a human UI.</param>
	public static List<GameAction> GetLegalActions(
		GameState state,
		int playerId,
		bool deduplicateAttackers = true
	)
	{
		var player1Id = state.GetWellKnownId(MtgObjectKeys.Player1);
		var player2Id = state.GetWellKnownId(MtgObjectKeys.Player2);
		var isPlayer1 = playerId == player1Id;
		var opponentId = isPlayer1 ? player2Id : player1Id;

		var handId = state.GetWellKnownId(
			isPlayer1 ? MtgObjectKeys.Player1Hand : MtgObjectKeys.Player2Hand
		);
		var battlefieldId = state.GetWellKnownId(
			isPlayer1 ? MtgObjectKeys.Player1Battlefield : MtgObjectKeys.Player2Battlefield
		);
		var opponentBattlefieldId = state.GetWellKnownId(
			isPlayer1 ? MtgObjectKeys.Player2Battlefield : MtgObjectKeys.Player1Battlefield
		);
		var gameId = state.GetWellKnownId(MtgObjectKeys.Game);

		var graveyardId = state.GetWellKnownId(
			isPlayer1 ? MtgObjectKeys.Player1Graveyard : MtgObjectKeys.Player2Graveyard
		);

		var actions = new List<GameAction>();
		AddHandActions(state, playerId, handId, actions);
		AddGraveyardFlashbackActions(state, playerId, graveyardId, actions);
		AddAttackActions(
			state,
			playerId,
			opponentId,
			battlefieldId,
			opponentBattlefieldId,
			actions,
			deduplicateAttackers
		);
		AddAbilityActions(state, playerId, battlefieldId, actions);
		actions.Add(
			new EndTurnAction
			{
				GameId = gameId,
				Player1Id = player1Id,
				Player2Id = player2Id,
			}
		);
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
			// Lands bypass the cost system — no mana cost, no additional costs
			if (card.HasSubtype("Land"))
			{
				AddLandAction(state, playerId, card, actions);
				continue;
			}

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

	private static void AddLandAction(
		GameState state,
		int playerId,
		Card card,
		List<GameAction> actions
	)
	{
		var action = new PlayLandAction { CardId = card.Id, CastingPlayerId = playerId };
		if (state.TryAddAction(action).Success)
			actions.Add(action);
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

	// ===== GRAVEYARD (FLASHBACK) =====

	private static void AddGraveyardFlashbackActions(
		GameState state,
		int playerId,
		int graveyardId,
		List<GameAction> actions
	)
	{
		foreach (var card in state.GetCardsInZone(graveyardId))
		{
			if (!card.HasComponent<FlashbackComponent>())
				continue;

			var spell = card.GetComponent<SpellComponent>();

			// Creatures with FlashbackComponent are graveyard recursion (Gravecrawler).
			// They carry no SpellComponent and so have no targets to enumerate.
			if (spell == null)
			{
				if (!card.HasComponent<CreatureComponent>())
					continue;

				var recurAction = new CastFromGraveyardAction
				{
					CardId = card.Id,
					CastingPlayerId = playerId,
					TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty,
				};
				if (state.TryAddAction(recurAction).Success)
					actions.Add(recurAction);
				continue;
			}

			var targetedEffect = spell.Effects.FirstOrDefault(e =>
				e.TargetingStrategy.RequiresUserSelection
			);
			if (targetedEffect != null)
			{
				AddTargetedFlashbackAction(state, playerId, card, targetedEffect, actions);
			}
			else
			{
				var castAction = new CastFromGraveyardAction
				{
					CardId = card.Id,
					CastingPlayerId = playerId,
					TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty,
				};
				if (state.TryAddAction(castAction).Success)
					actions.Add(castAction);
			}
		}
	}

	private static void AddTargetedFlashbackAction(
		GameState state,
		int playerId,
		Card card,
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
			var castAction = new CastFromGraveyardAction
			{
				CardId = card.Id,
				CastingPlayerId = playerId,
				TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
					0,
					ImmutableList.Create(target)
				),
			};
			if (state.TryAddAction(castAction).Success)
				actions.Add(castAction);
		}
	}

	// ===== ATTACKS =====

	private static void AddAttackActions(
		GameState state,
		int playerId,
		int opponentId,
		int battlefieldId,
		int opponentBattlefieldId,
		List<GameAction> actions,
		bool deduplicateAttackers
	)
	{
		var attackTargets = state
			.GetCardsInZone(opponentBattlefieldId)
			.Where(c => c.HasComponent<CreatureComponent>())
			.Select(c => c.Id)
			.Prepend(opponentId)
			.ToList();

		// Deduplicate by (target, attacker signature): two creatures with the same name,
		// effective P/T, current damage, and combat-relevant abilities produce identical
		// game outcomes when attacking the same target, so only one representative is needed.
		//
		// This is an AI search optimisation and must be OFF for a human: it suppresses the
		// duplicate's actions entirely, so a player holding two copies of the same creature
		// finds the second one simply unclickable.
		var seen = deduplicateAttackers
			? new HashSet<(int targetId, AttackerSignature sig)>()
			: null;

		foreach (var attacker in state.GetCardsInZone(battlefieldId))
		{
			var creature = attacker.GetComponent<CreatureComponent>();
			if (creature == null || creature.HasAttacked)
				continue;
			if (creature.HasSummoningSickness && !state.GetEffectiveHaste(attacker.Id))
				continue;

			var stats = state.GetEffectiveStats(attacker.Id);
			var sig = new AttackerSignature(
				attacker.Name,
				stats.Power,
				stats.Toughness,
				creature.Damage,
				stats.HasFlying,
				stats.HasTrample,
				creature.HasDoubleStrike,
				stats.HasLifelink
			);

			foreach (var targetId in attackTargets)
			{
				if (seen != null && !seen.Add((targetId, sig)))
					continue;

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
				var ab = abilities[i];
				if (ab.MaxActivationsPerTurn > 0 && ab.ActivationCount >= ab.MaxActivationsPerTurn)
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
		var needsTarget = ability.TargetedEffect?.TargetingStrategy.RequiresUserSelection == true;
		if (needsTarget)
		{
			var context = new TargetingContext
			{
				GameState = state,
				SourceCardId = cardId,
				CastingPlayerId = playerId,
			};
			var validTargets = ability.TargetedEffect!.TargetingStrategy.GetValidTargets(context);
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

/// <summary>
/// Identifies a unique attacker profile for deduplication in AddAttackActions.
/// Two creatures with the same signature produce identical outcomes when attacking
/// the same target, so only one representative action is generated per (target, sig) pair.
/// </summary>
file record struct AttackerSignature(
	string Name,
	int Power,
	int Toughness,
	int Damage,
	bool HasFlying,
	bool HasTrample,
	bool HasDoubleStrike,
	bool HasLifelink
);
