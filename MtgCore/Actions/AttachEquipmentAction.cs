using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Attaches an equipment card to a target creature.
///
/// Used as the ActionTemplate in an equipment card's ActivatedAbilityComponent.
/// ResolveEffectAction calls WithTargets to inject the chosen creature, and injects
/// ContextKeys.SourceCardId into InputContext so this action can find the equipment card.
///
/// On execution:
///   1. Removes EquippedBoostComponent from the previously-equipped creature (if any)
///   2. Updates EquipmentComponent.EquippedToCardId on the equipment card
///   3. Stamps a new EquippedBoostComponent onto the target creature
///
/// Implements ITargetedAction so ResolveEffectAction can inject resolved targets at
/// resolution time — the ActionTemplate stored on the card carries no target data.
/// </summary>
public record AttachEquipmentAction : GameAction, ITargetedAction
{
	public string EquipmentCardIdContextKey { get; init; } = ContextKeys.SourceCardId;
	public ImmutableList<int> TargetIds { get; init; } = ImmutableList<int>.Empty;

	public GameAction WithTargets(ImmutableList<int> targetIds) =>
		this with
		{
			TargetIds = targetIds,
		};

	public override ActionResult Execute(GameState gameState)
	{
		if (TargetIds.IsEmpty)
			return new ActionResult(gameState);

		var equipmentId = GetInput<int>(EquipmentCardIdContextKey, 0);
		if (equipmentId == 0)
			return new ActionResult(gameState);
		var targetCreatureId = TargetIds[0];

		if (!gameState.HasObject(equipmentId) || !gameState.HasObject(targetCreatureId))
			return new ActionResult(gameState);

		var equipment = gameState.GetObject(equipmentId) as Card;
		if (equipment == null)
			return new ActionResult(gameState);

		var equip = equipment.GetComponent<EquipmentComponent>();
		if (equip == null)
			return new ActionResult(gameState);

		var state = gameState;

		// Remove boost from previously equipped creature
		if (equip.EquippedToCardId != 0 && state.HasObject(equip.EquippedToCardId))
		{
			var prev = (Card)state.GetObject(equip.EquippedToCardId);
			var cleaned = prev
				.Components.Where(c =>
					c is not PowerToughnessModifier m || m.SourceCardId != equipmentId
				)
				.ToImmutableList();
			state = state.UpdateObject(equip.EquippedToCardId, prev with { Components = cleaned });
		}

		// Update EquipmentComponent.EquippedToCardId
		var equipComponents = equipment.Components;
		for (int i = 0; i < equipComponents.Count; i++)
		{
			if (equipComponents[i] is EquipmentComponent e)
			{
				equipComponents = equipComponents.SetItem(
					i,
					e with
					{
						EquippedToCardId = targetCreatureId,
					}
				);
				break;
			}
		}
		state = state.UpdateObject(equipmentId, equipment with { Components = equipComponents });

		// Stamp boost onto target creature
		var target = (Card)state.GetObject(targetCreatureId);
		var boost =
			equip.CustomBoostTemplate != null
				? equip.CustomBoostTemplate with
				{
					SourceCardId = equipmentId,
					Duration = ModifierDuration.Permanent,
				}
				: (PowerToughnessModifier)
					new EquippedBoostComponent
					{
						SourceCardId = equipmentId,
						PowerBonus = equip.PowerBonus,
						ToughnessBonus = equip.ToughnessBonus,
						Duration = ModifierDuration.Permanent,
					};
		state = state.UpdateObject(
			targetCreatureId,
			target with
			{
				Components = target.Components.Add(boost),
			}
		);

		return new ActionResult(state);
	}
}
