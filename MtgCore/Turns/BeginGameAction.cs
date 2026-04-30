using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Begins the game by running all pre-game setup then kicking off the first turn.
///
/// Order of operations:
///   1. SetupGameAction — shuffles both libraries, draws opening hands of 4
///   2. StartTurnAction — gives Player 1 their first mana, skips draw
///
/// Presentation layers call BeginGame() on the state and respond to the result.
/// No knowledge of this action, SetupGameAction, or StartTurnAction is required
/// outside MTGCore.
/// </summary>
public record BeginGameAction : GameAction
{
	public int GameId { get; init; }
	public int Player1Id { get; init; }
	public int Player2Id { get; init; }

	/// <summary>
	/// Number of cards each player draws as their opening hand.
	/// Defaults to 4 — lower than traditional MTG (7) to reduce
	/// opening consistency and first-player advantage in a land-free format.
	/// </summary>
	public int OpeningHandSize { get; init; } = 4;

	/// <summary>
	/// Seed used to shuffle both libraries. 0 = random (default).
	/// Pass a non-zero value for deterministic/benchmark runs.
	/// </summary>
	public int ShuffleSeed { get; init; } = 0;

	public override ActionResult Execute(GameState gameState)
	{
		var game = gameState.GetGame(GameId);
		var activePlayerId = game.ActivePlayerId;
		var battlefieldId = gameState.GetPlayerZoneId(activePlayerId, ZoneType.Battlefield);

		var setup = new SetupGameAction
		{
			Player1Id = Player1Id,
			Player2Id = Player2Id,
			OpeningHandSize = OpeningHandSize,
			ShuffleSeed = ShuffleSeed,
		};

		var startTurn = new StartTurnAction
		{
			ActivePlayerId = activePlayerId,
			BattlefieldId = battlefieldId,
			SkipDraw = true,
		};

		return new ActionResult(gameState.SpawnActions(new GameAction[] { setup, startTurn }));
	}
}
