namespace MtgCore;

/// <summary>
/// Static ability that grants a fixed P/T bonus to all creatures matching Filter.
/// Used for lord/anthem effects (e.g. Goblin Chieftain: other Goblins get +1/+1).
/// </summary>
public record StaticPTBoostAbility : StaticAbilityComponent
{
	public int PowerBonus { get; init; } = 0;
	public int ToughnessBonus { get; init; } = 0;
}
