using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Matches an exhausted creature — "target tapped creature" (Swift Response, Gideon Jura's -2).
///
/// This card class was unbuildable before exhaustion existed, and it is what gives the tapper
/// theme its payoff half: Swift Response is a dead card without a Gideon's Lawkeeper to set it up.
/// </summary>
public record IsExhaustedSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context) =>
		context.GameState.GetObject(candidateId) is Card card
		&& card.GetComponent<CreatureComponent>()?.IsExhausted == true;
}

/// <summary>
/// Matches every creature EXCEPT its controller's cheapest one.
///
/// This is how Tragic Arrogance's "each player keeps one, you choose which" is expressed as a
/// mass effect: the caster would choose the worst creature for each opponent to keep, and
/// cheapest is the closest thing to "worst" that code can judge honestly.
///
/// Ties break on the lowest card id so the result is deterministic — a mass sacrifice that
/// picked differently on re-evaluation would desync the AI's search from the real game.
/// </summary>
public record IsNotCheapestCreatureSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		var state = context.GameState;

		if (state.GetObject(candidateId) is not Card card)
			return false;
		if (!card.HasComponent<CreatureComponent>())
			return false;

		var battlefieldId = state.GetPlayerZoneId(card.ControllerId, ZoneType.Battlefield);
		if (battlefieldId == 0)
			return false;

		Card? cheapest = null;
		foreach (var other in state.GetCardsInZone(battlefieldId))
		{
			if (other.ControllerId != card.ControllerId)
				continue;
			if (!other.HasComponent<CreatureComponent>())
				continue;

			if (
				cheapest == null
				|| other.ManaCost < cheapest.ManaCost
				|| (other.ManaCost == cheapest.ManaCost && other.Id < cheapest.Id)
			)
				cheapest = other;
		}

		return cheapest != null && cheapest.Id != candidateId;
	}
}
