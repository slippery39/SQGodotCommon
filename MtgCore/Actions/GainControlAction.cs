using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "Gain control of target permanent" — Agent of Treachery.
///
/// Changes ControllerId and physically moves the card to the new controller's battlefield, since
/// every battlefield scan in the engine works from the zone rather than from ControllerId. Leaving
/// it in the old zone would make it invisible to its new controller's anthems, targeting and
/// attack generation.
///
/// OwnerId is untouched — that is what "permanents you don't own" counts, and it is also where the
/// card returns to when it dies.
///
/// The stolen creature keeps its damage and its exhausted state but is stamped summoning-sick, so
/// it cannot be stolen and swung with in the same turn.
/// </summary>
public record GainControlAction : EffectAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var newControllerId = GetInput<int>(ContextKeys.CastingPlayerId, 0);

		if (newControllerId == 0)
			return new ActionResult(gameState);

		foreach (var targetId in ResolveTargetIds())
		{
			if (state.GetObject(targetId) is not Card card)
				continue;
			if (card.ControllerId == newControllerId)
				continue;

			var updated = card with { ControllerId = newControllerId };

			var creature = updated.GetComponent<CreatureComponent>();
			if (creature != null)
				updated = (Card)
					updated.WithComponentReplaced(creature with { HasSummoningSickness = true });

			state = state.UpdateObject(targetId, updated);

			var battlefieldId = state.GetPlayerZoneId(newControllerId, ZoneType.Battlefield);
			if (battlefieldId != 0)
				state = state.MoveCardTracked(targetId, battlefieldId);
		}

		return new ActionResult(state);
	}
}

/// <summary>
/// "If you control three or more permanents you don't own" — Agent of Treachery's payoff.
///
/// Owner and controller are already separate fields on Card, so this needs no new bookkeeping.
/// </summary>
public record ControlsStolenPermanentsCondition : ActivationCondition
{
	public int Minimum { get; init; } = 3;

	public override bool IsSatisfied(GameState state, int cardId, int playerId)
	{
		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		if (battlefieldId == 0)
			return false;

		var stolen = state
			.GetCardsInZone(battlefieldId)
			.Count(c => c.ControllerId == playerId && c.OwnerId != playerId);

		return stolen >= Minimum;
	}

	public override string Describe() =>
		$"You must control {Minimum} or more permanents you don't own";
}
