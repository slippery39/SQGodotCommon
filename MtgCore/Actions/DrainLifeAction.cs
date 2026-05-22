using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Causes the target player to lose life and the casting player to gain the same amount.
/// Used by Siege Rhino's ETB: opponent loses Amount life, caster gains Amount life.
///
/// If TargetOpponent is true, derives the opponent from the casting player via
/// the well-known Player1/Player2 IDs (2-player game only).
/// No-ops if any player ID cannot be resolved.
/// </summary>
public record DrainLifeAction : GameAction
{
	public int Amount { get; init; }
	public bool TargetOpponent { get; init; } = true;
	public string PlayerIdContextKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		if (Amount == 0)
			return new ActionResult(gameState);

		var castingPlayerId = string.IsNullOrEmpty(PlayerIdContextKey)
			? 0
			: GetInput<int>(PlayerIdContextKey, 0);

		if (castingPlayerId == 0)
			return new ActionResult(gameState);

		var drainTargetId = TargetOpponent
			? GetOpponentId(gameState, castingPlayerId)
			: castingPlayerId;

		var gainTargetId = TargetOpponent
			? castingPlayerId
			: GetOpponentId(gameState, castingPlayerId);

		if (gameState.GetObject(drainTargetId) is not MtgPlayer drainTarget)
			return new ActionResult(gameState);
		if (gameState.GetObject(gainTargetId) is not MtgPlayer gainTarget)
			return new ActionResult(gameState);

		var drained = drainTarget with { Life = drainTarget.Life - Amount };
		var gained = gainTarget with { Life = gainTarget.Life + Amount };

		var state = gameState
			.UpdateObject(drainTarget.Id, drained)
			.UpdateObject(gainTarget.Id, gained);

		var events = ImmutableList.Create<GameEvent>(
			new PlayerLostLifeEvent { PlayerId = drainTarget.Id, Amount = Amount },
			new PlayerGainedLifeEvent { PlayerId = gainTarget.Id, Amount = Amount }
		);

		return new ActionResult(state) { Events = events };
	}

	private static int GetOpponentId(GameState gameState, int castingPlayerId)
	{
		var p1Id = gameState.GetWellKnownId(MtgObjectKeys.Player1);
		return castingPlayerId == p1Id ? gameState.GetWellKnownId(MtgObjectKeys.Player2) : p1Id;
	}
}
