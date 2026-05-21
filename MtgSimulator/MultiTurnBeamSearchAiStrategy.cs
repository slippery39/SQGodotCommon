using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// A beam search AI strategy that evaluates actions using a multi-turn lookahead.
///
/// The beam explores all action sequences within the current turn (same structure as
/// BeamSearchAiStrategy). Every node is scored by ScoreAfterCompletingTurn, which:
///   1. Completes the current turn greedily (plays any remaining land/spell + EndTurn)
///   2. Runs MultiTurnGreedyRollout for N future turns
///
/// This separates "what to do this turn" (beam) from "how good is this path long-term"
/// (rollout), so all root actions are compared from the same temporal starting point.
/// No root action gets an unfair depth advantage from crossing turn boundaries early.
///
/// Choices (discards, target picks) are branched in the beam rather than greedily
/// resolved, giving full current-turn visibility into which discard leads to the best
/// future (e.g. discard Carnage Tyrant to enable Reanimate next turn).
/// </summary>
public class MultiTurnBeamSearchAiStrategy : ICapturingAiStrategy
{
	private readonly MtgGameIds _ids;
	private readonly int _currentTurnDepth;
	private readonly int _lookaheadTurns;
	private readonly int _concreteSlots;
	private readonly OpponentSimulationMode _opponentMode;
	private readonly IReadOnlyList<IPotentialEvaluator> _potentialEvaluators;
	private readonly Random _rng;
	private readonly bool _captureDecisions;

	private const int MaxChoiceBranches = 20;

	// EndTurn must exceed the best non-EndTurn score by this margin to be chosen.
	// Prevents EndTurn from winning ties — concrete actions are at least as valuable as passing.
	private const float EndTurnBias = 0.01f;

	private record BeamNode(GameState State, GameAction RootAction, float ConcreteScore);

	public AiDecision? LastDecision { get; private set; }

