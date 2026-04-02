using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Ends the active player's turn and begins the next player's turn.
///
/// Responsibilities:
///   - Switches ActivePlayerId to the opponent
///   - Increments TurnNumber when a full round completes
///     (i.e. when Player 2 ends their turn — both players have gone)
///   - Spawns StartTurnAction for the next player
///
/// Emits TurnEndedEvent.
/// </summary>
public record EndTurnAction : GameAction
{
	public int GameId { get; init; }
	public int Player1Id { get; init; }
	public int Player2Id { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		var game = gameState.GetGame(GameId);
		var activePlayerId = game.ActivePlayerId;
		var nextPlayerId = activePlayerId == Player1Id ? Player2Id : Player1Id;

		// Increment turn number only when a full round completes
		// (Player 2 ending their turn means both players have gone)
		var newTurnNumber = activePlayerId == Player2Id ? game.TurnNumber + 1 : game.TurnNumber;

		var updatedGame = game with
		{
			ActivePlayerId = nextPlayerId,
			TurnNumber = newTurnNumber,
			Phase = TurnPhase.Main,
		};

		var state = gameState.UpdateObject(GameId, updatedGame);

		var nextBattlefieldId = state.GetPlayerZoneId(nextPlayerId, ZoneType.Battlefield);

		var startTurn = new StartTurnAction
		{
			ActivePlayerId = nextPlayerId,
			BattlefieldId = nextBattlefieldId,
		};

		var events = ImmutableList.Create<GameEvent>(
			new TurnEndedEvent { PlayerId = activePlayerId }
		);

		return new ActionResult(state)
		{
			Events = events,
			SpawnedActions = ImmutableList.Create<GameAction>(startTurn),
		};
	}
}
