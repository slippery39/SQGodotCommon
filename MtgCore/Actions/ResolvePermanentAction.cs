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

		return new ActionResult(state.SpawnAction(new EndResolutionScopeAction()))
		{
			Events = ImmutableList.Create<GameEvent>(enteredEvent),
		};
	}
}
