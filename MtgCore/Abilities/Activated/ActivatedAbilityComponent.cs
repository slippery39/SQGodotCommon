using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Marks a card as having an activated ability.
/// A card may have multiple ActivatedAbilityComponents — one per ability.
///
/// MaxActivationsPerTurn controls how many times the ability may fire per turn.
/// 1 (default) = once per turn. 0 = unlimited (e.g. Arcbound Ravager).
/// ActivationCount tracks uses this turn and is reset to 0 by StartTurnAction.
///
/// ManaCost is the primary mana cost (0 = free). AdditionalCosts holds any
/// extra costs beyond mana (sacrifice, discard, life payment, etc.).
/// The effect uses the existing CardEffect infrastructure — same targeting and
/// action template system as spells.
/// </summary>
public record ActivatedAbilityComponent : GameComponent
{
	public string Name { get; init; } = "";
	public int ManaCost { get; init; }
	public ImmutableList<AdditionalCost> AdditionalCosts { get; init; } =
		ImmutableList<AdditionalCost>.Empty;

	/// <summary>
	/// Effects resolve in order through a single ResolveEffectAction. This is a LIST — read it,
	/// not Effect. An ability like "discard a card, then draw a card" is two effects, and when
	/// this held only one the second half was silently dropped at build time.
	/// </summary>
	public ImmutableList<CardEffect> Effects { get; init; } = ImmutableList<CardEffect>.Empty;

	/// <summary>
	/// Convenience for the common single-effect case: <c>Effect = ...</c> appends to
	/// <see cref="Effects"/>. Write-only by design — read <see cref="Effects"/>.
	/// </summary>
	public CardEffect Effect
	{
		init => Effects = Effects.Add(value);
	}

	/// <summary>
	/// The effect that carries targeting. Only the first is offered a target, which keeps the
	/// action generator from having to enumerate target combinations.
	/// </summary>
	public CardEffect? TargetedEffect => Effects.Count > 0 ? Effects[0] : null;

	public int MaxActivationsPerTurn { get; init; } = 1;
	public int ActivationCount { get; init; } = 0;

	/// <summary>
	/// When true, activating exhausts the creature (see CreatureComponent.IsExhausted) and the
	/// ability cannot be activated while it is already exhausted or summoning-sick.
	/// </summary>
	public bool RequiresTap { get; init; } = false;

	/// <summary>
	/// Optional gate — "activate only if...". Null means no gate.
	/// Enforced by both ActivateAbilityAction.ValidateAdd and MtgActionGenerator.
	/// </summary>
	public ActivationCondition? Condition { get; init; } = null;

	/// <summary>
	/// True for a planeswalker loyalty ability. Needed as an explicit flag rather than inferred
	/// from a nonzero LoyaltyCost, because a 0-cost loyalty ability is a real thing
	/// (Gideon Jura's third ability) and would otherwise read as "not a loyalty ability".
	/// </summary>
	public bool IsLoyaltyAbility { get; init; } = false;

	/// <summary>
	/// Loyalty added on activation. Positive for "+1", negative for "-3", zero for "0".
	/// Only meaningful when IsLoyaltyAbility is true; the ability is illegal if it would take
	/// loyalty below zero.
	/// </summary>
	public int LoyaltyCost { get; init; } = 0;
}
