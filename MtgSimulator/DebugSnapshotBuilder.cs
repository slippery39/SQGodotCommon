using System.Text.Json;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

public static class DebugSnapshotBuilder
{
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

	public static string BuildJson(
		IReadOnlyList<(GameState State, string Description)> history,
		IReadOnlyList<(AiDecision Decision, int HistoryIndex)> aiDecisions
	)
	{
		if (history.Count == 0)
			return JsonSerializer.Serialize(new DebugSnapshot(), JsonOptions);

		var currentState = history[^1].State;
		var game = currentState.TryGetGame();
		var player1Id = currentState.GetWellKnownId(MtgObjectKeys.Player1);
		var player2Id = currentState.GetWellKnownId(MtgObjectKeys.Player2);

		var historyEntries = history
			.Select(
				(entry, i) =>
					new DebugHistoryEntry(
						i,
						entry.Description,
						BuildPlayerSnapshot(entry.State, player1Id),
						BuildPlayerSnapshot(entry.State, player2Id)
					)
			)
			.ToList();

		var snapshot = new DebugSnapshot
		{
			ExportedAt = DateTimeOffset.UtcNow.ToString("o"),
			TurnNumber = game?.TurnNumber ?? 0,
			ActivePlayerName = game?.ActivePlayerId == player1Id ? "Player 1" : "Player 2",
			CurrentPlayer1 = BuildPlayerSnapshot(currentState, player1Id),
			CurrentPlayer2 = BuildPlayerSnapshot(currentState, player2Id),
			StateHistory = historyEntries,
			AiDecisions = aiDecisions.Select(d => d.Decision).ToList(),
		};

		return JsonSerializer.Serialize(snapshot, JsonOptions);
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
			HandCards = [.. state.GetCardsInZone(handId).Select(c => c.Name)],
			LibraryCount = state.GetCardsInZone(libraryId).Count(),
			GraveyardCards = [.. state.GetCardsInZone(graveyardId).Select(c => c.Name)],
			Battlefield =
			[
				.. state.GetCardsInZone(battlefieldId).Select(c => BuildCreatureSnapshot(state, c)),
			],
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
