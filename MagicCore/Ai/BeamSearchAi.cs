using System.Collections.Immutable;
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
	public sealed record Settings(
		int Depth = 3,
		int BeamWidth = 4,
		int MaxBranching = 16,
		int ExpandBranching = 5,
		int RolloutBudget = 200,
		double PassBias = 0.01,
		bool CommitPlans = true,
		double DivergenceThreshold = 5.0,
		Rollout.Settings? RolloutSettings = null
	)
	{
		/// <summary>The search with held cards priced by their measured value (story AI-1).</summary>
		public const double HeldFraction = 0.5;

		public Settings WithHeld(double fraction = HeldFraction) =>
			this with
			{
				RolloutSettings = (RolloutSettings ?? Rollout.Default) with
				{
					Held = HeldValues.Fra(fraction),
				},
			};
	}

	/// <summary>A rollout at or above this has found a win; a decayed win is still far above it.</summary>
	public const double WinThreshold = 2_500;

	readonly Settings options = settings ?? new();

	// Mulligans and the choices of several picks: GreedyAi, pricing held cards as the search does.
	readonly GreedyAi greedy = new(playerId, (settings?.RolloutSettings ?? Rollout.Default).Held);

	public int PlayerId { get; } = playerId;

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

	Rollout.Settings RolloutSettings => options.RolloutSettings ?? Rollout.Default;

	public (GameState State, string What) Decide(GameState s)
	{
		if (s.GetGame().Step == Step.PreGame)
			return greedy.Decide(s); // mulligans: GreedyAi's rule
		if (!s.IsWaitingForChoice && Stops.TopOfStackControllerId(s) == PlayerId)
		{
			var pass = new PassPriorityAction { PlayerId = PlayerId };
			return (s.Act(pass), Describe.Action(s, pass)); // as the tree does: let our own object resolve
		}
		var decision = FromPlan(s) ?? Search(s);
		return decision switch
		{
			Move m => (s.Act(m.Action), Describe.Action(s, m.Action)),
			Pick p => (s.Choose(p.Ids), Describe.Answer(s, s.GetPendingChoice()!, p.Ids)),
			_ => throw new InvalidOperationException(decision.GetType().Name),
		};
	}

	Decision Search(GameState s)
	{
		(plan, planExpected, rollouts) = ([], [], 0);
		var rootTurn = s.GetGame().TurnNumber;
		var horizon = Rollout.Horizon(s, RolloutSettings);
		var children = Narrow(s, Children(s), options.MaxBranching);
		if (children.Count <= 1)
			return children.FirstOrDefault()
				?? new Pick([.. greedy.Answer(s, s.GetPendingChoice()!)], "");

		var root = new Node(s, [], [], 0);
		var beam = Expand(root, s, children, horizon);
		if (Winner(beam) is { } won)
			return Commit(won, rootTurn);

		for (var depth = 1; depth < options.Depth && rollouts < options.RolloutBudget; depth++)
		{
			var next = new List<Node>();
			var grew = false;
			foreach (var node in Prune(beam))
			{
				var (at, leaf) = Advance(node.State, rootTurn);
				if (leaf)
				{
					next.Add(node); // the turn is over on this line: its score already covers the rest
					continue;
				}
				next.AddRange(
					Expand(node, at, Narrow(at, Children(at), options.ExpandBranching), horizon)
				);
				grew = true;
			}
			beam = next;
			if (Winner(beam) is { } win)
				return Commit(win, rootTurn);
			if (!grew)
				break;
		}
		return Commit(Best(beam), rootTurn);
	}

	/// <summary>Each child of a line, rolled out in parallel into its own slot — the order threads finish in never reaches a decision.</summary>
	List<Node> Expand(Node parent, GameState at, IReadOnlyList<Decision> children, int horizon)
	{
		var expected = parent.Expected.Add(Evaluator.Evaluate(at, PlayerId, RolloutSettings.Held));
		var nodes = new Node[children.Count];
		Parallel.For(
			0,
			children.Count,
			i =>
			{
				var state = Apply(at, children[i]);
				var score = Rollout.Score(state, PlayerId, horizon, RolloutSettings);
				Interlocked.Increment(ref rollouts);
				nodes[i] = new Node(state, parent.Path.Add(children[i]), expected, score);
			}
		);
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
