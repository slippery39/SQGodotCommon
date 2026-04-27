using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Counts cards with a given subtype on a player's battlefield and writes the
/// result to pipeline context. Used by Krenko, Mob Boss to count Goblins before
/// creating tokens.
///
/// Player resolution:
///   - Set PlayerId directly, or
///   - Set PlayerIdContextKey to read the player ID from pipeline context
///
/// If PlayerIdContextKey is set it takes priority over PlayerId.
/// </summary>
public record CountCardsWithSubtypeAction : GameAction
{
	public string Subtype { get; init; } = "";
	public string OutputKey { get; init; } = "";
	public string PlayerIdContextKey { get; init; } = "";
	public int PlayerId { get; init; } = 0;

	public override ActionResult Execute(GameState gameState)
	{
		var playerId = string.IsNullOrEmpty(PlayerIdContextKey)
			? PlayerId
			: GetInput<int>(PlayerIdContextKey, 0);

		if (playerId == 0)
			return new ActionResult(gameState);

		var battlefieldId = gameState.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		var count = gameState.GetCardsInZone(battlefieldId).Count(c => c.HasSubtype(Subtype));

		return new ActionResult(gameState).WithOutput(OutputKey, count);
	}
}
