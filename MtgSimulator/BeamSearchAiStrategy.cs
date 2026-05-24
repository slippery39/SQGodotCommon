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
public class BeamSearchAiStrategy : ICapturingAiStrategy
{
	private readonly int _maxDepth;
	private readonly MtgGameIds _ids;
	private readonly Random _rng;
	private readonly int _concreteSlots;
	private readonly IReadOnlyList<IPotentialEvaluator> _potentialEvaluators;
	private readonly bool _captureDecisions;

	// EndTurn must exceed the best non-EndTurn score by this margin to be chosen.
	// Prevents EndTurn from winning ties — concrete actions are at least as valuable as passing.
	private const float EndTurnBias = 0.01f;

	// Carries a game state forward through the beam, together with the root action
	// that started this path (so SelectAction knows which first move to return).
	private record BeamNode(GameState State, GameAction RootAction, float ConcreteScore);

	/// <summary>
	/// When captureDecisions is true, populated after each SelectAction call with the
	/// chosen action, its score, and the top alternatives. Null when captureDecisions is false.
	/// </summary>
	public AiDecision? LastDecision { get; private set; }

	public BeamSearchAiStrategy(
		MtgGameIds ids,
		int maxDepth = 3,
		int concreteSlots = 6,
		IReadOnlyList<IPotentialEvaluator>? potentialEvaluators = null,
		Random? rng = null,
		bool captureDecisions = false
	)
	{
		_ids = ids;
		_maxDepth = maxDepth;
		_concreteSlots = concreteSlots;
		_potentialEvaluators = potentialEvaluators ?? [new FastManaPotentialEvaluator()];
		_rng = rng ?? new Random();
		_captureDecisions = captureDecisions;
	}

	public GameAction SelectAction(GameState state, MtgGameIds ids, int playerId)
	{
		var actions = MtgActionGenerator.GetLegalActions(state, ids, playerId);

		if (actions.Count == 0)
			throw new InvalidOperationException(
				"SelectAction called with no legal actions available"
			);

		if (actions.Count == 1)
		{
			if (_captureDecisions)
			{
				var desc = ActionDescriber.Describe(actions[0], state);
				LastDecision = new AiDecision(desc, 0f, [new AiActionCandidate(desc, 0f, true)]);
			}
			return actions[0];
		}

		// Always play a land if available — permanent mana is the highest-priority resource.
		// Edge cases (landfall combos, hand-size manipulation) are rare enough to ignore here.
		// var landAction = actions.OfType<PlayLandAction>().FirstOrDefault();
		// if (landAction != null)
		// 	return landAction;

		// Level 0: execute each root action and score the resulting state
		var beam = actions
			.AsParallel()
			.Select(action =>
			{
				var resultState = ExecuteAction(state, action);
				var score = StateEvaluator.Evaluate(resultState, _ids, playerId);
				return new BeamNode(resultState, action, score);
			})
			.ToList();

		// Track best reachable score per root action across all beam levels.
		// Only allocated when captureDecisions is true — zero overhead otherwise.
		Dictionary<GameAction, float>? rootScores = null;
		if (_captureDecisions)
		{
			rootScores = new Dictionary<GameAction, float>(ReferenceEqualityComparer.Instance);
			foreach (var n in beam)
				rootScores[n.RootAction] = n.ConcreteScore;
		}

		var winner = FindWinner(beam);
		if (winner != null)
		{
			if (_captureDecisions)
				SetLastDecision(winner.RootAction, rootScores!, state);
			return winner.RootAction;
		}

		beam = PruneBeam(beam, playerId);

		// Levels 1..maxDepth-1: expand survivors one level at a time
		for (var depth = 1; depth < _maxDepth; depth++)
		{
			var nextBeam = beam.AsParallel()
				.SelectMany(node => ExpandNode(node, playerId))
				.ToList();

			if (nextBeam.Count == 0)
				break;

			if (_captureDecisions)
				UpdateRootScores(rootScores!, nextBeam);

			winner = FindWinner(nextBeam);
			if (winner != null)
			{
				if (_captureDecisions)
					SetLastDecision(winner.RootAction, rootScores!, state);
				return winner.RootAction;
			}

			beam = PruneBeam(nextBeam, playerId);
		}

		if (_captureDecisions)
			UpdateRootScores(rootScores!, beam);

		// Return the root action of the highest-scoring leaf.
		// EndTurn must beat the best non-EndTurn score by EndTurnBias to be chosen —
		// concrete actions are at least as valuable as passing in a tie.
		var nonEndTurnNodes = beam.Where(n => n.RootAction is not EndTurnAction).ToList();
		GameAction chosen;
		if (nonEndTurnNodes.Count == 0)
		{
			chosen = beam.First(n => n.RootAction is EndTurnAction).RootAction;
		}
		else
		{
			var bestNonEndTurnScore = nonEndTurnNodes.Max(n => n.ConcreteScore);
			var endTurnScore = beam.Where(n => n.RootAction is EndTurnAction)
				.Select(n => n.ConcreteScore)
				.DefaultIfEmpty(float.MinValue)
				.Max();
			if (endTurnScore > bestNonEndTurnScore + EndTurnBias)
			{
				chosen = beam.First(n => n.RootAction is EndTurnAction).RootAction;
			}
			else
			{
				var bestNodes = nonEndTurnNodes
					.Where(n => n.ConcreteScore == bestNonEndTurnScore)
					.ToList();
				chosen = bestNodes[_rng.Next(bestNodes.Count)].RootAction;
			}
		}

		if (_captureDecisions)
			SetLastDecision(chosen, rootScores!, state);
		return chosen;
	}