	public MultiTurnBeamSearchAiStrategy(
		MtgGameIds ids,
		int currentTurnDepth = 3,
		int lookaheadTurns = 2,
		int concreteSlots = 10,
		OpponentSimulationMode opponentMode = OpponentSimulationMode.Greedy,
		IReadOnlyList<IPotentialEvaluator>? potentialEvaluators = null,
		Random? rng = null,
		bool captureDecisions = false
	)
	{
		_ids = ids;
		_currentTurnDepth = currentTurnDepth;
		_lookaheadTurns = lookaheadTurns;
		_concreteSlots = concreteSlots;
		_opponentMode = opponentMode;
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

		// Level 0: execute each root action and score via multi-turn rollout
		var beam = actions
			.Select(action =>
			{
				var resultState = ExecuteAction(state, action);
				var score = ScoreAfterCompletingTurn(resultState, playerId);
				return new BeamNode(resultState, action, score);
			})
			.ToList();

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

		// Levels 1..currentTurnDepth-1: expand within the current turn only
		for (var depth = 1; depth < _currentTurnDepth; depth++)
		{
			var nextBeam = beam.SelectMany(node => ExpandNode(node, playerId)).ToList();

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

		// EndTurn must beat the best non-EndTurn score by EndTurnBias to be chosen
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

		if (choice.MinChoices > 1)
		{
			var bestScore = float.MinValue;
			ImmutableList<int>? bestSelection = null;

			foreach (var combo in GetCombinations(choice.Options, choice.MinChoices))
			{
				var selectedIds = combo.Select(o => o.Id).ToImmutableList();
				var (resultState, _) = state.ResolveChoice(selectedIds);
				var score = ScoreAfterCompletingTurn(resultState, playerId);

				if (score > bestScore)
				{
					bestScore = score;
					bestSelection = selectedIds;
				}

				if (bestScore >= StateEvaluator.WinScore)
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
					bestScore,
					[new AiActionCandidate(comboDesc, bestScore, true)],
					IsChoiceResolution: true
				);
			}

			return bestSelection
				?? choice.Options.Take(choice.MinChoices).Select(o => o.Id).ToImmutableList();
		}

		var bestSingleScore = float.MinValue;
		var bestOption = choice.Options[0];
		var optionScores = _captureDecisions
			? new List<(ChoiceOption Option, float Score)>(choice.Options.Count)
			: null;

		foreach (var option in choice.Options)
		{
			var (resultState, _) = state.ResolveChoice(ImmutableList.Create(option.Id));
			var score = ScoreAfterCompletingTurn(resultState, playerId);

			optionScores?.Add((option, score));

			if (score > bestSingleScore)
			{
				bestSingleScore = score;
				bestOption = option;
			}

			if (bestSingleScore >= StateEvaluator.WinScore)
				break;
		}

		if (_captureDecisions && optionScores != null)
		{
			LastDecision = new AiDecision(
				$"{choice.Prompt} → {OptionDescription(state, bestOption)}",
				bestSingleScore,
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
	/// Expands one beam level within the current turn only. Stops at turn boundaries —
	/// the multi-turn lookahead is handled by ScoreAfterCompletingTurn, not here.
	/// </summary>
	private List<BeamNode> ExpandNode(BeamNode node, int playerId)
	{
		var state = node.State;

		// Branch on pending choices rather than resolving greedily
		if (state.IsWaitingForChoice)
		{
			var choice = state.GetPendingChoice()!;
			return BranchOnChoice(node, choice, playerId);
		}

		var game = state.TryGetGame();

		// Turn boundary — stop expanding, score handles the future
		if (game != null && game.ActivePlayerId != playerId)
		{
			var leafScore = ScoreAfterCompletingTurn(state, playerId);
			return [node with { ConcreteScore = leafScore }];
		}

		var actions = MtgActionGenerator.GetLegalActions(state, _ids, playerId);

		if (actions.Count == 0)
		{
			var leafScore = ScoreAfterCompletingTurn(state, playerId);
			return [node with { ConcreteScore = leafScore }];
		}

		var result = new List<BeamNode>(actions.Count);
		foreach (var action in actions)
		{
			var resultState = ExecuteAction(state, action);
			var score = ScoreAfterCompletingTurn(resultState, playerId);
			result.Add(new BeamNode(resultState, node.RootAction, score));
		}
		return result;
	}

	/// <summary>
	/// Returns one beam node per choice combination. Each is expanded one level further
	/// so the beam sees the state after the choice is resolved.
	/// Capped at MaxChoiceBranches to prevent explosion on large option sets.
	/// </summary>
	private List<BeamNode> BranchOnChoice(BeamNode node, ChoiceAction choice, int playerId)
	{
		var result = new List<BeamNode>();
		foreach (
			var combo in GetCombinations(choice.Options, choice.MinChoices).Take(MaxChoiceBranches)
		)
		{
			var selectedIds = combo.Select(o => o.Id).ToImmutableList();
			var (resolvedState, _) = node.State.ResolveChoice(selectedIds);
			result.AddRange(ExpandNode(node with { State = resolvedState }, playerId));
		}
		return result;
	}

	/// <summary>
	/// Scores a state by completing the current turn greedily (if still our turn),
	/// then running a multi-turn greedy rollout. All beam nodes are scored from the
	/// same temporal point — after the current turn ends — making them comparable.
	/// </summary>
	private float ScoreAfterCompletingTurn(GameState state, int playerId)
	{
		var game = state.TryGetGame();
		// Complete the current turn greedily before the lookahead
		if (game?.ActivePlayerId == playerId)
			state = PlayGreedyTurn(state, playerId);
		return MultiTurnGreedyRollout(state, playerId);
	}

	/// <summary>
	/// Simulates N turns starting from the given state, alternating between
	/// our greedy turn and the opponent's simulated turn.
	/// </summary>
	private float MultiTurnGreedyRollout(GameState state, int playerId)
	{
		var opponentId = playerId == _ids.Player1Id ? _ids.Player2Id : _ids.Player1Id;

		for (var t = 0; t < _lookaheadTurns; t++)
		{
			var game = state.TryGetGame();
			if (game == null)
				break;

			state =
				game.ActivePlayerId == playerId
					? PlayGreedyTurn(state, playerId)
					: SimulateOpponentTurn(state, playerId, opponentId);

			if (Math.Abs(StateEvaluator.Evaluate(state, _ids, playerId)) >= StateEvaluator.WinScore)
				break;
		}

		return StateEvaluator.Evaluate(state, _ids, playerId);
	}

	/// <summary>
	/// Plays one of our turns greedily: land drop then best scoring spell, then EndTurn.
	/// </summary>
	private GameState PlayGreedyTurn(GameState state, int playerId)
	{
		// Land drop first
		var landAction = MtgActionGenerator
			.GetLegalActions(state, _ids, playerId)
			.OfType<PlayLandAction>()
			.FirstOrDefault();
		if (landAction != null)
		{
			state = ExecuteAction(state, landAction);
			state = ResolveAllChoices(state, playerId);
		}

		// Best non-land, non-EndTurn action by immediate StateEvaluator score
		var spellActions = MtgActionGenerator
			.GetLegalActions(state, _ids, playerId)
			.Where(a => a is not EndTurnAction and not PlayLandAction)
			.ToList();

		if (spellActions.Count > 0)
		{
			var best = spellActions.MaxBy(a =>
				StateEvaluator.Evaluate(ExecuteAction(state, a), _ids, playerId)
			)!;
			state = ExecuteAction(state, best);
			state = ResolveAllChoices(state, playerId);
		}

		state = ExecuteAction(state, BuildEndTurnAction(state));
		return ResolveAllChoices(state, playerId);
	}

	/// <summary>
	/// Simulates the opponent's full turn according to the chosen mode.
	/// </summary>
	private GameState SimulateOpponentTurn(GameState state, int ourPlayerId, int opponentId)
	{
		switch (_opponentMode)
		{
			case OpponentSimulationMode.PassTurn:
				return ExecuteAction(state, BuildEndTurnAction(state));

			case OpponentSimulationMode.Greedy:
				var greedyActions = MtgActionGenerator
					.GetLegalActions(state, _ids, opponentId)
					.Where(a => a is not EndTurnAction)
					.ToList();
				if (greedyActions.Count > 0)
				{
					var best = greedyActions.MaxBy(a =>
						StateEvaluator.Evaluate(ExecuteAction(state, a), _ids, opponentId)
					)!;
					state = ExecuteAction(state, best);
					state = ResolveAllChoices(state, opponentId);
				}
				return ExecuteAction(state, BuildEndTurnAction(state));

			case OpponentSimulationMode.Random:
				var randomActions = MtgActionGenerator
					.GetLegalActions(state, _ids, opponentId)
					.Where(a => a is not EndTurnAction)
					.ToList();
				if (randomActions.Count > 0)
				{
					state = ExecuteAction(state, randomActions[_rng.Next(randomActions.Count)]);
					state = ResolveAllChoices(state, opponentId);
				}
				return ExecuteAction(state, BuildEndTurnAction(state));

			default:
				return ExecuteAction(state, BuildEndTurnAction(state));
		}
	}

	/// <summary>
	/// Greedily resolves all pending choices using immediate StateEvaluator scoring.
	/// </summary>
	private GameState ResolveAllChoices(GameState state, int playerId)
	{
		while (state.IsWaitingForChoice)
		{
			var choice = state.GetPendingChoice()!;
			var ids = GreedyResolveChoice(state, choice, playerId);
			(state, _) = state.ResolveChoice(ids);
		}
		return state;
	}

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

	private EndTurnAction BuildEndTurnAction(GameState state) =>
		new()
		{
			GameId = state.GetWellKnownId(MtgObjectKeys.Game),
			Player1Id = _ids.Player1Id,
			Player2Id = _ids.Player2Id,
		};

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
