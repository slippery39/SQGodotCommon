using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Restores a fixed amount of life to each target player.
///
/// The amount is passed through ApplyReplacements before anything is applied, so a
/// "you gain that much plus 1" effect (Angel of Vitality) modifies the single life gain
/// rather than triggering a second one. Because of that, exactly one PlayerGainedLifeEvent
/// is emitted per player and it carries the ALREADY-MODIFIED amount — a trigger reading it
/// sees the real number, and there is no way for a life-gain bonus to feed itself.
/// </summary>
public record GainLifeAction : EffectAction
{
	public int Amount { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;
		var baseAmount = ResolveAmount(Amount);

		if (baseAmount == 0)
			return new ActionResult(gameState);

		foreach (var targetId in ResolveTargetIds())
		{
			if (state.GetObject(targetId) is not MtgPlayer player)
				continue;

			// Replacements are per-player: each player's own permanents modify their gain.
			var amount = state.ApplyReplacements(ReplaceableEvent.LifeGain, player.Id, baseAmount);
			if (amount <= 0)
				continue;

			var updated = player with
			{
				Life = player.Life + amount,
				LifeGainedThisTurn = player.LifeGainedThisTurn + amount,
			};
			state = state.UpdateObject(player.Id, updated);

			// PendingGameEvents is the trigger feed; Events is only the caller-visible log.
			// Adding to Events alone leaves the event silently inert — which is exactly why
			// no "whenever you gain life" trigger had ever fired.
			var gainedEvent = new PlayerGainedLifeEvent { PlayerId = player.Id, Amount = amount };
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(gainedEvent) };
			events = events.Add(gainedEvent);
		}

		return new ActionResult(state) { Events = events };
	}
}
