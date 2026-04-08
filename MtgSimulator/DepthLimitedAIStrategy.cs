using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// An AI strategy that uses depth-limited greedy search to select actions.
///
/// For each legal action, executes it and evaluates the resulting state
/// to a fixed depth, then picks the action with the highest score from
/// the current player's perspective.
///
/// This is a greedy best-first search rather than true minimax — opponent
/// responses are not modelled during the search. The opponent's turn is
/// handled naturally when the game transitions to them.
///
/// Choices (ChoiceAction) are resolved by evaluating each option and
/// picking the one that produces the best immediate state.
///
/// Falls back to random selection when all actions score equally,
/// which prevents deterministic repetition in symmetric states.
/// </summary>
public class DepthLimitedAiStrategy : IAiStrategy
{
	private readonly int _maxDepth;
	private readonly MtgGameIds _ids;
	private readonly Random _rng;
	private readonly RandomAiStrategy _random;

	public DepthLimitedAiStrategy(MtgGameIds ids, int maxDepth = 3, Random? rng = null)
	{
		_ids = ids;
		_maxDepth = maxDepth;
		_rng = rng ?? new Random();
		_random = new RandomAiStrategy(_rng);
	}

	public GameAction SelectAction(GameState state, MtgGameIds ids, int playerId)
	{
		var actions = MtgActionGenerator.GetLegalActions(state, ids, playerId);

		if (actions.Count == 0)
			throw new InvalidOperationException(
				"SelectAction called with no legal actions available"
			);

		if (actions.Count == 1)
			return actions[0];

		var bestScore = float.MinValue;
		var bestActions = new List<GameAction>();

		foreach (var action in actions)
		{
			var resultState = ExecuteAction(state, action);
			var score = EvaluateToDepth(resultState, playerId, _maxDepth - 1);

			if (score > bestScore)
			{
				bestScore = score;
				bestActions.Clear();
				bestActions.Add(action);
			}
			else if (score == bestScore)
			{
				bestActions.Add(action);
			}
		}

		// Break ties randomly to avoid deterministic repetition
		return bestActions[_rng.Next(bestActions.Count)];
	}

	public ImmutableList<int> ResolveChoice(GameState state, ChoiceAction choice, int playerId)
	{
		if (choice.Options.IsEmpty)
			return ImmutableList<int>.Empty;

		// For choices we evaluate each option's immediate state
		var bestScore = float.MinValue;
		var bestOption = choice.Options[0];

		foreach (var option in choice.Options)
		{
			var (resultState, _) = state.ResolveChoice(ImmutableList.Create(option.Id));
			var score = StateEvaluator.Evaluate(resultState, _ids, playerId);

			if (score > bestScore)
			{
				bestScore = score;
				bestOption = option;
			}
		}

		return ImmutableList.Create(bestOption.Id);
	}

	/// <summary>
	/// Recursively evaluates the state by greedily selecting the best action
	/// at each depth level, returning the leaf score.
	/// </summary>
	private float EvaluateToDepth(GameState state, int playerId, int depth)
	{
		// Evaluate terminal or leaf states immediately
		var player = state.GetPlayer(playerId);
		var opponentId = playerId == _ids.Player1Id ? _ids.Player2Id : _ids.Player1Id;
		var opponent = state.GetPlayer(opponentId);

		if (player.HasLost)
			return StateEvaluator.LossScore;
		if (opponent.HasLost)
			return StateEvaluator.WinScore;

		if (depth <= 0)
			return StateEvaluator.Evaluate(state, _ids, playerId);

		// Handle pending choice — resolve greedily
		if (state.IsWaitingForChoice)
		{
			var choice = state.GetPendingChoice()!;
			var resolvedIds = ResolveChoice(state, choice, playerId);
			var (resolvedState, _) = state.ResolveChoice(resolvedIds);
			return EvaluateToDepth(resolvedState, playerId, depth);
		}

		var actions = MtgActionGenerator.GetLegalActions(state, _ids, playerId);

		// No actions available — this is effectively a turn-end state
		if (actions.Count == 0)
			return StateEvaluator.Evaluate(state, _ids, playerId);

		// Greedily pick the best action at this depth level
		var bestScore = float.MinValue;

		foreach (var action in actions)
		{
			var resultState = ExecuteAction(state, action);
			var score = EvaluateToDepth(resultState, playerId, depth - 1);

			if (score > bestScore)
				bestScore = score;
		}

		return bestScore;
	}

	/// <summary>
	/// Executes an action and returns the resulting state.
	/// Returns the original state if the action fails validation.
	/// </summary>
	private static GameState ExecuteAction(GameState state, GameAction action)
	{
		var (newState, success) = state.TryAddAction(action);
		if (!success)
			return state;

		var (finalState, _) = newState.ProcessAllActions();
		return finalState;
	}
}
