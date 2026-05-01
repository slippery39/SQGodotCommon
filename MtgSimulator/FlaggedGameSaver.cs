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
		int currentSaveCount,
		IReadOnlyDictionary<int, string> cardNames
	)
	{
		if (currentSaveCount >= MaxSaves)
			return null;

		Directory.CreateDirectory(SaveDirectory);

		var reasonSlug = result.EndReason.ToString().ToLowerInvariant();
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		var fileName = $"game_{gameNumber:D4}_{reasonSlug}_{timestamp}.json";
		var filePath = Path.Combine(SaveDirectory, fileName);

		var snapshot = BuildSnapshot(result, finalState, cardNames);
		File.WriteAllText(filePath, JsonSerializer.Serialize(snapshot, JsonOptions));

		return filePath;
	}

	private static GameStateSnapshot BuildSnapshot(
		GameResult result,
		GameState state,
		IReadOnlyDictionary<int, string> cardNames
	)
	{
		var game = state.GetGame();
		var player1Id = state.GetWellKnownId(MtgObjectKeys.Player1);
		var player2Id = state.GetWellKnownId(MtgObjectKeys.Player2);
		var activePlayerName = game.ActivePlayerId == player1Id ? "Player 1" : "Player 2";
		var stackId = state.GetStackId();

		return new GameStateSnapshot
		{
			FlagReason = result.EndReason.ToString(),
			DurationMs = result.GameDurationMs,
			TurnNumber = game.TurnNumber,
			ActivePlayerName = activePlayerName,
			TotalActions = result.TotalActions,
			StackCards = [.. state.GetCardsInZone(stackId).Select(c => c.Name)],
			Player1 = BuildPlayerSnapshot(state, player1Id),
			Player2 = BuildPlayerSnapshot(state, player2Id),
			TurnLogs = BuildTurnLogs(result.AllEvents, player1Id, cardNames),
			ExceptionMessage = result.ExceptionMessage,
			ExceptionStackTrace = result.ExceptionStackTrace,
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

	private static List<TurnLog> BuildTurnLogs(
		IReadOnlyList<GameEvent> events,
		int player1Id,
		IReadOnlyDictionary<int, string> cardNames
	)
	{
		var turns = new List<TurnLog>();
		var currentEvents = new List<string>();
		var displayTurn = 0;
		var playerName = "Game Start";

		void FlushTurn()
		{
			if (currentEvents.Count > 0)
				turns.Add(
					new TurnLog
					{
						TurnNumber = displayTurn,
						PlayerName = playerName,
						Events = currentEvents.ToList(),
					}
				);
			currentEvents.Clear();
		}

		foreach (var e in events)
		{
			if (e is TurnStartedEvent ts)
			{
				FlushTurn();
				displayTurn++;
				playerName = ts.PlayerId == player1Id ? "Player 1" : "Player 2";
				continue;
			}

			var line = FormatEvent(e, player1Id, cardNames);
			if (line != null)
				currentEvents.Add(line);
		}

		FlushTurn();
		return turns;
	}

	private static string? FormatEvent(
		GameEvent e,
		int player1Id,
		IReadOnlyDictionary<int, string> cardNames
	) =>
		e switch
		{
			TurnEndedEvent ended => $"{P(ended.PlayerId, player1Id)}'s turn ended",
			CardDrawnEvent draw =>
				$"{P(draw.PlayerId, player1Id)} drew {C(draw.CardId, cardNames)}",
			SpellCastEvent cast =>
				$"{P(cast.CastingPlayerId, player1Id)} cast {C(cast.CardId, cardNames)}",
			CreaturePlayedEvent played =>
				$"{P(played.PlayerId, player1Id)} played {C(played.CardId, cardNames)}",
			SpellResolvedEvent resolved => $"{C(resolved.CardId, cardNames)} resolved",
			CreatureEnteredBattlefieldEvent entered =>
				$"{C(entered.CardId, cardNames)} entered the battlefield under {P(entered.PlayerId, player1Id)}'s control",
			CreatureAttackedEvent attacked => $"{C(attacked.CreatureId, cardNames)} attacked",
			CombatDamageDealtToPlayerEvent combatDmg =>
				$"{C(combatDmg.AttackerId, cardNames)} dealt {combatDmg.Amount} combat damage to {P(combatDmg.DefendingPlayerId, player1Id)}",
			PlayerDamagedEvent playerDmg =>
				$"{P(playerDmg.PlayerId, player1Id)} took {playerDmg.Amount} damage",
			PlayerGainedLifeEvent gained =>
				$"{P(gained.PlayerId, player1Id)} gained {gained.Amount} life",
			PlayerLostLifeEvent lostLife =>
				$"{P(lostLife.PlayerId, player1Id)} lost {lostLife.Amount} life",
			CreatureDamagedEvent creatureDmg =>
				$"{C(creatureDmg.CreatureId, cardNames)} took {creatureDmg.Amount} damage",
			CreatureDestroyedEvent destroyed =>
				$"{C(destroyed.CreatureId, cardNames)} was destroyed",
			CreatureModifiedEvent modified =>
				$"{C(modified.CreatureId, cardNames)} got {FormatBonus(modified.PowerBonus)}/{FormatBonus(modified.ToughnessBonus)}",
			CardDiscardedEvent discarded =>
				$"{P(discarded.PlayerId, player1Id)} discarded {C(discarded.CardId, cardNames)}",
			CardRevealedEvent revealed =>
				$"{P(revealed.PlayerId, player1Id)} revealed {C(revealed.CardId, cardNames)} (cost {revealed.ManaCost})",
			CardExiledEvent exiled => $"{C(exiled.CardId, cardNames)} was exiled",
			LibraryEmptyEvent empty => $"{P(empty.PlayerId, player1Id)}'s library is empty",
			PlayerLostEvent playerLost =>
				$"{P(playerLost.PlayerId, player1Id)} lost ({playerLost.Reason})",
			GameOverEvent { WinnerPlayerId: -1 } => "Game over: draw",
			GameOverEvent over => $"Game over: {P(over.WinnerPlayerId, player1Id)} wins",
			// Internal engine event consumed by CheckStateBasedEffectsAction — redundant for readers
			PermanentLeftBattlefieldEvent => null,
			_ => null,
		};

	private static string P(int playerId, int player1Id) =>
		playerId == player1Id ? "Player 1" : "Player 2";

	private static string C(int cardId, IReadOnlyDictionary<int, string> cardNames) =>
		cardNames.TryGetValue(cardId, out var name) ? name : $"[Card #{cardId}]";

	private static string FormatBonus(int bonus) => bonus >= 0 ? $"+{bonus}" : $"{bonus}";
}
