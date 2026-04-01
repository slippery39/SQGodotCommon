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
			Console.WriteLine("  COMMANDS: [number] play card  |  attack  |  quit");
			Console.Write("  > ");

			var input = Console.ReadLine()?.Trim().ToLower() ?? "";

			if (input == "quit" || input == "q")
				break;

			if (input == "attack" || input == "a")
				HandleAttack();
			else if (int.TryParse(input, out var cardIndex))
				HandlePlayCard(cardIndex);
			else
				ConsoleRenderer.RenderMessage(
					"Unknown command. Type a card number, 'attack', or 'quit'."
				);
		}

		Console.WriteLine("Thanks for playing!");
	}

	private void HandlePlayCard(int cardIndex)
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

		if (cardObj.HasComponent<CreatureComponent>())
			HandlePlayCreature(cardObj);
		else if (cardObj.HasComponent<SpellComponent>())
			HandleCastSpell(cardObj);
		else
			ConsoleRenderer.RenderMessage("That card cannot be played.");
	}

	private void HandlePlayCreature(Card card)
	{
		var action = new PlayCreatureAction { CardId = card.Id, PlayerId = _ids.Player1Id };

		var (newState, success) = _state.TryAddAction(action);
		if (!success)
		{
			ConsoleRenderer.RenderMessage("Cannot play that creature right now.");
			return;
		}

		var (finalState, events) = newState.ProcessAllActions();
		_state = finalState;

		ConsoleRenderer.RenderGameState(_state, _ids);
		ConsoleRenderer.RenderEvents(events);
		WaitForKeyPress();
	}

	// ===== ATTACK =====

	private void HandleAttack()
	{
		// Step 1: List your creatures that can attack
		var myCreatures = _state
			.GetCardsInZone(_ids.Player1BattlefieldId)
			.Where(c => c.HasComponent<CreatureComponent>())
			.ToList();

		if (!myCreatures.Any())
		{
			ConsoleRenderer.RenderMessage("You have no creatures on the battlefield.");
			return;
		}

		ConsoleRenderer.RenderGameState(_state, _ids);
		ConsoleRenderer.RenderMessage("Choose a creature to attack with (0 to cancel):");
		ConsoleRenderer.RenderAttackers(_state, myCreatures);

		var attackerIndex = ReadIndex(1, myCreatures.Count);
		if (attackerIndex == null)
			return;

		var attacker = myCreatures[attackerIndex.Value - 1];

		// Step 2: Build the list of valid targets (opponent player + opponent creatures)
		var targets = BuildAttackTargets();

		if (!targets.Any())
		{
			ConsoleRenderer.RenderMessage("No valid targets to attack.");
			return;
		}

		ConsoleRenderer.RenderGameState(_state, _ids);
		ConsoleRenderer.RenderMessage(
			$"Attacking with {attacker.Name} — choose a target (0 to cancel):"
		);
		ConsoleRenderer.RenderAttackTargets(_state, targets);

		var targetIndex = ReadIndex(1, targets.Count);
		if (targetIndex == null)
			return;

		var targetId = targets[targetIndex.Value - 1];

		// Step 3: Dispatch the attack
		var attackAction = new AttackAction
		{
			AttackerId = attacker.Id,
			TargetId = targetId,
			AttackingPlayerId = _ids.Player1Id,
		};

		var (newState, success) = _state.TryAddAction(attackAction);
		if (!success)
		{
			ConsoleRenderer.RenderMessage("Cannot perform that attack.");
			return;
		}

		var (finalState, events) = newState.ProcessAllActions();
		_state = finalState;

		ConsoleRenderer.RenderGameState(_state, _ids);
		ConsoleRenderer.RenderEvents(events);
		WaitForKeyPress();
	}

	/// <summary>
	/// Builds the ordered list of valid attack targets: opponent player first, then their creatures.
	/// Returns a list of IDs.
	/// </summary>
	private List<int> BuildAttackTargets()
	{
		var targets = new List<int>();

		// Opponent player is always a valid target
		targets.Add(_ids.Player2Id);

		// Opponent's creatures on the battlefield
		var opponentCreatures = _state
			.GetCardsInZone(_ids.Player2BattlefieldId)
			.Where(c => c.HasComponent<CreatureComponent>())
			.Select(c => c.Id);

		targets.AddRange(opponentCreatures);

		return targets;
	}

	// ===== CAST SPELL =====

	private void HandleCastSpell(Card card)
	{
		var spellComponent = card.GetComponent<SpellComponent>()!;
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
				$"Casting {card.Name} — choose target(s) for effect {i + 1}:"
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

	// ===== INPUT HELPERS =====

	/// <summary>
	/// Reads a number in [min, max] from stdin. Returns null if the user enters 0 (cancel).
	/// </summary>
	private int? ReadIndex(int min, int max)
	{
		Console.WriteLine($"  (Enter {min}-{max}, or 0 to cancel)");
		Console.Write("  > ");

		var input = Console.ReadLine()?.Trim() ?? "";

		if (input == "0")
			return null;

		if (int.TryParse(input, out var index) && index >= min && index <= max)
			return index;

		ConsoleRenderer.RenderMessage("Invalid selection.");
		return null;
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

		// Single-select: accept a plain number
		if (choice.MinChoices == 1 && choice.MaxChoices == 1)
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

		// Multi-select: accept comma-separated numbers (e.g. "1,3")
		Console.WriteLine($"  Enter comma-separated numbers (e.g. 1,3):");
		Console.Write("  > ");
		var multiInput = Console.ReadLine()?.Trim() ?? "";

		if (multiInput == "0")
			return null;

		var parts = multiInput
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

	private (bool success, (GameState state, ImmutableList<GameEvent> events) result) TryCast(
		CastSpellAction castAction
	)
	{
		var (newState, success) = _state.TryAddAction(castAction);
		if (!success)
		{
			ConsoleRenderer.RenderMessage("Cannot cast that spell right now.");
			return (false, default);
		}

		var result = newState.ProcessAllActions();
		return (true, result);
	}

	private static void WaitForKeyPress()
	{
		Console.WriteLine();
		Console.WriteLine("  Press any key to continue...");
		Console.ReadKey(intercept: true);
	}
}
