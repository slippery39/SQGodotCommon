using ImmutableGameObjects;

namespace DoomCore;

/// <summary>A named container. Cards and enemies are children of their current zone.</summary>
public record Zone : GameObject
{
	public ZoneType ZoneType { get; init; }
}
