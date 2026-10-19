using System.Collections.Immutable;
using System.Diagnostics;
using ImmutableGameObjects;
using MagicCore.Actions;

namespace MagicCore.Ai;

/// <summary>
/// The search AI: a beam over this player's own decisions for the rest of the turn, every line scored
/// by a rollout to one shared horizon (<see cref="Rollout"/>) — MtgSimulator's
/// MultiTurnBeamSearchAiStrategy, ported to priority, the stack and choices (story AI-2).
///
/// A decision is a move or the answer to a choice — attacks and blocks are choices here — so the beam
/// branches on both. Between our decisions the opponent answers through <see cref="Rollout.OpponentStep"/>,
/// the same model the rollouts use. On the opponent's turn there is no turn of ours to expand: a
/// priority window is one ply plus its rollout, as MtgSimulator treats a decision at a turn boundary —
/// but our blocks are still branched together, one blocker's choice after another.
///
/// We never respond to our OWN object on top of the stack — in the tree or for real (<see cref="Decide"/>
/// passes there without searching), so a committed plan stays in step with the game.
/// ponytail: a line that wants to protect its own spell with a second one is never found.
///
/// Carried from MtgSimulator (.claude/rules/sim-ai.md): work is bounded by a rollout BUDGET checked
/// only between levels, where the parallel work has joined, so the search is deterministic; a found
/// win is taken at once — the fastest; passing must strictly beat acting (an unseen benefit must not
/// lose its tie to doing nothing); the best line is COMMITTED and replayed while the position stays
/// within <see cref="Settings.DivergenceThreshold"/> of what it predicted, re-found by
/// <see cref="MagicRules.MoveKey"/> so a planned move is never replayed as a different one.
/// </summary>
public sealed class BeamSearchAi(int playerId, BeamSearchAi.Settings? settings = null) : IAi
{
	/// <param name="Depth">Levels of our own decisions searched this turn, the root included.</param>
	/// <param name="BeamWidth">Lines kept per level (MtgSimulator's live beam: 2 concrete + 2 potential).</param>
	/// <param name="MaxBranching">Root candidates rolled out; beyond it the cheap score picks which.</param>
	/// <param name="ExpandBranching">The same below the root, where it is tighter (MtgSimulator measured 72% of its cost there).</param>
	/// <param name="RolloutBudget">Rollouts one decision may start a new level with — about 22 ms each.</param>
	/// <param name="CommitPlans">False searches afresh at every decision (to measure the plan's worth).</param>
	/// <param name="TimeLimit">
	/// Wall clock for one decision. Below the root, a line is expanded only if one more parallel batch
	/// still fits — a batch costing what the last one did (a level costs 0.4–0.6 s in a real mid-game
	/// position, so checking the time once per level overran by that much). A line not expanded keeps
	/// its rollout score, to the same horizon as every other. For the interactive table and console
	/// ONLY: a sim run must leave it null, or its results depend on the machine (<see cref="Interactive"/>).
	/// ponytail: the root level always completes — 16 root moves measured at 1.1–2.2 s; skip root
	/// rollouts past the limit (scoring them by the cheap evaluation) if that ever shows on the table.
	/// </param>
	/// <param name="Library">What the search may know of the libraries — <see cref="Hidden.Library"/> (story AI-3).</param>
	/// <param name="Hand">What it may know of the opponent's hand — <see cref="Hidden.Hand"/> (story AI-3.4).</param>
	/// <param name="Threads">
	/// Threads one decision's rollouts may use; null (or below 1) is every core. 1 keeps a search on its
	/// own thread — for games played in parallel, where nesting one parallel loop in another only adds
	/// overhead (AI-6). Never changes a decision: each rollout fills its own slot.
	/// </param>
	public sealed record Settings(
		int Depth = 3,
		int BeamWidth = 4,
		int MaxBranching = 16,
		int ExpandBranching = 5,
		int RolloutBudget = 200,
		double PassBias = 0.01,
		bool CommitPlans = true,
		double DivergenceThreshold = 5.0,
		Rollout.Settings? RolloutSettings = null,
		TimeSpan? TimeLimit = null,
		Hidden.Library Library = Hidden.Library.RealOrder,
		int? Threads = null,
		Hidden.Hand Hand = Hidden.Hand.Real
	)
	{
		/// <summary>The search a person plays against: about a second per decision (story AI-2.7).</summary>
		public static Settings Interactive => new(TimeLimit: TimeSpan.FromSeconds(1));

		/// <summary>
		/// Held cards at half their measured value — the DEFAULT (story AI-1, measured 2026-10-16: 38–34
		/// against the plain search, +11% time per decision; at 1.0 it hoards and loses 26–46).
		/// </summary>
		public const double HeldFraction = 0.5;

		public Settings WithHeld(double fraction = HeldFraction) =>
			this with
			{
				RolloutSettings = (RolloutSettings ?? Rollout.Default) with
				{
					Held = HeldValues.Fra(fraction),
				},
			};

		/// <summary>The search as it was before held values: every card in hand at the flat price (A/B runs).</summary>
		public Settings WithoutHeld() =>
			this with
			{
				RolloutSettings = (RolloutSettings ?? Rollout.Default) with { Held = null },
			};
	}

