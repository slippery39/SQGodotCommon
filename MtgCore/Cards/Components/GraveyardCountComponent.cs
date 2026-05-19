using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Dynamic P/T modifier that scales with the number of cards in the controller's graveyard.
/// Used by Tarmogoyf: base Power = 0, base Toughness = 1, so effective values become
/// (graveyard count) / (graveyard count + 1) at read time.
/// </summary>
public record GraveyardCountComponent : PowerToughnessModifier
{
	public override int GetPowerBonus(GameState state, int cardId) =>
		CountControllerGraveyardCards(state, cardId);

	public override int GetToughnessBonus(GameState state, int cardId) =>
		CountControllerGraveyardCards(state, cardId);

	private static int CountControllerGraveyardCards(GameState state, int cardId)
	{
		var card = state.GetObject(cardId) as Card;
		if (card == null)
			return 0;
		var graveyardId = state.GetPlayerZoneId(card.ControllerId, ZoneType.Graveyard);
		return state.GetChildrenIds(graveyardId).Count();
	}
}
