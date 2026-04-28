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
/// Alpha-beta style pruning:
///   - In SelectAction: stops evaluating further actions once a winning
///     action is found (score >= WinScore) since nothing can beat it
///   - In EvaluateToDepth: propagates the cutoff upward — once a winning
///     score is found at any depth level, remaining siblings are skipped
///
/// Falls back to random selection when all actions score equally.
/// </summary>
public class DepthLimitedAiStrategy : IAiStrategy
{
	private readonly int _maxDepth;
	private readonly MtgGameIds _ids;
	private readonly Random _rng;

	public DepthLimitedAiStrategy(MtgGameIds ids, int maxDepth = 3, Random? rng = null)
	{
		_ids = ids;
		_maxDepth = maxDepth;
		_rng = rng ?? new Random();
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

			// Win cutoff — can't do better than a guaranteed win
			if (bestScore >= StateEvaluator.WinScore)
				break;
		}

		return bestActions[_rng.Next(bestActions.Count)];
	}

	public ImmutableList<int> ResolveChoice(GameState state, ChoiceAction choice, int playerId)
	{
		if (choice.Options.IsEmpty)
			return ImmutableList<int>.Empty;

		// Multi-select choices (e.g. "discard 2"): evaluating all combinations is
		// too expensive for the search; pick randomly instead.
		if (choice.MinChoices > 1)
		{
			return choice
				.Options.OrderBy(_ => _rng.Next())
				.Take(choice.MinChoices)
				.Select(o => o.Id)
				.ToImmutableList();
		}

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

			if (bestScore >= StateEvaluator.WinScore)
				break;
		}

		return ImmutableList.Create(bestOption.Id);
	}

	/// <summary>
	/// Recursively evaluates the state by greedily selecting the best action
	/// at each depth level, returning the leaf score.
	///
	/// Returns as soon as a winning score is found — no need to evaluate
	/// remaining actions at that node.
	/// </summary>
	private float EvaluateToDepth(GameState state, int playerId, int depth)
	{
		var player = state.GetPlayer(playerId);
		var opponentId = playerId == _ids.Player1Id ? _ids.Player2Id : _ids.Player1Id;
		var opponent = state.GetPlayer(opponentId);

		if (player.HasLost)
			return StateEvaluator.LossScore;
		if (opponent.HasLost)
			return StateEvaluator.WinScore;

		if (depth <= 0)
			return StateEvaluator.Evaluate(state, _ids, playerId);

		if (state.IsWaitingForChoice)
		{
			var choice = state.GetPendingChoice()!;
			var resolvedIds = ResolveChoice(state, choice, playerId);
			var (resolvedState, _) = state.ResolveChoice(resolvedIds);
			return EvaluateToDepth(resolvedState, playerId, depth);
		}

		var actions = MtgActionGenerator.GetLegalActions(state, _ids, playerId);

		if (actions.Count == 0)
			return StateEvaluator.Evaluate(state, _ids, playerId);

		var bestScore = float.MinValue;

		foreach (var action in actions)
		{
			var resultState = ExecuteAction(state, action);
			var score = EvaluateToDepth(resultState, playerId, depth - 1);

			if (score > bestScore)
				bestScore = score;

			// Cutoff — winning score found, no need to evaluate remaining actions
			if (bestScore >= StateEvaluator.WinScore)
				break;
		}

		return bestScore;
	}

	private static GameState ExecuteAction(GameState state, GameAction action)
	{
		var (newState, success) = state.TryAddAction(action);
		if (!success)
			return state;

		var (finalState, _) = newState.ProcessAllActions();
		return finalState;
	}
}
