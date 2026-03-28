using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Draws Amount cards from the target player's library into their hand.
/// Cards are drawn from the top of the library (first child of the library zone).
///
/// Emits a CardDrawnEvent per card drawn.
/// Emits a LibraryEmptyEvent if the library runs out mid-draw and stops drawing.
///
/// Note: In MTG, drawing from an empty library means you lose the game at the
/// next state-based action check. That rule is not enforced here yet —
/// LibraryEmptyEvent gives the game layer the hook to handle it.
/// </summary>
public record DrawCardsAction : GameAction
{
	public int PlayerId { get; init; }
	public int Amount { get; init; } = 1;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		var handId = state.GetPlayerZoneId(PlayerId, ZoneType.Hand);
		var libraryId = state.GetPlayerZoneId(PlayerId, ZoneType.Library);

		for (int i = 0; i < Amount; i++)
		{
			var topCardId = state.GetChildrenIds(libraryId).FirstOrDefault();

			if (topCardId == 0)
			{
				events = events.Add(new LibraryEmptyEvent { PlayerId = PlayerId });
				break;
			}

			state = state.MoveObject(topCardId, handId);
			events = events.Add(new CardDrawnEvent { PlayerId = PlayerId, CardId = topCardId });
		}

		return new ActionResult(state) { Events = events };
	}
}
