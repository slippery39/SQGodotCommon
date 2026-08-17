using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Threshold — a conditional buff that switches on while the controller's graveyard holds
/// at least Minimum cards. Grants P/T and, optionally, keywords.
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
	/// Cards required in the controller's graveyard for the buff to be active.
	public int Minimum { get; init; } = 7;

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
	/// True while the controller's graveyard holds at least Minimum cards.
	/// Counted live — never cached — so mill and discard flip it immediately.
	/// </summary>
	public bool IsActive(GameState state, int cardId)
	{
		if (state.GetObject(cardId) is not Card card)
			return false;

		var graveyardId = state.GetPlayerZoneId(card.ControllerId, ZoneType.Graveyard);
		return state.GetChildrenIds(graveyardId).Count() >= Minimum;
	}
}
