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
/// Abstract base for all P/T modifiers attached to a creature card.
/// Each subclass owns its own calculation logic via GetPowerBonus / GetToughnessBonus.
/// CreatureEvaluator loops over all modifiers and calls these methods — no type switching.
///
/// Duration and SourceCardId are shared by all modifier types.
/// </summary>
public abstract record PowerToughnessModifier : GameComponent
{
	public ModifierDuration Duration { get; init; } = ModifierDuration.UntilEndOfTurn;

	/// <summary>
	/// The ID of the card or effect that applied this modifier.
	/// 0 if the source is unknown or not relevant.
	/// </summary>
	public int SourceCardId { get; init; } = 0;

	public abstract int GetPowerBonus(GameState state, int cardId);

	public abstract int GetToughnessBonus(GameState state, int cardId);
}
