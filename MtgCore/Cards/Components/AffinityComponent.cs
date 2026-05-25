using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Affinity for artifacts: reduce this card's mana cost by the number of artifact
/// permanents the casting player controls, minimum 0.
/// Checked by CastCreatureAction and CastSpellAction when computing effective mana cost.
/// </summary>
public record AffinityComponent : GameComponent { }
