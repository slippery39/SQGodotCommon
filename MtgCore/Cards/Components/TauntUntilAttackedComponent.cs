using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Taunt that switches off once this creature has been attacked this turn — Fog Bank.
///
/// Walls are pure blockers, and this engine has no blocking, so a wall's role is filled by Taunt
/// (which forces attackers through it). That works for a big-toughness wall, but Fog Bank also
/// prevents all combat damage to itself, which would make it a Taunt creature that can never be
/// removed by attacking. With no way to go wide, an opponent would simply have no answer — every
/// attack would be compelled into an indestructible roadblock forever.
///
/// Dropping Taunt after the first attack each turn restores the real behaviour: it absorbs one
/// attack per turn, exactly as a single blocker does, and everything else gets through.
///
/// Reads CreatureComponent.WasAttackedThisTurn, which AttackAction sets on the target and
/// StartTurnAction clears.
/// </summary>
public record TauntUntilAttackedComponent : GameComponent { }
