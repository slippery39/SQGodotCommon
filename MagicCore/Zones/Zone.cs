using ImmutableGameObjects;

namespace MagicCore;

public enum ZoneType
{
	Hand,
	Library,
	Graveyard,
	Battlefield,
	Exile,
	Stack,
	Command, // emblems, and "until your next turn" effects that outlive their source (CR 408)
}

/// <summary>
/// Cards are children of their zone. Each player has their own five zones; the Stack is shared.
///
/// The battlefield is per-CONTROLLER (copied from MtgCore): "permanents you control" is a child
/// lookup instead of a scan, and a control change is a move between the two.
/// Libraries: child index 0 is the TOP.
/// </summary>
public record Zone : GameObject
{
	public ZoneType ZoneType { get; init; }
}
