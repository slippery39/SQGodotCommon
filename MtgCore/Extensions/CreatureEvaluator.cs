using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Computes effective P/T and keywords for creatures by aggregating:
///   Pass 1 — PowerToughnessModifier components on the card itself (Giant Growth, etc.)
///   Pass 2 — StaticAbilityComponent on battlefield permanents (lord/anthem effects)
///
/// Values are always derived at read time — never stored as computed state.
/// Both game actions and the UI call these methods to get effective values.
/// </summary>
public static class CreatureEvaluator
{
	public static int GetEffectivePower(this GameState state, int cardId)
	{
		var card = state.GetObject(cardId) as Card;
		if (card == null)
			return 0;

		var creature = card.GetComponent<CreatureComponent>();
		if (creature == null)
			return 0;

		var power = creature.Power;

		foreach (var modifier in card.GetComponents<PowerToughnessModifier>())
			power += modifier.GetPowerBonus(state, cardId);

		foreach (
			var (_, ability) in GetApplicableStaticAbilities<StaticPTBoostAbility>(state, cardId)
		)
			power += ability.PowerBonus;

		return power;
	}

	public static int GetEffectiveToughness(this GameState state, int cardId)
	{
		var card = state.GetObject(cardId) as Card;
		if (card == null)
			return 0;

		var creature = card.GetComponent<CreatureComponent>();
		if (creature == null)
			return 0;

		var toughness = creature.Toughness;

		foreach (var modifier in card.GetComponents<PowerToughnessModifier>())
			toughness += modifier.GetToughnessBonus(state, cardId);

		foreach (
			var (_, ability) in GetApplicableStaticAbilities<StaticPTBoostAbility>(state, cardId)
		)
			toughness += ability.ToughnessBonus;

		return toughness;
	}

	/// <summary>
	/// Returns true if the creature has haste — either intrinsic (HasHaste on CreatureComponent)
	/// or granted by a StaticGrantKeywordAbility on a battlefield permanent.
	/// </summary>
	public static bool GetEffectiveHaste(this GameState state, int cardId)
	{
		var card = state.GetObject(cardId) as Card;
		if (card == null)
			return false;

		var creature = card.GetComponent<CreatureComponent>();
		if (creature == null)
			return false;

		if (creature.HasHaste)
			return true;

		return GetApplicableStaticAbilities<StaticGrantKeywordAbility>(state, cardId)
			.Any(x => x.Ability.GrantsHaste);
	}

	/// <summary>
	/// Scans all battlefield permanents for StaticAbilityComponents of type T that apply
	/// to the given card. SourceCardId on the targeting context is set to the permanent's ID
	/// so that IsNotSelfSpecification works correctly for "other creatures" filters.
	/// Only permanents controlled by the same player are scanned (sufficient for current cards).
	/// </summary>
	private static IEnumerable<(int SourceId, T Ability)> GetApplicableStaticAbilities<T>(
		GameState state,
		int cardId
	)
		where T : StaticAbilityComponent
	{
		var card = state.GetObject(cardId) as Card;
		if (card == null)
			yield break;

		var battlefieldId = state.GetPlayerZoneId(card.ControllerId, ZoneType.Battlefield);

		foreach (var permanent in state.GetCardsInZone(battlefieldId))
		{
			foreach (var ability in permanent.GetComponents<T>())
			{
				var context = new TargetingContext
				{
					GameState = state,
					CastingPlayerId = card.ControllerId,
					SourceCardId = permanent.Id,
				};

				if (ability.Filter.IsSatisfiedBy(cardId, context))
					yield return (permanent.Id, ability);
			}
		}
	}

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

		return creature.Damage >= state.GetEffectiveToughness(cardId);
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
