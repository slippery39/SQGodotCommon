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

		// Resolve zone IDs once
		var handId = playerId == ids.Player1Id ? ids.Player1HandId : ids.Player2HandId;
		var battlefieldId =
			playerId == ids.Player1Id ? ids.Player1BattlefieldId : ids.Player2BattlefieldId;
		var opponentBattlefieldId =
			playerId == ids.Player1Id ? ids.Player2BattlefieldId : ids.Player1BattlefieldId;

		// ===== SINGLE PASS OVER HAND =====
		// Categorize each card once rather than iterating the hand multiple times

		foreach (var card in state.GetCardsInZone(handId))
		{
			var creature = card.GetComponent<CreatureComponent>();
			if (creature != null)
			{
				var action = new CastCreatureAction
				{
					CardId = card.Id,
					CastingPlayerId = playerId,
				};
				if (state.TryAddAction(action).Success)
					actions.Add(action);
				continue; // a card is either a creature or a spell, not both
			}

			var spell = card.GetComponent<SpellComponent>();
			if (spell == null)
				continue;

			var needsTarget = spell.Effects.Any(e => e.TargetingStrategy.RequiresUserSelection);

			if (needsTarget)
			{
				var effect = spell.Effects.First(e => e.TargetingStrategy.RequiresUserSelection);
				var context = new TargetingContext
				{
					GameState = state,
					SourceCardId = card.Id,
					CastingPlayerId = playerId,
				};
				var validTargets = effect.TargetingStrategy.GetValidTargets(context);
				if (validTargets.IsEmpty)
					continue;

				foreach (var target in validTargets)
				{
					var castAction = new CastSpellAction
					{
						CardId = card.Id,
						CastingPlayerId = playerId,
						TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
							0,
							ImmutableList.Create(target)
						),
					};
					if (state.TryAddAction(castAction).Success)
					{
						actions.Add(castAction);
						break;
					}
				}
			}
			else
			{
				var castAction = new CastSpellAction
				{
					CardId = card.Id,
					CastingPlayerId = playerId,
					TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty,
				};
				if (state.TryAddAction(castAction).Success)
					actions.Add(castAction);
			}
		}

		// ===== ATTACKS =====

		var attackTargets = new List<int> { opponentId };
		foreach (var c in state.GetCardsInZone(opponentBattlefieldId))
		{
			if (c.HasComponent<CreatureComponent>())
				attackTargets.Add(c.Id);
		}

		foreach (var attacker in state.GetCardsInZone(battlefieldId))
		{
			var creature = attacker.GetComponent<CreatureComponent>();
			if (creature == null || creature.HasSummoningSickness || creature.HasAttacked)
				continue;

			// Inline the simple checks rather than calling TryAddAction for every target
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

		// ===== ACTIVATED ABILITIES =====

		foreach (var card in state.GetCardsInZone(battlefieldId))
		{
			var abilities = card.GetComponents<ActivatedAbilityComponent>().ToList();
			for (int i = 0; i < abilities.Count; i++)
			{
				if (abilities[i].HasActivated)
					continue;

				var context = new TargetingContext
				{
					GameState = state,
					SourceCardId = card.Id,
					CastingPlayerId = playerId,
				};

				var needsTarget = abilities[i].Effect.TargetingStrategy.RequiresUserSelection;
				var validTargets = needsTarget
					? abilities[i].Effect.TargetingStrategy.GetValidTargets(context)
					: ImmutableList<int>.Empty;

				if (needsTarget && validTargets.IsEmpty)
					continue;

				var abilityAction = new ActivateAbilityAction
				{
					CardId = card.Id,
					ActivatingPlayerId = playerId,
					AbilityIndex = i,
					TargetIds = needsTarget
						? ImmutableList.Create(validTargets[0])
						: ImmutableList<int>.Empty,
				};

				if (state.TryAddAction(abilityAction).Success)
					actions.Add(abilityAction);
			}
		}

		return actions;
	}
}
