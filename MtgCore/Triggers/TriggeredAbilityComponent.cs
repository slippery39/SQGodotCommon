using System.Collections.Immutable;
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
/// Effects use the existing CardEffect infrastructure — same targeting and action
/// template system as spells and activated abilities. They resolve in order through a
/// single ResolveEffectAction, which is what lets each one carry its own targeting
/// strategy (a PipelineAction cannot: pipeline steps read targets from context keys, so
/// mass "all valid" targeting is unavailable inside one).
/// </summary>
public record TriggeredAbilityComponent : GameComponent
{
	public string Name { get; init; } = "";
	public TriggerCondition Condition { get; init; } = null!;
	public ZoneType ActiveInZone { get; init; } = ZoneType.Battlefield;

	public ImmutableList<CardEffect> Effects { get; init; } = ImmutableList<CardEffect>.Empty;

	/// <summary>
	/// Convenience for the common single-effect case: <c>Effect = ...</c> appends to
	/// <see cref="Effects"/>. Write-only by design — read <see cref="Effects"/> instead, since
	/// an ability may now have several.
	/// </summary>
	public CardEffect Effect
	{
		init => Effects = Effects.Add(value);
	}
}
