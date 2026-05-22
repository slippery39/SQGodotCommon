using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Discards a random card from a target player's hand to their graveyard.
/// No-ops silently if the target player's hand is empty.
/// Emits CardDiscardedEvent on success.
///
/// If TargetOpponent is true, derives the opponent from the casting player via
/// the well-known Player1/Player2 IDs (2-player game only).
/// </summary>
public record DiscardRandomCardAction : GameAction
{
	public bool TargetOpponent { get; init; } = false;
	public string PlayerIdContextKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var castingPlayerId = string.IsNullOrEmpty(PlayerIdContextKey)
			? 0
			: GetInput<int>(PlayerIdContextKey, 0);

		if (castingPlayerId == 0)
			return new ActionResult(gameState);

		var targetPlayerId = TargetOpponent
			? GetOpponentId(gameState, castingPlayerId)
			: castingPlayerId;

		var handId = gameState.GetPlayerZoneId(targetPlayerId, ZoneType.Hand);
		var cards = gameState.GetCardsInZone(handId).ToList();

		if (cards.Count == 0)
			return new ActionResult(gameState);

		var chosen = cards[new Random().Next(cards.Count)];
		var graveyardId = gameState.GetPlayerZoneId(chosen.OwnerId, ZoneType.Graveyard);
		var state = gameState.MoveObject(chosen.Id, graveyardId);

		var events = ImmutableList.Create<GameEvent>(
			new CardDiscardedEvent { PlayerId = targetPlayerId, CardId = chosen.Id }
		);

		return new ActionResult(state) { Events = events };
	}

	private static int GetOpponentId(GameState gameState, int castingPlayerId)
	{
		var p1Id = gameState.GetWellKnownId(MtgObjectKeys.Player1);
		return castingPlayerId == p1Id ? gameState.GetWellKnownId(MtgObjectKeys.Player2) : p1Id;
	}
}
