using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Represents a player in the game.
/// A player owns their personal zones (Hand, Library, Graveyard) as children
/// in the GameState hierarchy. Shared zones (Battlefield, Stack, Exile)
/// belong to the game root object.
/// </summary>
public record MtgPlayer : GameObject
{
	public int Life { get; init; } = 20;
	public bool HasLost { get; init; } = false;
}
