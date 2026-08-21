using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "Until end of turn, target creature loses all abilities and becomes a 1/1."
/// — Turn to Frog, Polymorphist's Jest.
///
/// A PowerToughnessModifier rather than a rewrite of the creature's components, because the effect
/// is temporary and must fall off cleanly. Overwriting CreatureComponent would destroy the
/// original stats with nothing to restore them from.
///
/// It works by cancelling: GetPowerBonus returns whatever it takes to bring the creature's base
/// power to Power, so the final effective value is exactly Power regardless of what it was.
/// GetEffectiveStats also suppresses every keyword when one of these is present, which is the
/// "loses all abilities" half.
///
/// Stamped UntilEndOfTurn, so StartTurnAction's normal cleanup removes it — no special casing.
/// </summary>
public record BecomesBaseCreatureComponent : PowerToughnessModifier
{
	public int Power { get; init; } = 1;
	public int Toughness { get; init; } = 1;

	/// <summary>
	/// Keywords the new body HAS, granted after the suppression rather than before it.
	///
	/// "Becomes a 2/2 Bird with flying" (Skinshifter) is one effect, not two: it strips the
	/// creature's abilities and hands it a keyword in the same breath. A separate
	/// GrantKeywordAction cannot express that, because GetEffectiveStats deliberately applies
	/// this component's suppression after every grant — so the flying would be stripped by the
	/// very effect that is supposed to give it, and the mode would silently be a worse version
	/// of the plain-body mode.
	/// </summary>
	public bool GrantsFlying { get; init; } = false;

	public bool GrantsTrample { get; init; } = false;

	public bool GrantsReach { get; init; } = false;

	public override int GetPowerBonus(GameState state, int cardId) =>
		Power - BasePower(state, cardId);

	public override int GetToughnessBonus(GameState state, int cardId) =>
		Toughness - BaseToughness(state, cardId);

	private static int BasePower(GameState state, int cardId) =>
		(state.GetObject(cardId) as Card)?.GetComponent<CreatureComponent>()?.Power ?? 0;

	private static int BaseToughness(GameState state, int cardId) =>
		(state.GetObject(cardId) as Card)?.GetComponent<CreatureComponent>()?.Toughness ?? 0;
}
