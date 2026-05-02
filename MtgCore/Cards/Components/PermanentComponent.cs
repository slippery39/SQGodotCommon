using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Marks a card as a permanent — it enters the battlefield when cast and remains there
/// until destroyed, exiled, or otherwise removed.
///
/// All creature cards must also carry this component (enforced by CardLibrary factories).
/// The distinction between permanents and non-permanents (instants, sorceries) drives
/// casting routing in MtgActionGenerator: PermanentComponent → CastPermanentAction or
/// CastCreatureAction; SpellComponent only → CastSpellAction.
///
/// Subtype strings ("Artifact", "Enchantment") on Card.Subtypes distinguish permanent
/// types for targeting rules — this component carries no type data itself.
/// </summary>
public record PermanentComponent : GameComponent { }
