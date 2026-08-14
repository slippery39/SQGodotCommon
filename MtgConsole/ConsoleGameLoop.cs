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

		(_state, var beginEvents) = _state.BeginGame(_ids.GameId, _ids.Player1Id, _ids.Player2Id);
		CheckGameOver(beginEvents);

		while (!_gameOver)
		{
			if (_state.IsWaitingForChoice)
			{
				HandleChoice();
				continue;
			}

			var activePlayerId = _state.GetActivePlayerId();

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
		Console.WriteLine(
			"  COMMANDS: [number] play card  |  attack  |  ability  |  end turn  |  quit"
		);
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
			case "ability"
			or "ab":
				HandleAbility(activePlayerId);
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

			var actions = MtgActionGenerator.GetLegalActions(_state, _ids, _ids.Player2Id);

			if (!actions.Any())
			{
				ConsoleRenderer.RenderAiAction("Ends turn.");
				HandleEndTurn();
				return;
			}

			var chosen = actions[rng.Next(actions.Count)];
			ExecuteAiAction(chosen);
		}
	}

	private void ExecuteAiAction(GameAction action)
	{
		var description = action switch
		{
			CastCreatureAction cca => $"Plays creature (card {cca.CardId})",
			CastSpellAction csa => $"Casts spell (card {csa.CardId})",
			AttackAction aa => $"Attacks target {aa.TargetId} with creature {aa.AttackerId}",
			ActivateAbilityAction aaa =>
				$"Activates ability {aaa.AbilityIndex} on card {aaa.CardId}",
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

	private void HandleAbility(int activePlayerId)
	{
		var battlefieldId = _state.GetPlayerZoneId(activePlayerId, ZoneType.Battlefield);

		// Find all cards with at least one usable activated ability
		var cardsWithAbilities = _state
			.GetCardsInZone(battlefieldId)
			.Where(c => c.GetComponents<ActivatedAbilityComponent>().Any())
			.ToList();

		if (!cardsWithAbilities.Any())
		{
			ConsoleRenderer.RenderMessage("No cards with activated abilities on the battlefield.");
			return;
		}

		ConsoleRenderer.RenderGameState(_state, _ids);
		ConsoleRenderer.RenderMessage("Choose a card to activate an ability on (0 to cancel):");
		ConsoleRenderer.RenderCardsWithAbilities(_state, cardsWithAbilities);

		var cardIndex = ReadIndex(1, cardsWithAbilities.Count);
		if (cardIndex == null)
			return;

		var card = cardsWithAbilities[cardIndex.Value - 1];
		var abilities = card.GetComponents<ActivatedAbilityComponent>().ToList();

		ConsoleRenderer.RenderGameState(_state, _ids);
		ConsoleRenderer.RenderMessage(
			$"Choose an ability to activate on {card.Name} (0 to cancel):"
		);
		ConsoleRenderer.RenderAbilities(abilities);

		var abilityIndex = ReadIndex(1, abilities.Count);
		if (abilityIndex == null)
			return;

		var ability = abilities[abilityIndex.Value - 1];
		var targetIds = ImmutableList<int>.Empty;

		if (ability.TargetedEffect?.TargetingStrategy.RequiresUserSelection == true)
		{
			var context = new TargetingContext
			{
				GameState = _state,
				SourceCardId = card.Id,
				CastingPlayerId = activePlayerId,
			};

			var validTargets = ability
				.TargetedEffect!.TargetingStrategy.GetValidTargets(context)
				.ToList();

			if (!validTargets.Any())
			{
				ConsoleRenderer.RenderMessage("No valid targets available.");
				return;
			}

			ConsoleRenderer.RenderGameState(_state, _ids);
			ConsoleRenderer.RenderMessage($"Choose a target for {ability.Name} (0 to cancel):");
			ConsoleRenderer.RenderAttackTargets(_state, validTargets);

			var targetIndex = ReadIndex(1, validTargets.Count);
			if (targetIndex == null)
				return;

			targetIds = ImmutableList.Create(validTargets[targetIndex.Value - 1]);
		}

		var abilityAction = new ActivateAbilityAction
		{
			CardId = card.Id,
			ActivatingPlayerId = activePlayerId,
			AbilityIndex = abilityIndex.Value - 1,
			TargetIds = targetIds,
		};

		var (newState, success) = _state.TryAddAction(abilityAction);
		if (!success)
		{
			ConsoleRenderer.RenderMessage("Cannot activate that ability right now.");
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

	private void HandlePlayCreature(Card card, int activePlayerId)
	{
		// Auto-select first valid payment for selection costs (console UI enhancement deferred)
		var additionalCostPayments = ImmutableDictionary<int, ImmutableList<int>>.Empty;
		for (int i = 0; i < card.AdditionalCastCosts.Count; i++)
		{
			var cost = card.AdditionalCastCosts[i];
			if (!cost.RequiresSelection)
				continue;
			var validPayments = cost.GetValidPayments(_state, activePlayerId, card.Id);
			if (validPayments.IsEmpty)
			{
				ConsoleRenderer.RenderMessage("Cannot pay additional cost — no valid options.");
				return;
			}
			additionalCostPayments = additionalCostPayments.Add(
				i,
				ImmutableList.Create(validPayments[0])
			);
		}

		var action = new CastCreatureAction
		{
			CardId = card.Id,
			CastingPlayerId = activePlayerId,
			AdditionalCostPayments = additionalCostPayments,
		};

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

		// Auto-select first valid payment for selection costs (console UI enhancement deferred)
		var additionalCostPayments = ImmutableDictionary<int, ImmutableList<int>>.Empty;
		for (int i = 0; i < card.AdditionalCastCosts.Count; i++)
		{
			var cost = card.AdditionalCastCosts[i];
			if (!cost.RequiresSelection)
				continue;
			var validPayments = cost.GetValidPayments(_state, activePlayerId, card.Id);
			if (validPayments.IsEmpty)
			{
				ConsoleRenderer.RenderMessage("Cannot pay additional cost — no valid options.");
				return;
			}
			additionalCostPayments = additionalCostPayments.Add(
				i,
				ImmutableList.Create(validPayments[0])
			);
		}

		var castAction = new CastSpellAction
		{
			CardId = card.Id,
			CastingPlayerId = activePlayerId,
			TargetIds = targetIds,
			AdditionalCostPayments = additionalCostPayments,
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
