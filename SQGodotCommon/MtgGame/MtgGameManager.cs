using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;

namespace MtgGame;

public enum AiStrategyType
{
	BeamSearch,
	MultiTurnBeamSearch,
}

/// <summary>
/// Owns the GameState and drives the game loop.
/// Plain C# class — no Godot dependencies. The Godot scene owns one instance.
/// </summary>
public class MtgGameManager
{
	private GameState _state;
	private readonly Random _rng = new();
#pragma warning disable CS0618
	private readonly MtgGameIds _ids;
#pragma warning restore CS0618
	private readonly ICapturingAiStrategy _aiStrategy;

	private record HistoryEntry(GameState State, string ActionDescription);

	private readonly List<HistoryEntry> _history = new();
	private readonly List<(AiDecision Decision, int HistoryIndex)> _aiDecisions = new();

	public int HumanPlayerId { get; private set; }
	public int AiPlayerId { get; private set; }

	public GameState State => _state;
	public bool IsAiTurn => _state.TryGetGame()?.ActivePlayerId == AiPlayerId;
	public bool IsWaitingForChoice => _state.IsWaitingForChoice;

	public MtgGameManager(
		DeckSetupData setup,
		AiStrategyType strategyType = AiStrategyType.MultiTurnBeamSearch
	)
	{
#pragma warning disable CS0618
		(_state, _ids) = MtgGameFactory.Create();
		_aiStrategy = strategyType switch
		{
			AiStrategyType.MultiTurnBeamSearch => new MultiTurnBeamSearchAiStrategy(
				_ids,
				currentTurnDepth: 3,
				lookaheadTurns: 2,
				captureDecisions: true
			),
			_ => new BeamSearchAiStrategy(_ids, maxDepth: 3, captureDecisions: true),
		};
#pragma warning restore CS0618
		HumanPlayerId = _state.GetWellKnownId(MtgObjectKeys.Player1);
		AiPlayerId = _state.GetWellKnownId(MtgObjectKeys.Player2);
		PopulateDecks(setup);
	}

	public ImmutableList<GameEvent> StartGame()
	{
		(_state, var events) = _state.BeginGame(
			_state.GetWellKnownId(MtgObjectKeys.Game),
			HumanPlayerId,
			AiPlayerId
		);
		_history.Add(new HistoryEntry(_state, "Game Start"));
		return events;
	}

	public (bool Success, ImmutableList<GameEvent> Events) SubmitAction(GameAction action)
	{
		var preActionState = _state;
		var (newState, success) = _state.TryAddAction(action);
		if (!success)
			return (false, ImmutableList<GameEvent>.Empty);
		var (finalState, events) = newState.ProcessAllActions();
		_state = finalState;
		_history.Add(new HistoryEntry(_state, ActionDescriber.Describe(action, preActionState)));
		return (true, events);
	}

	public List<GameAction> GetLegalActions(int playerId)
	{
		return MtgActionGenerator.GetLegalActions(_state, playerId);
	}

	public ImmutableList<GameEvent> EndTurn()
	{
		var (_, events) = SubmitAction(
			new EndTurnAction
			{
				GameId = _state.GetWellKnownId(MtgObjectKeys.Game),
				Player1Id = HumanPlayerId,
				Player2Id = AiPlayerId,
			}
		);
		return events;
	}

	public (bool Success, ImmutableList<GameEvent> Events) CastCreature(int cardId)
	{
		return SubmitAction(
			new CastCreatureAction { CardId = cardId, CastingPlayerId = HumanPlayerId }
		);
	}

	public (bool Success, ImmutableList<GameEvent> Events) Attack(int attackerId, int targetId)
	{
		return SubmitAction(
			new AttackAction
			{
				AttackerId = attackerId,
				TargetId = targetId,
				AttackingPlayerId = HumanPlayerId,
			}
		);
	}

	public (bool Success, ImmutableList<GameEvent> Events) CastSpell(
		int cardId,
		ImmutableDictionary<int, ImmutableList<int>> targetIds
	)
	{
		return CastSpell(cardId, targetIds, ImmutableDictionary<int, ImmutableList<int>>.Empty);
	}

	public (bool Success, ImmutableList<GameEvent> Events) CastSpell(
		int cardId,
		ImmutableDictionary<int, ImmutableList<int>> targetIds,
		ImmutableDictionary<int, ImmutableList<int>> additionalCostPayments
	)
	{
		return SubmitAction(
			new CastSpellAction
			{
				CardId = cardId,
				CastingPlayerId = HumanPlayerId,
				TargetIds = targetIds,
				AdditionalCostPayments = additionalCostPayments,
			}
		);
	}

