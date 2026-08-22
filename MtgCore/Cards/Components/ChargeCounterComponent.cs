using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Named counters held on a permanent as a spendable RESOURCE — Dragon's Hoard's gold counters,
/// and charge counters generally.
///
/// A SIBLING OF PlusOneCounterComponent, NOT A SUBCLASS, and deliberately not a
/// PowerToughnessModifier. CreatureEvaluator walks every PowerToughnessModifier on a card, so
/// inheriting would silently turn a gold counter into +1/+1 — the counter would buff the artifact
/// it sits on the moment anything animated it, and would be doubled by a counter-doubler meant for
/// +1/+1 counters. The two kinds of counter share a name in Magic and nothing else here.
///
/// Kind exists so one permanent can hold two sorts at once and so the rules text can say what the
/// card says. Nothing dispatches on it beyond matching, which is why it is a plain string rather
/// than an enum: a new counter name must not require an engine edit.
///
/// Unlike +1/+1 counters there is no P/T consequence, so nothing reads this except the card that
/// placed it and RemoveCounterAdditionalCost. It rides zone changes untouched for the same reason
/// PlusOneCounterComponent does — see that type for why stripping happens on ENTRY.
/// </summary>
public record ChargeCounterComponent : GameComponent
{
	/// <summary>The counter's name — "gold", "charge". Matched case-insensitively.</summary>
	public string Kind { get; init; } = "charge";

	public int Count { get; init; } = 0;

	public bool IsKind(string kind) =>
		string.Equals(Kind, kind, StringComparison.OrdinalIgnoreCase);
}
