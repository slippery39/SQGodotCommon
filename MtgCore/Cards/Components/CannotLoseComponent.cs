using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "You can't lose the game and your opponents can't win the game." — Platinum Angel.
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
/// Life is still lost and libraries still empty while this is out — only the loss is suppressed.
/// The moment the Angel leaves, the pending loss is evaluated as normal on the very next
/// state-based check, so killing it is a genuine answer rather than merely stopping the bleeding.
///
/// KNOWN COST: an unanswered Angel means neither life nor decking can end the game, so it runs to
/// the simulator's turn cutoff and is reported as a flagged game. Accepted deliberately — see
/// DesignNotes.md.
/// </summary>
public record CannotLoseComponent : GameComponent { }
