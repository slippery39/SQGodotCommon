using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Dynamic P/T modifier that gives +X/+0 where X is the number of artifacts
/// the equipped creature's controller controls. Used by Cranial Plating.
/// </summary>
public record ArtifactCountPowerModifier : PowerToughnessModifier
{
	public override int GetPowerBonus(GameState state, int cardId)
	{
		var card = state.GetObject(cardId) as Card;
		if (card == null)
			return 0;
		var battlefieldId = state.GetPlayerZoneId(card.ControllerId, ZoneType.Battlefield);
		return state.GetCardsInZone(battlefieldId).Count(c => c.HasSubtype("Artifact"));
	}

	public override int GetToughnessBonus(GameState state, int cardId) => 0;
}
