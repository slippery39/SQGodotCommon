using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// Restores life to the player, capped at MaxLife.
///
/// Life does NOT heal on its own in this game (STS-style, see DoomJam.md), so every point of it
/// comes from somewhere a player chose. That makes healing genuinely valuable rather than filler,
/// and it is why this is capped rather than allowed to overshoot.
/// </summary>
public record GainLifeAction : EffectAction
{
	public int Amount { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		foreach (var id in TargetIds)
		{
			if (!state.HasObject(id) || state.GetObject(id) is not DoomPlayer player)
				continue;

			var life = Math.Min(player.Life + Amount, player.MaxLife);
			state = state.UpdateObject(id, player with { Life = life });
			events = events.Add(
				new LifeGainedEvent { Amount = life - player.Life, LifeNow = life }
			);
		}

		return new ActionResult(state).WithEvents(events);
	}
}