	public ImmutableList<int> ResolveChoice(GameState state, ChoiceAction choice, int playerId)
	{
		if (choice.Options.IsEmpty)
			return ImmutableList<int>.Empty;

		// Multi-select: enumerate all combinations and pick the one that scores best.
		if (choice.MinChoices > 1)
		{
			var bestMultiScore = float.MinValue;
			ImmutableList<int>? bestSelection = null;

			foreach (var combo in GetCombinations(choice.Options, choice.MinChoices))
			{
				var selectedIds = combo.Select(o => o.Id).ToImmutableList();
				var (resultState, _) = state.ResolveChoice(selectedIds);
				var score = LookaheadScore(resultState, playerId);

				if (score > bestMultiScore)
				{
					bestMultiScore = score;
					bestSelection = selectedIds;
				}

				if (bestMultiScore >= StateEvaluator.WinScore)
					break;
			}

			if (_captureDecisions && bestSelection != null)
			{
				var comboDesc = string.Join(
					", ",
					bestSelection.Select(id =>
						OptionDescription(state, choice.Options.First(o => o.Id == id))
					)
				);
				LastDecision = new AiDecision(
					$"{choice.Prompt} → [{comboDesc}]",
					bestMultiScore,
					[new AiActionCandidate(comboDesc, bestMultiScore, true)],
					IsChoiceResolution: true
				);
			}

			return bestSelection
				?? choice.Options.Take(choice.MinChoices).Select(o => o.Id).ToImmutableList();
		}

		var bestScore = float.MinValue;
		var bestOption = choice.Options[0];
		var optionScores = _captureDecisions
			? new List<(ChoiceOption Option, float Score)>(choice.Options.Count)
			: null;

		foreach (var option in choice.Options)
		{
			var (resultState, _) = state.ResolveChoice(ImmutableList.Create(option.Id));
			var score = LookaheadScore(resultState, playerId);

			optionScores?.Add((option, score));

			if (score > bestScore)
			{
				bestScore = score;
				bestOption = option;
			}

			if (bestScore >= StateEvaluator.WinScore)
				break;
		}

		if (_captureDecisions && optionScores != null)
		{
			LastDecision = new AiDecision(
				$"{choice.Prompt} → {OptionDescription(state, bestOption)}",
				bestScore,
				optionScores
					.Select(x => new AiActionCandidate(
						OptionDescription(state, x.Option),
						x.Score,
						x.Option.Id == bestOption.Id
					))
					.ToList(),
				IsChoiceResolution: true
			);
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

		// If the turn crossed to the opponent during choice resolution or after a prior action
		// (e.g. EndTurn was played), stop expanding. We don't model opponent responses.
		var game = state.TryGetGame();
		if (game != null && game.ActivePlayerId != playerId)
		{
			var leafScore = StateEvaluator.Evaluate(state, _ids, playerId);
			return [new BeamNode(state, node.RootAction, leafScore)];
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

	/// <summary>
	/// Scores a state by looking one ply ahead: tries every legal action from the given
	/// state, resolves any resulting choices greedily (immediate scoring, no further
	/// recursion), and returns the best reachable score.
	///
	/// Used inside ResolveChoice so the AI can see the downstream value of its choices —
	/// e.g. discarding Bloodghast then playing a land, or keeping Reanimate to cast it.
	/// </summary>
	private float LookaheadScore(GameState state, int playerId)
	{
		var game = state.TryGetGame();
		if (game == null || game.ActivePlayerId != playerId)
			return StateEvaluator.Evaluate(state, _ids, playerId);

		// Attacks and end-turn never depend on which card was just chosen — exclude them so
		// we don't pay O(attackers × targets) cost on every choice option or combination.
		var actions = MtgActionGenerator
			.GetLegalActions(state, _ids, playerId)
			.Where(a => a is not AttackAction and not EndTurnAction)
			.ToList();
		var best = StateEvaluator.Evaluate(state, _ids, playerId);

		foreach (var action in actions)
		{
			var nextState = ExecuteAction(state, action);

			// Greedily resolve any choices created by this action (e.g. Reanimate's
			// target picker) using plain StateEvaluator — no further recursion.
			while (nextState.IsWaitingForChoice)
			{
				var pending = nextState.GetPendingChoice()!;
				var greedyIds = GreedyResolveChoice(nextState, pending, playerId);
				(nextState, _) = nextState.ResolveChoice(greedyIds);
			}

			var score = StateEvaluator.Evaluate(nextState, _ids, playerId);
			if (score > best)
				best = score;

			if (best >= StateEvaluator.WinScore)
				break;
		}

		return best;
	}

	/// <summary>
	/// Resolves a choice using immediate StateEvaluator scoring only — no lookahead.
	/// Used by LookaheadScore to handle nested choices without further recursion.
	/// </summary>
	private ImmutableList<int> GreedyResolveChoice(
		GameState state,
		ChoiceAction choice,
		int playerId
	)
	{
		if (choice.Options.IsEmpty)
			return ImmutableList<int>.Empty;

		if (choice.MinChoices > 1)
			return choice.Options.Take(choice.MinChoices).Select(o => o.Id).ToImmutableList();

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

	private static IEnumerable<IEnumerable<ChoiceOption>> GetCombinations(
		ImmutableList<ChoiceOption> options,
		int count
	)
	{
		if (count == 0)
		{
			yield return [];
			yield break;
		}
		for (var i = 0; i <= options.Count - count; i++)
			foreach (var rest in GetCombinations(options.RemoveRange(0, i + 1), count - 1))
				yield return rest.Prepend(options[i]);
	}

	private void SetLastDecision(
		GameAction chosen,
		Dictionary<GameAction, float> rootScores,
		GameState originalState
	)
	{
		var candidates = rootScores
			.OrderByDescending(kvp => kvp.Value)
			.Take(10)
			.Select(kvp => new AiActionCandidate(
				ActionDescriber.Describe(kvp.Key, originalState),
				kvp.Value,
				ReferenceEquals(kvp.Key, chosen)
			))
			.ToList();
		rootScores.TryGetValue(chosen, out var chosenScore);
		LastDecision = new AiDecision(
			ActionDescriber.Describe(chosen, originalState),
			chosenScore,
			candidates
		);
	}

	private static void UpdateRootScores(Dictionary<GameAction, float> scores, List<BeamNode> beam)
	{
		foreach (var node in beam)
			if (!scores.TryGetValue(node.RootAction, out var prev) || node.ConcreteScore > prev)
				scores[node.RootAction] = node.ConcreteScore;
	}

	private static string OptionDescription(GameState state, ChoiceOption option)
	{
		if (!string.IsNullOrEmpty(option.DisplayText))
			return option.DisplayText;
		if (!state.HasObject(option.Id))
			return option.Id.ToString();
		return state.GetObject(option.Id) switch
		{
			Card c => c.Name,
			MtgPlayer p => p.Name,
			_ => option.Id.ToString(),
		};
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
