using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// What ThresholdComponent counts to decide whether its buff is on.
/// </summary>
public enum ThresholdSource
{
	/// <summary>Cards in the controller's graveyard — Threshold proper.</summary>
	GraveyardCards,

	/// <summary>
	/// +1/+1 counters on this creature. Primordial Hydra's "has trample as long as it has ten or
	/// more +1/+1 counters on it".
	/// </summary>
	PlusOneCounters,

	/// <summary>
	/// Enchantments the controller has on the battlefield — Blood-Cursed Knight's "as long as you
	/// control an enchantment, this gets +1/+1 and has lifelink".
	///
	/// This is the case MtgCore/CLAUDE.md's "conditional static abilities" deferral explicitly
	/// carves out: a conditional TEAM anthem has no home, because StaticAbilityEngine is a push
	/// model and would go stale, but a live-evaluated modifier on a SINGLE creature dodges that
	/// entirely. One enum value and one case, rather than the parallel component type the deferral
	/// was written about.
	/// </summary>
	ControlledEnchantments,
}

/// <summary>
/// Threshold — a conditional buff that switches on while the controller's graveyard holds
/// at least Minimum cards. Grants P/T and, optionally, keywords.
///
/// CountSource retargets that question without a second component type. A counter-gated keyword
/// has identical mechanics — a number compared against Minimum, read live because a push model
/// would go stale — so a parallel type would be one more place to forget Duration = Permanent
/// and one more Grants* list to keep in sync with the six-site keyword rule. Same reasoning as
/// CreatureCountComponent.Subtype.
///
/// Deliberately NOT built on StaticAbilityEngine. That engine is a push model: it stamps
/// AppliedStaticPTBoost / AppliedKeywordComponent onto affected permanents and re-stamps only
/// in response to CreatureEnteredBattlefieldEvent and PermanentLeftBattlefieldEvent. A
/// threshold keyed to graveyard size would go stale the instant a card was milled, discarded,
/// or exiled, because none of those events trigger a re-stamp.
///
/// Instead this is a live-evaluated PowerToughnessModifier: CreatureEvaluator calls
/// GetPowerBonus/GetToughnessBonus on every read, so the graveyard is counted at read time
/// and the buff is always current. GetEffectiveStats reads the keyword half directly for the
/// same reason. That makes threshold both cheaper and correct — no engine changes required.
///
/// Set Duration = ModifierDuration.Permanent in card definitions so StartTurnAction's
/// end-of-turn cleanup does not strip it.
/// </summary>
public record ThresholdComponent : PowerToughnessModifier
{
	/// How many of CountSource are required for the buff to be active.
	public int Minimum { get; init; } = 7;

	/// What is counted. Defaults to the graveyard, which is what every card using this predates.
	public ThresholdSource CountSource { get; init; } = ThresholdSource.GraveyardCards;

	public int PowerBonus { get; init; } = 0;
	public int ToughnessBonus { get; init; } = 0;

	public bool GrantsHaste { get; init; } = false;
	public bool GrantsFlying { get; init; } = false;
	public bool GrantsTaunt { get; init; } = false;
	public bool GrantsReach { get; init; } = false;
	public bool GrantsLifelink { get; init; } = false;
	public bool GrantsTrample { get; init; } = false;
	public bool GrantsShroud { get; init; } = false;
	public bool GrantsHexproof { get; init; } = false;
	public bool GrantsDeathtouch { get; init; } = false;
	public bool GrantsFirstStrike { get; init; } = false;
	public bool GrantsDoubleStrike { get; init; } = false;
	public bool GrantsIndestructible { get; init; } = false;

	public override int GetPowerBonus(GameState state, int cardId) =>
		IsActive(state, cardId) ? PowerBonus : 0;

	public override int GetToughnessBonus(GameState state, int cardId) =>
		IsActive(state, cardId) ? ToughnessBonus : 0;

	/// <summary>
	/// True while CountSource has reached Minimum.
	/// Counted live — never cached — so mill, discard and counter changes flip it immediately.
	/// </summary>
	public bool IsActive(GameState state, int cardId)
	{
		if (state.GetObject(cardId) is not Card card)
			return false;

		if (CountSource == ThresholdSource.PlusOneCounters)
			return (card.GetComponent<PlusOneCounterComponent>()?.Count ?? 0) >= Minimum;

		if (CountSource == ThresholdSource.ControlledEnchantments)
		{
			var battlefieldId = state.GetPlayerZoneId(card.ControllerId, ZoneType.Battlefield);
			if (battlefieldId == 0)
				return false;

			return state
					.GetCardsInZone(battlefieldId)
					.Count(c =>
						c.ControllerId == card.ControllerId && c.HasType(CardType.Enchantment)
					) >= Minimum;
		}

		var graveyardId = state.GetPlayerZoneId(card.ControllerId, ZoneType.Graveyard);
		return state.GetChildrenIds(graveyardId).Count() >= Minimum;
	}
}
