using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "Noncreature spells cost {1} more to cast." — Vryn Wingmare.
///
/// Read by CostEngine.ComputeEffectiveCost, which scans both battlefields: this taxes its own
/// controller too, exactly as printed.
/// </summary>
public record SpellTaxComponent : GameComponent
{
	public int Amount { get; init; } = 1;

	/// <summary>
	/// When true only noncreature spells are taxed. Creature-ness is decided by
	/// CreatureComponent, so a creature-land or an animated artifact is handled correctly.
	/// </summary>
	public bool NonCreatureOnly { get; init; } = true;
}
