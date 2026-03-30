using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Moves one or more cards to the owner's graveyard (discard).
///
/// Card IDs can be supplied in two ways:
///   - Hardcoded: set CardIds directly
///   - Dynamic: set CardIdsContextKey to read from pipeline context
///
/// PlayerId can be set directly or read from pipeline context via
/// ContextKeys.CastingPlayerId when used inside a spell effect pipeline.
/// </summary>
public record DiscardCardsAction : GameAction
{
	public int PlayerId { get; init; }
	public ImmutableList<int> CardIds { get; init; } = ImmutableList<int>.Empty;
	public string CardIdsContextKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var playerId = PlayerId != 0 ? PlayerId : GetInput<int>(ContextKeys.CastingPlayerId, 0);

		var cardIds = ResolveCardIds();

		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		foreach (var cardId in cardIds)
		{
			if (!state.HasObject(cardId))
				continue;

			var card = state.GetObject(cardId) as Card;
			if (card == null)
				continue;

			var graveyardId = state.GetPlayerZoneId(card.OwnerId, ZoneType.Graveyard);
			state = state.MoveObject(cardId, graveyardId);
			events = events.Add(new CardDiscardedEvent { PlayerId = playerId, CardId = cardId });
		}

		return new ActionResult(state) { Events = events };
	}

	private ImmutableList<int> ResolveCardIds()
	{
		if (string.IsNullOrEmpty(CardIdsContextKey))
			return CardIds;

		var raw = InputContext.TryGetValue(CardIdsContextKey, out var value) ? value : null;

		return raw switch
		{
			ImmutableList<int> list => list,
			int singleId => ImmutableList.Create(singleId),
			_ => ImmutableList<int>.Empty,
		};
	}
}
