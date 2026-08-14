using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Moves Amount cards from the top of each target player's library to their graveyard.
///
/// Mirrors DrawCardsAction's walk of the library (top card = first child, one at a time so
/// the count is re-read against the shrinking zone), but routes to the graveyard and emits
/// CardMilledEvent instead of CardDrawnEvent. The two events are deliberately distinct:
/// "whenever you discard a card" payoffs must not fire on self-mill.
///
/// Milling an empty or short library is legal and simply stops early, emitting
/// LibraryEmptyEvent — the same signal DrawCardsAction uses, so the existing empty-library
/// loss condition in CheckStateBasedEffectsAction picks it up with no changes.
///
/// Targets are players. "Each player mills N" is TargetingStrategy.AllValid over
/// IsPlayerSpecification; "target opponent mills N" narrows that with the opponent filter —
/// no TargetOpponent flag is needed here, unlike the data/query actions.
/// </summary>
public record MillAction : EffectAction
{
	public int Amount { get; init; } = 1;

	/// Card IDs milled by this action, in order, written to pipeline context when set.
	public string OutputKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;
		var milled = ImmutableList<int>.Empty;
		var amount = ResolveAmount(Amount);

		foreach (var playerId in ResolveTargetIds())
		{
			if (!state.HasObject(playerId))
				continue;

			var libraryId = state.GetPlayerZoneId(playerId, ZoneType.Library);
			var graveyardId = state.GetPlayerZoneId(playerId, ZoneType.Graveyard);

			for (int i = 0; i < amount; i++)
			{
				var topCardId = state.GetChildrenIds(libraryId).FirstOrDefault();

				if (topCardId == 0)
				{
					events = events.Add(new LibraryEmptyEvent { PlayerId = playerId });
					break;
				}

				// MoveCardTracked emits CardEnteredGraveyardEvent for us.
				state = state.MoveCardTracked(topCardId, graveyardId);
				milled = milled.Add(topCardId);

				// PendingGameEvents is what CheckStateBasedEffectsAction scans for triggers;
				// the returned Events list is the caller-visible log.
				var milledEvent = new CardMilledEvent { PlayerId = playerId, CardId = topCardId };
				state = state with { PendingGameEvents = state.PendingGameEvents.Add(milledEvent) };
				events = events.Add(milledEvent);
			}
		}

		var result = new ActionResult(state) { Events = events };
		return string.IsNullOrEmpty(OutputKey) ? result : result.WithOutput(OutputKey, milled);
	}
}
