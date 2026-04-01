using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Base record for all cards. Cards are always children of a Zone in GameState.
///
/// A card's type and behaviour are determined entirely by its components:
///   - SpellComponent   — the card is a spell with auto-resolving effects
///   - CreatureComponent — the card is a creature with power/toughness/damage
///
/// Multiple components can be active simultaneously (e.g. a creature-land).
/// OwnerId and ControllerId default to 0 and are stamped on at deck
/// construction time via 'with'.
/// </summary>
public record Card : GameObject
{
	public int ManaCost { get; init; }
	public int OwnerId { get; init; }
	public int ControllerId { get; init; }
}
