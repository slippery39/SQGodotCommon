using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Selects the non-land card with the highest or lowest ManaCost from a player's hand
/// and writes its ID to pipeline context. Land cards (HasSubtype("Land")) are excluded.
///
/// If no non-land cards are found, nothing is written to OutputKey — downstream
/// DiscardCardsAction falls back to empty TargetIds and no-ops cleanly.
///
/// If TargetOpponent is true, derives the opponent from the casting player via
/// the well-known Player1/Player2 IDs (2-player game only).
/// </summary>
public record SelectCardFromHandByManaCostAction : GameAction
{
	public bool SelectLowest { get; init; } = true;
	public bool TargetOpponent { get; init; } = false;
	public string PlayerIdContextKey { get; init; } = "";
	public string OutputKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var castingPlayerId = string.IsNullOrEmpty(PlayerIdContextKey)
			? 0
			: GetInput<int>(PlayerIdContextKey, 0);

		if (castingPlayerId == 0)
			return new ActionResult(gameState);

		var targetPlayerId = TargetOpponent
			? GetOpponentId(gameState, castingPlayerId)
			: castingPlayerId;

		var handId = gameState.GetPlayerZoneId(targetPlayerId, ZoneType.Hand);
		var nonLandCards = gameState
			.GetCardsInZone(handId)
			.Where(c => !c.HasSubtype("Land"))
			.ToList();

		if (nonLandCards.Count == 0)
			return new ActionResult(gameState);

		var selected = SelectLowest
			? nonLandCards.MinBy(c => c.ManaCost)!
			: nonLandCards.MaxBy(c => c.ManaCost)!;

		return new ActionResult(gameState).WithOutput(OutputKey, selected.Id);
	}

	private static int GetOpponentId(GameState gameState, int castingPlayerId)
	{
		var p1Id = gameState.GetWellKnownId(MtgObjectKeys.Player1);
		return castingPlayerId == p1Id ? gameState.GetWellKnownId(MtgObjectKeys.Player2) : p1Id;
	}
}