	/// <summary>
	/// The search's rollout when no RolloutSettings are given: the rollout defaults with held cards at
	/// <see cref="Settings.HeldFraction"/>. **Build a custom RolloutSettings from THIS, not from
	/// Rollout.Default** — that one has no held values, so it is the old flat-price search. Lazy, so a
	/// missing card-value table fails only where held values are wanted, not the whole type.
	/// </summary>
	public static Rollout.Settings DefaultRollout => defaultRollout.Value;

	static readonly Lazy<Rollout.Settings> defaultRollout =
		new(() => Rollout.Default with { Held = HeldValues.Fra(Settings.HeldFraction) });

	/// <summary>A rollout at or above this has found a win; a decayed win is still far above it.</summary>
	public const double WinThreshold = 2_500;

	// The fallback to DefaultRollout is resolved HERE, once: every reader below takes options.
	readonly Settings options = (settings ?? new()) with
	{
		RolloutSettings = settings?.RolloutSettings ?? DefaultRollout,
	};

	// Mulligans and the choices of several picks: GreedyAi, pricing held cards as the search does
	// (resolved like `options` above — a field initialiser can't read another).
	readonly GreedyAi greedy = new(playerId, (settings?.RolloutSettings ?? DefaultRollout).Held);

	public int PlayerId { get; } = playerId;

	/// <summary>The settings this search runs with, defaults resolved.</summary>
	public Settings Options => options;

	/// <summary>One of our decisions in a line, with what identifies it when the plan is replayed.</summary>
	abstract record Decision(object Key);

	sealed record Move(GameAction Action, object Key) : Decision(Key);

	sealed record Pick(ImmutableList<int> Ids, object Key) : Decision(Key);

	/// <summary>A line: the state after its last decision, the decisions, its rollout score, and our
	/// evaluation just before each decision — what the plan expects to see when it replays them.</summary>
	sealed record Node(
		GameState State,
		ImmutableList<Decision> Path,
		ImmutableList<double> Expected,
		double Score
	);

	ImmutableList<Decision> plan = [];
	ImmutableList<double> planExpected = [];
	int planTurn;
	int rollouts;

	long totalRollouts;

	// The last search's reading of each first move, in the order the root offered them (AI-6.4b).
	readonly Dictionary<Decision, double> firstMoves = new(ReferenceEqualityComparer.Instance);
	readonly List<Decision> firstMoveOrder = [];

