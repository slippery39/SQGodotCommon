using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Trigger condition that fires when the controlling player plays a land
/// and their total lands played (LandsPlayedTotal) meets or exceeds the threshold.
///
/// Used by Valakut's emblem: fires from the Threshold-th land onward.
/// LandsPlayedTotal is already incremented by PlayLandAction before the
/// LandPlayedEvent is emitted, so Threshold = 7 fires starting from the 7th land.
/// </summary>
public record LandsPlayedCondition : TriggerCondition
{
	public int Threshold { get; init; }

	public override bool IsSatisfiedBy(GameEvent gameEvent, TriggerContext context)
	{
		if (gameEvent is not LandPlayedEvent landEvent)
			return false;
		if (landEvent.PlayerId != context.ControllingPlayerId)
			return false;
		var player = context.GameState.GetPlayer(context.ControllingPlayerId);
		return player.LandsPlayedTotal >= Threshold;
	}
}
