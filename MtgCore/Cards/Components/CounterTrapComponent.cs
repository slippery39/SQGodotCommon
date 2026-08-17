using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// A counterspell, modelled as a TRAP that fires from hand rather than a spell you cast.
///
/// This engine has a stack but no priority — the non-active player never gets to act during your
/// turn — so a counterspell cannot be cast in response to anything. Rather than bolt on a priority
/// system, a counterspell simply sits in its owner's hand and fires automatically when the
/// opponent casts a matching spell, provided its owner left enough mana unspent.
///
/// "Leaving mana up" is already a real resource with no engine change: StartTurnAction refills
/// CurrentMana only for the ACTIVE player, so whatever the non-active player did not spend on
/// their own turn is still there during yours. That unspent mana is exactly what arms these.
///
/// Two consequences worth knowing:
///   - The trapper commits nothing and chooses nothing. Holding the card IS the decision, and the
///     cost is the mana you declined to spend. This is a real strategic difference from MTG, and
///     it is the design.
///   - The trap fires AFTER SpellCastEvent and the storm counter, because a countered spell was
///     still cast. Prowess and storm see it, matching the real rule.
/// </summary>
public record CounterTrapComponent : GameComponent
{
	/// <summary>
	/// Which spell types this can counter. Essence Scatter is Creature; Negate is everything
	/// except Creature (see <see cref="ExcludeTypes"/>); Mana Leak is any spell.
	/// </summary>
	public CardType TargetTypes { get; init; } = CardType.AnySpell | CardType.AnyPermanent;

	/// <summary>
	/// Types this will NOT counter, checked after <see cref="TargetTypes"/>. Negate is
	/// "noncreature spell", which is an exclusion rather than a list of everything else.
	/// </summary>
	public CardType ExcludeTypes { get; init; } = CardType.None;

	/// <summary>
	/// "Unless its controller pays N." 0 means a hard counter.
	///
	/// The caster pays automatically if they have the mana left after casting — they get no
	/// choice either, which keeps the mechanic symmetric and deterministic. The trap is spent
	/// whether or not the tax is paid, exactly as a real counterspell that resolves and does
	/// nothing still goes to the graveyard.
	/// </summary>
	public int ManaTax { get; init; } = 0;

	/// <summary>
	/// Clash of Wills is {X}{U}. A trap is never cast, so there is no moment to choose X — the
	/// tax instead becomes whatever mana the trapper still has after paying for the trap itself.
	/// Spending everything you held up is a fair reading of "X".
	/// </summary>
	public bool TaxAllRemaining { get; init; } = false;

	/// <summary>Dissipate — the countered card is exiled instead of going to the graveyard.</summary>
	public bool ExileInstead { get; init; } = false;

	/// <summary>Unsubstantiate — the countered card returns to its owner's hand.</summary>
	public bool ReturnToHandInstead { get; init; } = false;

	/// <summary>Bone to Ash — the trapper draws this many cards when it fires.</summary>
	public int DrawOnCounter { get; init; } = 0;

	/// <summary>True if this trap is allowed to counter the given card.</summary>
	public bool Matches(Card cast)
	{
		var types = cast.EffectiveTypes;

		if ((types & TargetTypes) == CardType.None)
			return false;

		return (types & ExcludeTypes) == CardType.None;
	}
}
