using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Marks a spell as castable from the graveyard for an alternate mana cost.
/// After resolving via flashback, the card is exiled instead of returned to the graveyard.
/// </summary>
public record FlashbackComponent : GameComponent
{
	public int FlashbackManaCost { get; init; }
}
