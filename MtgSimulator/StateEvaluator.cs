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
		foreach (var c in state.GetCardsInZone(playerBattlefieldId))
		{
			if (!c.HasComponent<CreatureComponent>())
				continue;
			playerCreatureCount++;
			// Permanent power only — UntilEndOfTurn buffs (Giant Growth etc.) evaporate next turn
			// and should not count as lasting board advantage.
			playerPower += state.GetEffectivePermanentPower(c.Id);
		}

		var opponentCreatureCount = 0;
		var opponentPower = 0;
		foreach (var c in state.GetCardsInZone(opponentBattlefieldId))
		{
			if (!c.HasComponent<CreatureComponent>())
				continue;
			opponentCreatureCount++;
			opponentPower += state.GetEffectivePermanentPower(c.Id);
		}

		score += (playerCreatureCount - opponentCreatureCount) * CreatureCountWeight;
		score += (playerPower - opponentPower) * TotalPowerWeight;

		var playerNonCreatureCount = 0;
		foreach (var c in state.GetCardsInZone(playerBattlefieldId))
		{
			if (c.HasComponent<PermanentComponent>() && !c.HasComponent<CreatureComponent>())
				playerNonCreatureCount++;
		}

		var opponentNonCreatureCount = 0;
		foreach (var c in state.GetCardsInZone(opponentBattlefieldId))
		{
			if (c.HasComponent<PermanentComponent>() && !c.HasComponent<CreatureComponent>())
				opponentNonCreatureCount++;
		}

		score += (playerNonCreatureCount - opponentNonCreatureCount) * NonCreaturePermanentWeight;

		score +=
			(
				state.GetChildrenIds(playerHandId).Count()
				- state.GetChildrenIds(opponentHandId).Count()
			) * CardsInHandWeight;

		// Count only permanent mana (MaxMana), not temporary fast mana (CurrentMana).
		// Fast mana should score 0 unless the depth search finds it enables something worthwhile.
		score += player.MaxMana * ManaWeight;

		return score;
	}
}
