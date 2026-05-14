using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Snapshot of all effective stats for a creature, computed from the card's own components.
/// Use GetEffectiveStats when multiple properties are needed to avoid redundant reads.
/// </summary>
public record CreatureStats(
	int Power,
	int Toughness,
	bool HasHaste,
	bool HasFlying,
	bool HasTaunt,
	bool HasReach,
	bool HasLifelink,
	bool HasTrample
);

/// <summary>
/// Computes effective P/T and keywords for creatures from components stamped onto the card.
/// No battlefield scan — all values are O(1) reads from the card's own component list.
///
/// Static ability effects (lord/anthem P/T boosts and keyword grants) are pre-computed by
/// StaticAbilityEngine and stored as AppliedStaticPTBoost and AppliedKeywordComponent
/// on each affected permanent. GetEffectiveStats reads those components directly.
///
/// Prefer GetEffectiveStats when multiple properties are needed. The individual methods
/// (GetEffectivePower, etc.) delegate to it and are safe to call for single-property reads.
///
/// Both game actions and the UI call these methods to get effective values.
/// </summary>
public static class CreatureEvaluator
{
	/// <summary>
	/// Computes all effective stats for a creature from its own components — no board scan.
	/// </summary>
	public static CreatureStats GetEffectiveStats(this GameState state, int cardId)
	{
		var card = state.GetObject(cardId) as Card;
		if (card == null)
			return new CreatureStats(0, 0, false, false, false, false, false, false);

		var creature = card.GetComponent<CreatureComponent>();
		if (creature == null)
			return new CreatureStats(0, 0, false, false, false, false, false, false);

		var power = creature.Power;
		var toughness = creature.Toughness;
		var hasHaste = creature.HasHaste;
		var hasFlying = creature.HasFlying;
		var hasTaunt = creature.HasTaunt;
		var hasReach = creature.HasReach;
		var hasLifelink = creature.HasLifelink;
		var hasTrample = creature.HasTrample;

		// Spell-based and static-ability-based P/T modifiers (AppliedStaticPTBoost is a subtype)
		foreach (var modifier in card.GetComponents<PowerToughnessModifier>())
		{
			power += modifier.GetPowerBonus(state, cardId);
			toughness += modifier.GetToughnessBonus(state, cardId);
		}

		// Keyword grants from static abilities (stamped by StaticAbilityEngine)
		foreach (var applied in card.GetComponents<AppliedKeywordComponent>())
		{
			hasHaste |= applied.GrantsHaste;
			hasFlying |= applied.GrantsFlying;
			hasTaunt |= applied.GrantsTaunt;
			hasReach |= applied.GrantsReach;
			hasLifelink |= applied.GrantsLifelink;
			hasTrample |= applied.GrantsTrample;
		}

		return new CreatureStats(
			power,
			toughness,
			hasHaste,
			hasFlying,
			hasTaunt,
			hasReach,
			hasLifelink,
			hasTrample
		);
	}

	public static int GetEffectivePower(this GameState state, int cardId) =>
		state.GetEffectiveStats(cardId).Power;

	public static int GetEffectiveToughness(this GameState state, int cardId) =>
		state.GetEffectiveStats(cardId).Toughness;

	public static bool GetEffectiveHaste(this GameState state, int cardId) =>
		state.GetEffectiveStats(cardId).HasHaste;

	public static bool GetEffectiveFlying(this GameState state, int cardId) =>
		state.GetEffectiveStats(cardId).HasFlying;

	public static bool GetEffectiveTaunt(this GameState state, int cardId) =>
		state.GetEffectiveStats(cardId).HasTaunt;

	public static bool GetEffectiveReach(this GameState state, int cardId) =>
		state.GetEffectiveStats(cardId).HasReach;

	/// <summary>
	/// Returns true if the creature has accumulated damage >= its effective toughness.
	/// </summary>
	public static bool HasLethalDamage(this GameState state, int cardId)
	{
		var card = state.GetObject(cardId) as Card;
		if (card == null)
			return false;

		var creature = card.GetComponent<CreatureComponent>();
		if (creature == null)
			return false;

		return creature.Damage >= state.GetEffectiveStats(cardId).Toughness;
	}

	/// <summary>
	/// Removes all UntilEndOfTurn PowerToughnessModifiers from a card.
	/// Called by StartTurnAction at the start of each turn.
	/// </summary>
	public static GameState ClearEndOfTurnModifiers(this GameState state, int cardId)
	{
		var card = state.GetObject(cardId) as Card;
		if (card == null)
			return state;

		var hasEndOfTurnModifiers = card.GetComponents<PowerToughnessModifier>()
			.Any(m => m.Duration == ModifierDuration.UntilEndOfTurn);

		if (!hasEndOfTurnModifiers)
			return state;

		var updatedComponents = card
			.Components.Where(c =>
				c is not PowerToughnessModifier m || m.Duration != ModifierDuration.UntilEndOfTurn
			)
			.ToImmutableList();

		return state.UpdateObject(cardId, card with { Components = updatedComponents });
	}
}
