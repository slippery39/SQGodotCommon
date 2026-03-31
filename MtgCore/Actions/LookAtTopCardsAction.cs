using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Reads the top N card IDs from a player's library into pipeline context
/// without moving them. Acts as a query — no game state change.
///
/// Writes an ImmutableList&lt;int&gt; of card IDs to the context key specified
/// by OutputKey. If the library has fewer than Amount cards, all available
/// IDs are written.
///
/// PlayerId can be set directly or read from context via ContextKeys.CastingPlayerId.
/// </summary>
public record LookAtTopCardsAction : GameAction
{
	public int PlayerId { get; init; }
	public int Amount { get; init; } = 3;
	public string OutputKey { get; init; } = ContextKeys.TopCardIds;

	public override ActionResult Execute(GameState gameState)
	{
		var playerId = PlayerId != 0 ? PlayerId : GetInput<int>(ContextKeys.CastingPlayerId, 0);

		var libraryId = gameState.GetPlayerZoneId(playerId, ZoneType.Library);

		var topCardIds = gameState.GetChildrenIds(libraryId).Take(Amount).ToImmutableList();

		return new ActionResult(gameState).WithOutput(OutputKey, topCardIds);
	}
}
