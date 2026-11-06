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

	/// "Outside the game" (Bo1 limited: the pool's unplayed cards) — made only for a game given one
	/// (MagicRules.WithSideboard), so a game without keeps every id it had.
	Sideboard,

	/// Phased-out permanents (Rules/Phasing.cs, The City on the Edge of Forever), by controller — made the
	/// first time something phases out, so a game without phasing keeps every id.
	PhasedOut,
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
