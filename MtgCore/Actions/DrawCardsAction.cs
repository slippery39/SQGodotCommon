using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Draws Amount cards from each target player's library into their hand.
///
/// AmountContextKey (inherited from EffectAction) overrides Amount, which is what lets "draw
/// that many cards" work off ContextKeys.TriggerAmount. This action looped on the raw Amount
/// field and ignored the key entirely, so any context-driven draw silently drew the default 1.
/// </summary>
public record DrawCardsAction : EffectAction
{
	public int Amount { get; init; } = 1;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;
		var amount = ResolveAmount(Amount);

		if (amount <= 0)
			return new ActionResult(gameState);

		foreach (var playerId in ResolveTargetIds())
		{
			if (!state.HasObject(playerId))
				continue;

			var handId = state.GetPlayerZoneId(playerId, ZoneType.Hand);
			var libraryId = state.GetPlayerZoneId(playerId, ZoneType.Library);

			for (int i = 0; i < amount; i++)
			{
				var topCardId = state.GetChildrenIds(libraryId).FirstOrDefault();

				if (topCardId == 0)
				{
					events = events.Add(new LibraryEmptyEvent { PlayerId = playerId });
					break;
				}

				// MoveCardTracked, not MoveObject: a draw can cross a graveyard boundary in
				// principle, and it is the one move here that was still bypassing the tracker.
				state = state.MoveCardTracked(topCardId, handId);

				// PendingGameEvents is the trigger feed; the returned Events list is only the
				// caller-visible log. This event reached only the log, so NO "whenever you draw a
				// card" trigger in the engine had ever fired — Teferi's Tutelage never milled.
				// Fourth instance of this exact bug; see CLAUDE.md.
				var drawn = new CardDrawnEvent { PlayerId = playerId, CardId = topCardId };
				state = state with { PendingGameEvents = state.PendingGameEvents.Add(drawn) };
				events = events.Add(drawn);
			}
		}

		return new ActionResult(state) { Events = events };
	}
}
