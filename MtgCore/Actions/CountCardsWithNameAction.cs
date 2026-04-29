using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Counts cards with a specific name in a player's zone and writes the result to
/// pipeline context. Used by Rite of Flame to count copies in the graveyard before
/// calculating bonus mana.
///
/// Player resolution:
///   - Set PlayerId directly, or
///   - Set PlayerIdContextKey to read the player ID from pipeline context
/// If PlayerIdContextKey is set it takes priority over PlayerId.
/// </summary>
public record CountCardsWithNameAction : GameAction
{
	public string CardName { get; init; } = "";
	public ZoneType Zone { get; init; } = ZoneType.Graveyard;
	public string OutputKey { get; init; } = "";
	public int PlayerId { get; init; } = 0;
	public string PlayerIdContextKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var playerId = string.IsNullOrEmpty(PlayerIdContextKey)
			? PlayerId
			: GetInput<int>(PlayerIdContextKey, PlayerId);

		if (playerId == 0 || string.IsNullOrEmpty(OutputKey))
			return new ActionResult(gameState);

		var zoneId = gameState.GetPlayerZoneId(playerId, Zone);
		var count = gameState
			.GetCardsInZone(zoneId)
			.Count(c => string.Equals(c.Name, CardName, StringComparison.OrdinalIgnoreCase));

		return new ActionResult(gameState).WithOutput(OutputKey, count);
	}
}
