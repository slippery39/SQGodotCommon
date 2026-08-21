using System.Collections.Immutable;
using System.Diagnostics;
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
	private readonly float _scoreDivergenceThreshold;

	// Optional wall-clock budget per move. When set, the search degrades gracefully
	// (stops expanding, returns the best node found so far) once the budget is spent.
	// Null = unbounded — the simulator uses the deterministic rollout budget below instead,
	// because a wall-clock budget would make its results depend on machine speed.
	private readonly long _moveBudgetTimestampTicks;
	private long _moveStartTimestamp;

	// Deterministic per-move work budget, counted in rollouts (ScoreAfterCompletingTurn calls
	// — the expensive unit; each one plays out a turn plus a two-turn lookahead).
	private readonly int _rolloutBudget;
	private int _rolloutsThisMove;

	// Widest set of actions any one level will roll out. See DefaultMaxBranching.
	private readonly int _maxBranching;

	// Same, for levels below the root. See DefaultExpandBranching.
	private readonly int _expandBranching;

	private ImmutableList<GameAction>? _committedChain;
	private ImmutableList<float>? _committedChainExpectedScores;
	private int _chainIndex;

	private const int MaxChoiceBranches = 3;

	private const float EndTurnBias = 0.01f;

	// Upper bound on sequential choice resolutions in a single rollout step. Generous —
	// only trips if a choice fails to advance, preventing a tight infinite loop.
	private const int MaxChoiceResolutionIterations = 1000;

	private record BeamNode(
		GameState State,
		ImmutableList<GameAction> ActionPath,
		float ConcreteScore
	)
	{
		public GameAction RootAction => ActionPath[0];
	}

	public AiDecision? LastDecision { get; private set; }

	public MultiTurnBeamSearchAiStrategy(
		MtgGameIds ids,
		int currentTurnDepth = 3,
		int lookaheadTurns = 2,
		int concreteSlots = 2,
		OpponentSimulationMode opponentMode = OpponentSimulationMode.BoardOnly,
		IReadOnlyList<IPotentialEvaluator>? potentialEvaluators = null,
		Random? rng = null,
		bool captureDecisions = false,
		float scoreDivergenceThreshold = 5.0f,
		TimeSpan? moveTimeBudget = null,
		int rolloutBudget = DefaultRolloutBudget,
		int maxBranching = DefaultMaxBranching,
		int expandBranching = DefaultExpandBranching
	)
	{
		_rolloutBudget = rolloutBudget;
		_maxBranching = maxBranching;
		_expandBranching = expandBranching;
		_ids = ids;
		_currentTurnDepth = currentTurnDepth;
		_lookaheadTurns = lookaheadTurns;
		_concreteSlots = concreteSlots;
		_opponentMode = opponentMode;
		_potentialEvaluators = potentialEvaluators ?? [new FastManaPotentialEvaluator()];
		_rng = rng ?? new Random();
		_captureDecisions = captureDecisions;
		_scoreDivergenceThreshold = scoreDivergenceThreshold;
		_moveBudgetTimestampTicks = moveTimeBudget.HasValue
			? (long)(moveTimeBudget.Value.TotalSeconds * Stopwatch.Frequency)
			: 0;
	}

	/// <summary>
	/// Rollouts a single move may spend before the search stops widening. Calibrated against
	/// the measured spread of legal actions at the search root — p50 4, p90 9, p99 17,
	/// p99.9 28, max 74 — so a typical move (9 actions: 9 + 2 levels x 15 beam slots x 9 ≈ 280)
	/// never reaches it and only the wide-board tail loses a level of search.
	///
	/// This replaces wall-clock as the way the simulator bounds its own cost. Time was never
	/// usable for that: it makes the same game play differently on a loaded machine.
	/// </summary>
	public const int DefaultRolloutBudget = 400;

	/// <summary>
	/// Most actions any single level will roll out. Beyond this the level is pre-ranked with the
	/// cheap immediate evaluator and only the best survive to be scored properly.
	///
	/// Calibrated against the measured spread of legal actions per decision on CSC — p50 4,
	/// p90 8, p99 16, p99.9 24, max 47 — so roughly 99% of decisions are below it and pay
	/// nothing but one integer compare. It exists for the other 1%: with a 4-node beam at
	/// depth 3, a 47-action board costs ~423 rollouts against ~36 for a typical one, and the
	/// actions driving that count are near-identical anyway (which of twenty Goblins attacks
	/// first). Capping at 16 takes that worst case to ~144.
	///
	/// Cutting an action here is not refusing to ever play it. SelectAction runs afresh after
	/// every action, so a pruned action is re-offered from the next state — the search declines
	/// to explore it *in this ordering*, not to make the play.
	/// </summary>
	public const int DefaultMaxBranching = 16;

	/// <summary>
	/// The same cap for levels below the root, where it is deliberately much tighter.
	///
	/// Profiling a move attributes **72% of search cost to `ExpandNode` and 27% to level 0** —
	/// expansion spends `beam (4) x branching (16)` per level against level 0's single pass, so
	/// roughly three times the budget goes to levels that cannot change *which* action is
	/// returned. `SelectAction` always returns a ROOT action; deeper levels only refine the score
	/// that ranks the roots. Buying that refinement at 3x the price of the decision itself is the
	/// wrong allocation.
	/// </summary>
	public const int DefaultExpandBranching = 5;

	// Starts the per-move clock and work counter. Called at the top of every public entry
	// point so that both action selection and choice resolution honor the same budget.
	private void StartMoveTimer()
	{
		_moveStartTimestamp = Stopwatch.GetTimestamp();
		Interlocked.Exchange(ref _rolloutsThisMove, 0);
	}

	// True once the per-move wall-clock budget is spent. Always false when no budget is
	// configured (simulator path), so deterministic behavior is preserved there.
	private bool BudgetExceeded() =>
		_moveBudgetTimestampTicks > 0
		&& Stopwatch.GetTimestamp() - _moveStartTimestamp > _moveBudgetTimestampTicks;

	/// <summary>
	/// True once this move has spent its deterministic rollout budget.
	///
	/// **Only ever call this where the parallel work has joined.** The counter is incremented
	/// from inside Parallel.For bodies, so its value mid-level depends on thread scheduling;
	/// its total between levels does not, because a sum does not care what order it was added
	/// in. Testing it at a sequential point is what keeps the search deterministic — testing
	/// it inside a parallel body would reintroduce exactly the machine-dependence this whole
	/// change exists to remove.
	/// </summary>
	private bool RolloutBudgetExhausted() =>
		_rolloutBudget > 0 && Volatile.Read(ref _rolloutsThisMove) >= _rolloutBudget;

	public GameAction SelectAction(GameState state, MtgGameIds ids, int playerId)
	{
		StartMoveTimer();
		var actions = MtgActionGenerator.GetLegalActions(state, ids, playerId);

		if (actions.Count == 0)
			throw new InvalidOperationException(
				"SelectAction called with no legal actions available"
			);

		if (actions.Count == 1)
		{
			InvalidateChain();
			if (_captureDecisions)
			{
				var desc = ActionDescriber.Describe(actions[0], state);
				LastDecision = new AiDecision(desc, 0f, [new AiActionCandidate(desc, 0f, true)]);
			}
			return actions[0];
		}

		var chainAction = TryConsumeCommittedAction(state, actions, playerId);
		if (chainAction != null)
			return chainAction;

		// Level 0: execute each root action and score via multi-turn rollout.
		// Pre-allocated array + Parallel.For gives deterministic ordering without AsOrdered buffering overhead.
		actions = NarrowActions(state, actions, playerId);
		var beamArray = new BeamNode[actions.Count];
		Parallel.For(
			0,
			actions.Count,
			i =>
			{
				var resultState = ExecuteAction(state, actions[i]);
				// Once the budget is spent, score remaining roots with the cheap immediate
				// evaluator instead of a full rollout so level 0 can't run away on a wide board.
				var score = BudgetExceeded()
					? StateEvaluator.Evaluate(resultState, _ids, playerId)
					: ScoreAfterCompletingTurn(resultState, playerId);
				beamArray[i] = new BeamNode(resultState, [actions[i]], score);
			}
		);
		var beam = beamArray.ToList();

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
			CommitChain(winner.ActionPath, state, playerId);
			return winner.RootAction;
		}

		beam = PruneBeam(beam, playerId);

		// Levels 1..currentTurnDepth-1: expand within the current turn only
		for (var depth = 1; depth < _currentTurnDepth; depth++)
		{
			// Out of time (interactive) or out of work (simulator) — stop expanding and pick
			// the best node found so far. Both tests sit here, between levels, where the
			// previous level's Parallel.For has joined.
			if (BudgetExceeded() || RolloutBudgetExhausted())
				break;

			var expansions = new List<BeamNode>[beam.Count];
			Parallel.For(0, beam.Count, i => expansions[i] = ExpandNode(beam[i], playerId));
			var nextBeam = expansions.SelectMany(x => x).ToList();

			if (nextBeam.Count == 0)
				break;

			if (_captureDecisions)
				UpdateRootScores(rootScores!, nextBeam);

			winner = FindWinner(nextBeam);
			if (winner != null)
			{
				if (_captureDecisions)
					SetLastDecision(winner.RootAction, rootScores!, state);
				CommitChain(winner.ActionPath, state, playerId);
				return winner.RootAction;
			}

			beam = PruneBeam(nextBeam, playerId);
		}

		if (_captureDecisions)
			UpdateRootScores(rootScores!, beam);

		var chosenNode = PickBestNode(beam);
		if (_captureDecisions)
			SetLastDecision(chosenNode.RootAction, rootScores!, state);
		CommitChain(chosenNode.ActionPath, state, playerId);
		return chosenNode.RootAction;
	}

	/// <summary>
	/// Returns the committed chain action if the chain is still valid, or null to trigger a fresh search.
	/// </summary>
	private GameAction? TryConsumeCommittedAction(
		GameState state,
		List<GameAction> actions,
		int playerId
	)
	{
		if (_committedChain == null || _chainIndex >= _committedChain.Count)
			return null;

		if (_chainIndex > 0)
		{
			var currentScore = StateEvaluator.Evaluate(state, _ids, playerId);
			var expectedScore = _committedChainExpectedScores![_chainIndex];
			if (Math.Abs(currentScore - expectedScore) > _scoreDivergenceThreshold)
			{
				InvalidateChain();
				return null;
			}
		}

		var freshAction = FindCommittedAction(actions, _committedChain[_chainIndex]);
		if (freshAction == null)
		{
			InvalidateChain();
			return null;
		}

		if (_captureDecisions)
		{
			var desc = ActionDescriber.Describe(freshAction, state);
			var score = _committedChainExpectedScores?[_chainIndex] ?? 0f;
			LastDecision = new AiDecision(
				$"[Chain] {desc}",
				score,
				[new AiActionCandidate(desc, score, true)]
			);
		}

		_chainIndex++;
		if (_chainIndex >= _committedChain.Count)
			InvalidateChain();
		return freshAction;
	}

	public ImmutableList<int> ResolveChoice(GameState state, ChoiceAction choice, int playerId)
	{
		StartMoveTimer();
		if (choice.Options.IsEmpty)
			return ImmutableList<int>.Empty;

		if (choice.MinChoices > 1)
		{
			var bestScore = float.MinValue;
			ImmutableList<int>? bestSelection = null;

			foreach (var combo in GetCombinations(choice.Options, choice.MinChoices))
			{
				// Stop enumerating combinations once either budget is spent; keep the best found.
				//
				// The rollout budget is the load-bearing one here. This loop is C(n, k) and pays
				// a FULL rollout per combination, and its only previous escape was the wall-clock
				// budget — which is null in the simulator, so it never fired. That is how games
				// with three permanents on the board burned five minutes: choice resolution is
				// not counted in TotalActions either, so the cost was invisible in every report.
				// Sequential loop, so reading the counter here is deterministic.
				if (bestSelection != null && (BudgetExceeded() || RolloutBudgetExhausted()))
					break;
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
		var scored = 0;
		var optionScores = _captureDecisions
			? new List<(ChoiceOption Option, float Score)>(choice.Options.Count)
			: null;

		foreach (var option in choice.Options)
		{
			// Budget spent — keep the best option scored so far rather than scoring all.
			// Always score at least one so bestOption is meaningful. Same reasoning as the
			// combination loop above: one full rollout per option, previously bounded only by a
			// wall clock the simulator does not set.
			if (scored > 0 && (BudgetExceeded() || RolloutBudgetExhausted()))
				break;
			scored++;

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
	/// Narrows a level to the actions worth paying a rollout for. Returns the input untouched
	/// below the cap, which is the common case by a wide margin.
	///
	/// Ranking uses StateEvaluator directly — one ExecuteAction and an immediate score, against
	/// a rollout's turn completion plus two-turn lookahead. That is the same cheap/expensive
	/// pair SelectAction already falls back to when the move time budget is spent.
	///
	/// Keeps a potential bucket alongside the concrete one, exactly as PruneBeam does. Ranking
	/// on immediate score alone would cut a fast-mana setup line before it was ever rolled out
	/// — the precise failure IPotentialEvaluator exists to prevent, and it would be invisible
	/// because the action simply never gets explored.
	/// </summary>
	private List<GameAction> NarrowActions(
		GameState state,
		List<GameAction> actions,
		int playerId,
		int? capOverride = null
	)
	{
		// The override never widens: an explicit maxBranching of 1 must stay 1 at every level.
		var cap = capOverride is null ? _maxBranching : Math.Min(capOverride.Value, _maxBranching);
		if (actions.Count <= cap)
			return actions;

		// Pre-allocated array + Parallel.For, then sort: the ranking must not depend on the
		// order threads happen to finish in. Same rule as RolloutBudgetExhausted.
		var scored = new (float Concrete, float Potential, int Index)[actions.Count];
		Parallel.For(
			0,
			actions.Count,
			i =>
			{
				var next = ExecuteAction(state, actions[i]);
				scored[i] = (
					StateEvaluator.Evaluate(next, _ids, playerId),
					_potentialEvaluators.Count > 0
						? _potentialEvaluators[0].Score(next, playerId)
						: 0f,
					i
				);
			}
		);

		var keep = new HashSet<int>();
		foreach (var s in scored.OrderByDescending(s => s.Concrete).ThenBy(s => s.Index).Take(cap))
			keep.Add(s.Index);

		// One potential bucket, sized as the evaluators themselves ask for.
		if (_potentialEvaluators.Count > 0)
			foreach (
				var s in scored
					.OrderByDescending(s => s.Potential)
					.ThenBy(s => s.Index)
					.Take(_potentialEvaluators[0].SlotCount)
			)
				keep.Add(s.Index);

		return [.. keep.OrderBy(i => i).Select(i => actions[i])];
	}

	/// <summary>
	/// Expands one beam level within the current turn only. Stops at turn boundaries —
	/// the multi-turn lookahead is handled by ScoreAfterCompletingTurn, not here.
	/// </summary>
	private List<BeamNode> ExpandNode(BeamNode node, int playerId)
	{
		// Budget spent mid-level — return the node unexpanded with its existing score
		// rather than executing every child action (each of which runs a full rollout).
		if (BudgetExceeded())
			return [node];

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

		// The bigger of the two call sites by far — 72% of search cost measured — because it runs
		// once per beam node. Capped tighter than the root for that reason; see
		// DefaultExpandBranching.
		actions = NarrowActions(state, actions, playerId, _expandBranching);

		var result = new List<BeamNode>(actions.Count);
		foreach (var action in actions)
		{
			var resultState = ExecuteAction(state, action);
			var score = ScoreAfterCompletingTurn(resultState, playerId);
			result.Add(new BeamNode(resultState, node.ActionPath.Add(action), score));
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
		Interlocked.Increment(ref _rolloutsThisMove);
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
					var rand = state.RngSeed != 0 ? new Random(state.RngSeed) : Random.Shared;
					state = ExecuteAction(state, randomActions[rand.Next(randomActions.Count)]);
					state = ResolveAllChoices(state, opponentId);
				}
				return ExecuteAction(state, BuildEndTurnAction(state));

			case OpponentSimulationMode.BoardOnly:
				// Iterate all attacks greedily so the simulation captures full damage from
				// multiple attackers — stopping after one attack underestimates lethal threats.
				// Activated abilities are excluded: they don't affect attack outcomes and
				// iterating them causes O(n²) blowup across the beam search.
				while (true)
				{
					var attackActions = MtgActionGenerator
						.GetLegalActions(state, _ids, opponentId)
						.OfType<AttackAction>()
						.ToList();
					if (attackActions.Count == 0)
						break;
					var bestAttack = attackActions.MaxBy(a =>
						StateEvaluator.Evaluate(ExecuteAction(state, a), _ids, opponentId)
					)!;
					state = ExecuteAction(state, bestAttack);
					state = ResolveAllChoices(state, opponentId);
					if (
						Math.Abs(StateEvaluator.Evaluate(state, _ids, ourPlayerId))
						>= StateEvaluator.WinScore
					)
						break;
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
		// Safety cap: if a choice ever fails to clear its waiting flag, bail out instead of
		// spinning forever on the calling thread. A real game never chains this many choices.
		for (var i = 0; i < MaxChoiceResolutionIterations && state.IsWaitingForChoice; i++)
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

	// EndTurn must exceed the best non-EndTurn score by EndTurnBias to be chosen.
	private BeamNode PickBestNode(List<BeamNode> beam)
	{
		var nonEndTurnNodes = beam.Where(n => n.RootAction is not EndTurnAction).ToList();
		if (nonEndTurnNodes.Count == 0)
			return beam.First(n => n.RootAction is EndTurnAction);

		var bestNonEndTurnScore = nonEndTurnNodes.Max(n => n.ConcreteScore);
		var endTurnScore = beam.Where(n => n.RootAction is EndTurnAction)
			.Select(n => n.ConcreteScore)
			.DefaultIfEmpty(float.MinValue)
			.Max();

		if (endTurnScore > bestNonEndTurnScore + EndTurnBias)
			return beam.First(n => n.RootAction is EndTurnAction);

		var bestNodes = nonEndTurnNodes.Where(n => n.ConcreteScore == bestNonEndTurnScore).ToList();
		return bestNodes[_rng.Next(bestNodes.Count)];
	}

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

	private void InvalidateChain()
	{
		_committedChain = null;
		_committedChainExpectedScores = null;
		_chainIndex = 0;
	}

	private void CommitChain(
		ImmutableList<GameAction> path,
		GameState stateBeforeFirstAction,
		int playerId
	)
	{
		if (path.Count <= 1)
		{
			InvalidateChain();
			return;
		}
		_committedChain = path;
		_chainIndex = 1; // position 0 is being returned right now

		// Pre-compute cheap StateEvaluator scores at each step for sanity checking.
		// _committedChainExpectedScores[i] = score of the state just before executing path[i].
		var scores = ImmutableList.CreateBuilder<float>();
		var s = stateBeforeFirstAction;
		foreach (var action in path)
		{
			scores.Add(StateEvaluator.Evaluate(s, _ids, playerId));
			s = ExecuteAction(s, action);
		}
		_committedChainExpectedScores = scores.ToImmutable();
	}

	private static GameAction? FindCommittedAction(List<GameAction> legal, GameAction committed) =>
		legal.FirstOrDefault(a => ActionsMatch(a, committed));

	/// <remarks>
	/// Internal so the X-cost identity rule below can be asserted directly. Forcing the beam to
	/// commit a chain from a unit test is not reliably reproducible, and the invariant — a planned
	/// action must never be re-found as a materially different one — is worth pinning on its own.
	/// </remarks>
	internal static bool ActionsMatch(GameAction a, GameAction b) =>
		(a, b) switch
		{
			(PlayLandAction x, PlayLandAction y) => x.CardId == y.CardId,
			// XValue IS PART OF THE IDENTITY OF AN X SPELL. Without it, a chain that planned
			// "cast this for X=4" re-finds the FIRST legal action with the same card id on replay —
			// and MtgActionGenerator enumerates X ascending, so that is X=0. Every X card in the
			// cube was therefore cast for zero whenever the beam search committed a chain: Banefire
			// and Earthquake dealt no damage, Mind Spring drew nothing, and the two green Hydras
			// arrived as 0/0s and died on the spot. Indistinguishable from a blank card, and it is
			// what put Primordial Hydra at the bottom of the win-rate table.
			(CastCreatureAction x, CastCreatureAction y) => x.CardId == y.CardId
				&& x.XValue == y.XValue,
			(CastSpellAction x, CastSpellAction y) => x.CardId == y.CardId
				&& x.XValue == y.XValue
				&& TargetIdsMatch(x.TargetIds, y.TargetIds),
			(AttackAction x, AttackAction y) => x.AttackerId == y.AttackerId
				&& x.TargetId == y.TargetId,
			(EndTurnAction, EndTurnAction) => true,
			(ActivateAbilityAction x, ActivateAbilityAction y) => x.CardId == y.CardId
				&& x.AbilityIndex == y.AbilityIndex,
			(CastFromGraveyardAction x, CastFromGraveyardAction y) => x.CardId == y.CardId,
			_ => false,
		};

	private static bool TargetIdsMatch(
		ImmutableDictionary<int, ImmutableList<int>> a,
		ImmutableDictionary<int, ImmutableList<int>> b
	)
	{
		if (a.Count != b.Count)
			return false;
		foreach (var kvp in a)
		{
			if (!b.TryGetValue(kvp.Key, out var bList))
				return false;
			if (!kvp.Value.SequenceEqual(bList))
				return false;
		}
		return true;
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
