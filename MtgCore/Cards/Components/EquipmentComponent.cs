using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Marks a card as equipment and holds its stat bonuses and attachment state.
/// Equipment stays on the battlefield after casting and can be attached to creatures
/// you control via an ActivatedAbilityComponent whose ActionTemplate is AttachEquipmentAction.
///
/// EquippedToCardId tracks the creature currently wearing this equipment (0 = unequipped).
/// Maintained by AttachEquipmentAction (attach) and CheckStateBasedEffectsAction (detach on death).
///
/// PowerBonus / ToughnessBonus are copied onto the creature as an EquippedBoostComponent
/// when the equipment attaches. They are not read directly from here at evaluation time.
/// </summary>
public record EquipmentComponent : GameComponent
{
	public int PowerBonus { get; init; }
	public int ToughnessBonus { get; init; }
	public int EquippedToCardId { get; init; } = 0;
	public PowerToughnessModifier? CustomBoostTemplate { get; init; } = null;
}
