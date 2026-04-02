using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;

namespace MtgConsole;

public enum GameMode
{
	Hotseat,
	Ai,
}

/// <summary>
/// Drives the console game loop.
/// Handles player input, dispatches to game state, and renders output.
/// No game logic lives here — this is purely a UI layer.
///
/// Supports two modes:
///   Hotseat — both players take turns at the same keyboard
///   Ai      — Player 1 is human, Player 2 is a random action AI
/// </summary>
public class ConsoleGameLoop
{
	private GameState _state;
	private readonly MtgGameIds _ids;
	private readonly GameMode _mode;
	private bool _gameOver = false;

	public ConsoleGameLoop(GameState state, MtgGameIds ids, GameMode mode)
	{
		_state = state;
		_ids = ids;
		_mode = mode;
	}

	public void Run()
	{
		ConsoleRenderer.RenderMessage("Welcome to MTG Sandbox!");

		// Kick off the first turn — Player 1 starts but does NOT draw
		// (first turn draw skip is handled by not pushing StartTurnAction here)
		while (!_gameOver)
		{
			if (_state.IsWaitingForChoice)
			{
				HandleChoice();
				continue;
			}

			var activePlayerId = _state.GetActivePlayerId(_ids.GameId);

			if (_mode == GameMode.Ai && activePlayerId == _ids.Player2Id)
			{
				RunAiTurn();
				continue;
			}

			RunHumanTurn(activePlayerId);
		}

		Console.WriteLine();
		Console.WriteLine("  Thanks for playing!");
	}

	// ===== HUMAN TURN =====

	private void RunHumanTurn(int activePlayerId)
	{
		ConsoleRenderer.RenderGameState(_state, _ids);
		Console.WriteLine("  COMMANDS: [number] play card  |  attack  |  end turn  |  quit");
		Console.Write("  > ");

		var input = Console.ReadLine()?.Trim().ToLower() ?? "";

		switch (input)
		{
			case "quit"
			or "q":
				_gameOver = true;
				break;
			case "end turn"
			or "end"
			or "e":
				HandleEndTurn();
				break;
			case "attack"
			or "a":
				HandleAttack(activePlayerId);
				break;
			default:
				if (int.TryParse(input, out var cardIndex))
					HandlePlayCard(cardIndex, activePlayerId);
				else
					ConsoleRenderer.RenderMessage("Unknown command.");
				break;
		}
	}

	// ===== AI TURN =====

	private void RunAiTurn()
	{
		ConsoleRenderer.RenderGameState(_state, _ids);
		ConsoleRenderer.RenderMessage("Opponent is thinking...");
		WaitForKeyPress();

		var rng = new Random();

		while (true)
		{
			if (_gameOver)
				return;

			var actions = GenerateLegalActions(_ids.Player2Id);

			// AI always ends its turn if no other actions are available
			if (!actions.Any())
			{
				ConsoleRenderer.RenderAiAction("Ends turn.");
				HandleEndTurn();
				return;
			}

			// Randomly decide whether to end turn (gives ~25% chance each loop)
			// ensuring the AI doesn't always play everything it can
			var allOptions = actions.Append(null).ToList(); // null = end turn
			var chosen = allOptions[rng.Next(allOptions.Count)];

			if (chosen == null)
			{
				ConsoleRenderer.RenderAiAction("Ends turn.");
				HandleEndTurn();
				return;
			}

			ExecuteAiAction(chosen);
		}
	}

