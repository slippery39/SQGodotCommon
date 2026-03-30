using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Draws Amount cards from the target player's library into their hand.
///
/// PlayerId can be set directly or read from pipeline context via
/// ContextKeys.CastingPlayerId when used inside a spell effect pipeline.
/// </summary>
public record DrawCardsAction : GameAction
{
	public int PlayerId { get; init; }
	public int Amount { get; init; } = 1;

	public override ActionResult Execute(GameState gameState)
	{
		var playerId = PlayerId != 0 ? PlayerId : GetInput<int>(ContextKeys.CastingPlayerId, 0);

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
