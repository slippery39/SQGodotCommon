using ImmutableGameObjects;
using MtgCore;

namespace MtgConsole;

/// <summary>
/// Renders game state and events to the console.
/// Separated from game logic — reads GameState, writes to console, nothing else.
///
/// All rendering is perspective-aware — it derives the active player and opponent
/// from the game state rather than assuming a fixed Player 1 perspective.
/// </summary>
public static class ConsoleRenderer
{
	public static void RenderGameState(GameState state, MtgGameIds ids)
	{
		var game = state.GetGame(ids.GameId);
		var activePlayerId = game.ActivePlayerId;
		var opponentId = activePlayerId == ids.Player1Id ? ids.Player2Id : ids.Player1Id;

		var activeBattlefieldId = state.GetPlayerZoneId(activePlayerId, ZoneType.Battlefield);
		var activeGraveyardId = state.GetPlayerZoneId(activePlayerId, ZoneType.Graveyard);
		var activeLibraryId = state.GetPlayerZoneId(activePlayerId, ZoneType.Library);
		var activeHandId = state.GetPlayerZoneId(activePlayerId, ZoneType.Hand);

		var opponentBattlefieldId = state.GetPlayerZoneId(opponentId, ZoneType.Battlefield);
		var opponentGraveyardId = state.GetPlayerZoneId(opponentId, ZoneType.Graveyard);

		var activePlayer = state.GetPlayer(activePlayerId);
		var opponent = state.GetPlayer(opponentId);

		Console.Clear();
		Console.WriteLine("╔══════════════════════════════════════════╗");
		Console.WriteLine($"║  MTG SANDBOX  |  Turn {game.TurnNumber, -2}                 ║");
		Console.WriteLine("╚══════════════════════════════════════════╝");
		Console.WriteLine();

		Console.WriteLine($"  OPPONENT ({opponent.Name})  Life: {opponent.Life}");
		RenderZone(state, opponentBattlefieldId, "Battlefield");
		RenderZone(state, opponentGraveyardId, "Graveyard");
		Console.WriteLine();

		Console.WriteLine("  ─────────────────────────────────────────");
		Console.WriteLine();

		var libraryCount = state.GetCardsInZone(activeLibraryId).Count();
		Console.WriteLine(
			$"  YOU ({activePlayer.Name})  Life: {activePlayer.Life}  |  Library: {libraryCount} cards"
		);
		RenderZone(state, activeBattlefieldId, "Battlefield");
		RenderZone(state, activeGraveyardId, "Graveyard");
		Console.WriteLine();
		RenderHand(state, activeHandId);
		Console.WriteLine();
	}

	public static void RenderHand(GameState state, int handId)
	{
		var cards = state.GetCardsInZone(handId).ToList();
		Console.WriteLine("  YOUR HAND:");

		if (!cards.Any())
		{
			Console.WriteLine("    (empty)");
			return;
		}

		for (int i = 0; i < cards.Count; i++)
			Console.WriteLine($"    [{i + 1}] {FormatCard(cards[i])}");
	}

	public static void RenderAttackers(GameState state, IReadOnlyList<Card> creatures)
	{
		Console.WriteLine("  YOUR CREATURES:");
		for (int i = 0; i < creatures.Count; i++)
			Console.WriteLine($"    [{i + 1}] {FormatCreature(creatures[i])}");
	}

	public static void RenderAttackTargets(GameState state, IReadOnlyList<int> targetIds)
	{
		Console.WriteLine("  VALID TARGETS:");
		for (int i = 0; i < targetIds.Count; i++)
		{
			var id = targetIds[i];
			var obj = state.GetObject(id);
			var label = obj switch
			{
				MtgPlayer player => $"Player: {player.Name} (Life: {player.Life})",
				Card card when card.HasComponent<CreatureComponent>() => FormatCreature(card),
				_ => $"Object {id}",
			};
			Console.WriteLine($"    [{i + 1}] {label}");
		}
	}

	public static void RenderTargets(
		GameState state,
		MtgGameIds ids,
		IReadOnlyList<int> validTargetIds
	)
	{
		Console.WriteLine("  VALID TARGETS:");
		for (int i = 0; i < validTargetIds.Count; i++)
		{
			var id = validTargetIds[i];
			var obj = state.GetObject(id);
			var label = obj switch
			{
				MtgPlayer player => $"Player: {player.Name} (Life: {player.Life})",
				Card card when card.HasComponent<CreatureComponent>() => FormatCreature(card),
				Card card => $"Card: {card.Name}",
				_ => $"Object {id}",
			};
			Console.WriteLine($"    [{i + 1}] {label}");
		}
	}