	public bool HasFlashback(int cardId)
	{
		if (!_state.HasObject(cardId))
			return false;
		var card = _state.GetObject(cardId) as Card;
		return card?.HasComponent<FlashbackComponent>() == true;
	}

	public (bool Success, ImmutableList<GameEvent> Events) CastFromGraveyard(
		int cardId,
		ImmutableDictionary<int, ImmutableList<int>> targetIds
	)
	{
		return SubmitAction(
			new CastFromGraveyardAction
			{
				CardId = cardId,
				CastingPlayerId = HumanPlayerId,
				TargetIds = targetIds,
			}
		);
	}

	public bool IsSpell(int cardId)
	{
		var card = _state.GetObject(cardId) as Card;
		return card?.GetComponent<SpellComponent>() != null;
	}

	public bool IsLand(int cardId)
	{
		var card = _state.GetObject(cardId) as Card;
		return card?.HasSubtype("Land") ?? false;
	}

	public (bool Success, ImmutableList<GameEvent> Events) PlayLand(int cardId)
	{
		return SubmitAction(
			new PlayLandAction { CardId = cardId, CastingPlayerId = HumanPlayerId }
		);
	}

	public bool IsNonCreaturePermanent(int cardId)
	{
		var card = _state.GetObject(cardId) as Card;
		return card != null
			&& card.HasComponent<PermanentComponent>()
			&& !card.HasComponent<CreatureComponent>();
	}

	public (bool Success, ImmutableList<GameEvent> Events) CastPermanent(int cardId)
	{
		return SubmitAction(
			new CastPermanentAction { CardId = cardId, CastingPlayerId = HumanPlayerId }
		);
	}

	public bool SpellNeedsTargets(int cardId)
	{
		var card = _state.GetObject(cardId) as Card;
		var spell = card?.GetComponent<SpellComponent>();
		return spell?.Effects.Any(e => e.TargetingStrategy.RequiresUserSelection) ?? false;
	}

	public List<int> GetSpellValidTargets(int cardId, int effectIndex)
	{
		var card = _state.GetObject(cardId) as Card;
		var spell = card?.GetComponent<SpellComponent>();
		if (spell == null || effectIndex < 0 || effectIndex >= spell.Effects.Count)
			return new List<int>();

		var context = new TargetingContext
		{
			GameState = _state,
			SourceCardId = cardId,
			CastingPlayerId = HumanPlayerId,
		};
		return spell.Effects[effectIndex].TargetingStrategy.GetValidTargets(context).ToList();
	}

	public int GetNextEffectNeedingTarget(int cardId, int afterIndex)
	{
		var card = _state.GetObject(cardId) as Card;
		var spell = card?.GetComponent<SpellComponent>();
		if (spell == null)
			return -1;
		for (int i = afterIndex + 1; i < spell.Effects.Count; i++)
		{
			if (spell.Effects[i].TargetingStrategy.RequiresUserSelection)
				return i;
		}
		return -1;
	}

	// ===== ADDITIONAL COST HELPERS =====

	public bool SpellHasAdditionalCostSelection(int cardId)
	{
		var card = _state.GetObject(cardId) as Card;
		return card?.AdditionalCastCosts.Any(c => c.RequiresSelection) ?? false;
	}

	public List<int> GetAdditionalCostValidPayments(int cardId, int costIndex)
	{
		var card = _state.GetObject(cardId) as Card;
		if (card == null || costIndex < 0 || costIndex >= card.AdditionalCastCosts.Count)
			return new List<int>();
		return card.AdditionalCastCosts[costIndex]
			.GetValidPayments(_state, HumanPlayerId, cardId)
			.ToList();
	}

	public int GetNextAdditionalCostNeedingSelection(int cardId, int afterIndex)
	{
		var card = _state.GetObject(cardId) as Card;
		if (card == null)
			return -1;
		for (int i = afterIndex + 1; i < card.AdditionalCastCosts.Count; i++)
			if (card.AdditionalCastCosts[i].RequiresSelection)
				return i;
		return -1;
	}

	// ===== ACTIVATED ABILITY HELPERS =====