	/// <summary>
	/// The last decision's reading of each first move: the best score found for a line starting with
	/// it — its own rollout, or one deeper in its line. What the position suite's oracle stores (story
	/// AI-6.4b). Empty when the decision didn't search: one option, a plan replayed, a forced pass.
	/// </summary>
	public IReadOnlyList<(
		GameAction? Action,
		IReadOnlyList<int>? Answer,
		double Score
	)> FirstMoveScores =>
		[
			.. firstMoveOrder.Select(d =>
				d switch
				{
					Move m => (m.Action, (IReadOnlyList<int>?)null, firstMoves[d]),
					Pick p => ((GameAction?)null, p.Ids, firstMoves[d]),
					_ => throw new InvalidOperationException(d.GetType().Name),
				}
			),
		];

	/// <summary>Rollouts every decision so far has played — beside MagicRules.CountingSteps, the search's cost (story AI-6).</summary>
	public long Rollouts => totalRollouts;

	Rollout.Settings RolloutSettings => options.RolloutSettings!;

	public AiDecision Decide(GameState s)
	{
		rollouts = 0; // a plan replay or a pass doesn't search, and so doesn't reset it
		firstMoves.Clear();
		firstMoveOrder.Clear();
		var decided = DecideHere(s);
		totalRollouts += rollouts;
		return decided;
	}

	AiDecision DecideHere(GameState s)
	{
		if (s.GetGame().Step == Step.PreGame)
			return greedy.Decide(s); // mulligans: GreedyAi's rule
		if (!s.IsWaitingForChoice && Stops.TopOfStackControllerId(s) == PlayerId) // as the tree does: let our own object resolve
			return AiDecision.Acting(s, new PassPriorityAction { PlayerId = PlayerId });
		var decision = FromPlan(s) ?? Search(s);
		return decision switch
		{
			Move m => AiDecision.Acting(s, m.Action),
			Pick p => AiDecision.Answering(s, p.Ids),
			_ => throw new InvalidOperationException(decision.GetType().Name),
		};
	}

	/// <summary>The search, on the position <see cref="Hidden.Sample"/> lets it know; Decide plays the answer in the real game.</summary>
	Decision Search(GameState s)
	{
		s = Hidden.Sample(s, PlayerId, options.Library, options.Hand);
		(plan, planExpected, rollouts) = ([], [], 0);
		var clock = Stopwatch.StartNew();
		var rootTurn = s.GetGame().TurnNumber;
		var horizon = Rollout.Horizon(s, RolloutSettings);
		var children = Narrow(s, Children(s), options.MaxBranching);
		if (children.Count <= 1)
			return children.FirstOrDefault()
				?? new Pick([.. greedy.Answer(s, s.GetPendingChoice()!)], "");

		var root = new Node(s, [], [], 0);
		var beam = Expand(root, s, children, horizon);
		// Below the root each kept line is one parallel batch of at most ExpandBranching rollouts; the
		// first is estimated from the root's batch, scaled to that size (measured: a 0.8 s root of 16
		// moves, then batches of ~0.25 s).
		var perBatch =
			clock.Elapsed * Math.Min(1.0, (double)options.ExpandBranching / children.Count);
		if (Winner(beam) is { } won)
			return Commit(won, rootTurn);

		for (var depth = 1; depth < options.Depth && rollouts < options.RolloutBudget; depth++)
		{
			var (levelStart, batches) = (clock.Elapsed, 0);
			var next = new List<Node>();
			foreach (var node in Prune(beam))
			{
				var (at, leaf) = Advance(node.State, rootTurn);
				// The turn is over on this line, or there is no time to expand it: its score already
				// reaches the horizon.
				if (leaf || clock.Elapsed + perBatch >= options.TimeLimit)
				{
					next.Add(node);
					continue;
				}
				next.AddRange(
					Expand(node, at, Narrow(at, Children(at), options.ExpandBranching), horizon)
				);
				batches++;
			}
			beam = next;
			if (batches > 0) // none: every line's turn is over, or the time is
				perBatch = (clock.Elapsed - levelStart) / batches;
			if (Winner(beam) is { } win)
				return Commit(win, rootTurn);
			if (batches == 0)
				break;
		}
		return Commit(Best(beam), rootTurn);
	}

