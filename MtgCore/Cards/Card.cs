using System.Collections.Immutable;
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
/// Subtypes (e.g. "Goblin", "Elf", "Beast") support tribal and type-based queries.
/// </summary>
public record Card : GameObject
{
	public int ManaCost { get; init; }
	public ImmutableList<AdditionalCost> AdditionalCastCosts { get; init; } =
		ImmutableList<AdditionalCost>.Empty;
	public int OwnerId { get; init; }
	public int ControllerId { get; init; }
	public ImmutableHashSet<string> Subtypes { get; init; } =
		ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase);

	public bool HasSubtype(string subtype) => Subtypes.Contains(subtype);
}
