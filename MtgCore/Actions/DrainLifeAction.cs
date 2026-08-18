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

		// Each half is replaced independently — the drainer's life-gain bonuses must not
		// change how much the drained player loses, and vice versa.
		var lossAmount = gameState.ApplyReplacements(
			ReplaceableEvent.LifeLoss,
			drainTarget.Id,
			Amount
		);
		var gainAmount = gameState.ApplyReplacements(
			ReplaceableEvent.LifeGain,
			gainTarget.Id,
			Amount
		);

		var drained = drainTarget with
		{
			Life = drainTarget.Life - lossAmount,
			LifeLostThisTurn = drainTarget.LifeLostThisTurn + lossAmount,
		};
		var gained = gainTarget with
		{
			Life = gainTarget.Life + gainAmount,
			LifeGainedThisTurn = gainTarget.LifeGainedThisTurn + gainAmount,
		};

		var state = gameState
			.UpdateObject(drainTarget.Id, drained)
			.UpdateObject(gainTarget.Id, gained);

		var lostEvent = new PlayerLostLifeEvent { PlayerId = drainTarget.Id, Amount = lossAmount };
		var gainedEvent = new PlayerGainedLifeEvent
		{
			PlayerId = gainTarget.Id,
			Amount = gainAmount,
		};

		// PendingGameEvents is the trigger feed — Events alone is silently inert.
		state = state with
		{
			PendingGameEvents = state.PendingGameEvents.Add(lostEvent).Add(gainedEvent),
		};

		var events = ImmutableList.Create<GameEvent>(lostEvent, gainedEvent);

		return new ActionResult(state) { Events = events };
	}

	private static int GetOpponentId(GameState gameState, int castingPlayerId)
	{
		var p1Id = gameState.GetWellKnownId(MtgObjectKeys.Player1);
		return castingPlayerId == p1Id ? gameState.GetWellKnownId(MtgObjectKeys.Player2) : p1Id;
	}
}
