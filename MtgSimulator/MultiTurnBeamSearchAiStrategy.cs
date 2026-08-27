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

	/// <summary>
	/// Sandbox card values, consulted ONLY by ResolveChoice. Null disables the term entirely.
	/// </summary>
	private readonly CardValueTable? _cardValues;
	private readonly IReadOnlyList<IPotentialEvaluator> _potentialEvaluators;
	private readonly Random _rng;
	private readonly bool _captureDecisions;
	private readonly float _scoreDivergenceThreshold;

	// Per-half-turn terminal decay. A field only so the strength harness can turn it off; every
	// production path takes the TerminalDiscount default.
	private readonly float _terminalDiscount;

	// Whether FindWinner returns the best win or the first one it sees. A field only so the
	// strength harness can measure the change; every production path takes the default.
	private readonly bool _preferFastestWin;

	// False restores the half-applied-action bug, so the harness can measure the fix. Production
	// always takes true.
	private readonly bool _resolveChoicesOnExecute;

	/// <summary>
	/// The evaluator every score in this search comes from. Defaults to
	/// <c>WeightedStateEvaluator.Default</c>, which is exactly what the static
	/// <c>StateEvaluator</c> wraps, so injecting nothing is byte-identical to the old behaviour.
	///
	/// **One evaluator, not two.** Cowling's rollout-policy distinction argues for separating the
	/// LEAF score (which action is returned) from the ROLLOUT POLICY (how the greedy simulation
	/// plays) — a better card valuer can help the first and hurt the second. That split is real
	/// and belongs here eventually, but nothing needs it yet: the current experiments vary weights,
	/// which want to move together. Adding a second parameter later is a small change; guessing
	/// wrong about which sites belong in which group today is not.
	/// </summary>
	private readonly IStateEvaluator _eval;

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

	/// <summary>
	/// Internal rather than private so <c>WinDetectionTests</c> can hand a beam straight to
	/// <see cref="FindWinner"/>. Same reasoning as <c>ActionsMatch</c>: the invariant is worth
	/// pinning at the function, because a game-level test passes whether or not it holds.
	/// </summary>
	internal record BeamNode(
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
		int expandBranching = DefaultExpandBranching,
		float terminalDiscount = TerminalDiscount,
		bool preferFastestWin = true,
		IStateEvaluator? evaluator = null,
		bool resolveChoicesOnExecute = true,
		CardValueTable? cardValues = null
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
		_terminalDiscount = terminalDiscount;
		_cardValues = cardValues;
		_preferFastestWin = preferFastestWin;
		_resolveChoicesOnExecute = resolveChoicesOnExecute;
		_eval = evaluator ?? WeightedStateEvaluator.Default;
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

	/// <summary>
	/// Per-half-turn decay applied to a rollout that ends in a win or a loss.
	///
	/// Every rollout that does NOT reach a terminal runs the full lookahead, so they all describe
	/// the same moment and are directly comparable. A terminal breaks out early and returns the
	/// same +/-WinScore whenever it happened, which throws that common horizon away: winning next
	/// half-turn and winning in two scored *identically*, as did dying next half-turn and dying in
	/// two. Inside the "someone dies within the lookahead" region every line collapsed to one
	/// number, so the search had no gradient left and fell through to its tiebreak — at exactly
	/// the point where the choice matters most.
	///
	/// Because a loss is negative, decaying it moves it *toward* zero, so a later death outscores
	/// an earlier one. Playing for the topdeck therefore falls out of the arithmetic rather than
	/// needing a rule of its own. This is why LossScore must stay negative rather than 0 — a zero
	/// loss decays to zero and the survival half of the effect disappears.
	///
	/// The value is not sensitive. At the default 2-turn lookahead any factor in ~0.85-0.99 ranks
	/// identically; all that is required is that a discounted terminal stay far above the largest
	/// non-terminal score (~150 at the current weights), which holds to 0.95^40 ≈ 1285. Raise the
	/// lookahead far enough and that margin is what would break first.
	///
	/// From Cowling, Ward &amp; Powley (2012), "Ensemble Determinization in MCTS for Magic: The
	/// Gathering", section D. Their λ = 0.99 is calibrated for rollouts that run to a terminal
	/// over 40-60 turns; this rollout is bounded at 2, so the factor per step has to be larger.
	/// </summary>
	public const float TerminalDiscount = 0.95f;

	/// <summary>
	/// Decays a terminal rollout result by how many half-turns it took to arrive. Non-terminal
	/// scores pass through untouched — they already share a horizon and need no correction.
	///
	/// Internal so <c>TerminalDiscountTests</c> can assert the ordering directly. A game-level
	/// test would only reach this through a full beam search, which is the same trap
	/// <c>ActionsMatch</c> documents: the assertion passes whether or not the logic is present.
	/// </summary>
	/// <param name="lambda">
	/// Per-half-turn decay. Defaults to <see cref="TerminalDiscount"/>; exists as a parameter so
	/// <c>EvaluatorStrengthTests</c> can play discount-on against discount-off (lambda 1.0) in one
	/// process. A behaviour change that alters which action is returned has to be measurable
	/// against its own absence, and rebuilding an old commit to get an opponent is not a harness.
	/// </param>
	internal static float DiscountTerminal(
		float score,
		int halfTurns,
		float lambda = TerminalDiscount
	) => MathF.Abs(score) >= StateEvaluator.WinScore ? score * MathF.Pow(lambda, halfTurns) : score;

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
				// StateBefore is carried even on a forced move. There is nothing to compare it
				// against, but the inspector renders the current position from it, and a panel
				// that goes blank whenever the AI had no choice reads as broken tooling rather
				// than as "no decision was made here".
				var desc = ActionDescriber.Describe(actions[0], state);
				LastDecision = new AiDecision(
					desc,
					0f,
					[new AiActionCandidate(desc, 0f, true)],
					StateBefore: _eval.Explain(state, _ids, playerId)
				);
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
					? _eval.Evaluate(resultState, _ids, playerId)
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

		var winner = FindWinner(beam, _preferFastestWin);
		if (winner != null)
		{
			if (_captureDecisions)
				SetLastDecision(winner.RootAction, rootScores!, state, playerId);
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

			winner = FindWinner(nextBeam, _preferFastestWin);
			if (winner != null)
			{
				if (_captureDecisions)
					SetLastDecision(winner.RootAction, rootScores!, state, playerId);
				CommitChain(winner.ActionPath, state, playerId);
				return winner.RootAction;
			}

			beam = PruneBeam(nextBeam, playerId);
		}

		if (_captureDecisions)
			UpdateRootScores(rootScores!, beam);

		var chosenNode = PickBestNode(beam);
		if (_captureDecisions)
			SetLastDecision(chosenNode.RootAction, rootScores!, state, playerId);
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
			var currentScore = _eval.Evaluate(state, _ids, playerId);
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

	/// <summary>
	/// The rollout, plus what the resulting HAND is worth.
	///
	/// Only choices are scored this way. An evaluator term would be consulted about every action in
	/// the game, including land drops — and because a card's reach discount moves with MaxMana, that
	/// made playing a land score NEGATIVE and cost 24.6% win rate over 1120 games. Within one choice
	/// the mana is identical across every option, so the discount is a constant and cannot distort
	/// anything except the ranking it is there to inform.
	///
	/// Scoring the resulting hand rather than the chosen card is what makes direction automatic:
	/// discarding a bomb leaves a worse hand, tutoring one leaves a better hand, and nothing has to
	/// know which kind of choice this is.
	/// </summary>
	private float ScoreChoiceResult(GameState state, int playerId)
	{
		var score = ScoreAfterCompletingTurn(state, playerId);
		// A decided position is already worth +/-WinScore; adding hand value to that is noise on a
		// number the search compares against a threshold.
		if (_cardValues == null || StateEvaluator.IsDecisive(score))
			return score;
		return score + _cardValues.HandValue(state, playerId);
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
				var score = ScoreChoiceResult(resultState, playerId);

				if (score > bestScore)
				{
					bestScore = score;
					bestSelection = selectedIds;
				}

				if (StateEvaluator.IsWin(bestScore))
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
			var score = ScoreChoiceResult(resultState, playerId);

			optionScores?.Add((option, score));

			if (score > bestSingleScore)
			{
				bestSingleScore = score;
				bestOption = option;
			}

			if (StateEvaluator.IsWin(bestSingleScore))
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
		//
		// Every potential evaluator gets its own column. This read _potentialEvaluators[0] for both
		// the score AND the slot count, so a second evaluator was silently ignored by the branching
		// cap while PruneBeam honoured it — the two disagreed about which lines were worth keeping.
		// Behaviour-identical with the one evaluator configured today, which is why it is safe to
		// fix here rather than entangled with whatever adds the second one.
		var scored = new (float Concrete, float[] Potential, int Index)[actions.Count];
		Parallel.For(
			0,
			actions.Count,
			i =>
			{
				var next = ExecuteAction(state, actions[i]);
				var potential = new float[_potentialEvaluators.Count];
				for (var e = 0; e < _potentialEvaluators.Count; e++)
					potential[e] = _potentialEvaluators[e].Score(next, playerId);
				scored[i] = (_eval.Evaluate(next, _ids, playerId), potential, i);
			}
		);

		var keep = new HashSet<int>();
		foreach (var s in scored.OrderByDescending(s => s.Concrete).ThenBy(s => s.Index).Take(cap))
			keep.Add(s.Index);

		// One bucket per evaluator, each sized as that evaluator asks. Mirrors PruneBeam.
		for (var e = 0; e < _potentialEvaluators.Count; e++)
		{
			var index = e;
			foreach (
				var s in scored
					.OrderByDescending(s => s.Potential[index])
					.ThenBy(s => s.Index)
					.Take(_potentialEvaluators[index].SlotCount)
			)
				keep.Add(s.Index);
		}

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
	/// <summary>
	/// The rollout's END state alongside its score, for diagnosis.
	///
	/// The AI inspector shows the position each candidate leads to IMMEDIATELY, which is the right
	/// default — it answers "what did this action buy". It cannot answer "why do two actions with
	/// identical immediate positions score differently", because that divergence happens inside
	/// the rollout. This is the seam for that question, and the Swiftfoot Boots oscillation is the
	/// case that needed it: two lines, both +0.00 immediate, 1.40 apart after the rollout.
	/// </summary>
	internal (float Score, GameState EndState) ScoreAndEndState(GameState state, int playerId)
	{
		var g0 = state.TryGetGame();
		if (g0?.ActivePlayerId == playerId)
			state = PlayGreedyTurn(state, playerId);

		var opponentId = playerId == _ids.Player1Id ? _ids.Player2Id : _ids.Player1Id;
		var halfTurns = 0;
		for (var t = 0; t < _lookaheadTurns; t++)
		{
			var g = state.TryGetGame();
			if (g == null)
				break;
			state =
				g.ActivePlayerId == playerId
					? PlayGreedyTurn(state, playerId)
					: SimulateOpponentTurn(state, playerId, opponentId);
			halfTurns = t + 1;
			if (StateEvaluator.IsDecisive(_eval.Evaluate(state, _ids, playerId)))
				break;
		}

		return (
			DiscountTerminal(_eval.Evaluate(state, _ids, playerId), halfTurns, _terminalDiscount),
			state
		);
	}

	/// <summary>
	/// Internal rather than private so <see cref="CardValueSandbox"/> can score a fixture with the
	/// same rollout the live search uses. A second playout written for the sandbox would measure
	/// card value in units nothing else consumes — the same reason <c>Explain</c> is the
	/// implementation and <c>Evaluate</c> the wrapper.
	/// </summary>
	internal float ScoreAfterCompletingTurn(GameState state, int playerId)
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
	/// <summary>
	/// Rolls forward to a FIXED point in game time, measured from the state the search started
	/// from — not a fixed number of half-turns from wherever this candidate happens to be.
	///
	/// **This is what stops the AI deferring costs it cannot actually avoid.** With a relative
	/// horizon, a line that dawdles inside its own turn pushes the window along with it, so a
	/// different number of turn boundaries — and therefore of end-step triggers, upkeep costs and
	/// draw steps — lands inside it. Deferring a cost genuinely improved the score, because the
	/// cost fell off the end of a window that moved.
	///
	/// Measured before the fix, on a board with Avaricious Dragon (discard 1 when your turn ends)
	/// and a free equip available: ending the turn scored 48.50 against the equip's 47.10 at
	/// lookahead 0, 59.30 against 57.90 at lookahead 1, and then INVERTED to 72.70 against 74.10
	/// at the shipped lookahead of 2. An answer that changes with depth is the definition of a
	/// horizon effect. The engine needed a per-turn cap on equips to stop the resulting loop.
	///
	/// With an absolute anchor every candidate spans the identical stretch of game time, so every
	/// recurring cost fires the same number of times in every line and cancels. Dawdling buys
	/// nothing because the window does not move.
	///
	/// The anchor deliberately does NOT change the cost knobs — <c>currentTurnDepth</c>,
	/// <c>maxBranching</c>, <c>expandBranching</c> and <c>rolloutBudget</c> all still bound the
	/// work. It only changes where the rollout stops for comparison. Cost per candidate moves by
	/// at most one half-turn, and the candidate that already ended its turn simulating less is
	/// correct — it genuinely spent less of its turn.
	/// </summary>
	private float MultiTurnGreedyRollout(GameState state, int playerId)
	{
		var opponentId = playerId == _ids.Player1Id ? _ids.Player2Id : _ids.Player1Id;

		var halfTurns = 0;
		for (var t = 0; t < _lookaheadTurns; t++)
		{
			var game = state.TryGetGame();
			if (game == null)
				break;

			state =
				game.ActivePlayerId == playerId
					? PlayGreedyTurn(state, playerId)
					: SimulateOpponentTurn(state, playerId, opponentId);
			halfTurns++;

			if (StateEvaluator.IsDecisive(_eval.Evaluate(state, _ids, playerId)))
				break;
		}

		// Terminals short-circuit the loop, so they arrive from different points in time and need
		// to be made comparable again. See TerminalDiscount.
		return DiscountTerminal(
			_eval.Evaluate(state, _ids, playerId),
			halfTurns,
			_terminalDiscount
		);
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
				_eval.Evaluate(ExecuteAction(state, a), _ids, playerId)
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
						_eval.Evaluate(ExecuteAction(state, a), _ids, opponentId)
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
						_eval.Evaluate(ExecuteAction(state, a), _ids, opponentId)
					)!;
					state = ExecuteAction(state, bestAttack);
					state = ResolveAllChoices(state, opponentId);
					if (
						Math.Abs(_eval.Evaluate(state, _ids, ourPlayerId))
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
	/// <summary>
	/// Drains pending choices inside a rollout, answering each one AS ITS OWNER.
	///
	/// **The owner comes from <c>GetPendingChoiceDecidingPlayerId</c>, never from whose simulated
	/// turn it happens to be.** That distinction is documented in ImmutableGameObjects/CLAUDE.md
	/// and honoured by GameRunner and MtgGameManager; this rollout was the one place that ignored
	/// it, and it scored choices from a fixed perspective for the whole drain.
	///
	/// The consequence was adversarial: <c>ExecuteAction</c> does not resolve choices, so a
	/// candidate action that leaves one pending — an end-step discard, say — hands a waiting state
	/// to the rollout. If the next simulated half-turn was the opponent's, SimulateOpponentTurn
	/// called this with the OPPONENT's id, and <c>GreedyResolveChoice</c> then picked whichever of
	/// OUR discards scored best for THEM. Our own trigger was resolved against us.
	///
	/// That is what made ending the turn look worse than a pointless free equip on a board with
	/// Avaricious Dragon: the equip line's turn ended inside PlayGreedyTurn, which drained the
	/// discard from our own perspective, while the EndTurn line's identical discard was drained by
	/// the opponent's. Two lines, the same board, the same game time, one card apart — and it read
	/// as a horizon effect for two rounds of investigation because the symptom (an answer that
	/// changes with search depth) is exactly what a horizon effect looks like.
	/// </summary>
	private GameState ResolveAllChoices(GameState state, int playerId)
	{
		// Safety cap: if a choice ever fails to clear its waiting flag, bail out instead of
		// spinning forever on the calling thread. A real game never chains this many choices.
		for (var i = 0; i < MaxChoiceResolutionIterations && state.IsWaitingForChoice; i++)
		{
			var choice = state.GetPendingChoice()!;
			// 0 means the owner could not be determined; the documented contract is to fall back
			// to the caller rather than leave the choice unanswerable and wedge the stack.
			var owner = state.GetPendingChoiceDecidingPlayerId();
			var ids = GreedyResolveChoice(state, choice, owner == 0 ? playerId : owner);
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

	/// <summary>
	/// The BEST winning node in a scored beam, if there is one — this is what lets SelectAction
	/// return a win immediately instead of finishing the search.
	///
	/// <c>ConcreteScore</c> is a ROLLOUT score and has been through <see cref="DiscountTerminal"/>,
	/// so it must be tested with <c>StateEvaluator.IsWin</c>. Comparing it against
	/// <c>WinScore</c> directly is the regression <c>WinDetectionTests</c> exists to catch: a
	/// discounted win is 9500, <c>>= 10000</c> is false, and this silently returns null for every
	/// win the search will ever find.
	///
	/// **Best, not first, and that is a second bug the discount exposed.** This was
	/// <c>FirstOrDefault</c>, which was correct while every win scored exactly WinScore — "first
	/// win" and "best win" were the same node. Once terminals decay by how long they took, they
	/// become rankable, and taking the first one in beam order means settling for a win two turns
	/// out while a win this turn sits further down the list. Measured: on a board with three
	/// damage available and the opponent at six, it cast its burn spell at ITSELF (still a win in
	/// the rollout, since the creature kills over the following two turns) in preference to two
	/// lines that won immediately.
	///
	/// <c>MaxBy</c> is stable on ties, so beam order still breaks equal-length wins and this stays
	/// deterministic.
	/// </summary>
	/// <param name="preferFastest">
	/// False restores the old FirstOrDefault behaviour, so <c>EvaluatorStrengthTests</c> can play
	/// this against its own absence. Production never passes it.
	/// </param>
	internal static BeamNode? FindWinner(List<BeamNode> beam, bool preferFastest = true)
	{
		BeamNode? best = null;
		foreach (var node in beam)
		{
			if (!StateEvaluator.IsWin(node.ConcreteScore))
				continue;
			if (!preferFastest)
				return node;
			if (node.ConcreteScore > (best?.ConcreteScore ?? float.MinValue))
				best = node;
		}
		return best;
	}

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

		// EndTurn must strictly beat the best action; ties go to acting.
		//
		// Breaking ties toward EndTurn was tried here and REVERTED — it made the AI never equip
		// anything. Swiftfoot Boots grants keywords the evaluator has no term for, so an equip
		// scores exactly 0.00, and a ties-to-EndTurn rule kills every action whose value the
		// evaluator cannot see rather than only the pointless ones.
		// Equip_IsStillTakenOncePerTurn is the gate that caught it: equipment that can never be
		// attached is a worse bug than the loop it was meant to stop.
		//
		// With the double-resolution bug in ExecuteAction fixed, the equip and EndTurn now score
		// IDENTICALLY on the Boots board (74.10 each) rather than the equip winning 74.10 to 72.70,
		// so the systematic preference is gone. What remains is a genuine tie, bounded by the
		// engine's per-turn equip cap. Note this contradicts DesignNotes.md, which records that
		// there is no tie — that was true, but only because of the bug.
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
			var score = _eval.Evaluate(resultState, _ids, playerId);
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
		GameState originalState,
		int playerId
	)
	{
		// Re-executing each candidate to read its immediate position costs one ExecuteAction per
		// displayed action. Only reached under _captureDecisions — every call site is guarded — so
		// the simulator and training paths pay nothing for it.
		var candidates = rootScores
			.OrderByDescending(kvp => kvp.Value)
			.Take(10)
			.Select(kvp => new AiActionCandidate(
				ActionDescriber.Describe(kvp.Key, originalState),
				kvp.Value,
				ReferenceEquals(kvp.Key, chosen),
				TryExplainAfter(originalState, kvp.Key, playerId)
			))
			.ToList();
		rootScores.TryGetValue(chosen, out var chosenScore);
		LastDecision = new AiDecision(
			ActionDescriber.Describe(chosen, originalState),
			chosenScore,
			candidates,
			StateBefore: _eval.Explain(originalState, _ids, playerId)
		);
	}

	/// <summary>
	/// The position one action leads to, for display only. An action that throws while being
	/// replayed for the inspector must not take the game down with it — the decision has already
	/// been made and returned by the time this runs.
	/// </summary>
	private EvaluationBreakdown? TryExplainAfter(GameState state, GameAction action, int playerId)
	{
		try
		{
			return _eval.Explain(ExecuteAction(state, action), _ids, playerId);
		}
		catch
		{
			return null;
		}
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
			scores.Add(_eval.Evaluate(s, _ids, playerId));
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

	/// <summary>
	/// Applies an action and drains any choice it raises, so the state handed on is a position and
	/// not a half-finished resolution.
	///
	/// **The drain is the point.** ProcessAllActions stops at a pending ChoiceAction, so an action
	/// with a trigger that asks a question left the state frozen mid-resolution: the action had
	/// executed, but its consequences had not, and the active player had not changed.
	///
	/// The rollout then read that state, saw it was still our turn, and ENDED THE TURN AGAIN —
	/// firing every end-of-turn trigger a second time. Measured on a board with Avaricious Dragon
	/// (discard 1 when your turn ends): the EndTurn candidate came back with game time unchanged
	/// and IsWaitingForChoice true, and finished the rollout one card worse than a line that had
	/// done nothing at all. That is what made a free, pointless equip outscore ending the turn,
	/// 74.10 to 72.70, and it cost the engine a per-turn cap on equips to contain the loop.
	///
	/// It presented as a horizon effect — the answer inverted with search depth — and two
	/// diagnoses were built on that reading before the state was actually inspected. Only actions
	/// that raise a choice were affected, which is why it looked card-specific.
	/// </summary>
	private GameState ExecuteAction(GameState state, GameAction action)
	{
		var (newState, success) = state.TryAddAction(action);
		if (!success)
			return state;
		var (finalState, _) = newState.ProcessAllActions();
		return _resolveChoicesOnExecute && finalState.IsWaitingForChoice
			? ResolveAllChoices(finalState, finalState.GetPendingChoiceDecidingPlayerId())
			: finalState;
	}
}
