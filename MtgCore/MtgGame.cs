using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// The root object of the game. Owns the shared zones:
/// Battlefield, Stack, and Exile.
/// Players are also children of the game root.
/// </summary>
public record MtgGame : GameObject;
