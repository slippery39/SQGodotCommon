using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Bloodthirst — "if an opponent was dealt damage this turn" (Vampire Outcasts).
///
/// Reads the opponent's MtgPlayer.LifeLostThisTurn, so it counts drain and life-loss as well as
/// damage. That is deliberately a shade wider than the printed keyword: this engine has no
/// separate damage-taken tally, adding one to distinguish "lost 2 life" from "was dealt 2
/// damage" would buy a single card nothing, and every black source of life loss here is a
/// source of aggression anyway. The clause still does its job — it rewards attacking first.
///
/// An ActivationCondition rather than a TriggerCondition because bloodthirst is asked once, as
/// the creature enters, and answered from board state. Use it via
/// SpellCardBuilder.WithConditionalAction inside an ETB trigger.
/// </summary>
public record OpponentLostLifeThisTurnCondition : ActivationCondition
{
	public int Minimum { get; init; } = 1;

	public override bool IsSatisfied(GameState state, int cardId, int playerId)
	{
		var p1 = state.GetWellKnownId(MtgObjectKeys.Player1);
		var opponentId = playerId == p1 ? state.GetWellKnownId(MtgObjectKeys.Player2) : p1;

		return state.GetObject(opponentId) is MtgPlayer opponent
			&& opponent.LifeLostThisTurn >= Minimum;
	}

	public override string Describe() =>
		Minimum == 1
			? "an opponent lost life this turn"
			: $"an opponent lost {Minimum} or more life this turn";
}

/// <summary>
/// "Activate only if there are four or more creature cards in your graveyard."
/// — Shadows of the Past.
///
/// A build-around gate rather than a cost: the enchantment's own scry trigger fills the
/// graveyard that turns it on, so the two halves of the card feed each other.
/// </summary>
public record CreaturesInGraveyardCondition : ActivationCondition
{
	public int Minimum { get; init; } = 4;

	public override bool IsSatisfied(GameState state, int cardId, int playerId)
	{
		var graveyardId = state.GetPlayerZoneId(playerId, ZoneType.Graveyard);
		if (graveyardId == 0)
			return false;

		return state.GetCardsInZone(graveyardId).Count(c => c.HasComponent<CreatureComponent>())
			>= Minimum;
	}

	public override string Describe() => $"{Minimum}+ creature cards in your graveyard";
}
