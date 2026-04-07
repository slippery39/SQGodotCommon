using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Checks all state-based loss conditions and marks any qualifying players as having lost.
///
/// Registered as GameState.PostActionProcessor at game setup. Runs automatically
/// after every standalone action and every completed pipeline — never between
/// individual pipeline steps.
///
/// While GameState.SuppressPostProcessor is true this action never runs — the
/// executor skips the PostActionProcessor entirely. MtgCore uses this to defer
/// SBE checks until a spell or ability has fully resolved.
///
/// Loss conditions checked:
///   - Life <= 0
///   - Library is empty (drew from an empty library)
///
/// If both players lose simultaneously, a draw is declared (WinnerPlayerId = -1).
///
/// IsPostProcessor = true prevents this action from triggering another post-processing
/// cycle after it runs, avoiding infinite recursion.
/// </summary>
public record CheckStateBasedEffectsAction : GameAction
{
	public int Player1Id { get; init; }
	public int Player2Id { get; init; }

	public override bool IsPostProcessor => true;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		var player1 = state.GetPlayer(Player1Id);
		var player2 = state.GetPlayer(Player2Id);

		// Skip if both players are already marked as lost — game is already over
		if (player1.HasLost && player2.HasLost)
			return new ActionResult(state);

		var p1ShouldLose = !player1.HasLost && PlayerMeetsLossCondition(state, player1);
		var p2ShouldLose = !player2.HasLost && PlayerMeetsLossCondition(state, player2);

		if (p1ShouldLose)
		{
			player1 = player1 with { HasLost = true };
			state = state.UpdateObject(Player1Id, player1);
			events = events.Add(
				new PlayerLostEvent
				{
					PlayerId = Player1Id,
					Reason = GetLossReason(gameState, player1),
				}
			);
		}

		if (p2ShouldLose)
		{
			player2 = player2 with { HasLost = true };
			state = state.UpdateObject(Player2Id, player2);
			events = events.Add(
				new PlayerLostEvent
				{
					PlayerId = Player2Id,
					Reason = GetLossReason(gameState, player2),
				}
			);
		}

		if (p1ShouldLose || p2ShouldLose)
		{
			var winnerId = (p1ShouldLose, p2ShouldLose) switch
			{
				(true, true) => -1,
				(true, false) => Player2Id,
				(false, true) => Player1Id,
				_ => -1,
			};

			events = events.Add(new GameOverEvent { WinnerPlayerId = winnerId });
		}

		return new ActionResult(state) { Events = events };
	}

	private static bool PlayerMeetsLossCondition(GameState state, MtgPlayer player)
	{
		if (player.Life <= 0)
			return true;

		var libraryId = state.GetPlayerZoneId(player.Id, ZoneType.Library);
		if (!state.GetCardsInZone(libraryId).Any())
			return true;

		return false;
	}

	private static string GetLossReason(GameState state, MtgPlayer player)
	{
		if (player.Life <= 0)
			return "life total reached zero";

		var libraryId = state.GetPlayerZoneId(player.Id, ZoneType.Library);
		if (!state.GetCardsInZone(libraryId).Any())
			return "drew from an empty library";

		return "unknown";
	}
}
