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

	public override ActionResult Execute(GameState gameState)
	{
		if (!gameState.HasObject(CardId))
			return new ActionResult(gameState);

		var card = gameState.GetObject(CardId) as Card;
		if (card == null)
			return new ActionResult(gameState);

		var graveyardId = gameState.GetPlayerZoneId(card.OwnerId, ZoneType.Graveyard);
		return new ActionResult(gameState.MoveCardTracked(CardId, graveyardId));
	}
}
