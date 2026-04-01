using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Moves a single card to the top (front) of a player's library.
/// Uses MoveObjectToFront so the card becomes the next card drawn.
///
/// Card resolution:
///   - Set CardId directly, or
///   - Set CardIdContextKey to read from pipeline context
///
/// Player resolution:
///   - Set PlayerId directly, or
///   - Set PlayerIdContextKey to read from pipeline context
///
/// If the context key variants are set they take priority over the direct values.
/// </summary>
public record MoveCardToTopOfLibraryAction : GameAction
{
	public int PlayerId { get; init; }
	public string PlayerIdContextKey { get; init; } = "";
	public int CardId { get; init; }
	public string CardIdContextKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var playerId = string.IsNullOrEmpty(PlayerIdContextKey)
			? PlayerId
			: GetInput<int>(PlayerIdContextKey, 0);

		if (playerId == 0 || !gameState.HasObject(playerId))
			return new ActionResult(gameState);

		var cardId = string.IsNullOrEmpty(CardIdContextKey)
			? CardId
			: GetInput<int>(CardIdContextKey, 0);

		if (cardId == 0 || !gameState.HasObject(cardId))
			return new ActionResult(gameState);

		var libraryId = gameState.GetPlayerZoneId(playerId, ZoneType.Library);
		var newState = gameState.MoveObjectToFront(cardId, libraryId);

		return new ActionResult(newState);
	}
}

/// <summary>
/// Moves one or more cards to the bottom of a player's library.
/// Uses MoveObject which appends to the end of the children list.
///
/// Card resolution:
///   - Set CardIds directly, or
///   - Set CardIdsContextKey to read from pipeline context
///
/// Player resolution:
///   - Set PlayerId directly, or
///   - Set PlayerIdContextKey to read from pipeline context
///
/// If the context key variants are set they take priority over the direct values.
/// Handles both single int and ImmutableList&lt;int&gt; context values — consistent with
/// how ResolveChoice stores its output.
/// </summary>
public record MoveCardToBottomOfLibraryAction : GameAction
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

		if (playerId == 0 || !gameState.HasObject(playerId))
			return new ActionResult(gameState);

		var cardIds = ResolveCardIds();
		var state = gameState;
		var libraryId = state.GetPlayerZoneId(playerId, ZoneType.Library);

		foreach (var cardId in cardIds)
		{
			if (cardId == 0 || !state.HasObject(cardId))
				continue;

			state = state.MoveObject(cardId, libraryId);
		}

		return new ActionResult(state);
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
