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

	/// <summary>
	/// Survives the end of the current turn and is cleared at the start of the OWNER's next turn,
	/// so it covers the opponent's turn in between.
	///
	/// This exists because there is no priority window: a spell can only be cast on your own turn,
	/// so an UntilEndOfTurn shield always expires before the opponent's attack it was meant to
	/// stop. Safe Passage was blank for exactly that reason.
	///
	/// Currently honoured for REPLACEMENT effects only — StartTurnAction clears these off the
	/// player whose turn is beginning. A P/T modifier carrying this duration would never be
	/// cleared, since ClearEndOfTurnModifiers only looks for UntilEndOfTurn; wire that up before
	/// using it on a creature.
	/// </summary>
	UntilYourNextTurn,
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