	/// <summary>Each child of a line, rolled out in parallel into its own slot — the order threads finish in never reaches a decision.</summary>
	List<Node> Expand(Node parent, GameState at, IReadOnlyList<Decision> children, int horizon)
	{
		var expected = parent.Expected.Add(Evaluator.Evaluate(at, PlayerId, RolloutSettings.Held));
		var nodes = new Node[children.Count];
		var atRoot = parent.Path.IsEmpty;
		void RollOut(int i)
		{
			var state = Apply(at, children[i]);
			var score = Rollout.Score(state, PlayerId, horizon, RolloutSettings);
			Interlocked.Increment(ref rollouts);
			nodes[i] = new Node(state, parent.Path.Add(children[i]), expected, score);
		}
		// One thread is a plain loop: Parallel.For would still hand its work to a pool thread and wait.
		if (options.Threads == 1)
			for (var i = 0; i < children.Count; i++)
				RollOut(i);
		else
			Parallel.For(
				0,
				children.Count,
				new ParallelOptions
				{
					MaxDegreeOfParallelism = options.Threads is > 1 and var n ? n : -1,
				},
				RollOut
			);
		foreach (var node in nodes) // after the parallel loop: one thread writes the reading
		{
			var first = node.Path[0];
			if (atRoot)
				firstMoveOrder.Add(first);
			firstMoves[first] = firstMoves.TryGetValue(first, out var best)
				? Math.Max(best, node.Score)
				: node.Score;
		}
		return [.. nodes];
	}

	/// <summary>
	/// From the state after one of our decisions to our next one this turn: forced passes skipped, the
	/// opponent answered by the model, and our own priority with OUR object on top of the stack passed.
	/// A leaf once the turn ends, the game ends, or nothing of ours is left to decide — and on the
	/// opponent's turn at any priority window of ours (a choice, like the next blocker, still expands).
	/// </summary>
	(GameState State, bool Leaf) Advance(GameState s, int rootTurn)
	{
		for (var i = 0; i < 1_000; i++)
		{
			s = s.AutoPass();
			if (s.IsOver() || s.GetGame().TurnNumber != rootTurn)
				return (s, true);
			var who = s.DecidingPlayerId();
			if (who != PlayerId)
				s = Rollout.OpponentStep(s, who, RolloutSettings);
			else if (!s.IsWaitingForChoice && Stops.TopOfStackControllerId(s) == PlayerId)
				s = s.Act(new PassPriorityAction { PlayerId = who });
			else
				return (s, !s.IsWaitingForChoice && s.GetGame().ActivePlayerId != PlayerId);
		}
		return (s, true);
	}

	/// <summary>Our options here: each move, or each single answer to a choice. A choice of several picks gets GreedyAi's one answer.</summary>
	List<Decision> Children(GameState s)
	{
		if (s.GetPendingChoice() is not { } choice)
			return [.. s.DistinctMoves(s.LegalActions()).Select(a => new Move(a, s.MoveKey(a)))];
		var key = ChoiceKey(choice);
		if (choice.MaxChoices != 1)
			return [new Pick([.. greedy.Answer(s, choice)], key)];
		var picks = choice.Options.Select(o => (Decision)new Pick([o.Id], key)).ToList();
		if (choice.MinChoices == 0)
			picks.Insert(0, new Pick([], key));
		return picks;
	}

