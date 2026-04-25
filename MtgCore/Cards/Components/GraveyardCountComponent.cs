using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Dynamic P/T modifier that scales with the total number of cards across all graveyards.
/// Used by Tarmogoyf: base Power = 0, base Toughness = 1, so effective values become
/// (graveyard count) / (graveyard count + 1) at read time.
/// </summary>
public record GraveyardCountComponent : PowerToughnessModifier
{
	public override int GetPowerBonus(GameState state, int cardId) => CountAllGraveyardCards(state);

	public override int GetToughnessBonus(GameState state, int cardId) =>
		CountAllGraveyardCards(state);

	private static int CountAllGraveyardCards(GameState state) =>
		state
			.IdToGameObjectMap.Values.OfType<Zone>()
			.Where(z => z.ZoneType == ZoneType.Graveyard)
			.Sum(z => state.GetCardsInZone(z.Id).Count());
}
