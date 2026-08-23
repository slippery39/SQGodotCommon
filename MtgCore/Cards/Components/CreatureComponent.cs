using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Marks a card as a creature and holds its combat-relevant data.
/// A card with this component can enter the battlefield, attack, block,
/// and receive damage.
///
/// Base power and toughness are the printed values on the card.
/// Damage is marked damage accumulated this turn and resets at end of turn.
/// HasSummoningSickness prevents attacking the turn the creature enters
/// the battlefield — cleared at the start of the controller's next turn.
/// HasAttacked prevents a creature from attacking more than once per turn —
/// cleared at the start of the controller's next turn alongside summoning sickness.
/// </summary>
public record CreatureComponent : GameComponent
{
	public int Power { get; init; }
	public int Toughness { get; init; }
	public int Damage { get; init; } = 0;
	public bool HasSummoningSickness { get; init; } = true;
	public bool HasAttacked { get; init; } = false;

	/// <summary>
	/// This engine's equivalent of being tapped. An exhausted creature cannot attack and
	/// cannot activate abilities that require tapping. Set by ExhaustCreatureAction ("tappers")
	/// and by activating a RequiresTap ability; cleared by StartTurnAction for the active
	/// player only — so exhausting an opponent's creature on your turn costs them exactly one
	/// attack, matching an untap step.
	///
	/// Deliberately SEPARATE from HasAttacked. Attacking does not set this, so vigilance
	/// remains an open design question rather than being decided by implication here.
	/// </summary>
	public bool IsExhausted { get; init; } = false;

	/// <summary>
	/// Extra untap steps this creature must sit out — "doesn't untap during its controller's next
	/// untap step". StartTurnAction decrements this instead of clearing IsExhausted while it is
	/// above zero, so one point equals one additional turn frozen.
	///
	/// A plain tapper leaves this at 0 and the creature untaps normally next turn.
	/// </summary>
	public int FrozenTurns { get; init; } = 0;

	/// <summary>
	/// "Doesn't untap for as long as you control this" (Dungeon Geists). Holds the id of the
	/// permanent maintaining the lock; while that card is on the battlefield the creature never
	/// untaps, and CheckStateBasedEffectsAction releases it when the source leaves.
	///
	/// Distinct from FrozenTurns because the duration is not a number of turns — it is a
	/// dependency on another permanent, and a turn count could not express "indefinitely".
	/// </summary>
	public int FrozenBySourceId { get; init; } = 0;

	/// <summary>
	/// COVER N — "this creature can't be attacked for N of your turns, or until it attacks".
	///
	/// The counterpart to Taunt, and the answer to the problem that a utility creature in a
	/// blockerless game must be printed with high toughness or it never survives to do its job.
	/// Cover buys that survival on the DEFENSIVE axis instead, so a 1/1 with a real ability can
	/// exist without being statted like a wall.
	///
	/// Burns down one per controller's turn in StartTurnAction, exactly like FrozenTurns, so
	/// Cover 1 means "survives the opponent's next turn". Attacking spends it immediately
	/// (AttackAction clears it on the attacker): the creature is either hiding or fighting, and
	/// without that clause Cover would be pure upside on an aggressive creature rather than
	/// protection for a slow one.
	///
	/// NOT a keyword flag, so the six-site keyword rule does not apply — it is a countdown like
	/// FrozenTurns, cannot currently be granted by an aura or equipment, and lives only here.
	/// </summary>
	public int CoverTurns { get; init; } = 0;

	/// <summary>
	/// This creature was attacked this turn. Set by AttackAction on the TARGET, cleared by
	/// StartTurnAction. Read by TauntUntilAttackedComponent so Fog Bank soaks exactly one attack
	/// per turn and then stops compelling — otherwise a damage-immune Taunt wall is unanswerable
	/// in an engine with no way to go wide.
	/// </summary>
	public bool WasAttackedThisTurn { get; init; } = false;

	public bool HasHaste { get; init; } = false;
	public bool HasDoubleStrike { get; init; } = false;

	/// <summary>
	/// Deals combat damage before creatures without it. With no blockers this makes every
	/// attack into a favourable creature a one-sided trade, so it is a premium keyword here —
	/// see the note in AttackAction.ApplyCreatureVsCreature.
	/// </summary>
	public bool HasFirstStrike { get; init; } = false;

	/// <summary>
	/// Damage and "destroy" effects do not kill this creature. Zero effective toughness still
	/// does, which is the real MTG rule — see CheckStateBasedEffectsAction.
	/// </summary>
	public bool HasIndestructible { get; init; } = false;

	public bool HasFlying { get; init; } = false;
	public bool HasTaunt { get; init; } = false;
	public bool HasReach { get; init; } = false;
	public bool HasLifelink { get; init; } = false;
	public bool HasTrample { get; init; } = false;
	public bool HasShroud { get; init; } = false;
	public bool HasHexproof { get; init; } = false;

	/// <summary>
	/// Any nonzero damage from this creature is lethal to the creature it damages.
	/// Unusually strong in this engine's no-blocker combat — every attack becomes a
	/// favourable trade — so keep it rare in card design.
	/// </summary>
	public bool HasDeathtouch { get; init; } = false;
}
