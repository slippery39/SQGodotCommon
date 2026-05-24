using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Moves a specific card to its owner's exile zone.
/// Used by ResolveSpellAction when a flashback spell finishes resolving,
/// and by pipeline effects that exile cards by ID (e.g. Scavenging Ooze).
///
/// Card resolution:
///   - Set CardId directly, or
///   - Set CardIdContextKey to read the card ID from pipeline context (takes priority).
/// If the resolved card ID is 0 or the card no longer exists, this is a no-op.
/// </summary>
public record MoveCardToExileAction : GameAction
{
	public int CardId { get; init; }
	public string CardIdContextKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var cardId = string.IsNullOrEmpty(CardIdContextKey)
			? CardId
			: GetInput<int>(CardIdContextKey, 0);

		if (cardId == 0 || !gameState.HasObject(cardId))
			return new ActionResult(gameState);

		var card = gameState.GetObject(cardId) as Card;
		if (card == null)
			return new ActionResult(gameState);

		var exileId = gameState.GetPlayerZoneId(card.OwnerId, ZoneType.Exile);
		return new ActionResult(gameState.MoveObject(cardId, exileId));
	}
}