	/// <summary>Which choice this is, so a planned answer is only replayed into the same question.</summary>
	static string ChoiceKey(ChoiceAction choice) =>
		$"{choice.GetType().Name}|{choice switch
		{
			DeclareAttackerChoice a => a.CreatureId,
			DeclareBlockerChoice b => b.CreatureId,
			_ => 0,
		}}|{choice.Prompt}|{string.Join(",", choice.Options.Select(o => o.Id))}";

	static GameState Apply(GameState s, Decision d) =>
		d switch
		{
			Move m => s.Act(m.Action),
			Pick p => s.Choose(p.Ids),
			_ => throw new InvalidOperationException(d.GetType().Name),
		};

	/// <summary>
	/// At most <paramref name="cap"/> candidates, ranked by the cheap score (the move settled, then
	/// evaluated); passing is always kept. Below the cap the input comes back untouched. Not refusing a
	/// play: the next decision searches afresh and offers it again.
	/// </summary>
	List<Decision> Narrow(GameState s, List<Decision> children, int cap)
	{
		if (children.Count <= cap)
			return children;
		var keep = children
			.Select(
				(c, i) =>
					(
						i,
						Score: IsPass(c)
							? double.MaxValue
							: Evaluator.Evaluate(
								Lookahead.Settle(Apply(s, c)),
								PlayerId,
								RolloutSettings.Held
							)
					)
			)
			.OrderByDescending(x => x.Score)
			.ThenBy(x => x.i)
			.Take(cap)
			.Select(x => x.i)
			.ToHashSet();
		return [.. children.Where((_, i) => keep.Contains(i))];
	}

	List<Node> Prune(List<Node> nodes) =>
		[
			.. nodes
				.Select((n, i) => (n, i))
				.OrderByDescending(x => x.n.Score)
				.ThenBy(x => x.i)
				.Take(options.BeamWidth)
				.Select(x => x.n),
		];

	/// <summary>The FASTEST win found, if any: a sooner win's rollout decays less, so it scores higher.</summary>
	static Node? Winner(List<Node> nodes) =>
		nodes.Where(n => n.Score >= WinThreshold).MaxBy(n => n.Score);

	/// <summary>The best line; one starting with a pass must beat the best line that acts by <see cref="Settings.PassBias"/>.</summary>
	Node Best(List<Node> nodes)
	{
		var best = nodes.MaxBy(n => n.Score)!;
		if (!IsPass(best.Path[0]))
			return best;
		var acting = nodes.Where(n => !IsPass(n.Path[0])).MaxBy(n => n.Score);
		return acting is not null && best.Score <= acting.Score + options.PassBias ? acting : best;
	}

	static bool IsPass(Decision d) => d is Move { Action: PassPriorityAction };

	Decision Commit(Node line, int rootTurn)
	{
		if (options.CommitPlans)
			(plan, planExpected, planTurn) = (
				line.Path.RemoveAt(0),
				line.Expected.RemoveAt(0),
				rootTurn
			);
		return line.Path[0];
	}

	/// <summary>
	/// The plan's next decision, if the game is still where the plan expected: the same turn, no
	/// opponent's object on the stack (the plan's opponent never casts — a spell it didn't foresee, which
	/// the evaluation can't see until it resolves, is exactly when to think again), our evaluation within
	/// the threshold, and the same move (by MoveKey) or the same question still open. Anything else drops
	/// the plan, and the caller searches.
	/// </summary>
	Decision? FromPlan(GameState s)
	{
		if (plan.IsEmpty)
			return null;
		var (next, expected) = (plan[0], planExpected[0]);
		(plan, planExpected) = (plan.RemoveAt(0), planExpected.RemoveAt(0));
		var top = Stops.TopOfStackControllerId(s);
		var found =
			s.GetGame().TurnNumber == planTurn
			&& (top == 0 || top == PlayerId)
			&& Math.Abs(Evaluator.Evaluate(s, PlayerId, RolloutSettings.Held) - expected)
				<= options.DivergenceThreshold
				? Refind(s, next)
				: null;
		if (found is null)
			(plan, planExpected) = ([], []);
		return found;
	}

	static Decision? Refind(GameState s, Decision planned)
	{
		if (s.GetPendingChoice() is { } choice)
			return planned is Pick p && Equals(ChoiceKey(choice), p.Key) ? p : null;
		return
			planned is Move
			&& s.DistinctMoves(s.LegalActions())
				.FirstOrDefault(a => Equals(s.MoveKey(a), planned.Key))
				is { } move
			? new Move(move, planned.Key)
			: null;
	}
}
