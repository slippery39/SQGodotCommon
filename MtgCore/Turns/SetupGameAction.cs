using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Performs all pre-game setup before the first turn begins:
///   1. Shuffles both players' libraries
///   2. Draws opening hands (size configured by BeginGameAction, default 4) for both players
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
	public int OpeningHandSize { get; init; } = 4;

	/// <summary>
	/// Seed used to shuffle both libraries. 0 = random (default).
	/// Player 1 uses this seed directly; Player 2 uses seed + 1.
	/// </summary>
	public int ShuffleSeed { get; init; } = 0;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;

		// Shuffle both libraries
		var rng1 = ShuffleSeed == 0 ? null : new Random(ShuffleSeed);
		var rng2 = ShuffleSeed == 0 ? null : new Random(ShuffleSeed + 1);
		state = state.ShuffleLibrary(Player1Id, rng1);
		state = state.ShuffleLibrary(Player2Id, rng2);

		// Draw opening hands for both players
		var (state1, events1) = DrawOpeningHand(state, Player1Id);
		var (state2, events2) = DrawOpeningHand(state1, Player2Id);

		return new ActionResult(state2) { Events = events1.AddRange(events2) };
	}

	private (GameState, ImmutableList<GameEvent>) DrawOpeningHand(GameState state, int playerId)
	{
		var libraryId = state.GetPlayerZoneId(playerId, ZoneType.Library);
		var handId = state.GetPlayerZoneId(playerId, ZoneType.Hand);
		var events = ImmutableList<GameEvent>.Empty;

		for (int i = 0; i < OpeningHandSize; i++)
		{
			var topCardId = state.GetChildrenIds(libraryId).FirstOrDefault();
			if (topCardId == 0)
				break;

			state = state.MoveObject(topCardId, handId);
			events = events.Add(new CardDrawnEvent { PlayerId = playerId, CardId = topCardId });
		}

		return (state, events);
	}
}
