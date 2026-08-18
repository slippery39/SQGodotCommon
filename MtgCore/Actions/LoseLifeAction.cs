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

			var lost = state.ApplyReplacements(ReplaceableEvent.LifeLoss, player.Id, amount);
			if (lost <= 0)
				continue;

			var updated = player with
			{
				Life = player.Life - lost,
				LifeLostThisTurn = player.LifeLostThisTurn + lost,
			};
			state = state.UpdateObject(player.Id, updated);

			// PendingGameEvents is the trigger feed — Events alone is silently inert.
			var lostEvent = new PlayerLostLifeEvent { PlayerId = player.Id, Amount = lost };
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(lostEvent) };
			events = events.Add(lostEvent);
		}

		return new ActionResult(state) { Events = events };
	}
}