	public List<(int Index, string Name, int ManaCost)> GetLegalAbilities(int cardId)
	{
		var legalActions = MtgActionGenerator.GetLegalActions(_state, HumanPlayerId);
		var result = new List<(int, string, int)>();
		var card = _state.GetObject(cardId) as Card;
		if (card == null)
			return result;
		var abilities = card.GetComponents<ActivatedAbilityComponent>().ToList();
		foreach (var action in legalActions.OfType<ActivateAbilityAction>())
		{
			if (action.CardId != cardId)
				continue;
			if (action.AbilityIndex >= abilities.Count)
				continue;
			var ability = abilities[action.AbilityIndex];
			result.Add((action.AbilityIndex, ability.Name, ability.ManaCost));
		}
		return result;
	}

	public bool AbilityNeedsTarget(int cardId, int abilityIndex)
	{
		var card = _state.GetObject(cardId) as Card;
		var abilities = card?.GetComponents<ActivatedAbilityComponent>().ToList();
		if (abilities == null || abilityIndex >= abilities.Count)
			return false;
		return abilities[abilityIndex].Effect.TargetingStrategy.RequiresUserSelection;
	}

	public List<int> GetAbilityValidTargets(int cardId, int abilityIndex)
	{
		var card = _state.GetObject(cardId) as Card;
		var abilities = card?.GetComponents<ActivatedAbilityComponent>().ToList();
		if (abilities == null || abilityIndex >= abilities.Count)
			return new List<int>();
		var context = new TargetingContext
		{
			GameState = _state,
			SourceCardId = cardId,
			CastingPlayerId = HumanPlayerId,
		};
		return abilities[abilityIndex].Effect.TargetingStrategy.GetValidTargets(context).ToList();
	}

	public bool AbilityHasAdditionalCostSelection(int cardId, int abilityIndex)
	{
		var card = _state.GetObject(cardId) as Card;
		var abilities = card?.GetComponents<ActivatedAbilityComponent>().ToList();
		if (abilities == null || abilityIndex >= abilities.Count)
			return false;
		return abilities[abilityIndex].AdditionalCosts.Any(c => c.RequiresSelection);
	}

	public List<int> GetAbilityAdditionalCostValidPayments(
		int cardId,
		int abilityIndex,
		int costIndex
	)
	{
		var card = _state.GetObject(cardId) as Card;
		var abilities = card?.GetComponents<ActivatedAbilityComponent>().ToList();
		if (abilities == null || abilityIndex >= abilities.Count)
			return new List<int>();
		var costs = abilities[abilityIndex].AdditionalCosts;
		if (costIndex < 0 || costIndex >= costs.Count)
			return new List<int>();
		return costs[costIndex].GetValidPayments(_state, HumanPlayerId, cardId).ToList();
	}

	public int GetNextAbilityCostNeedingSelection(int cardId, int abilityIndex, int afterIndex)
	{
		var card = _state.GetObject(cardId) as Card;
		var abilities = card?.GetComponents<ActivatedAbilityComponent>().ToList();
		if (abilities == null || abilityIndex >= abilities.Count)
			return -1;
		var costs = abilities[abilityIndex].AdditionalCosts;
		for (int i = afterIndex + 1; i < costs.Count; i++)
			if (costs[i].RequiresSelection)
				return i;
		return -1;
	}

	public (bool Success, ImmutableList<GameEvent> Events) ActivateAbility(
		int cardId,
		int abilityIndex,
		ImmutableList<int> targetIds,
		ImmutableDictionary<int, ImmutableList<int>> additionalCostPayments
	)
	{
		return SubmitAction(
			new ActivateAbilityAction
			{
				CardId = cardId,
				ActivatingPlayerId = HumanPlayerId,
				AbilityIndex = abilityIndex,
				TargetIds = targetIds,
				AdditionalCostPayments = additionalCostPayments,
			}
		);
	}

	public ChoiceAction GetPendingChoice() => _state.GetPendingChoice();

	public ImmutableList<ChoiceOption> GetPendingChoiceOptions()
	{
		if (!_state.IsWaitingForChoice)
			return ImmutableList<ChoiceOption>.Empty;
		if (_state.ActionStack.Peek() is not PipelineAction pipeline)
			return ImmutableList<ChoiceOption>.Empty;
		if (pipeline.CurrentStep is not ChoiceAction choice)
			return ImmutableList<ChoiceOption>.Empty;
		return choice.GetOptions(_state, pipeline.PipelineContext);
	}

