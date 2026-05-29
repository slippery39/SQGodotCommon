using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Dynamic P/T modifier that scales with the total number of lands the controlling
/// player has played this game (LandsPlayedTotal). Counts both lands played from hand
/// and lands put into play by effects (Rampant Growth, Primeval Titan ETB).
///
/// Used by Terravore: base Power = 0, base Toughness = 0, so effective P/T equals
/// the controller's LandsPlayedTotal at read time.
///
/// Must be stamped with Duration = Permanent when included in a card's component list
/// so StartTurnAction does not clear it.
/// </summary>
public record LandsPlayedCountComponent : PowerToughnessModifier
{
	public override int GetPowerBonus(GameState state, int cardId) =>
		GetLandsPlayedTotal(state, cardId);

	public override int GetToughnessBonus(GameState state, int cardId) =>
		GetLandsPlayedTotal(state, cardId);

	private static int GetLandsPlayedTotal(GameState state, int cardId)
	{
		var card = state.GetObject(cardId) as Card;
		if (card == null)
			return 0;
		var player = state.GetPlayer(card.ControllerId);
		return player.LandsPlayedTotal;
	}
}
