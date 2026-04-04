using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Performs all pre-game setup before the first turn begins:
///   1. Shuffles both players' libraries
///   2. Draws opening hands of 7 cards for both players
///
/// Spawned by BeginGameAction before StartTurnAction so that both players
/// have a full hand when the first turn starts.
///
/// The presentation layer never needs to know about this action — it is
/// an implementation detail of BeginGame().
/// </summary>
public record SetupGameAction : GameAction
{
	public int Player1Id { get; init; }
	public int Player2Id { get; init; }
	public int OpeningHandSize { get; init; } = 7;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;

		// Shuffle both libraries
		state = state.ShuffleLibrary(Player1Id);
		state = state.ShuffleLibrary(Player2Id);

		// Draw opening hands for both players
		state = DrawOpeningHand(state, Player1Id);
		state = DrawOpeningHand(state, Player2Id);

		return new ActionResult(state);
	}

	private GameState DrawOpeningHand(GameState state, int playerId)
	{
		var libraryId = state.GetPlayerZoneId(playerId, ZoneType.Library);
		var handId = state.GetPlayerZoneId(playerId, ZoneType.Hand);

		for (int i = 0; i < OpeningHandSize; i++)
		{
			var topCardId = state.GetChildrenIds(libraryId).FirstOrDefault();
			if (topCardId == 0)
				break;

			state = state.MoveObject(topCardId, handId);
		}

		return state;
	}
}
