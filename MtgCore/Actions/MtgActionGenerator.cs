using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Generates all legal actions a player can take in a given game state.
/// Used by both the console and simulator to drive the game loop.
///
/// Returns a flat list of GameAction — the caller decides which to execute.
/// All actions are validated via TryAddAction before being included, so
/// every action in the returned list is guaranteed to be legal.
/// </summary>
public static class MtgActionGenerator
{
	public static List<GameAction> GetLegalActions(GameState state, MtgGameIds ids, int playerId)
	{
		var actions = new List<GameAction>();
		var opponentId = playerId == ids.Player1Id ? ids.Player2Id : ids.Player1Id;

		var handId = state.GetPlayerZoneId(playerId, ZoneType.Hand);
		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		var opponentBattlefieldId = state.GetPlayerZoneId(opponentId, ZoneType.Battlefield);

		// Play creatures from hand
		foreach (var card in state.GetCardsInZone(handId))
		{
			if (!card.HasComponent<CreatureComponent>())
				continue;

			var action = new PlayCreatureAction { CardId = card.Id, PlayerId = playerId };
			if (state.TryAddAction(action).Success)
				actions.Add(action);
		}

		// Cast spells from hand
		foreach (var card in state.GetCardsInZone(handId))
		{
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

				// Include one action per valid target so the caller can pick randomly or smartly
				foreach (var target in validTargets)
				{
					var castAction = new CastSpellAction
					{
						CardId = card.Id,
						CastingPlayerId = playerId,
						GameId = ids.GameId,
						TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
							0,
							ImmutableList.Create(target)
						),
					};
					if (state.TryAddAction(castAction).Success)
					{
						actions.Add(castAction);
						break; // one target per spell is enough — caller picks randomly anyway
					}
				}
			}
			else
			{
				var castAction = new CastSpellAction
				{
					CardId = card.Id,
					CastingPlayerId = playerId,
					GameId = ids.GameId,
					TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty,
				};
				if (state.TryAddAction(castAction).Success)
					actions.Add(castAction);
			}
		}

		// Attack with creatures
		var attackTargets = new List<int> { opponentId };
		attackTargets.AddRange(
			state
				.GetCardsInZone(opponentBattlefieldId)
				.Where(c => c.HasComponent<CreatureComponent>())
				.Select(c => c.Id)
		);

		foreach (
			var attacker in state
				.GetCardsInZone(battlefieldId)
				.Where(c => c.HasComponent<CreatureComponent>())
		)
		{
			foreach (var targetId in attackTargets)
			{
				var attack = new AttackAction
				{
					AttackerId = attacker.Id,
					TargetId = targetId,
					AttackingPlayerId = playerId,
				};
				if (state.TryAddAction(attack).Success)
				{
					actions.Add(attack);
					break; // one valid target per attacker is enough
				}
			}
		}

		// Activate abilities on battlefield permanents
		foreach (var card in state.GetCardsInZone(battlefieldId))
		{
			var abilities = card.GetComponents<ActivatedAbilityComponent>().ToList();
			for (int i = 0; i < abilities.Count; i++)
			{
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
