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
/// Matches a creature with flying — compose with .Not() for "each creature without flying"
/// (Earthquake), which is the clause that stops a symmetric sweeper from killing the flyers red
/// cannot otherwise beat.
///
/// Reads EFFECTIVE flying, so a creature granted flight this turn is included and one that lost
/// its anthem is not. Asking CreatureComponent.HasFlying directly would disagree with combat,
/// which routes every flying question through GetEffectiveStats.
/// </summary>
public record HasFlyingSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context) =>
		context.GameState.GetObject(candidateId) is Card card
		&& card.HasComponent<CreatureComponent>()
		&& context.GameState.GetEffectiveStats(candidateId).HasFlying;
}

/// <summary>
/// Matches a creature that has attacked this turn — Royal Assassin's "target tapped creature",
/// retargeted.
///
/// The printed wording is unplayable here and the reason is structural, not a balance call:
/// attacking does not exhaust in this engine (IsExhausted is deliberately separate from
/// HasAttacked, see the Exhaust notes in CLAUDE.md), so the only exhausted creatures are ones a
/// tapper set up. White owns every tapper in this cube, which would leave Royal Assassin a blank
/// card in the mono-black deck that wants it. "Attacked this turn" is the clause the printed one
/// is a proxy for in real Magic, and it is live every turn.
///
/// HasAttacked is cleared by StartTurnAction for the active player, so on your turn this reads
/// the opponent's attackers from their last turn — exactly the window the card wants.
/// </summary>
public record HasAttackedThisTurnSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context) =>
		context.GameState.GetObject(candidateId) is Card card
		&& card.GetComponent<CreatureComponent>()?.HasAttacked == true;
}

/// <summary>
/// Matches a creature whose effective power and toughness differ — Gilt-Leaf Winnower's
/// "destroy target creature with different power and toughness".
///
/// Effective, not printed: a creature pumped out of being square is a legal target, which is
/// what the card says and also what makes it interact with the rest of the board.
/// </summary>
public record DifferentPowerAndToughnessSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (context.GameState.GetObject(candidateId) is not Card card)
			return false;
		if (!card.HasComponent<CreatureComponent>())
			return false;

		var stats = context.GameState.GetEffectiveStats(candidateId);
		return stats.Power != stats.Toughness;
	}
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
