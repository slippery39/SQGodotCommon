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
/// Player resolution:
///   - Set PlayerId directly, or
///   - Set PlayerIdContextKey to read the player ID from pipeline context
///
/// If PlayerIdContextKey is set it takes priority over PlayerId.
/// </summary>
public record LookAtTopCardsAction : GameAction
{
	public int PlayerId { get; init; }
	public string PlayerIdContextKey { get; init; } = "";
	public int Amount { get; init; } = 3;
	public string OutputKey { get; init; } = ContextKeys.TopCardIds;

	public override ActionResult Execute(GameState gameState)
	{
		var playerId = string.IsNullOrEmpty(PlayerIdContextKey)
			? PlayerId
			: GetInput<int>(PlayerIdContextKey, 0);

		if (playerId == 0 || !gameState.HasObject(playerId))
			return new ActionResult(gameState);

		var libraryId = gameState.GetPlayerZoneId(playerId, ZoneType.Library);
		var topCardIds = gameState.GetChildrenIds(libraryId).Take(Amount).ToImmutableList();

		return new ActionResult(gameState).WithOutput(OutputKey, topCardIds);
	}
}
