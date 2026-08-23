using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "You can't lose the game to damage." — Platinum Angel, narrowed.
///
/// SCOPE: the LIFE clause only. Decking still kills its controller, and that is a deliberate
/// divergence from the printed card. A blanket "you can't lose" left no way to end the game at
/// all: neither life nor decking could finish it, so an unanswered Angel ran every game to the
/// harness cutoff. Measured on a 140 000-game run it sat near the top of the draw table at 11.8%
/// against a 4.0% base. Decking is the one clock nothing on the board can interact with, so
/// leaving it live gives every such game a guaranteed end.
///
/// A marker on a battlefield permanent. CheckStateBasedEffectsAction.CheckLossConditions consults
/// it before declaring a loss, which is the ONE place in the engine a player can lose — the same
/// single-site invariant that makes SetLifeTotalAction the only way to express "you lose the game".
///
/// STRUCTURAL, NOT NUMERIC, which is why it is not a ReplacementModifierComponent. That system
/// alters the AMOUNT of an event; this alters whether an outcome happens at all. It is small
/// enough to live as a direct check rather than as the first citizen of a structural-replacement
/// system that does not exist yet.
///
/// Life is still lost while this is out — only the loss is suppressed. The moment the Angel
/// leaves, the pending loss is evaluated as normal on the very next state-based check, so killing
/// it is a genuine answer rather than merely stopping the bleeding.
/// </summary>
public record CannotLoseComponent : GameComponent { }
