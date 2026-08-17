using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "Take an extra turn after this one" — Time Warp, Teferi's ultimate.
///
/// Queues the turn on MtgGame; EndTurnAction spends it instead of passing play. The extra turn is
/// always taken by the ACTIVE player, which is the only player who can be casting this.
///
/// MaxQueued is a hard cap and deliberately low. The simulator warns at 50 actions per turn and
/// cuts a game off at 100; an AI that rates extra turns highly could otherwise chain them until
/// it trips that cutoff, and a game lost to the loop detector is indistinguishable from a bug.
/// </summary>
public record TakeExtraTurnAction : GameAction
{
	public int Turns { get; init; } = 1;

	public const int MaxQueued = 2;

	public override ActionResult Execute(GameState gameState)
	{
		var game = gameState.TryGetGame();
		if (game == null)
			return new ActionResult(gameState);

		var queued = Math.Min(MaxQueued, game.ExtraTurnsQueued + Math.Max(0, Turns));

		return new ActionResult(
			gameState.UpdateObject(game.Id, game with { ExtraTurnsQueued = queued })
		);
	}
}
