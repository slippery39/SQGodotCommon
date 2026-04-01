using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;

namespace MtgConsole;

/// <summary>
/// Drives the console sandbox game loop.
/// Handles player input, dispatches to game state, and renders output.
/// No game logic lives here — this is purely a UI layer.
/// </summary>
public class ConsoleGameLoop
{
	private GameState _state;
	private readonly MtgGameIds _ids;

	public ConsoleGameLoop(GameState state, MtgGameIds ids)
	{
		_state = state;
		_ids = ids;
	}

	public void Run()
	{
		ConsoleRenderer.RenderMessage("Welcome to MTG Sandbox! Type 'help' for commands.");

		while (true)
		{
			if (_state.IsWaitingForChoice)
			{
				HandleChoice();
				continue;
			}

			ConsoleRenderer.RenderGameState(_state, _ids);
			Console.WriteLine("  COMMANDS: [number] cast card  |  quit");
			Console.Write("  > ");

			var input = Console.ReadLine()?.Trim().ToLower() ?? "";

			if (input == "quit" || input == "q")
				break;

			if (int.TryParse(input, out var cardIndex))
				HandleCastCard(cardIndex);
			else
				ConsoleRenderer.RenderMessage("Unknown command. Type a card number to cast it.");
		}

		Console.WriteLine("Thanks for playing!");
	}

	private void HandleCastCard(int cardIndex)
	{
		var handCards = _state.GetCardsInZone(_ids.Player1HandId).ToList();

		if (cardIndex < 1 || cardIndex > handCards.Count)
		{
			ConsoleRenderer.RenderMessage(
				$"Invalid selection. Choose between 1 and {handCards.Count}."
			);
			return;
		}

		var card = handCards[cardIndex - 1];
		var cardObj = (Card)_state.GetObject(card.Id);
		var spellComponent = cardObj.GetComponent<SpellComponent>();

		if (spellComponent == null)
		{
			ConsoleRenderer.RenderMessage("That card is not a spell and cannot be cast.");
			return;
		}

		var targetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty;

		for (int i = 0; i < spellComponent.Effects.Count; i++)
		{
			var effect = spellComponent.Effects[i];
			if (!effect.TargetingStrategy.RequiresUserSelection)
				continue;

			var context = new TargetingContext
			{
				GameState = _state,
				SourceCardId = card.Id,
				CastingPlayerId = _ids.Player1Id,
			};

			var validTargets = effect.TargetingStrategy.GetValidTargets(context).ToList();

			if (!validTargets.Any())
			{
				ConsoleRenderer.RenderMessage("No valid targets available.");
				return;
			}

			ConsoleRenderer.RenderGameState(_state, _ids);
			ConsoleRenderer.RenderMessage(
				$"Casting {cardObj.Name} — choose target(s) for effect {i + 1}:"
			);
			ConsoleRenderer.RenderTargets(_state, _ids, validTargets);

			var chosen = GetTargetSelections(
				validTargets,
				effect.TargetingStrategy.MinTargets,
				effect.TargetingStrategy.MaxTargets
			);

			if (chosen == null)
				return;

			targetIds = targetIds.Add(i, chosen);
		}

		var castAction = new CastSpellAction
		{
			CardId = card.Id,
			CastingPlayerId = _ids.Player1Id,
			GameId = _ids.GameId,
			TargetIds = targetIds,
		};

		var (success, newState) = TryCast(castAction);
		if (!success)
			return;

		_state = newState.state;

		ConsoleRenderer.RenderGameState(_state, _ids);
		ConsoleRenderer.RenderEvents(newState.events);

		WaitForKeyPress();
	}

	private void HandleChoice()
	{
		var choice = _state.GetPendingChoice()!;

		ConsoleRenderer.RenderGameState(_state, _ids);
		ConsoleRenderer.RenderChoice(choice);
		Console.WriteLine();

		var chosen = GetChoiceSelections(choice);
		if (chosen == null)
			return;

		var (newState, events) = _state.ResolveChoice(chosen);
		_state = newState;

		ConsoleRenderer.RenderEvents(events);

		if (!_state.IsWaitingForChoice)
			WaitForKeyPress();
	}

	private ImmutableList<int>? GetTargetSelections(
		IReadOnlyList<int> validTargets,
		int min,
		int max
	)
	{
		Console.WriteLine(
			$"  (Select {min}" + (max > min ? $"-{max}" : "") + " target(s), or 0 to cancel)"
		);
		Console.Write("  > ");

		var input = Console.ReadLine()?.Trim() ?? "";

		if (input == "0")
			return null;

		if (int.TryParse(input, out var index) && index >= 1 && index <= validTargets.Count)
			return ImmutableList.Create(validTargets[index - 1]);

		ConsoleRenderer.RenderMessage("Invalid selection.");
		return null;
	}

	private ImmutableList<int>? GetChoiceSelections(ChoiceAction choice)
	{
		Console.WriteLine(
			$"  (Select {choice.MinChoices}"
				+ (choice.MaxChoices > choice.MinChoices ? $"-{choice.MaxChoices}" : "")
				+ " option(s), or 0 to cancel)"
		);

		if (choice.MinChoices == choice.MaxChoices && choice.MinChoices == 1)
		{
			Console.Write("  > ");
			var input = Console.ReadLine()?.Trim() ?? "";

			if (input == "0")
				return null;

			if (int.TryParse(input, out var index) && index >= 1 && index <= choice.Options.Count)
				return ImmutableList.Create(choice.Options[index - 1].Id);

			ConsoleRenderer.RenderMessage("Invalid selection.");
			return null;
		}
		else
		{
			Console.WriteLine($"  Enter comma-separated numbers (e.g. 1,3):");
			Console.Write("  > ");
			var input = Console.ReadLine()?.Trim() ?? "";

			if (input == "0")
				return null;

			var parts = input
				.Split(',')
				.Select(p => p.Trim())
				.Where(p => int.TryParse(p, out _))
				.Select(int.Parse)
				.ToList();

			if (parts.Any(p => p < 1 || p > choice.Options.Count))
			{
				ConsoleRenderer.RenderMessage("Invalid selection.");
				return null;
			}

			if (parts.Count < choice.MinChoices || parts.Count > choice.MaxChoices)
			{
				ConsoleRenderer.RenderMessage(
					$"Must select between {choice.MinChoices} and {choice.MaxChoices} options."
				);
				return null;
			}

			return parts.Select(p => choice.Options[p - 1].Id).ToImmutableList();
		}
	}

	private (bool success, (GameState state, ImmutableList<GameEvent> events)) TryCast(
		CastSpellAction action
	)
	{
		var (newState, success) = _state.TryAddAction(action);
		if (!success)
		{
			ConsoleRenderer.RenderMessage("Cannot cast that card right now.");
			return (false, default);
		}

		var (finalState, events) = newState.ProcessAllActions();
		return (true, (finalState, events));
	}

	private static void WaitForKeyPress()
	{
		Console.WriteLine();
		Console.WriteLine("  Press any key to continue...");
		Console.ReadKey(intercept: true);
	}
}
