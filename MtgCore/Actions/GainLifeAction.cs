using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Restores a fixed amount of life to each target player.
/// </summary>
public record GainLifeAction : EffectAction
{
	public int Amount { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;
		var amount = ResolveAmount(Amount);

		if (amount == 0)
			return new ActionResult(gameState);

		foreach (var targetId in ResolveTargetIds())
		{
			if (state.GetObject(targetId) is not MtgPlayer player)
				continue;

			var updated = player with { Life = player.Life + amount };
			state = state.UpdateObject(player.Id, updated);
			events = events.Add(
				new PlayerGainedLifeEvent { PlayerId = player.Id, Amount = amount }
			);
		}

		return new ActionResult(state) { Events = events };
	}
}
