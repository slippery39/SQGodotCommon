using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// How long a PowerToughnessModifier lasts.
/// </summary>
public enum ModifierDuration
{
	/// <summary>
	/// Lasts until the end of the current turn.
	/// Cleared by StartTurnAction at the start of the controller's next turn.
	/// Used for spells like Giant Growth.
	/// </summary>
	UntilEndOfTurn,

	/// <summary>
	/// Lasts indefinitely until the card leaves the battlefield or the
	/// modifier is explicitly removed.
	/// Used for permanent enchantment-style buffs like Unholy Strength.
	/// </summary>
	Permanent,
}

/// <summary>
/// Represents a power and/or toughness modification applied to a creature.
///
/// Modifiers are components on the card itself — they travel with the card.
/// GetEffectivePower / GetEffectiveToughness sum all modifiers on a card
/// on top of the base printed values.
///
/// Sources of modifiers:
///   - Spells like Giant Growth (UntilEndOfTurn)
///   - Permanent enchantment-style spells like Unholy Strength (Permanent)
///
/// Static ability bonuses (anthem effects) are evaluated separately at read
/// time and do not add modifier components to cards.
///
/// SourceCardId tracks which card or effect applied this modifier.
/// Useful for removal effects that target specific buffs.
/// </summary>
public record PowerToughnessModifier : GameComponent
{
	public int PowerBonus { get; init; } = 0;
	public int ToughnessBonus { get; init; } = 0;
	public ModifierDuration Duration { get; init; } = ModifierDuration.UntilEndOfTurn;

	/// <summary>
	/// The ID of the card or effect that applied this modifier.
	/// 0 if the source is unknown or not relevant.
	/// </summary>
	public int SourceCardId { get; init; } = 0;
}
