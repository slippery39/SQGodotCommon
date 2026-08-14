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
	bool HasTrample,
	bool HasShroud,
	bool HasHexproof,
	bool HasDeathtouch
)
{
	/// All-false stats for a missing card or a non-creature.
	public static readonly CreatureStats None =
		new(0, 0, false, false, false, false, false, false, false, false, false);
}

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
			return CreatureStats.None;

		var creature = card.GetComponent<CreatureComponent>();
		if (creature == null)
			return CreatureStats.None;

		var power = creature.Power;
		var toughness = creature.Toughness;
		var hasHaste = creature.HasHaste;
		var hasFlying = creature.HasFlying;
		var hasTaunt = creature.HasTaunt;
		var hasReach = creature.HasReach;
		var hasLifelink = creature.HasLifelink;
		var hasTrample = creature.HasTrample;
		var hasShroud = creature.HasShroud;
		var hasHexproof = creature.HasHexproof;
		var hasDeathtouch = creature.HasDeathtouch;

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
			hasShroud |= applied.GrantsShroud;
			hasHexproof |= applied.GrantsHexproof;
			hasDeathtouch |= applied.GrantsDeathtouch;
		}

		// Threshold keyword grants are evaluated live rather than stamped, because the
		// graveyard count that gates them changes without any battlefield event firing.
		// The P/T half is already covered by the PowerToughnessModifier loop above.
		foreach (var threshold in card.GetComponents<ThresholdComponent>())
		{
			if (!threshold.IsActive(state, cardId))
				continue;

			hasHaste |= threshold.GrantsHaste;
			hasFlying |= threshold.GrantsFlying;
			hasTaunt |= threshold.GrantsTaunt;
			hasReach |= threshold.GrantsReach;
			hasLifelink |= threshold.GrantsLifelink;
			hasTrample |= threshold.GrantsTrample;
			hasShroud |= threshold.GrantsShroud;
			hasHexproof |= threshold.GrantsHexproof;
			hasDeathtouch |= threshold.GrantsDeathtouch;
		}

		return new CreatureStats(
			power,
			toughness,
			hasHaste,
			hasFlying,
			hasTaunt,
			hasReach,
			hasLifelink,
			hasTrample,
			hasShroud,
			hasHexproof,
			hasDeathtouch
		);
	}

	public static int GetEffectivePower(this GameState state, int cardId) =>
		state.GetEffectiveStats(cardId).Power;

	/// <summary>
	/// Returns the creature's power counting only permanent modifiers — excludes
	/// UntilEndOfTurn buffs such as Giant Growth. Used by StateEvaluator so that
	/// temporary pumps are not counted as lasting board advantage.
	/// Combat code must continue using GetEffectivePower; temporary buffs are valid during combat.
	/// </summary>
	public static int GetEffectivePermanentPower(this GameState state, int cardId)
	{
		var card = state.GetObject(cardId) as Card;
		if (card == null)
			return 0;

		var creature = card.GetComponent<CreatureComponent>();
		if (creature == null)
			return 0;

		var power = creature.Power;
		foreach (
			var modifier in card.GetComponents<PowerToughnessModifier>()
				.Where(m => m.Duration != ModifierDuration.UntilEndOfTurn)
		)
		{
			power += modifier.GetPowerBonus(state, cardId);
		}
		return power;
	}

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

	public static bool GetEffectiveShroud(this GameState state, int cardId) =>
		state.GetEffectiveStats(cardId).HasShroud;

	public static bool GetEffectiveHexproof(this GameState state, int cardId) =>
		state.GetEffectiveStats(cardId).HasHexproof;

	public static bool GetEffectiveDeathtouch(this GameState state, int cardId) =>
		state.GetEffectiveStats(cardId).HasDeathtouch;

	/// <summary>
	/// Returns true if the creature has accumulated damage >= its effective toughness.
	/// </summary>
	public static bool HasLethalDamage(this GameState state, int cardId) =>
		state.IsLethalDamage(cardId, DamageOn(state, cardId), fromDeathtouch: false);

	/// <summary>
	/// The single lethality rule for damage. Both combat (AttackAction) and effect damage
	/// (DealDamageAction) route through this so deathtouch cannot work in one and not the other.
	///
	/// Deathtouch is a property of the damage SOURCE, not the creature taking it, so callers
	/// pass it in. Any nonzero deathtouch damage is lethal immediately — there is no lingering
	/// "deathtouched" state to persist, which is why no marker is stored on the creature.
	/// </summary>
	public static bool IsLethalDamage(
		this GameState state,
		int cardId,
		int totalDamage,
		bool fromDeathtouch
	)
	{
		if (state.GetObject(cardId) is not Card card)
			return false;
		if (card.GetComponent<CreatureComponent>() == null)
			return false;

		if (fromDeathtouch && totalDamage > 0)
			return true;

		return totalDamage >= state.GetEffectiveStats(cardId).Toughness;
	}

	private static int DamageOn(GameState state, int cardId) =>
		(state.GetObject(cardId) as Card)?.GetComponent<CreatureComponent>()?.Damage ?? 0;

	/// <summary>
	/// Removes all UntilEndOfTurn PowerToughnessModifiers and AppliedKeywordComponents from a card.
	/// Called by StartTurnAction at the start of each turn.
	///
	/// Both temporary buff kinds are cleared here so a combat trick and a temporary keyword
	/// grant expire at exactly the same moment.
	/// </summary>
	public static GameState ClearEndOfTurnModifiers(this GameState state, int cardId)
	{
		var card = state.GetObject(cardId) as Card;
		if (card == null)
			return state;

		var hasEndOfTurnModifiers =
			card.GetComponents<PowerToughnessModifier>()
				.Any(m => m.Duration == ModifierDuration.UntilEndOfTurn)
			|| card.GetComponents<AppliedKeywordComponent>()
				.Any(k => k.Duration == ModifierDuration.UntilEndOfTurn);

		if (!hasEndOfTurnModifiers)
			return state;

		var updatedComponents = card
			.Components.Where(c =>
				c switch
				{
					PowerToughnessModifier m => m.Duration != ModifierDuration.UntilEndOfTurn,
					AppliedKeywordComponent k => k.Duration != ModifierDuration.UntilEndOfTurn,
					_ => true,
				}
			)
			.ToImmutableArray();

		return state.UpdateObject(cardId, card with { Components = updatedComponents });
	}
}
