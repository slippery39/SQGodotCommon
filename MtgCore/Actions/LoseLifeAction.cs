using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Causes each target player to lose a fixed amount of life.
/// </summary>
public record LoseLifeAction : EffectAction
{
	public int Amount { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		var amount = ResolveAmount(Amount);

		if (amount == 0)
			return new ActionResult(gameState);

		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		foreach (var targetId in ResolveTargetIds())
		{
			if (!state.HasObject(targetId))
				continue;
			if (state.GetObject(targetId) is not MtgPlayer player)
				continue;

			var updated = player with { Life = player.Life - amount };
			state = state.UpdateObject(player.Id, updated);
			events = events.Add(new PlayerLostLifeEvent { PlayerId = player.Id, Amount = amount });
		}

		return new ActionResult(state) { Events = events };
	}
}
