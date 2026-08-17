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
