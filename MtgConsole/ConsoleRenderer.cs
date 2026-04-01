using ImmutableGameObjects;
using MtgCore;

namespace MtgConsole;

/// <summary>
/// Handles all console output for the game.
/// Separated from game logic — reads GameState, writes to console, nothing else.
/// </summary>
public static class ConsoleRenderer
{
	public static void RenderGameState(GameState state, MtgGameIds ids)
	{
		Console.Clear();
		Console.WriteLine("╔═══════════════════════════════════════╗");
		Console.WriteLine("║           MTG SANDBOX                 ║");
		Console.WriteLine("╚═══════════════════════════════════════╝");
		Console.WriteLine();

		var opponent = state.GetPlayer(ids.Player2Id);
		Console.WriteLine($"  OPPONENT  Life: {opponent.Life}");
		RenderZone(state, ids.Player2BattlefieldId, "Battlefield");
		RenderZone(state, ids.Player2GraveyardId, "Graveyard");
		Console.WriteLine();

		Console.WriteLine("  ─────────────────────────────────────");
		Console.WriteLine();

		var player = state.GetPlayer(ids.Player1Id);
		var libraryCount = state.GetCardsInZone(ids.Player1LibraryId).Count();
		Console.WriteLine($"  YOU  Life: {player.Life}  |  Library: {libraryCount} cards");
		RenderZone(state, ids.Player1BattlefieldId, "Battlefield");
		RenderZone(state, ids.Player1GraveyardId, "Graveyard");
		Console.WriteLine();
		RenderHand(state, ids.Player1HandId);
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
				Card card when card.HasComponent<CreatureComponent>() => FormatCreatureTarget(card),
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

	public static void RenderEvents(IEnumerable<GameEvent> events)
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
					$"  {GetPlayerName(pde.PlayerId)} takes {pde.Amount} damage",
				PlayerGainedLifeEvent pge =>
					$"  {GetPlayerName(pge.PlayerId)} gains {pge.Amount} life",
				PlayerLostLifeEvent ple =>
					$"  {GetPlayerName(ple.PlayerId)} loses {ple.Amount} life",
				CreatureDestroyedEvent cde => $"  Creature {cde.CreatureId} is destroyed",
				CreatureDamagedEvent cde =>
					$"  Creature {cde.CreatureId} takes {cde.Amount} damage",
				CardDrawnEvent cde => $"  Card drawn: {cde.CardId}",
				CardDiscardedEvent cde => $"  Card discarded: {cde.CardId}",
				CardRevealedEvent cre => $"  Card revealed: mana cost {cre.ManaCost}",
				LibraryEmptyEvent _ => $"  Library is empty!",
				CreaturePlayedEvent cpe => $"  {cpe.CardId} enters the battlefield",
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
		return creature != null
			? $"{card.Name} ({creature.Power}/{creature.Toughness}) [{card.ManaCost}]"
			: $"{card.Name} [{card.ManaCost}]";
	}

	private static string FormatCreatureTarget(Card card)
	{
		var creature = card.GetComponent<CreatureComponent>()!;
		return $"Creature: {card.Name} ({creature.Power}/{creature.Toughness})";
	}

	private static string GetPlayerName(int playerId) => playerId == 1 ? "You" : "Opponent";
}
