using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "This creature enters with N +1/+1 counters on it."
///
/// Applied by PutIntoBattlefieldAction.ApplyEtbCeremony, NOT as an ETB trigger. In real MTG this
/// is a replacement effect that modifies how the permanent enters, and the ceremony is the single
/// path every creature takes onto the battlefield — so a cast Hydra, a reanimated one, a cloned
/// one and a token all behave alike. That is the same argument CopyOnEnterComponent uses, and it
/// also sidesteps threading ContextKeys.XValue through CheckStateBasedEffectsAction into a
/// trigger's ResolveEffectAction, where that plumbing does not exist.
///
/// FromXValue reads the X chosen when the creature was cast (ContextKeys.XValue) instead of Count.
/// That is how {X}{G} Hydras are expressed — the card is printed 0/0 and X becomes its body.
///
/// DELIBERATELY EMITS NO CountersAddedEvent. Entering with counters is not "counters being put on"
/// a creature, so Wildwood Scourge does not grow off another Hydra merely arriving. Note it on any
/// card where the distinction is visible.
/// </summary>
public record EntersWithCountersComponent : GameComponent
{
	public int Count { get; init; } = 1;

	/// <summary>
	/// When true, the number of counters is the X paid to cast the creature, and Count is ignored.
	/// </summary>
	public bool FromXValue { get; init; } = false;
}
