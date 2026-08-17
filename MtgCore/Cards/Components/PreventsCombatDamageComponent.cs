using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "Prevent all combat damage that would be dealt to and dealt by this creature." — Fog Bank.
///
/// A marker on the creature rather than a ReplacementModifierComponent, because those are scanned
/// per-PLAYER by ReplacementEngine (they answer "how much damage does this player take") and this
/// is a property of one specific creature on both sides of an exchange.
///
/// Checked in AttackAction.ApplyDamageToCreature, which is the single site all combat damage
/// between creatures flows through. Deliberately does NOT prevent effect damage — Fog Bank still
/// dies to a burn spell, which is what stops it being unanswerable.
/// </summary>
public record PreventsCombatDamageComponent : GameComponent { }