	/// <summary>
	/// Generates all legal actions the given player can take right now.
	/// Returns a flat list of GameAction — the game loop picks from these.
	/// </summary>
	private List<GameAction> GenerateLegalActions(int playerId)
	{
		var actions = new List<GameAction>();

		var handId = _state.GetPlayerZoneId(playerId, ZoneType.Hand);
		var battlefieldId = _state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		var opponentId = playerId == _ids.Player1Id ? _ids.Player2Id : _ids.Player1Id;
		var opponentBattlefieldId = _state.GetPlayerZoneId(opponentId, ZoneType.Battlefield);

		// Play creatures from hand
		foreach (var card in _state.GetCardsInZone(handId))
		{
			if (!card.HasComponent<CreatureComponent>())
				continue;

			var action = new PlayCreatureAction { CardId = card.Id, PlayerId = playerId };
			if (_state.TryAddAction(action).Success)
				actions.Add(action);
		}

		// Cast spells from hand (no targets for now — only no-target spells)
		foreach (var card in _state.GetCardsInZone(handId))
		{
			var spell = card.GetComponent<SpellComponent>();
			if (spell == null)
				continue;

			if (spell.Effects.Any(e => e.TargetingStrategy.RequiresUserSelection))
				continue;

			var castAction = new CastSpellAction
			{
				CardId = card.Id,
				CastingPlayerId = playerId,
				GameId = _ids.GameId,
				TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty,
			};
			if (_state.TryAddAction(castAction).Success)
				actions.Add(castAction);
		}

		// Attack with creatures
		var attackerCandidates = _state
			.GetCardsInZone(battlefieldId)
			.Where(c => c.HasComponent<CreatureComponent>())
			.ToList();

		var targets = new List<int> { opponentId };
		targets.AddRange(
			_state
				.GetCardsInZone(opponentBattlefieldId)
				.Where(c => c.HasComponent<CreatureComponent>())
				.Select(c => c.Id)
		);

		foreach (var attacker in attackerCandidates)
		{
			foreach (var targetId in targets)
			{
				var attack = new AttackAction
				{
					AttackerId = attacker.Id,
					TargetId = targetId,
					AttackingPlayerId = playerId,
				};
				if (_state.TryAddAction(attack).Success)
				{
					actions.Add(attack);
					break; // one valid target per attacker is enough to add it as an option
				}
			}
		}

		return actions;
	}

	private void ExecuteAiAction(GameAction action)
	{
		var description = action switch
		{
			PlayCreatureAction pca => $"Plays creature (card {pca.CardId})",
			CastSpellAction csa => $"Casts spell (card {csa.CardId})",
			AttackAction aa => $"Attacks target {aa.TargetId} with creature {aa.AttackerId}",
			_ => action.GetType().Name,
		};

		ConsoleRenderer.RenderAiAction(description);

		var (newState, success) = _state.TryAddAction(action);
		if (!success)
		{
			ConsoleRenderer.RenderAiAction("Action was invalid, skipping.");
			return;
		}

		var (finalState, events) = newState.ProcessAllActions();
		_state = finalState;

		ConsoleRenderer.RenderEvents(events, _ids);
		CheckGameOver(events);

		Thread.Sleep(600); // brief pause so the player can follow along
	}

	// ===== HUMAN ACTIONS =====

	private void HandlePlayCard(int cardIndex, int activePlayerId)
	{
		var handId = _state.GetPlayerZoneId(activePlayerId, ZoneType.Hand);
		var handCards = _state.GetCardsInZone(handId).ToList();

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
			HandlePlayCreature(cardObj, activePlayerId);
		else if (cardObj.HasComponent<SpellComponent>())
			HandleCastSpell(cardObj, activePlayerId);
		else
			ConsoleRenderer.RenderMessage("That card cannot be played.");
	}

	private void HandlePlayCreature(Card card, int activePlayerId)
	{
		var action = new PlayCreatureAction { CardId = card.Id, PlayerId = activePlayerId };

		var (newState, success) = _state.TryAddAction(action);
		if (!success)
		{
			ConsoleRenderer.RenderMessage("Cannot play that creature right now.");
			return;
		}

		var (finalState, events) = newState.ProcessAllActions();
		_state = finalState;

		ConsoleRenderer.RenderGameState(_state, _ids);
		ConsoleRenderer.RenderEvents(events, _ids);
		CheckGameOver(events);
		if (!_gameOver)
			WaitForKeyPress();
	}

