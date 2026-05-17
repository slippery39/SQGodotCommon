using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Marker component that grants the controller one additional land play per turn.
/// Add this to artifacts or enchantments (e.g. Exploration) that allow extra land drops.
///
/// PlayLandAction counts the number of ExtraLandPerTurnComponent permanents the player
/// controls and adds that to the base limit of 1, computed fresh at validation time so
/// multiple copies stack correctly.
/// </summary>
public record ExtraLandPerTurnComponent : GameComponent { }
