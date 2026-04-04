using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Begins the game by kicking off the first player's first turn.
/// The first player skips their draw step on turn 1 — this is handled
/// by passing SkipDraw = true to StartTurnAction.
///
/// Presentation layers call BeginGame() on the state and respond to events.
/// No knowledge of this action or StartTurnAction is required outside MTGCore.
/// </summary>
public record BeginGameAction : GameAction
{
	public int GameId { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		var game = gameState.GetGame(GameId);
		var activePlayerId = game.ActivePlayerId;
		var battlefieldId = gameState.GetPlayerZoneId(activePlayerId, ZoneType.Battlefield);

		var startTurn = new StartTurnAction
		{
			ActivePlayerId = activePlayerId,
			BattlefieldId = battlefieldId,
			SkipDraw = true,
		};

		return new ActionResult(gameState)
		{
			SpawnedActions = ImmutableList.Create<GameAction>(startTurn),
		};
	}
}
