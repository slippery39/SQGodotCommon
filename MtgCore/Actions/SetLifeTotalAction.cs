using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Sets each target player's life total to an absolute value — "target player's life total
/// becomes 10" (Sorin Markov).
///
/// This is a SET, not a gain or a loss, so it deliberately does NOT route through
/// ReplacementEngine: a life-gain bonus must not turn "becomes 10" into "becomes 11". It still
/// emits the matching gain/loss event for the difference, because trigger payoffs did observe a
/// life change and the amount is what they need.
///
/// Amount = 0 is how "you lose the game" is expressed (Demonic Pact). The state-based loss check
/// in CheckStateBasedEffectsAction already owns everything that follows from a player hitting
/// zero — HasLost, PlayerLostEvent, winner determination — so routing the alternate loss through
/// the life total reuses all of it rather than adding a second, parallel way to lose. Nothing in
/// this engine can prevent loss or gain life in response, so the two are indistinguishable in
/// play. A "you can't lose the game" card would be the point to split them.
/// </summary>
public record SetLifeTotalAction : EffectAction
{
	public int Amount { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		foreach (var targetId in ResolveTargetIds())
		{
			if (!state.HasObject(targetId))
				continue;
			if (state.GetObject(targetId) is not MtgPlayer player)
				continue;

			var delta = Amount - player.Life;
			if (delta == 0)
				continue;

			var updated = player with
			{
				Life = Amount,
				LifeGainedThisTurn = player.LifeGainedThisTurn + Math.Max(delta, 0),
				LifeLostThisTurn = player.LifeLostThisTurn + Math.Max(-delta, 0),
			};
			state = state.UpdateObject(player.Id, updated);

			// PendingGameEvents is the trigger feed; Events alone is silently inert.
			GameEvent changed =
				delta > 0
					? new PlayerGainedLifeEvent { PlayerId = player.Id, Amount = delta }
					: new PlayerLostLifeEvent { PlayerId = player.Id, Amount = -delta };
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(changed) };
			events = events.Add(changed);
		}

		return new ActionResult(state) { Events = events };
	}
}
