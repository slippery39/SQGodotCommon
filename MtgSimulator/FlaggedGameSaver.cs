using System.Text.Json;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Saves flagged game state snapshots to disk for manual inspection.
/// Capped at MaxSaves per run — if a run produces more flagged games than that,
/// the first MaxSaves are sufficient to diagnose the cause.
/// </summary>
public static class FlaggedGameSaver
{
	public const int MaxSaves = 25;
	private const string SaveDirectory = "flagged_games";

	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

	/// <summary>
	/// Attempts to save a snapshot of the flagged game to disk.
	/// Returns the file path on success, or null if the cap has been reached.
	/// </summary>
	public static string? TrySave(
		int gameNumber,
		GameResult result,
		GameState finalState,
		MtgGameIds ids,
		int currentSaveCount
	)
	{
		if (currentSaveCount >= MaxSaves)
			return null;

		Directory.CreateDirectory(SaveDirectory);

		var reasonSlug = result.EndReason.ToString().ToLowerInvariant();
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		var fileName = $"game_{gameNumber:D4}_{reasonSlug}_{timestamp}.json";
		var filePath = Path.Combine(SaveDirectory, fileName);

		var snapshot = BuildSnapshot(result, finalState, ids);
		File.WriteAllText(filePath, JsonSerializer.Serialize(snapshot, JsonOptions));

		return filePath;
	}

	private static GameStateSnapshot BuildSnapshot(
		GameResult result,
		GameState state,
		MtgGameIds ids
	)
	{
		var game = state.GetGame(ids.GameId);
		var activePlayerName = game.ActivePlayerId == ids.Player1Id ? "Player 1" : "Player 2";

		return new GameStateSnapshot
		{
			FlagReason = result.EndReason.ToString(),
			DurationMs = result.GameDurationMs,
			TurnNumber = game.TurnNumber,
			ActivePlayerName = activePlayerName,
			TotalActions = result.TotalActions,
			Player1 = BuildPlayerSnapshot(state, ids.Player1Id),
			Player2 = BuildPlayerSnapshot(state, ids.Player2Id),
		};
	}

	private static PlayerSnapshot BuildPlayerSnapshot(GameState state, int playerId)
	{
		var player = state.GetPlayer(playerId);
		var handId = state.GetPlayerZoneId(playerId, ZoneType.Hand);
		var libraryId = state.GetPlayerZoneId(playerId, ZoneType.Library);
		var graveyardId = state.GetPlayerZoneId(playerId, ZoneType.Graveyard);
		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);

		return new PlayerSnapshot
		{
			Name = player.Name,
			Life = player.Life,
			CurrentMana = player.CurrentMana,
			MaxMana = player.MaxMana,
			HandCount = state.GetCardsInZone(handId).Count(),
			LibraryCount = state.GetCardsInZone(libraryId).Count(),
			GraveyardCards = state.GetCardsInZone(graveyardId).Select(c => c.Name).ToList(),
			Battlefield = state
				.GetCardsInZone(battlefieldId)
				.Select(c => BuildCreatureSnapshot(state, c))
				.ToList(),
		};
	}

	private static CreatureSnapshot BuildCreatureSnapshot(GameState state, Card card)
	{
		var creature = card.GetComponent<CreatureComponent>();
		return new CreatureSnapshot
		{
			Name = card.Name,
			Power = state.GetEffectivePower(card.Id),
			Toughness = state.GetEffectiveToughness(card.Id),
			Damage = creature?.Damage ?? 0,
			HasSummoningSickness = creature?.HasSummoningSickness ?? false,
			HasAttacked = creature?.HasAttacked ?? false,
		};
	}
}
