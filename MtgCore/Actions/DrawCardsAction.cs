using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Draws Amount cards from the target player's library into their hand.
///
/// Player resolution:
///   - Set PlayerId directly, or
///   - Set PlayerIdContextKey to read the player ID from pipeline context
///
/// If PlayerIdContextKey is set it takes priority over PlayerId.
/// </summary>
public record DrawCardsAction : GameAction
{
	public int PlayerId { get; init; }
	public string PlayerIdContextKey { get; init; } = "";
	public int Amount { get; init; } = 1;

	public override ActionResult Execute(GameState gameState)
	{
		var playerId = string.IsNullOrEmpty(PlayerIdContextKey)
			? PlayerId
			: GetInput<int>(PlayerIdContextKey, 0);

		if (playerId == 0 || !gameState.HasObject(playerId))
			return new ActionResult(gameState);

		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		var handId = state.GetPlayerZoneId(playerId, ZoneType.Hand);
		var libraryId = state.GetPlayerZoneId(playerId, ZoneType.Library);

		for (int i = 0; i < Amount; i++)
		{
			var topCardId = state.GetChildrenIds(libraryId).FirstOrDefault();

			if (topCardId == 0)
			{
				events = events.Add(new LibraryEmptyEvent { PlayerId = playerId });
				break;
			}

			state = state.MoveObject(topCardId, handId);
			events = events.Add(new CardDrawnEvent { PlayerId = playerId, CardId = topCardId });
		}

		return new ActionResult(state) { Events = events };
	}
}
