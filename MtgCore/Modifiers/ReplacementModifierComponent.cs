using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// The kinds of numeric event a ReplacementModifierComponent can alter.
///
/// One value per call site in ApplyReplacements. Adding a card that replaces a new kind of
/// event means adding a value here and one line at the action that produces it — the scan
/// machinery itself never changes.
/// </summary>
public enum ReplaceableEvent
{
	LifeGain,
	LifeLoss,
	DamageToPlayer,
	DamageToCreature,
	Draw,
	Mill,
}

/// <summary>
/// A replacement effect that alters the AMOUNT of an event before it happens —
/// "if you would gain life, gain that much plus 1", "if a source would deal damage to you,
/// prevent 2 of it", "if you would draw a card, draw two instead".
///
/// Deliberately built the same way PowerToughnessModifier and TriggerCondition are: an
/// abstract serializable record with a virtual method, scanned live at the point of use. That
/// keeps it inside the serialization rule (no delegates anywhere near GameState) and needs no
/// change to the ImmutableGameObjects action loop.
///
/// It is a REPLACEMENT, not a trigger, and that distinction is the whole point. A trigger that
/// gains life in response to gaining life is an infinite loop; because this modifies the amount
/// *inside* the single originating action, exactly one event is emitted and nothing can feed
/// itself.
///
/// Scope note: this covers numeric replacement only. STRUCTURAL replacement — "enters the
/// battlefield tapped", "if it would die, exile it instead" — rewrites an action rather than a
/// number and is not covered here. See DesignNotes.md.
/// </summary>
public abstract record ReplacementModifierComponent : GameComponent
{
	/// <summary>Which event this modifier applies to.</summary>
	public abstract ReplaceableEvent Event { get; }

	/// <summary>Added to the amount. May be negative (damage prevention).</summary>
	public virtual int Bonus => 0;

	/// <summary>Multiplies the amount. 1 = no change; 2 = doubling effects.</summary>
	public virtual int Multiplier => 1;
}

/// <summary>
/// "If you would gain life, you gain that much plus N life instead." — Angel of Vitality.
///
/// Lives on a battlefield permanent; applies to its controller's life gain only.
/// </summary>
public record LifeGainBonusComponent : ReplacementModifierComponent
{
	public override ReplaceableEvent Event => ReplaceableEvent.LifeGain;

	public int Amount { get; init; } = 1;

	public override int Bonus => Amount;
}
