using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "Equipped creature" / "enchanted creature" — the permanent that the SOURCE of this effect is
/// currently attached to.
///
/// This is the piece that lets an attachment address its own wearer from a trigger of its own.
/// The five Rings say "at the beginning of your upkeep, put a +1/+1 counter on equipped creature"
/// and Sword of the Animist says "whenever equipped creature attacks" — in both cases the trigger
/// lives on the equipment, which is a non-creature permanent, and the effect has to land on a
/// completely different card.
///
/// No new context key was needed: TargetingContext already carries SourceCardId, which is exactly
/// the equipment. Without this spec the Rings collapse into five near-identical vanilla equipment,
/// which the Hollowmere rate rule forbids.
///
/// Works for Auras as well as Equipment — EquipmentComponent.IsAura is the only difference between
/// the two and it does not change where EquippedToCardId points.
/// </summary>
public record IsEquippedBySourceSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (context.Find(context.SourceCardId) is not Card source)
			return false;

		var equipment = source.GetComponent<EquipmentComponent>();
		if (equipment == null || equipment.EquippedToCardId == 0)
			return false;

		return equipment.EquippedToCardId == candidateId;
	}
}
