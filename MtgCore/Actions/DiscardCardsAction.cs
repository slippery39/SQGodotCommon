using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Moves one or more target cards to the owner's graveyard (discard).
/// TargetIds holds the card IDs; TargetContextKey reads them from pipeline context.
/// </summary>
public record DiscardCardsAction : EffectAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		foreach (var cardId in ResolveTargetIds())
		{
			if (!state.HasObject(cardId))
				continue;

			if (state.GetObject(cardId) is not Card card)
				continue;

			var graveyardId = state.GetPlayerZoneId(card.OwnerId, ZoneType.Graveyard);
			state = state.MoveCardTracked(cardId, graveyardId);

			var discarded = new CardDiscardedEvent { PlayerId = card.OwnerId, CardId = cardId };
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(discarded) };
			events = events.Add(discarded);
		}

		return new ActionResult(state) { Events = events };
	}
}
