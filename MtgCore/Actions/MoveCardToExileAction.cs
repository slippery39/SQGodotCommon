using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Moves a specific card to its owner's exile zone.
/// Used by ResolveSpellAction when a flashback spell finishes resolving.
/// If the card no longer exists, this is a no-op.
/// </summary>
public record MoveCardToExileAction : GameAction
{
	public int CardId { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		if (!gameState.HasObject(CardId))
			return new ActionResult(gameState);

		var card = gameState.GetObject(CardId) as Card;
		if (card == null)
			return new ActionResult(gameState);

		var exileId = gameState.GetPlayerZoneId(card.OwnerId, ZoneType.Exile);
		return new ActionResult(gameState.MoveObject(CardId, exileId));
	}
}
