using System.Collections.Immutable;
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
/// Battlefield departures are announced here too, for exactly the same reason: every route OFF
/// the battlefield other than dying (bounce, exile, "put it on top of its library") used to move
/// the card silently, so nothing the permanent was doing to other cards was ever undone. A
/// bounced Aura left its -3/-0 stamped on the creature forever and a bounced Oblivion Ring never
/// gave back what it had exiled.
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
		var leftBattlefield =
			IsBattlefield(state, sourceZoneId) && !IsBattlefield(state, destinationZoneId);

		state = state.MoveObject(cardId, destinationZoneId);

		// Battlefield to battlefield is a control change, not a departure — GainControlAction
		// moves the card between the two battlefield zones and the permanent never leaves play.
		if (leftBattlefield && !HasPendingDeparture(state, cardId))
			state = state with
			{
				PendingGameEvents = state.PendingGameEvents.Add(
					new PermanentLeftBattlefieldEvent { CardId = cardId, OwnerId = card.OwnerId }
				),
			};

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

		// Everything an effect STAMPED on the permanent goes the same way, and for the same
		// reason: it belonged to the permanent, not to the card. Nothing removed these, so a
		// creature bounced under an anthem kept the anthem's bonus in hand and collected a
		// SECOND one when it was replayed, and a creature killed by -4/-4 sat in the graveyard
		// still printing the -4/-4 in its text box.
		if (leftBattlefield)
			state = StripAppliedComponents(state, cardId);

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

	/// <summary>
	/// Removes the components that an EFFECT stamped onto a permanent while it was in play.
	///
	/// Deliberately a closed list of the four applied types rather than "every
	/// PowerToughnessModifier". Several modifier subclasses are PRINTED on the card and define
	/// what it is — Tarmogoyf's GraveyardCountComponent, Threshold, CreatureCountComponent,
	/// LifeTotalComponent, LandsPlayedCountComponent. Stripping by base type would delete the
	/// card's own rules text on its way to the graveyard and reanimate it as a vanilla creature.
	/// </summary>
	private static GameState StripAppliedComponents(GameState state, int cardId)
	{
		if (state.GetObject(cardId) is not Card card)
			return state;

		var kept = card
			.Components.Where(c =>
				c
					is not (
						AppliedStaticPTBoost
						or AppliedKeywordComponent
						or StaticPowerToughnessModifier
						or EquippedBoostComponent
					)
			)
			.ToImmutableArray();

		return kept.Length == card.Components.Length
			? state
			: state.UpdateObject(cardId, card with { Components = kept });
	}

	private static bool IsBattlefield(GameState state, int zoneId) =>
		state.HasObject(zoneId)
		&& state.GetObject(zoneId) is Zone { ZoneType: ZoneType.Battlefield };

	/// <summary>
	/// Whether a departure for this card is already queued. Destruction, sacrifice and combat
	/// death all announce the departure themselves before moving the card, and firing it a second
	/// time here would run every death trigger twice.
	/// </summary>
	private static bool HasPendingDeparture(GameState state, int cardId) =>
		state.PendingGameEvents.Any(e =>
			e is PermanentLeftBattlefieldEvent left && left.CardId == cardId
		);
}