	public static void RenderChoice(ChoiceAction choice)
	{
		Console.WriteLine($"  CHOOSE: {choice.Prompt}");
		Console.WriteLine(
			$"  (Select {choice.MinChoices}"
				+ (choice.MaxChoices > choice.MinChoices ? $"-{choice.MaxChoices}" : "")
				+ " option(s))"
		);
		Console.WriteLine();

		for (int i = 0; i < choice.Options.Count; i++)
			Console.WriteLine($"    [{i + 1}] {choice.Options[i].DisplayText}");
	}

	public static void RenderEvents(IEnumerable<GameEvent> events, MtgGameIds ids)
	{
		var eventList = events.ToList();
		if (!eventList.Any())
			return;

		Console.WriteLine();
		Console.WriteLine("  --- Events ---");
		foreach (var e in eventList)
		{
			var message = e switch
			{
				PlayerDamagedEvent pde =>
					$"  {GetPlayerName(pde.PlayerId, ids)} takes {pde.Amount} damage",
				PlayerGainedLifeEvent pge =>
					$"  {GetPlayerName(pge.PlayerId, ids)} gains {pge.Amount} life",
				PlayerLostLifeEvent ple =>
					$"  {GetPlayerName(ple.PlayerId, ids)} loses {ple.Amount} life",
				PlayerLostEvent ple =>
					$"  {GetPlayerName(ple.PlayerId, ids)} has lost: {ple.Reason}",
				GameOverEvent goe when goe.WinnerPlayerId == -1 => $"  The game is a draw!",
				GameOverEvent goe => $"  {GetPlayerName(goe.WinnerPlayerId, ids)} wins!",
				TurnStartedEvent tse => $"  {GetPlayerName(tse.PlayerId, ids)}'s turn begins",
				TurnEndedEvent tee => $"  {GetPlayerName(tee.PlayerId, ids)}'s turn ends",
				CreatureDestroyedEvent cde => $"  Creature #{cde.CreatureId} is destroyed",
				CreatureDamagedEvent cde =>
					$"  Creature #{cde.CreatureId} takes {cde.Amount} damage",
				CardDrawnEvent cde => $"  {GetPlayerName(cde.PlayerId, ids)} draws a card",
				CardDiscardedEvent cde => $"  {GetPlayerName(cde.PlayerId, ids)} discards a card",
				CardRevealedEvent cre => $"  Card revealed: mana cost {cre.ManaCost}",
				LibraryEmptyEvent lse =>
					$"  {GetPlayerName(lse.PlayerId, ids)}'s library is empty!",
				CreaturePlayedEvent cpe => $"  {GetPlayerName(cpe.PlayerId, ids)} plays a creature",
				_ => $"  {e.GetType().Name}",
			};
			Console.WriteLine(message);
		}
		Console.WriteLine("  --------------");
	}

	public static void RenderMessage(string message)
	{
		Console.WriteLine();
		Console.WriteLine($"  > {message}");
	}

	public static void RenderAiAction(string description)
	{
		Console.WriteLine($"  [AI] {description}");
	}

	private static void RenderZone(GameState state, int zoneId, string label)
	{
		var cards = state.GetCardsInZone(zoneId).ToList();
		if (!cards.Any())
			return;

		Console.WriteLine($"    {label}: {string.Join(", ", cards.Select(FormatCard))}");
	}

	private static string FormatCard(Card card)
	{
		var creature = card.GetComponent<CreatureComponent>();
		return creature != null ? FormatCreature(card) : $"{card.Name} [{card.ManaCost}]";
	}

	private static string FormatCreature(Card card)
	{
		var creature = card.GetComponent<CreatureComponent>()!;
		var sickStr = creature.HasSummoningSickness ? " (sick)" : "";
		var attackedStr = creature.HasAttacked ? " (attacked)" : "";
		var damageStr = creature.Damage > 0 ? $" *{creature.Damage} dmg*" : "";
		return $"{card.Name} ({creature.Power}/{creature.Toughness}){sickStr}{attackedStr}{damageStr} [{card.ManaCost}]";
	}

	private static string GetPlayerName(int playerId, MtgGameIds ids) =>
		playerId == ids.Player1Id ? "Player 1" : "Player 2";
}
