using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Placed on a land card to grant the player an Emblem when the land is played.
/// Processed by PlayLandAction and PutLandIntoPlayAction immediately after the
/// mana counters are updated, before the LandPlayedEvent is emitted.
/// </summary>
public record GrantEmblemComponent : GameComponent
{
	public Emblem Emblem { get; init; } = null!;
}
