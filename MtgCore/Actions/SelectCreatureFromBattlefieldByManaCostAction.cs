using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Selects the creature with the highest or lowest ManaCost from a player's battlefield
/// and writes its ID to pipeline context. Only cards with CreatureComponent qualify.
///
/// If no creatures are found, nothing is written to OutputKey — downstream
/// DestroyCreatureAction falls back to empty TargetIds and no-ops cleanly.
///
/// If TargetOpponent is true, derives the opponent from the casting player via
/// the well-known Player1/Player2 IDs (2-player game only).
///
/// ExcludeSubtype skips creatures of that subtype — "each player sacrifices a non-Zombie
/// creature" (Call to the Grave). Empty means no exclusion.
/// </summary>
public record SelectCreatureFromBattlefieldByManaCostAction : GameAction
{
	public bool SelectLowest { get; init; } = true;
	public bool TargetOpponent { get; init; } = false;
	public string ExcludeSubtype { get; init; } = "";
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

		var battlefieldId = gameState.GetPlayerZoneId(targetPlayerId, ZoneType.Battlefield);
		var creatures = gameState
			.GetCardsInZone(battlefieldId)
			.Where(c => c.HasComponent<CreatureComponent>())
			.Where(c => string.IsNullOrEmpty(ExcludeSubtype) || !c.HasSubtype(ExcludeSubtype))
			.ToList();

		if (creatures.Count == 0)
			return new ActionResult(gameState);

		var selected = SelectLowest
			? creatures.MinBy(c => c.ManaCost)!
			: creatures.MaxBy(c => c.ManaCost)!;

		return new ActionResult(gameState).WithOutput(OutputKey, selected.Id);
	}

	private static int GetOpponentId(GameState gameState, int castingPlayerId)
	{
		var p1Id = gameState.GetWellKnownId(MtgObjectKeys.Player1);
		return castingPlayerId == p1Id ? gameState.GetWellKnownId(MtgObjectKeys.Player2) : p1Id;
	}
}
