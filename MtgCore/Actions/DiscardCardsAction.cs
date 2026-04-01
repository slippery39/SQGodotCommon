using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Moves one or more cards to the owner's graveyard (discard).
///
/// Card resolution:
///   - Set CardIds directly, or
///   - Set CardIdsContextKey to read from pipeline context
///
/// Player resolution (used for the discard event — graveyard destination always uses card.OwnerId):
///   - Set PlayerId directly, or
///   - Set PlayerIdContextKey to read the player ID from pipeline context
///
/// If the context key variants are set they take priority over the direct values.
/// </summary>
public record DiscardCardsAction : GameAction
{
	public int PlayerId { get; init; }
	public string PlayerIdContextKey { get; init; } = "";
	public ImmutableList<int> CardIds { get; init; } = ImmutableList<int>.Empty;
	public string CardIdsContextKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var playerId = string.IsNullOrEmpty(PlayerIdContextKey)
			? PlayerId
			: GetInput<int>(PlayerIdContextKey, 0);

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
