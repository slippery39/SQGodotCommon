namespace MtgCore;

/// <summary>
/// Static ability that grants keyword abilities to all creatures matching Filter.
/// Used for lord effects that grant keywords (e.g. Goblin Chieftain: other Goblins gain haste).
/// </summary>
public record StaticGrantKeywordAbility : StaticAbilityComponent
{
	public bool GrantsHaste { get; init; } = false;
}
