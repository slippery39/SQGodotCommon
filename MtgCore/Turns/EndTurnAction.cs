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

		// An extra turn is spent here rather than passing play: the same player starts again, and
		// the round counter does not advance, because no round completed.
		var takingExtraTurn = game.ExtraTurnsQueued > 0;

		var nextPlayerId =
			takingExtraTurn ? activePlayerId
			: activePlayerId == Player1Id ? Player2Id
			: Player1Id;

		var newTurnNumber =
			!takingExtraTurn && activePlayerId == Player2Id ? game.TurnNumber + 1 : game.TurnNumber;

		var updatedGame = game with
		{
			ActivePlayerId = nextPlayerId,
			TurnNumber = newTurnNumber,
			Phase = TurnPhase.Main,
			ExtraTurnsQueued = takingExtraTurn ? game.ExtraTurnsQueued - 1 : 0,
		};

		var state = gameState.UpdateObject(GameId, updatedGame);

		// Strip "this turn" replacement effects from BOTH players. Done here rather than in
		// StartTurnAction because that only touches the active player, so a prevention effect
		// cleaned up there would linger through the opponent's entire turn.
		state = ClearEndOfTurnReplacements(state, Player1Id);
		state = ClearEndOfTurnReplacements(state, Player2Id);

		var nextBattlefieldId = state.GetPlayerZoneId(nextPlayerId, ZoneType.Battlefield);

		var startTurn = new StartTurnAction
		{
			ActivePlayerId = nextPlayerId,
			BattlefieldId = nextBattlefieldId,
		};

		// PendingGameEvents is the trigger feed. TurnEndedEvent was previously only on the
		// caller-visible Events list, so no "at the beginning of the end step" trigger could
		// ever fire — the same silent-inertness as CardDiscardedEvent and PlayerGainedLifeEvent.
		var turnEndedEvent = new TurnEndedEvent { PlayerId = activePlayerId };
		state = state with { PendingGameEvents = state.PendingGameEvents.Add(turnEndedEvent) };

		var events = ImmutableList.Create<GameEvent>(turnEndedEvent);

		return new ActionResult(state.SpawnAction(startTurn)) { Events = events };
	}

	private static GameState ClearEndOfTurnReplacements(GameState state, int playerId)
	{
		if (state.GetObject(playerId) is not MtgPlayer player)
			return state;

		var kept = player
			.Components.Where(c =>
				c is not ReplacementModifierComponent r
				|| r.Duration != ModifierDuration.UntilEndOfTurn
			)
			.ToImmutableArray();

		return kept.Length == player.Components.Length
			? state
			: state.UpdateObject(playerId, player with { Components = kept });
	}
}
