using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "As long as your life total is N or more, this creature gets +X/+Y." — Angel of Vitality,
/// and later Path of Bravery.
///
/// Live-evaluated for the same reason ThresholdComponent is: the gating value (a life total)
/// changes on damage, lifelink, and drain, none of which fire the battlefield events
/// StaticAbilityEngine re-stamps on. A pushed value would go stale the moment anyone took damage.
///
/// Set Duration = ModifierDuration.Permanent in card definitions or StartTurnAction strips it.
/// </summary>
public record LifeTotalComponent : PowerToughnessModifier
{
	/// <summary>Life total at or above which the bonus applies.</summary>
	public int Minimum { get; init; } = 25;

	public int PowerBonus { get; init; } = 0;
	public int ToughnessBonus { get; init; } = 0;

	public override int GetPowerBonus(GameState state, int cardId) =>
		IsActive(state, cardId) ? PowerBonus : 0;

	public override int GetToughnessBonus(GameState state, int cardId) =>
		IsActive(state, cardId) ? ToughnessBonus : 0;

	public bool IsActive(GameState state, int cardId)
	{
		if (state.GetObject(cardId) is not Card card)
			return false;

		if (state.GetObject(card.ControllerId) is not MtgPlayer player)
			return false;

		return player.Life >= Minimum;
	}
}