	public ImmutableList<GameEvent> ResolveChoice(ImmutableList<int> selectedIds)
	{
		var (newState, events) = _state.ResolveChoice(selectedIds);
		_state = newState;
		_history.Add(new HistoryEntry(_state, "Choice resolved"));
		return events;
	}

	public ImmutableList<GameEvent> ResolveAiChoice()
	{
		if (!IsWaitingForChoice)
			return ImmutableList<GameEvent>.Empty;
		var choice = _state.GetPendingChoice()!;
		var historyIndex = _history.Count;
		var selected = _aiStrategy.ResolveChoice(_state, choice, AiPlayerId);
		var events = ResolveChoice(selected);
		if (_aiStrategy.LastDecision != null)
			_aiDecisions.Add((_aiStrategy.LastDecision, historyIndex));
		return events;
	}

	/// <summary>
	/// Executes one AI action using BeamSearch, or ends the turn if no legal actions remain.
	/// Call repeatedly with a visual delay between calls until IsAiTurn is false.
	/// </summary>
	public ImmutableList<GameEvent> RunAiTurnStep()
	{
		if (!IsAiTurn || IsWaitingForChoice)
			return ImmutableList<GameEvent>.Empty;

		var actions = GetLegalActions(AiPlayerId);
		GameAction next;
		if (actions.Count == 0)
		{
			next = new EndTurnAction
			{
				GameId = _state.GetWellKnownId(MtgObjectKeys.Game),
				Player1Id = HumanPlayerId,
				Player2Id = AiPlayerId,
			};
		}
		else
		{
#pragma warning disable CS0618
			next = _aiStrategy.SelectAction(_state, _ids, AiPlayerId);
#pragma warning restore CS0618
			if (_aiStrategy.LastDecision != null)
				_aiDecisions.Add((_aiStrategy.LastDecision, _history.Count));
		}

		var (_, events) = SubmitAction(next);
		return events;
	}

	// ===== DEBUG / REPLAY =====

	public IReadOnlyList<(int Index, string Description)> GetHistorySummary() =>
		_history.Select((e, i) => (i, e.ActionDescription)).ToList();

	/// <summary>
	/// Restores the game to a previous state in the history and trims everything after it.
	/// </summary>
	public void RewindTo(int historyIndex)
	{
		if (historyIndex < 0 || historyIndex >= _history.Count)
			return;
		_state = _history[historyIndex].State;
		_history.RemoveRange(historyIndex + 1, _history.Count - historyIndex - 1);
		_aiDecisions.RemoveAll(d => d.HistoryIndex > historyIndex);
	}

	/// <summary>
	/// Builds and returns a JSON debug snapshot of the full game history and AI decisions.
	/// </summary>
	public string ExportDebugSnapshot()
	{
		var historyForBuilder = _history.Select(e => (e.State, e.ActionDescription)).ToList();
		return DebugSnapshotBuilder.BuildJson(historyForBuilder, _aiDecisions);
	}

	// ===== DECK SETUP =====

	private void PopulateDecks(DeckSetupData setup)
	{
		AddCardsToLibrary(
			HumanPlayerId,
			MtgObjectKeys.Player1Library,
			ResolveCards(setup.PlayerDeck, HumanPlayerId)
		);
		AddCardsToLibrary(
			AiPlayerId,
			MtgObjectKeys.Player2Library,
			ResolveCards(setup.OpponentDeck, AiPlayerId)
		);
	}

	private IReadOnlyList<Card> ResolveCards(DeckChoice choice, int ownerId) =>
		choice switch
		{
			DeckChoice.Premade p => DeckRegistry.Build(p.Name, ownerId),
			DeckChoice.RandomPremade => DeckRegistry.Build(
				DeckRegistry.All[_rng.Next(DeckRegistry.All.Count)].Name,
				ownerId
			),
			DeckChoice.Randomized => CardPool.BuildRandomDeck(
				ownerId,
				[.. CardPool.All, .. CardLibrary.All]
			),
			_ => throw new ArgumentOutOfRangeException(nameof(choice)),
		};

	private void AddCardsToLibrary(int playerId, string libraryKey, IReadOnlyList<Card> cards)
	{
		var libraryId = _state.GetWellKnownId(libraryKey);
		foreach (var card in cards)
		{
			var stamped = card with { OwnerId = playerId, ControllerId = playerId };
			(_state, _) = _state.AddObject(stamped, parentId: libraryId);
		}
	}
}
