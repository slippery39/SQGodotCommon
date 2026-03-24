using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Represents a named zone that holds cards.
/// Each player owns their own Hand, Library, and Graveyard.
/// Battlefield and Stack are shared (owned by the game root object).
/// Cards are children of their current zone in the GameState hierarchy.
/// </summary>
public record Zone : GameObject
{
	public ZoneType ZoneType { get; init; }

	/// <summary>
	/// Whether this zone is visible to all players.
	/// Hand = false, Library = false, all others = true.
	/// </summary>
	public bool IsPublic { get; init; }
}
