using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Exiles the top card of each target player's library and marks it playable this turn —
/// "exile the top card of your library. You may play it this turn."
///
/// The card is moved to the owner's exile zone (MoveCardTracked, so a graveyard-active static
/// would still see the crossing correctly even though this move never touches one) and stamped
/// with ExiledPlayableComponent. See that component's header for how casting picks it back up.
/// </summary>
public record ExileTopCardPlayableAction : EffectAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		foreach (var playerId in ResolveTargetIds())
		{
			if (!state.HasObject(playerId))
				continue;

			var libraryId = state.GetPlayerZoneId(playerId, ZoneType.Library);
			var topCardId = state.GetChildrenIds(libraryId).FirstOrDefault();
			if (topCardId == 0)
			{
				events = events.Add(new LibraryEmptyEvent { PlayerId = playerId });
				continue;
			}

			var exileId = state.GetPlayerZoneId(playerId, ZoneType.Exile);
			state = state.MoveCardTracked(topCardId, exileId);

			var exiledCard = (Card)state.GetObject(topCardId);
			state = state.UpdateObject(
				topCardId,
				exiledCard.WithComponent(new ExiledPlayableComponent())
			);

			events = events.Add(new CardExiledEvent { CardId = topCardId, PlayerId = playerId });
		}

		return new ActionResult(state) { Events = events };
	}
}
