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
}
