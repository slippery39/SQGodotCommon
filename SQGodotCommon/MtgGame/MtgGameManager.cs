using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;

namespace MtgGame;

/// <summary>
/// Owns the GameState and drives the game loop.
/// Plain C# class — no Godot dependencies. The Godot scene owns one instance.
/// </summary>
public class MtgGameManager
{
	private GameState _state;
	private readonly Random _rng = new();

	public int HumanPlayerId { get; private set; }
	public int AiPlayerId { get; private set; }

	public GameState State => _state;
	public bool IsAiTurn => _state.TryGetGame()?.ActivePlayerId == AiPlayerId;
	public bool IsWaitingForChoice => _state.IsWaitingForChoice;

	public MtgGameManager()
	{
		(_state, _) = MtgGameFactory.Create();
		HumanPlayerId = _state.GetWellKnownId(MtgObjectKeys.Player1);
		AiPlayerId = _state.GetWellKnownId(MtgObjectKeys.Player2);
		PopulateDecks();
	}

	public ImmutableList<GameEvent> StartGame()
	{
		(_state, var events) = _state.BeginGame(
			_state.GetWellKnownId(MtgObjectKeys.Game),
			HumanPlayerId,
			AiPlayerId
		);
		return events;
	}

	public (bool Success, ImmutableList<GameEvent> Events) SubmitAction(GameAction action)
	{
		var (newState, success) = _state.TryAddAction(action);
		if (!success)
			return (false, ImmutableList<GameEvent>.Empty);
		var (finalState, events) = newState.ProcessAllActions();
		_state = finalState;
		return (true, events);
	}

	public List<GameAction> GetLegalActions(int playerId)
	{
		return MtgActionGenerator.GetLegalActions(_state, playerId);
	}

	/// <summary>
	/// Executes one AI action (or ends the turn if no legal actions remain).
	/// Call repeatedly with a visual delay between calls until IsAiTurn is false.
	/// </summary>
	public ImmutableList<GameEvent> RunAiTurnStep()
	{
		if (!IsAiTurn || IsWaitingForChoice)
			return ImmutableList<GameEvent>.Empty;

		var actions = GetLegalActions(AiPlayerId);
		GameAction next =
			actions.Count > 0
				? actions[_rng.Next(actions.Count)]
				: new EndTurnAction
				{
					GameId = _state.GetWellKnownId(MtgObjectKeys.Game),
					Player1Id = HumanPlayerId,
					Player2Id = AiPlayerId,
				};

		var (_, events) = SubmitAction(next);
		return events;
	}

	// ===== DECK SETUP =====

	private void PopulateDecks()
	{
		AddCreaturesToLibrary(HumanPlayerId, MtgObjectKeys.Player1Library, HumanDeck);
		AddCreaturesToLibrary(AiPlayerId, MtgObjectKeys.Player2Library, AiDeck);
	}

	private void AddCreaturesToLibrary(
		int playerId,
		string libraryKey,
		(string Name, int Cost, int Power, int Toughness)[] cards
	)
	{
		var libraryId = _state.GetWellKnownId(libraryKey);
		foreach (var (name, cost, power, toughness) in cards)
		{
			var card = new Card
			{
				Name = name,
				ManaCost = cost,
				OwnerId = playerId,
				ControllerId = playerId,
				Components = ImmutableList.Create<GameComponent>(
					new CreatureComponent { Power = power, Toughness = toughness }
				),
			};
			(_state, _) = _state.AddObject(card, parentId: libraryId);
		}
	}

	private static readonly (string Name, int Cost, int Power, int Toughness)[] HumanDeck =
	[
		("Grizzly Bears", 2, 2, 2),
		("Hill Giant", 3, 3, 4),
		("Llanowar Elves", 1, 1, 1),
		("Serra Angel", 5, 4, 4),
		("Siege Rhino", 4, 4, 5),
		("Goblin Raider", 1, 2, 1),
		("Centaur Courser", 3, 3, 3),
		("Wind Drake", 3, 2, 2),
		("Iron Golem", 4, 4, 4),
		("Runeclaw Bear", 2, 2, 2),
		("Elvish Warrior", 2, 2, 3),
		("Jackal Pup", 1, 2, 1),
		("Bladetusk Boar", 4, 3, 3),
		("Kalonian Tusker", 3, 3, 3),
		("Goblin Guide", 1, 2, 2),
		("Craw Wurm", 6, 6, 4),
		("Savannah Lions", 1, 2, 1),
		("Raging Goblin", 1, 1, 1),
		("Mahamoti Djinn", 6, 5, 6),
		("Ancient Ooze", 7, 6, 6),
	];

	private static readonly (string Name, int Cost, int Power, int Toughness)[] AiDeck =
	[
		("Goblin Guide", 1, 2, 2),
		("Grizzly Bears", 2, 2, 2),
		("Craw Wurm", 6, 6, 4),
		("Wall of Stone", 3, 0, 8),
		("Hill Giant", 3, 3, 4),
		("Iron Golem", 4, 4, 4),
		("Serra Angel", 5, 4, 4),
		("Centaur Courser", 3, 3, 3),
		("Jackal Pup", 1, 2, 1),
		("Wind Drake", 3, 2, 2),
		("Goblin Raider", 1, 2, 1),
		("Elvish Warrior", 2, 2, 3),
		("Runeclaw Bear", 2, 2, 2),
		("Bladetusk Boar", 4, 3, 3),
		("Kalonian Tusker", 3, 3, 3),
		("Savannah Lions", 1, 2, 1),
		("Raging Goblin", 1, 1, 1),
		("Llanowar Elves", 1, 1, 1),
		("Mahamoti Djinn", 6, 5, 6),
		("Ancient Ooze", 7, 6, 6),
	];
}
