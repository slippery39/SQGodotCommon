using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Marks a card as having a triggered ability.
/// A card may have multiple TriggeredAbilityComponents — one per ability.
///
/// When the PostActionProcessor runs after a resolution scope closes, it scans
/// all permanents in ActiveInZone for TriggeredAbilityComponents, evaluates each
/// Condition against the PendingTriggerEvents, and spawns a
/// ResolveTriggeredAbilityAction for each match.
///
/// ActiveInZone defaults to Battlefield. Set to Graveyard for abilities that fire
/// while the card is in the graveyard (e.g. Bloodghast landfall).
///
/// The Effect uses the existing CardEffect infrastructure — same targeting and
/// action template system as spells and activated abilities.
/// </summary>
public record TriggeredAbilityComponent : GameComponent
{
	public string Name { get; init; } = "";
	public TriggerCondition Condition { get; init; } = null!;
	public CardEffect Effect { get; init; } = null!;
	public ZoneType ActiveInZone { get; init; } = ZoneType.Battlefield;
}
