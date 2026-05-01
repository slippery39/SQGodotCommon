using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// An AI strategy that uses beam search to select actions.
///
/// Expands the game tree level by level (breadth-first) rather than depth-first.
/// At each depth level, candidates are pruned down to two buckets before expanding
/// the next level:
///
///   Concrete bucket  — top N by StateEvaluator score (board advantage)
///   Potential bucket — top M per IPotentialEvaluator (setup/combo value)
///
/// The potential bucket lets combo lines survive pruning even when the main evaluator
/// scores them poorly (e.g. fast mana plays score 0 on StateEvaluator but may enable
/// a win-condition spell one or two levels later).
///
/// The final action returned is always the root action of the highest concrete-scoring
/// leaf in the surviving beam — potential scores only influence which paths survive to
/// the leaf level, never which action is ultimately chosen.
///
/// Falls back to random selection when multiple leaf nodes share the best score.
/// </summary>
public class BeamSearchAiStrategy : IAiStrategy
{
	private readonly int _maxDepth;
	private readonly MtgGameIds _ids;
	private readonly Random _rng;
	private readonly int _concreteSlots;
	private readonly IReadOnlyList<IPotentialEvaluator> _potentialEvaluators;

	// Carries a game state forward through the beam, together with the root action
	// that started this path (so SelectAction knows which first move to return).
	private record BeamNode(GameState State, GameAction RootAction, float ConcreteScore);

	public BeamSearchAiStrategy(
		MtgGameIds ids,
		int maxDepth = 3,
		int concreteSlots = 10,
		IReadOnlyList<IPotentialEvaluator>? potentialEvaluators = null,
		Random? rng = null
	)
	{
		_ids = ids;
		_maxDepth = maxDepth;
		_concreteSlots = concreteSlots;
		_potentialEvaluators = potentialEvaluators ?? [new FastManaPotentialEvaluator()];
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

		// Level 0: execute each root action and score the resulting state
		var beam = new List<BeamNode>(actions.Count);
		foreach (var action in actions)
		{
			var resultState = ExecuteAction(state, action);
			var score = StateEvaluator.Evaluate(resultState, _ids, playerId);
			beam.Add(new BeamNode(resultState, action, score));
		}

		var winner = FindWinner(beam);
		if (winner != null)
			return winner.RootAction;

		beam = PruneBeam(beam, playerId);

		// Levels 1..maxDepth-1: expand survivors one level at a time
		for (var depth = 1; depth < _maxDepth; depth++)
		{
			var nextBeam = new List<BeamNode>();
			foreach (var node in beam)
				nextBeam.AddRange(ExpandNode(node, playerId));

			if (nextBeam.Count == 0)
				break;

			winner = FindWinner(nextBeam);
			if (winner != null)
				return winner.RootAction;

			beam = PruneBeam(nextBeam, playerId);
		}

		// Return the root action of the highest-scoring leaf
		var bestScore = beam.Max(n => n.ConcreteScore);
		var bestNodes = beam.Where(n => n.ConcreteScore == bestScore).ToList();
		return bestNodes[_rng.Next(bestNodes.Count)].RootAction;
	}

	public ImmutableList<int> ResolveChoice(GameState state, ChoiceAction choice, int playerId)
	{
		if (choice.Options.IsEmpty)
			return ImmutableList<int>.Empty;

		// Multi-select: evaluating all combinations is too expensive; pick randomly
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
	/// Expands a beam node by one level: resolves any pending choice, then applies
	/// every legal action and scores each resulting state.
	///
	/// If the state has no legal actions (e.g. opponent's turn in the simulation),
	/// the node is returned as-is with a refreshed concrete score.
	/// </summary>
	private List<BeamNode> ExpandNode(BeamNode node, int playerId)
	{
		var state = node.State;

		while (state.IsWaitingForChoice)
		{
			var choice = state.GetPendingChoice()!;
			var resolvedIds = ResolveChoice(state, choice, playerId);
			(state, _) = state.ResolveChoice(resolvedIds);
		}

		var actions = MtgActionGenerator.GetLegalActions(state, _ids, playerId);

		if (actions.Count == 0)
		{
			var leafScore = StateEvaluator.Evaluate(state, _ids, playerId);
			return [new BeamNode(state, node.RootAction, leafScore)];
		}

		var result = new List<BeamNode>(actions.Count);
		foreach (var action in actions)
		{
			var resultState = ExecuteAction(state, action);
			var score = StateEvaluator.Evaluate(resultState, _ids, playerId);
			result.Add(new BeamNode(resultState, node.RootAction, score));
		}
		return result;
	}

	/// <summary>
	/// Keeps the top N candidates by concrete score (concrete bucket) plus the top M
	/// per potential evaluator (potential bucket). Duplicates across buckets are
	/// removed by object identity — a node in both buckets only appears once.
	/// </summary>
	private List<BeamNode> PruneBeam(List<BeamNode> candidates, int playerId)
	{
		var seen = new HashSet<BeamNode>(ReferenceEqualityComparer.Instance);
		var result = new List<BeamNode>();

		foreach (
			var node in candidates.OrderByDescending(n => n.ConcreteScore).Take(_concreteSlots)
		)
		{
			if (seen.Add(node))
				result.Add(node);
		}

		foreach (var evaluator in _potentialEvaluators)
		{
			foreach (
				var node in candidates
					.OrderByDescending(n => evaluator.Score(n.State, playerId))
					.Take(evaluator.SlotCount)
			)
			{
				if (seen.Add(node))
					result.Add(node);
			}
		}

		return result;
	}

	private static BeamNode? FindWinner(List<BeamNode> beam) =>
		beam.FirstOrDefault(n => n.ConcreteScore >= StateEvaluator.WinScore);

	private static GameState ExecuteAction(GameState state, GameAction action)
	{
		var (newState, success) = state.TryAddAction(action);
		if (!success)
			return state;

		var (finalState, _) = newState.ProcessAllActions();
		return finalState;
	}
}
