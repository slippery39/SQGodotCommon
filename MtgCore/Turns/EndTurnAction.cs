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
/// First-turn compensation for Player 2 (TurnNumber == 1):
///   - BonusMana = 1 — one extra mana this turn only
///   - BonusDraws = 1 — one extra card draw this turn only
/// This mirrors Hearthstone's "The Coin" concept and partially offsets
/// the inherent first-player advantage in an aggressive land-free format.
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

		var newTurnNumber = activePlayerId == Player2Id ? game.TurnNumber + 1 : game.TurnNumber;

		var updatedGame = game with
		{
			ActivePlayerId = nextPlayerId,
			TurnNumber = newTurnNumber,
			Phase = TurnPhase.Main,
		};

		var state = gameState.UpdateObject(GameId, updatedGame);

		var nextBattlefieldId = state.GetPlayerZoneId(nextPlayerId, ZoneType.Battlefield);

		// Give Player 2 bonus mana and an extra draw on their very first turn
		var isPlayer2FirstTurn = nextPlayerId == Player2Id && game.TurnNumber == 1;

		var startTurn = new StartTurnAction
		{
			ActivePlayerId = nextPlayerId,
			BattlefieldId = nextBattlefieldId,
			//BonusMana = isPlayer2FirstTurn ? 1 : 0,
			//BonusDraws = isPlayer2FirstTurn ? 1 : 0,
		};

		var events = ImmutableList.Create<GameEvent>(
			new TurnEndedEvent { PlayerId = activePlayerId }
		);

		return new ActionResult(state.SpawnAction(startTurn)) { Events = events };
	}
}
