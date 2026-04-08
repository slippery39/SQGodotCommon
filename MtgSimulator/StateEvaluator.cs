using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Evaluates a game state from a given player's perspective.
///
/// Returns a float score where higher is better for the given player.
/// Terminal states (win/loss) return the maximum or minimum score.
/// Non-terminal states are scored using a weighted sum of game factors.
///
/// Weights are tunable constants — adjust based on simulator output.
/// </summary>
public static class StateEvaluator
{
	// Terminal scores
	public const float WinScore = 10000f;
	public const float LossScore = -10000f;

	// Factor weights — tune these based on simulation results
	private const float LifeWeight = 2.0f;
	private const float CreatureCountWeight = 3.0f;
	private const float TotalPowerWeight = 1.5f;
	private const float CardsInHandWeight = 1.0f;
	private const float ManaWeight = 0.5f;

	/// <summary>
	/// Scores the game state from the perspective of the given player.
	/// Higher scores are better for that player.
	/// </summary>
	public static float Evaluate(GameState state, MtgGameIds ids, int playerId)
	{
		var opponentId = playerId == ids.Player1Id ? ids.Player2Id : ids.Player1Id;

		var player = state.GetPlayer(playerId);
		var opponent = state.GetPlayer(opponentId);

		// Terminal conditions
		if (player.HasLost)
			return LossScore;
		if (opponent.HasLost)
			return WinScore;

		var score = 0f;

		// Life total difference
		score += (player.Life - opponent.Life) * LifeWeight;

		// Battlefield presence
		var playerBattlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		var opponentBattlefieldId = state.GetPlayerZoneId(opponentId, ZoneType.Battlefield);

		var playerCreatures = state
			.GetCardsInZone(playerBattlefieldId)
			.Where(c => c.HasComponent<CreatureComponent>())
			.ToList();
		var opponentCreatures = state
			.GetCardsInZone(opponentBattlefieldId)
			.Where(c => c.HasComponent<CreatureComponent>())
			.ToList();

		score += (playerCreatures.Count - opponentCreatures.Count) * CreatureCountWeight;

		// Total power on board — measures offensive pressure
		var playerPower = playerCreatures.Sum(c => c.GetComponent<CreatureComponent>()!.Power);
		var opponentPower = opponentCreatures.Sum(c => c.GetComponent<CreatureComponent>()!.Power);

		score += (playerPower - opponentPower) * TotalPowerWeight;

		// Cards in hand — hand size is a resource advantage
		var playerHandId = state.GetPlayerZoneId(playerId, ZoneType.Hand);
		var opponentHandId = state.GetPlayerZoneId(opponentId, ZoneType.Hand);

		var playerHand = state.GetCardsInZone(playerHandId).Count();
		var opponentHand = state.GetCardsInZone(opponentHandId).Count();

		score += (playerHand - opponentHand) * CardsInHandWeight;

		// Current mana — more mana means more options this turn
		score += player.CurrentMana * ManaWeight;

		return score;
	}
}
