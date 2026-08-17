using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Card moves that announce graveyard boundary crossings.
///
/// Roughly a dozen actions move cards to or from a graveyard (discard, destroy, combat
/// damage, effect damage, mill, reanimate, flashback, exile, bounce). Emitting the boundary
/// events at each of those call sites would mean a dozen places to keep in sync and a dozen
/// chances to forget one. Instead the events are emitted here, at the move itself, so any
/// action that uses MoveCardTracked gets them for free.
///
/// Only graveyard transitions are tracked, because those are the only ones with a consumer
/// today (StaticAbilityEngine's zone-dependent statics). Extend here — not at the callers —
/// if exile or hand transitions ever need the same treatment.
/// </summary>
public static class ZoneTransitionExtensions
{
	/// <summary>
	/// Moves a card to a destination zone, appending CardLeftGraveyardEvent and/or
	/// CardEnteredGraveyardEvent to PendingGameEvents when the move crosses a graveyard
	/// boundary. A move that neither leaves nor enters a graveyard behaves exactly like
	/// MoveObject.
	/// </summary>
	public static GameState MoveCardTracked(this GameState state, int cardId, int destinationZoneId)
	{
		if (state.GetObject(cardId) is not Card card)
			return state.MoveObject(cardId, destinationZoneId);

		var graveyardId = state.GetPlayerZoneId(card.OwnerId, ZoneType.Graveyard);
		var sourceZoneId = state.GetCardZoneId(cardId);
		var wasInGraveyard = sourceZoneId == graveyardId;
		var willBeInGraveyard = destinationZoneId == graveyardId;

		state = state.MoveObject(cardId, destinationZoneId);

		// Marked damage belongs to the permanent, not the card. A creature that leaves the
		// battlefield and comes back is a new permanent and arrives undamaged — reanimating a
		// creature that died at 3 damage must not bring the damage with it. Cleared here rather
		// than at each caller for the same reason the boundary events are.
		if (
			sourceZoneId != destinationZoneId
			&& card.GetComponent<CreatureComponent>() is { Damage: > 0 } damaged
		)
		{
			state = state.UpdateObject(
				cardId,
				((Card)state.GetObject(cardId)).WithComponentReplaced(damaged with { Damage = 0 })
			);
		}

		// A move within the same zone crosses no boundary.
		if (wasInGraveyard == willBeInGraveyard)
			return state;

		GameEvent boundary = willBeInGraveyard
			? new CardEnteredGraveyardEvent { CardId = cardId, OwnerId = card.OwnerId }
			: new CardLeftGraveyardEvent { CardId = cardId, OwnerId = card.OwnerId };

		return state with
		{
			PendingGameEvents = state.PendingGameEvents.Add(boundary),
		};
	}
}
