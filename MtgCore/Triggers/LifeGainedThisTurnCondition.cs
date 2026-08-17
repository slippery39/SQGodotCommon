using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "At the beginning of each end step, if you gained N or more life this turn, ..."
/// — Resplendent Angel.
///
/// Fires on TurnEndedEvent when the controller's LifeGainedThisTurn has reached Minimum.
/// MtgPlayer.LifeGainedThisTurn accumulates in GainLifeAction (after replacement modifiers, so
/// a life-gain bonus counts toward the threshold) and is zeroed by StartTurnAction.
///
/// Checking at end of turn rather than on each life gain matters: the card asks a "this turn"
/// question, so it must be answered once the turn's gains are all in. Pair it with
/// MaxTriggersPerTurn = 1 so a turn cannot fire it more than once.
/// </summary>
public record LifeGainedThisTurnCondition : TriggerCondition
{
	public int Minimum { get; init; } = 5;

	public override bool IsSatisfiedBy(GameEvent gameEvent, TriggerContext context)
	{
		if (gameEvent is not TurnEndedEvent)
			return false;

		if (context.GameState.GetObject(context.ControllingPlayerId) is not MtgPlayer player)
			return false;

		return player.LifeGainedThisTurn >= Minimum;
	}
}
