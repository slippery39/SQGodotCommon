using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Puts or removes named charge counters on target permanents — "put a gold counter on this
/// artifact" (Dragon's Hoard).
///
/// The arithmetic mirrors AddCountersAction (Amount, negative removes, clamped at 0) but this is a
/// separate action rather than a flag on that one, because the two differ on every question that
/// matters: charge counters are not a PowerToughnessModifier, are not read by CreatureEvaluator,
/// are NOT subject to the CountersPlaced replacement (Conclave Mentor says "+1/+1 counters"), and
/// do not emit CountersAddedEvent, so a counters-matter payoff cannot fire on a gold counter.
///
/// Unlike AddCountersAction this accepts NON-CREATURE permanents, which is the entire point — the
/// cards that want charge counters are artifacts.
/// </summary>
public record AddChargeCountersAction : EffectAction
{
	/// <summary>Which counter to place. Must match the kind a cost later tries to remove.</summary>
	public string Kind { get; init; } = "charge";

	/// <summary>Counters added; negative removes. Overridden by AmountContextKey when set.</summary>
	public int Amount { get; init; } = 1;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var amount = ResolveAmount(Amount);

		if (amount == 0)
			return new ActionResult(gameState);

		foreach (var targetId in ResolveTargetIds())
		{
			if (!state.HasObject(targetId))
				continue;

			if (state.GetObject(targetId) is not Card card)
				continue;

			var existing = card.GetComponents<ChargeCounterComponent>()
				.FirstOrDefault(c => c.IsKind(Kind));

			var oldCount = existing?.Count ?? 0;
			var newCount = Math.Max(0, oldCount + amount);

			if (newCount == oldCount)
				continue;

			var counter = (existing ?? new ChargeCounterComponent { Kind = Kind }) with
			{
				Count = newCount,
			};

			var updated =
				existing == null
					? card with
					{
						Components = card.Components.Add(counter),
					}
					: card with
					{
						Components = card.Components.Replace(existing, counter),
					};

			state = state.UpdateObject(targetId, updated);
		}

		return new ActionResult(state);
	}
}
