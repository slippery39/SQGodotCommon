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

	/// <summary>
	/// This attachment is an AURA, not an Equipment.
	///
	/// Auras ride the same attachment rails — one mechanism, not two — and differ in exactly
	/// two ways, both handled by the flag:
	///   - an aura attaches once, on entering the battlefield, instead of via a repeatable
	///     equip ability
	///   - when the enchanted permanent leaves, the aura goes to the graveyard, whereas an
	///     equipment merely detaches and stays on the battlefield
	///
	/// The keyword grants and the can't-attack flag live on EquippedBoostComponent, so an aura
	/// that only grants keywords (Pacifism) still works with 0/0 stat bonuses.
	/// </summary>
	public bool IsAura { get; init; } = false;
}
