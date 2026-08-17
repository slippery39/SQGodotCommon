namespace MtgCore;

/// <summary>
/// Static ability that grants keyword abilities to all creatures matching Filter.
/// Used for lord effects that grant keywords (e.g. Goblin Chieftain: other Goblins gain haste).
/// </summary>
public record StaticGrantKeywordAbility : StaticAbilityComponent
{
	public bool GrantsHaste { get; init; } = false;
	public bool GrantsFlying { get; init; } = false;
	public bool GrantsTaunt { get; init; } = false;
	public bool GrantsReach { get; init; } = false;
	public bool GrantsLifelink { get; init; } = false;
	public bool GrantsTrample { get; init; } = false;
	public bool GrantsShroud { get; init; } = false;
	public bool GrantsHexproof { get; init; } = false;
	public bool GrantsDeathtouch { get; init; } = false;
	public bool GrantsFirstStrike { get; init; } = false;
	public bool GrantsDoubleStrike { get; init; } = false;
	public bool GrantsIndestructible { get; init; } = false;

	/// <summary>
	/// Grants exalted. Unlike the other keywords this is COUNTED, not just tested: each
	/// granted instance adds +1/+1 when a creature attacks alone, so Sublime Archangel's
	/// "other creatures you control have exalted" stacks with their own instances.
	/// See AttackAction.CountExaltedInstances.
	/// </summary>
	public bool GrantsExalted { get; init; } = false;
}
