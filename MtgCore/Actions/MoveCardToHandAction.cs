using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Moves a card to a player's hand.
///
/// Card resolution:
///   - Set CardId directly, or
///   - Set CardIdContextKey to read the card ID from pipeline context
///
/// Player resolution:
///   - Set PlayerId directly, or
///   - Set PlayerIdContextKey to read the player ID from pipeline context
///
/// If the context key variants are set they take priority over the direct values.
/// If the resolved card ID is 0 or the card no longer exists, the action does nothing.
/// </summary>
public record MoveCardToHandAction : GameAction
{
	public int PlayerId { get; init; }
	public string PlayerIdContextKey { get; init; } = "";
	public int CardId { get; init; }
	public string CardIdContextKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var cardId = string.IsNullOrEmpty(CardIdContextKey)
			? CardId
			: GetInput<int>(CardIdContextKey, 0);

		if (cardId == 0 || !gameState.HasObject(cardId))
			return new ActionResult(gameState);

		var playerId = string.IsNullOrEmpty(PlayerIdContextKey)
			? PlayerId
			: GetInput<int>(PlayerIdContextKey, 0);

		if (playerId == 0 || !gameState.HasObject(playerId))
			return new ActionResult(gameState);

		var handId = gameState.GetPlayerZoneId(playerId, ZoneType.Hand);
		var newState = gameState.MoveObject(cardId, handId);

		return new ActionResult(newState);
	}
}
