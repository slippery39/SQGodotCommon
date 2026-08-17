using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Resolves a non-creature permanent spell that is currently on the stack.
///
/// Moves the card directly to the controller's battlefield and emits
/// PermanentEnteredBattlefieldEvent so triggered abilities can fire.
/// Unlike creatures, no summoning sickness is stamped — activated abilities
/// on non-creature permanents are usable immediately.
///
/// Suppresses the post-processor during resolution and spawns EndResolutionScopeAction
/// to restore it, matching the same pattern as ResolveCreatureAction.
/// </summary>
public record ResolvePermanentAction : GameAction
{
	public int CardId { get; init; }
	public int CastingPlayerId { get; init; }

	/// <summary>
	/// What an Aura enchants, chosen at cast time by CastPermanentAction. Empty for everything
	/// else. The attach happens here rather than through an ETB trigger, because a trigger
	/// cannot make the spell illegal when there is nothing to enchant.
	/// </summary>
	public ImmutableList<int> AuraTargetIds { get; init; } = ImmutableList<int>.Empty;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState with { SuppressPostProcessor = true };
		var card = (Card)state.GetObject(CardId);
		var battlefieldId = state.GetPlayerZoneId(card.ControllerId, ZoneType.Battlefield);
		state = state.MoveObject(CardId, battlefieldId);

		// Planeswalkers arrive at their starting loyalty. Shared with PutIntoBattlefieldAction so
		// the cast path and the reanimate path cannot disagree.
		state = state.StampPlaneswalkerEntry(CardId);

		var enteredEvent = new PermanentEnteredBattlefieldEvent
		{
			CardId = CardId,
			PlayerId = card.ControllerId,
		};
		state = state with { PendingGameEvents = state.PendingGameEvents.Add(enteredEvent) };

		var spawned = ImmutableList<GameAction>.Empty;

		// Attach the Aura to the target chosen when it was cast. Spawned rather than executed
		// inline so it resolves inside this scope alongside any ETB trigger the Aura also has
		// (Faith's Fetters gains life, Claustrophobia taps what it enchants).
		if (!AuraTargetIds.IsEmpty && card.GetComponent<AuraTargetComponent>() != null)
			spawned = spawned.Add(
				new AttachEquipmentAction
				{
					TargetIds = AuraTargetIds,
					// AttachEquipmentAction finds the attachment itself through this key, which
					// ResolveEffectAction would normally inject.
					InputContext = ImmutableDictionary<string, object>.Empty.Add(
						ContextKeys.SourceCardId,
						CardId
					),
				}
			);

		spawned = spawned.Add(new EndResolutionScopeAction());

		return new ActionResult(state.SpawnActions(spawned))
		{
			Events = ImmutableList.Create<GameEvent>(enteredEvent),
		};
	}
}
