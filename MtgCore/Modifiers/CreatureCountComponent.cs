using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "Power and toughness are each equal to the number of creatures you control."
/// — Crusader of Odric, printed as */*.
///
/// The engine has no */* templating — Power and Toughness are ints — so a */* card is written
/// with base 0/0 plus this modifier. Anything reading effective stats sees the right numbers;
/// only the raw CreatureComponent shows 0/0.
///
/// Live-evaluated, like ThresholdComponent: the creature count changes on every death and every
/// token, and a pushed value would be wrong between re-stamps.
///
/// Set Duration = ModifierDuration.Permanent in card definitions or StartTurnAction strips it.
/// </summary>
public record CreatureCountComponent : PowerToughnessModifier
{
	public int PowerPerCreature { get; init; } = 1;
	public int ToughnessPerCreature { get; init; } = 1;

	/// <summary>
	/// When false the creature does not count itself — for "other creatures you control"
	/// wordings. Crusader of Odric counts itself, so this defaults to true.
	/// </summary>
	public bool CountsSelf { get; init; } = true;

	public override int GetPowerBonus(GameState state, int cardId) =>
		Count(state, cardId) * PowerPerCreature;

	public override int GetToughnessBonus(GameState state, int cardId) =>
		Count(state, cardId) * ToughnessPerCreature;

	private int Count(GameState state, int cardId)
	{
		if (state.GetObject(cardId) is not Card card)
			return 0;

		var battlefieldId = state.GetPlayerZoneId(card.ControllerId, ZoneType.Battlefield);
		if (battlefieldId == 0)
			return 0;

		var count = 0;
		foreach (var other in state.GetCardsInZone(battlefieldId))
		{
			if (other.ControllerId != card.ControllerId)
				continue;
			if (!other.HasComponent<CreatureComponent>())
				continue;
			if (!CountsSelf && other.Id == cardId)
				continue;
			count++;
		}

		return count;
	}
}
