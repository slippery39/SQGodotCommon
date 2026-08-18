using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// A generic trigger condition that fires on any GameEvent matching the given
/// type name, with an optional TargetSpecification filter applied to the event's
/// subject (the primary card or player the event is "about").
///
/// This replaces the need for one TriggerCondition class per event type.
/// Any GameEvent is immediately triggerable just by knowing its type name —
/// use EventTypeNames constants to avoid magic strings.
///
/// Examples:
///
///   // Fires when any creature dies
///   new EventTriggerCondition { EventTypeName = EventTypeNames.CreatureDestroyed }
///
///   // Fires when an opponent's creature dies
///   new EventTriggerCondition
///   {
///       EventTypeName = EventTypeNames.CreatureDestroyed,
///       Filter = new IsControlledByOpponentSpecification(),
///   }
///
///   // Fires at the start of your turn (upkeep-style)
///   new EventTriggerCondition { EventTypeName = EventTypeNames.TurnStarted }
///
/// Filter notes:
///   - Filter uses the existing TargetSpecification pattern
///   - The filter is evaluated against the event's subject ID (see ExtractSubjectId)
///   - If the event has no subject (SubjectId == 0) the filter is skipped
///   - CastingPlayerId in the TargetingContext is the controlling player of
///     the card that owns this trigger
///
/// Future improvement: move SubjectId onto GameEvent itself so ExtractSubjectId
/// is not needed here. Deferred to avoid touching ImmutableGameObjects now.
/// </summary>
public record EventTriggerCondition : TriggerCondition
{
	/// <summary>
	/// The event type name to listen for.
	/// Use EventTypeNames constants: EventTypeNames.CreatureDestroyed etc.
	/// </summary>
	public string EventTypeName { get; init; } = "";

	/// <summary>
	/// Optional filter applied to the event's subject.
	/// Uses the existing TargetSpecification pattern — same specs used in targeting.
	/// Null means no filtering — condition fires for all events of this type.
	/// </summary>
	public TargetSpecification? Filter { get; init; } = null;

	public override bool IsSatisfiedBy(GameEvent gameEvent, TriggerContext context)
	{
		// Fast type name check first — cheap string comparison
		if (gameEvent.GetType().Name != EventTypeName)
			return false;

		// No filter — fires on any event of this type
		if (Filter == null)
			return true;

		// Extract the subject of the event and run the filter against it
		var subjectId = ExtractSubjectId(gameEvent);

		// If the event has no subject, skip the filter and allow it
		if (subjectId == 0)
			return true;

		return Filter.IsSatisfiedBy(
			subjectId,
			new TargetingContext
			{
				GameState = context.GameState,
				SourceCardId = context.SourceCardId,
				CastingPlayerId = context.ControllingPlayerId,
			}
		);
	}

	/// <summary>
	/// Returns the primary subject ID of the event — the card or player the event
	/// is most directly "about". Used to evaluate the Filter specification against.
	///
	/// Returns 0 if the event has no meaningful subject or is not recognised.
	///
	/// Future improvement: move this onto GameEvent as a virtual SubjectId property
	/// so events are self-describing. Deferred to avoid touching ImmutableGameObjects.
	/// </summary>
	/// <summary>
	/// Internal so CheckStateBasedEffectsAction can publish the same subject to the effect as
	/// ContextKeys.TriggerSubjectId. One extraction, so what a trigger FILTERS on and what its
	/// effect ACTS on can never disagree.
	/// </summary>
	internal static int ExtractSubjectId(GameEvent gameEvent) =>
		gameEvent switch
		{
			CreatureDestroyedEvent e => e.CreatureId,
			CreatureDamagedEvent e => e.CreatureId,
			CreaturePlayedEvent e => e.CardId,
			CreatureAttackedEvent e => e.CreatureId,
			CreatureExhaustedEvent e => e.CreatureId,
			PlayerDamagedEvent e => e.PlayerId,
			PlayerGainedLifeEvent e => e.PlayerId,
			PlayerLostLifeEvent e => e.PlayerId,
			CardDrawnEvent e => e.CardId,
			CardDiscardedEvent e => e.CardId,
			// The milled/graveyard events report the CARD, not the player, so an
			// IsControlledByYouSpecification filter reads "a card of yours was milled".
			// Without these entries the subject is 0, the filter is skipped, and a
			// "whenever you mill" trigger would also fire on the opponent's mills.
			CardMilledEvent e => e.CardId,
			CardEnteredGraveyardEvent e => e.CardId,
			CardLeftGraveyardEvent e => e.CardId,
			CardRevealedEvent e => e.CardId,
			SpellCastEvent e => e.CardId,
			SpellCounteredEvent e => e.CardId,
			PermanentPlayedEvent e => e.CardId,
			TurnStartedEvent e => e.PlayerId,
			TurnEndedEvent e => e.PlayerId,
			CreatureEnteredBattlefieldEvent e => e.CardId,
			PermanentEnteredBattlefieldEvent e => e.CardId,
			PermanentLeftBattlefieldEvent e => e.CardId,
			CombatDamageDealtToPlayerEvent e => e.AttackerId,
			LandPlayedEvent e => e.PlayerId,
			ArtifactLeftBattlefieldEvent e => e.CardId,
			_ => 0,
		};
}