	private void HandleCastSpell(Card card, int activePlayerId)
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
				CastingPlayerId = activePlayerId,
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
			CastingPlayerId = activePlayerId,
			GameId = _ids.GameId,
			TargetIds = targetIds,
		};

		var (newState, success) = _state.TryAddAction(castAction);
		if (!success)
		{
			ConsoleRenderer.RenderMessage("Cannot cast that spell right now.");
			return;
		}

		var (finalState, events) = newState.ProcessAllActions();
		_state = finalState;

		ConsoleRenderer.RenderGameState(_state, _ids);
		ConsoleRenderer.RenderEvents(events, _ids);
		CheckGameOver(events);
		if (!_gameOver)
			WaitForKeyPress();
	}

	private void HandleAttack(int activePlayerId)
	{
		var battlefieldId = _state.GetPlayerZoneId(activePlayerId, ZoneType.Battlefield);
		var opponentId = activePlayerId == _ids.Player1Id ? _ids.Player2Id : _ids.Player1Id;
		var opponentBattlefieldId = _state.GetPlayerZoneId(opponentId, ZoneType.Battlefield);

		var myCreatures = _state
			.GetCardsInZone(battlefieldId)
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

		var targets = new List<int> { opponentId };
		targets.AddRange(
			_state
				.GetCardsInZone(opponentBattlefieldId)
				.Where(c => c.HasComponent<CreatureComponent>())
				.Select(c => c.Id)
		);

		ConsoleRenderer.RenderGameState(_state, _ids);
		ConsoleRenderer.RenderMessage(
			$"Attacking with {attacker.Name} — choose a target (0 to cancel):"
		);
		ConsoleRenderer.RenderAttackTargets(_state, targets);

		var targetIndex = ReadIndex(1, targets.Count);
		if (targetIndex == null)
			return;

		var targetId = targets[targetIndex.Value - 1];

		var attackAction = new AttackAction
		{
			AttackerId = attacker.Id,
			TargetId = targetId,
			AttackingPlayerId = activePlayerId,
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
		ConsoleRenderer.RenderEvents(events, _ids);
		CheckGameOver(events);
		if (!_gameOver)
			WaitForKeyPress();
	}

	private void HandleEndTurn()
	{
		var action = new EndTurnAction
		{
			GameId = _ids.GameId,
			Player1Id = _ids.Player1Id,
			Player2Id = _ids.Player2Id,
		};

		var (newState, _) = _state.TryAddAction(action);
		var (finalState, events) = newState.ProcessAllActions();
		_state = finalState;

		ConsoleRenderer.RenderGameState(_state, _ids);
		ConsoleRenderer.RenderEvents(events, _ids);
		CheckGameOver(events);

		// In hotseat, pause so the next player can sit down
		if (!_gameOver && _mode == GameMode.Hotseat)
		{
			var nextPlayer = _state.GetGame(_ids.GameId).ActivePlayerId;
			var nextName = nextPlayer == _ids.Player1Id ? "Player 1" : "Player 2";
			ConsoleRenderer.RenderMessage($"{nextName}'s turn — press any key when ready.");
			WaitForKeyPress();
		}
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

		ConsoleRenderer.RenderEvents(events, _ids);
		CheckGameOver(events);

		if (!_state.IsWaitingForChoice && !_gameOver)
			WaitForKeyPress();
	}

	// ===== GAME OVER =====

	private void CheckGameOver(ImmutableList<GameEvent> events)
	{
		if (events.OfType<GameOverEvent>().Any())
		{
			_gameOver = true;
			ConsoleRenderer.RenderGameState(_state, _ids);
			ConsoleRenderer.RenderEvents(events, _ids);
			WaitForKeyPress();
		}
	}

	// ===== INPUT HELPERS =====

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

	private static void WaitForKeyPress()
	{
		Console.WriteLine();
		Console.WriteLine("  Press any key to continue...");
		Console.ReadKey(intercept: true);
	}
}
