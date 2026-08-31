using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Moves a card to its owner's graveyard.
/// Used by ResolveSpellAction to move a spell to the graveyard after its
/// effects have fully resolved, rather than before.
///
/// If the card no longer exists (e.g. countered or already moved), this
/// action is a no-op.
/// </summary>
public record MoveCardToGraveyardAction : GameAction
{
	public int CardId { get; init; }

	/// <summary>
	/// Reads the card id from pipeline context instead, so a selection step can feed this one —
	/// "find a card, put it in the graveyard". `MoveCardToExileAction` has always had this; the
	/// graveyard twin did not, which made Entomb-style effects inexpressible.
	/// </summary>
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

		var graveyardId = gameState.GetPlayerZoneId(card.OwnerId, ZoneType.Graveyard);
		return new ActionResult(gameState.MoveCardTracked(cardId, graveyardId));
	}
}
