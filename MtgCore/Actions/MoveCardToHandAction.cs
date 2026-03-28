using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Moves a card to a player's hand.
///
/// The card to move can be specified in two ways:
///   - Hardcoded: set CardId directly (useful when the card is known at design time)
///   - Dynamic: set CardIdContextKey to read the card ID from pipeline context
///              (useful when the card is only known at resolution time, e.g. Dark Confidant)
///
/// If CardIdContextKey is set it takes priority over CardId.
/// If the resolved card ID is 0 or the card no longer exists, the action does nothing.
/// </summary>
public record MoveCardToHandAction : GameAction
{
	public int PlayerId { get; init; }
	public int CardId { get; init; }
	public string CardIdContextKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var cardId = string.IsNullOrEmpty(CardIdContextKey)
			? CardId
			: GetInput<int>(CardIdContextKey, 0);

		if (cardId == 0 || !gameState.HasObject(cardId))
			return new ActionResult(gameState);

		var handId = gameState.GetPlayerZoneId(PlayerId, ZoneType.Hand);
		var newState = gameState.MoveObject(cardId, handId);

		return new ActionResult(newState);
	}
}
