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

				state = state.MoveObject(topCardId, handId);
				events = events.Add(new CardDrawnEvent { PlayerId = playerId, CardId = topCardId });
			}
		}

		return new ActionResult(state) { Events = events };
	}
}
