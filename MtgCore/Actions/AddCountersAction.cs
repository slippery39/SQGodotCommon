using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Puts, removes or multiplies +1/+1 counters on target creatures.
///
///   newCount = max(0, oldCount * Multiplier + ResolveAmount(Amount))
///
/// Three verbs in one action rather than three actions, because they are the same arithmetic on
/// the same field and a card can want two at once:
///   Amount = 1                     "put a +1/+1 counter on target creature"
///   Amount = -1                    "remove a +1/+1 counter"
///   Multiplier = 2, Amount = 0     "double the number of +1/+1 counters on it" (Primordial Hydra)
///   AmountContextKey = ...         "put that many +1/+1 counters on it"
///
/// AmountContextKey is inherited from EffectAction, so a counted pipeline step feeds this with no
/// extra field — the same route DrawCardsAction and DealDamageAction already take.
///
/// TARGETING A CREATURE THAT IS RUNNING THE EFFECT: use TargetContextKey = ContextKeys.SourceCardId
/// rather than hardcoding TargetIds. ResolveEffectAction overwrites hardcoded targets on a
/// NoTarget() strategy, which is the trap WithSelfBuff documents.
///
/// CountersAddedEvent is staged into PendingGameEvents — the TRIGGER feed, not just
/// ActionResult.Events — and only when the net change is positive, so "remove a counter" cannot
/// feed a "whenever counters are put on a creature" payoff.
/// </summary>
public record AddCountersAction : EffectAction
{
	/// <summary>Counters added; negative removes. Overridden by AmountContextKey when set.</summary>
	public int Amount { get; init; } = 1;

	/// <summary>Applied to the existing count BEFORE Amount. 2 is "double the counters on it".</summary>
	public int Multiplier { get; init; } = 1;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;
		var amount = ResolveAmount(Amount);

		foreach (var targetId in ResolveTargetIds())
		{
			if (!state.HasObject(targetId))
				continue;

			if (state.GetObject(targetId) is not Card card)
				continue;

			if (!card.HasComponent<CreatureComponent>())
				continue;

			var existing = card.GetComponent<PlusOneCounterComponent>();
			var oldCount = existing?.Count ?? 0;
			var newCount = Math.Max(0, (oldCount * Multiplier) + amount);

			if (newCount == oldCount)
				continue;

			var counter = (existing ?? new PlusOneCounterComponent()) with { Count = newCount };

			var updated =
				existing == null
					? card with
					{
						Components = card.Components.Add(counter),
					}
					: card.WithComponentReplaced(counter);

			state = state.UpdateObject(targetId, updated);

			if (newCount <= oldCount)
				continue;

			var added = new CountersAddedEvent
			{
				CardId = targetId,
				PlayerId = card.ControllerId,
				Amount = newCount - oldCount,
			};

			events = events.Add(added);
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(added) };
		}

		return new ActionResult(state) { Events = events };
	}
}
