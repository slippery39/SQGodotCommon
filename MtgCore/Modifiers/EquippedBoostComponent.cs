using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// P/T modifier stamped onto a creature when equipment attaches to it.
/// SourceCardId identifies the equipment card so the boost can be removed
/// when the equipment detaches or moves to a different creature.
///
/// Extends PowerToughnessModifier so CreatureEvaluator picks it up in its
/// normal component scan — no special-casing required.
///
/// Managed exclusively by AttachEquipmentAction and CheckStateBasedEffectsAction.
/// </summary>
public record EquippedBoostComponent : PowerToughnessModifier
{
	public int PowerBonus { get; init; }
	public int ToughnessBonus { get; init; }

	public override int GetPowerBonus(GameState state, int cardId) => PowerBonus;

	public override int GetToughnessBonus(GameState state, int cardId) => ToughnessBonus;
}
