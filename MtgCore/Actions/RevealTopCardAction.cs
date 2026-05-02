using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Reveals the top card of a player's library without moving it.
/// Outputs the card's ID and mana cost into pipeline context for
/// subsequent steps to consume.
///
/// Used by Dark Confidant-style effects where the amount of a later
/// effect depends on the revealed card's mana cost.
/// </summary>
public record RevealTopCardAction : GameAction
{
	public int PlayerId { get; init; }
	public string PlayerIdContextKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var playerId = string.IsNullOrEmpty(PlayerIdContextKey)
			? PlayerId
			: GetInput<int>(PlayerIdContextKey, 0);
		var libraryId = gameState.GetPlayerZoneId(playerId, ZoneType.Library);
		var topCardId = gameState.GetChildrenIds(libraryId).FirstOrDefault();

		if (topCardId == 0)
			return new ActionResult(gameState);

		var topCard = (Card)gameState.GetObject(topCardId);

		return new ActionResult(gameState)
			.WithOutput(ContextKeys.RevealedCardId, topCardId)
			.WithOutput(ContextKeys.RevealedCardManaCost, topCard.ManaCost)
			.WithEvent(
				new CardRevealedEvent
				{
					PlayerId = playerId,
					CardId = topCardId,
					ManaCost = topCard.ManaCost,
				}
			);
	}
}
