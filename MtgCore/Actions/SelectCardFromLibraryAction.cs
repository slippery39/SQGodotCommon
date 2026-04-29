using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Searches a player's library for the first card matching the given subtype and
/// writes its ID to OutputKey in pipeline context. Used as the search step in
/// storm combo pipelines (e.g. Dragonstorm).
///
/// Player resolution:
///   - Set PlayerId directly, or
///   - Set PlayerIdContextKey to read the player ID from pipeline context.
/// If PlayerIdContextKey is set it takes priority over PlayerId.
///
/// Selection is the first matching card in current library order (shuffled at game start).
/// If no matching card is found, OutputKey is set to 0 — downstream actions must handle 0.
/// </summary>
public record SelectCardFromLibraryAction : GameAction
{
	public string Subtype { get; init; } = "";
	public int PlayerId { get; init; } = 0;
	public string PlayerIdContextKey { get; init; } = "";
	public string OutputKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var playerId = string.IsNullOrEmpty(PlayerIdContextKey)
			? PlayerId
			: GetInput<int>(PlayerIdContextKey, PlayerId);

		if (playerId == 0)
			return new ActionResult(gameState);

		var libraryId = gameState.GetPlayerZoneId(playerId, ZoneType.Library);

		var candidate = gameState
			.GetCardsInZone(libraryId)
			.FirstOrDefault(c => string.IsNullOrEmpty(Subtype) || c.HasSubtype(Subtype));

		var foundId = candidate?.Id ?? 0;

		var result = new ActionResult(gameState);
		if (!string.IsNullOrEmpty(OutputKey))
			result = result.WithOutput(OutputKey, foundId);

		return result;
	}
}
