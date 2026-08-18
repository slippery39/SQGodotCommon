using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Evaluates a game state from a given player's perspective.
///
/// Returns a float score where higher is better for the given player.
/// Terminal states (win/loss) return the maximum or minimum score.
/// Non-terminal states use a weighted sum of game factors.
///
/// Uses MtgGameIds zone IDs directly to avoid GetPlayerZoneId lookups,
/// which scan the child list on every call.
/// </summary>
public static class StateEvaluator
{
	public const float WinScore = 10000f;
	public const float LossScore = -10000f;

	private const float LifeWeight = 0.2f;
	private const float CreatureCountWeight = 3.0f;
	private const float TotalPowerWeight = 2.0f;
	private const float CreatureDamageWeight = 0.1f;
	private const float CardsInHandWeight = 1.4f;
	private const float ManaWeight = 2.0f;
	private const float NonCreaturePermanentWeight = 1.5f;

	public static float Evaluate(GameState state, MtgGameIds ids, int playerId)
	{
		var opponentId = playerId == ids.Player1Id ? ids.Player2Id : ids.Player1Id;

		var player = state.GetPlayer(playerId);
		var opponent = state.GetPlayer(opponentId);

		if (player.HasLost)
			return LossScore;
		if (opponent.HasLost)
			return WinScore;

		// Use known zone IDs directly — no child list scanning
		var playerBattlefieldId =
			playerId == ids.Player1Id ? ids.Player1BattlefieldId : ids.Player2BattlefieldId;
		var opponentBattlefieldId =
			playerId == ids.Player1Id ? ids.Player2BattlefieldId : ids.Player1BattlefieldId;
		var playerHandId = playerId == ids.Player1Id ? ids.Player1HandId : ids.Player2HandId;
		var opponentHandId = playerId == ids.Player1Id ? ids.Player2HandId : ids.Player1HandId;

		var score = 0f;

		score += (player.Life - opponent.Life) * LifeWeight;

		var playerCreatureCount = 0;
		var playerPower = 0;
		var playerCreatureDamage = 0;
		var playerNonCreatureCount = 0;
		foreach (var c in state.GetCardsInZone(playerBattlefieldId))
		{
			var creature = c.GetComponent<CreatureComponent>();
			if (creature != null)
			{
				playerCreatureCount++;
				// Permanent power only — UntilEndOfTurn buffs (Giant Growth etc.) evaporate next turn
				// and should not count as lasting board advantage.
				playerPower += state.GetEffectivePermanentPower(c.Id);
				playerCreatureDamage += creature.Damage;
			}
			else if (c.HasComponent<PermanentComponent>())
			{
				playerNonCreatureCount++;
			}
		}

		var opponentCreatureCount = 0;
		var opponentPower = 0;
		var opponentCreatureDamage = 0;
		var opponentNonCreatureCount = 0;
		foreach (var c in state.GetCardsInZone(opponentBattlefieldId))
		{
			var creature = c.GetComponent<CreatureComponent>();
			if (creature != null)
			{
				opponentCreatureCount++;
				opponentPower += state.GetEffectivePermanentPower(c.Id);
				opponentCreatureDamage += creature.Damage;
			}
			else if (c.HasComponent<PermanentComponent>())
			{
				opponentNonCreatureCount++;
			}
		}

		score += (playerCreatureCount - opponentCreatureCount) * CreatureCountWeight;
		score += (playerPower - opponentPower) * TotalPowerWeight;
		// Damage on surviving creatures is a hidden disadvantage the board snapshot misses.
		// A creature with lethal-minus-one damage is much more fragile than a fresh one.
		score += (opponentCreatureDamage - playerCreatureDamage) * CreatureDamageWeight;

		score += (playerNonCreatureCount - opponentNonCreatureCount) * NonCreaturePermanentWeight;

		// Lands in hand are deliberately NOT counted.
		//
		// Hand size is a proxy for options, and a land held is not an option — it is a resource
		// you have failed to deploy. Counting it made playing a land worth only +2.0 mana minus
		// 1.4 for the card leaving hand: a net +0.6, small enough that the beam would sometimes
		// prefer any other line and simply skip the land drop for a turn. Skipping an early land
		// drop is close to the worst play available, and it was costing the AI a third of a mana
		// step whenever the noise went the wrong way.
		score +=
			(CountNonLand(state, playerHandId) - CountNonLand(state, opponentHandId))
			* CardsInHandWeight;

		// Count only permanent mana (MaxMana), not temporary fast mana (CurrentMana).
		// Fast mana should score 0 unless the depth search finds it enables something worthwhile.
		score += player.MaxMana * ManaWeight;

		return score;
	}

	private static int CountNonLand(GameState state, int handId) =>
		state.GetCardsInZone(handId).Count(c => !c.HasSubtype("Land"));
}
