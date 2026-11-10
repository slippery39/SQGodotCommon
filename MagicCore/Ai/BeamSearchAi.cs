using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Effects;

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
	/// <param name="Library">
	/// What the search may know of the libraries — <see cref="Hidden.Library"/>. Honest by default
	/// (Shayne, AI-3.9): only what a player could know, at a measured 5pp or more against the real order
	/// (AI-6.5), which stays available for simulations that want the stronger search.
	/// </param>
	/// <param name="Hand">What it may know of the opponent's hand — <see cref="Hidden.Hand"/> (story AI-3.4).</param>
	/// <param name="Without">Evaluator terms switched off (AI-5) — to measure the search without each.</param>
	/// <param name="WinNowOnly">
	/// Only a win inside the search's own turn stops it early (AI-17.3); a later win keeps it searching to
	/// its depth, and the end takes the best line found — the fastest win. Before, any win stopped it: at
	/// Shayne's turn 21 both root lines ("Tether Technician attacks", "doesn't") won by turn 23, so it
	/// stopped after two rollouts and the tie stayed home — one level deeper, both attacking won that turn.
	/// Off is beam+anywin.
	/// </param>
	/// <param name="WholeAttacks">
	/// The search chooses an attack as ONE decision at the declaration's first question — the candidates
	/// of <see cref="WholeAttacks"/>, each rolled out — not a creature at a time (AI-17.3, Shayne). The
	/// look-ahead chooses its attacks whole too since AI-17.6 (Rollout.Attacks.AsOne). Off is beam+eachalone.
	/// </param>
	/// <param name="BranchPicks">
	/// A choice of several picks — surveil's "any number of them", a discard of two, two modes — is branched
	/// on every combination, each rolled out (at most <see cref="MaxPicks"/>; more falls back to GreedyAi's
	/// one answer), as MtgSimulator's ResolveChoice did (AI-17.6). Before, it got that one answer, which for
	/// surveil was the minimum: eight lands in play and two more on top, it binned nothing (Shayne: "surveil
	/// excess lands that are not needed"). The tree only: the rollout's own answers stay cheap. Off is
	/// beam+onepick.
	/// </param>
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
		Hidden.Library Library = Hidden.Library.ShuffleUnknown,
		int? Threads = null,
		Hidden.Hand Hand = Hidden.Hand.Real,
		Evaluator.Terms Without = Evaluator.Terms.None,
		bool WinNowOnly = true,
		bool WholeAttacks = true,
		bool BranchPicks = true
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

	/// <summary>A whole attack, chosen at the declaration's first question (<see cref="WholeAttacks"/>, AI-17.3).</summary>
	sealed record Declare(WholeAttacks.Plan Plan, int FirstAnswer, object Key) : Decision(Key);

	// The whole attack being declared, and its turn: the rest of the declaration's questions it decides
	// for are answered from it.
	(WholeAttacks.Plan Plan, int Turn)? declaring;

	/// <summary>A line: the state after its last decision, the decisions, its rollout score, and our
	/// evaluation just before each decision — what the plan expects to see when it replays them.</summary>
	sealed record Node(
		GameState State,
		ImmutableList<Decision> Path,
		ImmutableList<double> Expected,
		double Score,
		double Bonus = 0
	);

	ImmutableList<Decision> plan = [];
	ImmutableList<double> planExpected = [];
	int planTurn;
	int rollouts;

	long totalRollouts;

	// The last search's reading of each first move, in the order the root offered them (AI-6.4b).
	readonly Dictionary<Decision, double> firstMoves = new(ReferenceEqualityComparer.Instance);

	// …and the line that reading came from, in the world the search sampled (Explain, AI-17).
	readonly Dictionary<Decision, Node> bestLines = new(ReferenceEqualityComparer.Instance);
	GameState? sampled;
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
					// a whole attack reads as its first answer: candidates may share it (readers group by token)
					Declare a => (
						(GameAction?)null,
						(IReadOnlyList<int>)[a.FirstAnswer],
						firstMoves[d]
					),
					_ => throw new InvalidOperationException(d.GetType().Name),
				}
			),
		];

	/// <summary>
	/// The last decision's first moves, best first, named as <see cref="Explain"/> names them (a whole attack
	/// as one line) — the table's F6 inspector (story AI-14.4). <paramref name="s"/> is the position it decided
	/// in. Empty when it didn't search.
	/// </summary>
	public IReadOnlyList<(string Move, double Score)> Ranked(GameState s) =>
		[
			.. firstMoveOrder
				.OrderByDescending(f => firstMoves[f])
				.Select(f => (Describe(s, f), firstMoves[f])),
		];

	/// <summary>
	/// What the last search imagined (AI-17): for each first move, best first, the line its reading came
	/// from — our decisions in the tree, the simulated opponent's answers between them, then the rollout
	/// to its horizon — written as the replay writes a game (<see cref="DecisionReport"/>), in the world
	/// the search sampled, with its evaluator terms. Empty but for a note when it didn't search.
	/// </summary>
	public string Explain() =>
		sampled is null
			? "\n(no search: one option, a plan replayed, or a forced pass)\n"
			: Evaluator.Without(
				options.Without,
				() => Evaluator.WithRootImpulse(sampled, () => ExplainLines(sampled))
			);

	string ExplainLines(GameState s)
	{
		var md = new StringBuilder();
		var names = new[] { PlayerId, s.OpponentOf(PlayerId) }.ToDictionary(
			p => p,
			p => s.GetPlayer(p).Name
		);
		var (rootTurn, horizon) = (s.GetGame().TurnNumber, Rollout.Horizon(s, RolloutSettings));
		foreach (var first in firstMoveOrder.OrderByDescending(f => firstMoves[f]))
		{
			var line = bestLines[first];
			md.Append($"\n## {Describe(s, first)}: {firstMoves[first]:0.00}\n");
			var replay = new DecisionReport.Replay(md, names, s);
			var at = s;
			var end = at;
			void Told(GameState before, int who, AiDecision d)
			{
				replay.Move(before, who, d, [], out _);
				end = d.State;
			}
			for (var i = 0; i < line.Path.Count; i++)
			{
				if (i > 0)
					at = Rollout.Traced(Told, () => Advance(at, rootTurn).State);
				if (line.Path[i] is Declare whole)
				{
					end = at = WholeAttacks.Declare(
						at,
						PlayerId,
						whole.Plan,
						(x, ids) =>
						{
							var d = AiDecision.Answering(x, ids);
							replay.Move(x, PlayerId, d, [], out _);
							return d.State;
						}
					);
					continue;
				}
				var mine = line.Path[i] switch
				{
					Move m => AiDecision.Acting(at, m.Action),
					Pick p => AiDecision.Answering(at, p.Ids),
					_ => throw new InvalidOperationException(line.Path[i].GetType().Name),
				};
				replay.Move(at, PlayerId, mine, [], out _);
				end = at = mine.State;
			}
			md.Append("- *(the rollout from here)*\n");
			var score = Rollout.Traced(
				Told,
				() => Rollout.Score(at, PlayerId, horizon, RolloutSettings)
			);
			replay.End(end);
			var result = end.IsOver() ? (end.WinnerId() == PlayerId ? " — won" : " — lost") : "";
			md.Append($"\n**Rollout {score:0.00}**{result}\n");
			// where the line ended, term by term (AI-17.10): "scores 26.9" is not a diagnosis
			md.Append(
				$"\nEnds at: {string.Join(", ", Evaluator.Explain(end, PlayerId, RolloutSettings.Held).Where(t => Math.Abs(t.Value) >= 0.005).Select(t => $"{t.Term} {t.Value:+0.00;-0.00}"))}\n"
			);
		}
		return md.ToString();
	}

	static string Describe(GameState s, Decision d) =>
		d switch
		{
			Move m => MagicCore.Describe.Action(s, m.Action),
			Pick p => MagicCore.Describe.Answer(s, s.GetPendingChoice()!, p.Ids),
			Declare a => WholeAttacks.Describe(s, a.Plan),
			_ => d.ToString()!,
		};

	/// <summary>Rollouts every decision so far has played — beside MagicRules.CountingSteps, the search's cost (story AI-6).</summary>
	public long Rollouts => totalRollouts;

	Rollout.Settings RolloutSettings => options.RolloutSettings!;

	public AiDecision Decide(GameState s)
	{
		rollouts = 0; // a plan replay or a pass doesn't search, and so doesn't reset it
		firstMoves.Clear();
		firstMoveOrder.Clear();
		bestLines.Clear();
		sampled = null;
		var decided = Evaluator.Without(
			options.Without,
			() => Evaluator.WithRootImpulse(s, () => DecideHere(s))
		);
		totalRollouts += rollouts;
		return decided;
	}

	AiDecision DecideHere(GameState s)
	{
		if (s.GetGame().Step == Step.PreGame)
			return greedy.Decide(s); // mulligans: GreedyAi's rule
		if (!s.IsWaitingForChoice && Stops.TopOfStackControllerId(s) == PlayerId) // as the tree does: let our own object resolve
			return AiDecision.Acting(s, new PassPriorityAction { PlayerId = PlayerId });
		if (
			declaring is var (attack, turn)
			&& turn == s.GetGame().TurnNumber
			&& s.GetPendingChoice() is DeclareAttackerChoice ask
			&& ask.PlayerId == PlayerId
			&& !WholeAttacks.IsFirst(s, ask)
			&& attack.Creatures.Contains(ask.CreatureId)
		)
			return AiDecision.Answering(s, [WholeAttacks.Answer(s, PlayerId, attack, ask)]);
		declaring = null;
		var decision = FromPlan(s) ?? Search(s);
		if (decision is Declare d)
		{
			declaring = (d.Plan, s.GetGame().TurnNumber);
			var first = (DeclareAttackerChoice)s.GetPendingChoice()!;
			return AiDecision.Answering(s, [WholeAttacks.Answer(s, PlayerId, d.Plan, first)]);
		}
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
		s = sampled = Hidden.Sample(s, PlayerId, options.Library, options.Hand);
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
			// a surveil newly waiting on the stack, which the rollout's one sampled card cannot price (AI-17.11)
			var bonus =
				parent.Bonus
				+ Math.Max(
					0,
					Evaluator.PendingSurveil(state, PlayerId, RolloutSettings.Held)
						- Evaluator.PendingSurveil(at, PlayerId, RolloutSettings.Held)
				);
			var score = Rollout.Score(state, PlayerId, horizon, RolloutSettings) + bonus;
			Interlocked.Increment(ref rollouts);
			nodes[i] = new Node(state, parent.Path.Add(children[i]), expected, score, bonus);
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
			if (!firstMoves.TryGetValue(first, out var best) || node.Score > best)
				(firstMoves[first], bestLines[first]) = (node.Score, node);
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

	/// <summary>Our options here: each move, or each answer to a choice — a choice of several picks every combination (<see cref="Settings.BranchPicks"/>), or past <see cref="MaxPicks"/> GreedyAi's one answer.</summary>
	List<Decision> Children(GameState s)
	{
		if (s.GetPendingChoice() is not { } choice)
		{
			var moves = s.AiMoves(s.SearchMoves()).ToList();
			// a land is never worth passing on (Terms.LandDrop): the land first, then whatever else
			if (!Evaluator.IsOff(Evaluator.Terms.LandDrop) && moves.Any(a => a is PlayLandAction))
				moves.RemoveAll(a => a is PassPriorityAction);
			// a Spacecraft that already has its threshold gains nothing from another counter (Terms.StationCap)
			if (!Evaluator.IsOff(Evaluator.Terms.StationCap))
				moves.RemoveAll(a => a is ActivateAbilityAction act && OverCharges(s, act));
			// burn at their face waits for a kill within two turns (Terms.SaveBurn)
			if (
				!Evaluator.IsOff(Evaluator.Terms.SaveBurn)
				&& moves.Where(a => FaceBurn(s, a)).ToList() is { Count: > 0 } burns
				&& !KillsWithinTwoTurns(s, PlayerId)
			)
				moves.RemoveAll(burns.Contains);
			return [.. moves.Select(a => new Move(a, s.MoveKey(a)))];
		}
		var key = ChoiceKey(choice);
		// from any question no whole attack decides for (a fresh AI mid-declaration — the suite, --compare —
		// plays as the table does): the questions from here on
		if (choice is DeclareAttackerChoice attacker && options.WholeAttacks)
			return
			[
				.. WholeAttacks
					.Candidates(s, PlayerId, RolloutSettings.Held)
					.Select(plan =>
						(Decision)
							new Declare(plan, WholeAttacks.Answer(s, PlayerId, plan, attacker), key)
					),
			];
		if (
			choice
			is ChooseNumberChoice { NamesAManaValue: true }
				or ChooseKeywordChoice { NamesACard: true }
		)
			return [new Pick([.. greedy.Answer(s, choice)], key)]; // Nyota: a rule, not 11 rollouts (Shayne)
		// a surveil's lands by rule (LandUnlock.Surveil), its spells branched as any pick
		if (
			choice is ChooseFromLibraryTopChoice top
			&& !Evaluator.IsOff(Evaluator.Terms.SurveilLands)
		)
		{
			var (decided, bin) = LandUnlock.Surveil(s, top);
			// the spells too, when we have prices: below the unknown-card average they are binned, else kept
			if (!Evaluator.IsOff(Evaluator.Terms.SurveilSpells) && RolloutSettings.Held is { } held)
				foreach (var o in choice.Options.Where(o => !decided.Contains(o.Id)))
				{
					decided.Add(o.Id); // decided, not branched
					if (held.Of(s.GetCard(o.Id).Name) < held.Unknown)
						bin.Add(o.Id);
				}
			return
			[
				.. (
					options.BranchPicks && Picks(s, choice) is { } all
						? all.Select(p => p.AsEnumerable())
						: [greedy.Answer(s, choice)]
				)
					.Select(p =>
						p.Where(id => !decided.Contains(id)).Concat(bin).Order().ToImmutableList()
					)
					.DistinctBy(p => string.Join("|", p))
					.Select(p => (Decision)new Pick(p, key)),
			];
		}
		if (choice.MaxChoices != 1)
			return options.BranchPicks && Picks(s, choice) is { } every
				? [.. every.Select(p => (Decision)new Pick(p, key))]
				: [new Pick([.. greedy.Answer(s, choice)], key)];
		var picks = choice.Options.Select(o => (Decision)new Pick([o.Id], key)).ToList();
		if (choice.MinChoices == 0)
			picks.Insert(0, new Pick([], key));
		return picks;
	}

	/// <summary>
	/// A burn spell at their face: an instant or sorcery that only deals damage, aimed at the opponent - or a noncreature
	/// permanent whose enters-trigger only deals damage (Ajani's Anguish) while they have no creature or planeswalker for
	/// it to hit. A creature is a body whatever its trigger hits. The lint's face-burn class, as a move (<see cref="BlunderLint"/>).
	/// </summary>
	public static bool FaceBurn(GameState s, GameAction a)
	{
		if (a is not CastSpellAction cast || !s.HasObject(cast.CardId))
			return false;
		var card = s.GetCard(cast.CardId);
		var them = s.OpponentOf(cast.PlayerId);
		if (card.Is(Cards.CardType.Instant) || card.Is(Cards.CardType.Sorcery))
			return BurnOnly(card.GetComponent<SpellEffectsComponent>()?.Effects ?? [])
				&& cast.Targets.Contains(them);
		return EntersBurn(s, card)
			&& !s.CardsIn(them, ZoneType.Battlefield)
				.Any(c => c.Is(Cards.CardType.Creature) || c.Is(Cards.CardType.Planeswalker));
	}

	static bool BurnOnly(IReadOnlyCollection<GameAction> effects) =>
		effects.Count > 0 && effects.All(e => e is DealDamage);

	/// <summary>A noncreature permanent whose targeted enters-triggers only deal damage (Ajani's Anguish) - shared with the lint.</summary>
	internal static bool EntersBurn(GameState s, Cards.Card card) =>
		!card.Is(Cards.CardType.Creature)
		&& s.AbilitiesOf<TriggeredAbilityComponent>(card)
			.Where(t => t.On.On == EventKind.Entered && t.Targets.Count > 0)
			.ToList()
			is { Count: > 0 } triggers
		&& triggers.All(t => BurnOnly(t.Effects));

	/// <summary>A burn card in hand that can reach a player: its spell, or its enters-burn, can target one.</summary>
	static bool FaceCapable(GameState s, Cards.Card c) =>
		(c.Is(Cards.CardType.Instant) || c.Is(Cards.CardType.Sorcery))
			? c.GetComponent<SpellEffectsComponent>() is { } spell
				&& BurnOnly(spell.Effects)
				&& spell.Targets.Any(t => t.CanTargetPlayers)
			: EntersBurn(s, c)
				&& s.AbilitiesOf<TriggeredAbilityComponent>(c)
					.Any(t =>
						t.On.On == EventKind.Entered && t.Targets.Any(x => x.CanTargetPlayers)
					);

	/// <summary>
	/// Shayne's burn rule (AI-17.6, 2026-11-09): "save it until you can actually kill the opponent" - this turn or the
	/// next. Their life against the damage reachable in the two: the burn in our hand cast cheapest first with each
	/// turn's mana (now: untapped sources and a land still to play; next: every source and another land), X taking
	/// what is left (X spells last), and what our attackers get past their blockers each turn (<see cref="Evaluator.Through"/>;
	/// this turn's only while we can still attack).
	/// ponytail: mana counted by source, colours ignored; this turn's attack counts every creature, sick or not, and next
	/// turn's meets their blockers as they stand now; a count's multipliers (Times) unread; lifegain they may have
	/// (Shayne's lifelinker) not subtracted - the search then weighs killing it; their answers to come unseen.
	/// </summary>
	static bool KillsWithinTwoTurns(GameState s, int me)
	{
		var them = s.OpponentOf(me);
		var game = s.GetGame();
		var ours = game.ActivePlayerId == me;
		var sources = s.CardsIn(me, ZoneType.Battlefield)
			.Where(c => ManaAbilities.ColorsOf(s, c).Any())
			.ToList();
		var hand = s.CardsIn(me, ZoneType.Hand).ToList();
		var lands = hand.Count(c => c.Is(Cards.CardType.Land));
		var landNow =
			ours
			&& Priority.AtSorcerySpeed(s, me)
			&& s.GetPlayer(me).ThisTurn.LandsPlayed < s.LandDrops(me)
			&& lands > 0;
		var burns = hand.Where(c => FaceCapable(s, c))
			.OrderBy(c => c.ManaCost.HasX)
			.ThenBy(c => c.ManaCost.ManaValue)
			.ToList();
		var damage = 0;
		foreach (
			var mana in new[]
			{
				sources.Count(c => !c.IsTapped) + (landNow ? 1 : 0),
				sources.Count + (landNow ? 1 : 0) + (lands > (landNow ? 1 : 0) ? 1 : 0),
			}
		)
		{
			var left = mana;
			foreach (var burn in burns.ToList())
				if (burn.ManaCost.ManaValue <= left)
				{
					left -= burn.ManaCost.ManaValue;
					var x = burn.ManaCost.HasX ? left : 0;
					left -= x;
					damage += Damage(s, burn, x);
					burns.Remove(burn);
				}
		}
		var combat =
			(
				ours && game.Step <= Step.DeclareAttackers && !Turns.Combat.Attackers(s).Any()
					? Evaluator.Through(s, me, them)
					: 0
			) + Evaluator.Through(s, me, them);
		return s.GetPlayer(them).Life <= damage + combat;
	}

	/// <summary>A burn card's damage at X: its fixed amounts and X; any other count reads 0 (held, to be safe).</summary>
	static int Damage(GameState s, Cards.Card card, int x) =>
		(card.GetComponent<SpellEffectsComponent>()?.Effects ?? [])
			.Concat(s.AbilitiesOf<TriggeredAbilityComponent>(card).SelectMany(t => t.Effects))
			.OfType<DealDamage>()
			.Sum(d =>
				d.Amount.Kind switch
				{
					CountOf.Fixed => d.Amount.Fixed,
					CountOf.X => x,
					_ => 0,
				}
			);

	/// <summary>Station once the Spacecraft's charge is at its threshold: the ability that puts charge on it, tapping a creature.</summary>
	static bool OverCharges(GameState s, ActivateAbilityAction act)
	{
		var card = s.GetCard(act.CardId);
		var abilities = s.ActivatedAbilitiesOf(card).ToList();
		return card.GetComponent<StationComponent>() is { } station
			&& card.ChargeCounters >= station.CreatureAt
			&& act.AbilityIndex < abilities.Count
			&& abilities[act.AbilityIndex]
				.Effects.OfType<PutCounters>()
				.Any(p => p.Kind == CounterKinds.Charge);
	}

	/// <summary>The most combinations a choice of several picks is branched on (<see cref="Settings.BranchPicks"/>).</summary>
	public const int MaxPicks = 32;

	/// <summary>
	/// Every legal answer to a choice of several picks — each size from its least (no more than there are
	/// options, as the engine allows) to its most — one per set of interchangeable cards (two Forests on top:
	/// "bin one" is one answer, by <see cref="MagicRules.Identity"/>); null past <see cref="MaxPicks"/> or
	/// with none.
	/// </summary>
	static List<ImmutableList<int>>? Picks(GameState s, ChoiceAction choice)
	{
		var ids = choice.Options.Select(o => o.Id).ToList();
		var picks = new List<ImmutableList<int>>();
		var seen = new HashSet<string>();
		var objects = MagicRules.OptionsAreObjects(choice); // modes and the like are numbers, not cards
		string Same(int id) =>
			objects && s.HasObject(id) && s.GetObject(id) is Cards.Card
				? MagicRules.Identity(s, id)
				: $"{id}";
		for (
			var k = Math.Min(choice.MinChoices, ids.Count);
			k <= Math.Min(choice.MaxChoices, ids.Count);
			k++
		)
			foreach (var pick in GreedyAi.Combinations(ids, k))
			{
				if (!seen.Add(string.Join("|", pick.Select(Same).Order())))
					continue;
				picks.Add([.. pick]);
				if (picks.Count > MaxPicks)
					return null;
			}
		return picks.Count == 0 ? null : picks;
	}

	/// <summary>Which choice this is, so a planned answer is only replayed into the same question.</summary>
	static string ChoiceKey(ChoiceAction choice) =>
		$"{choice.GetType().Name}|{choice switch
		{
			DeclareAttackerChoice a => a.CreatureId,
			DeclareBlockerChoice b => b.CreatureId,
			_ => 0,
		}}|{choice.Prompt}|{string.Join(",", choice.Options.Select(o => o.Id))}";

	GameState Apply(GameState s, Decision d) =>
		d switch
		{
			Move m => s.Act(m.Action),
			Pick p => s.Choose(p.Ids),
			Declare a => WholeAttacks.Declare(s, PlayerId, a.Plan),
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
						Score: IsPass(c) || c is Declare { Plan.Targets.Count: 0 } // "no attack", as passing
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

	/// <summary>
	/// The FASTEST win found that ends the search early, if any: a sooner win's rollout decays less, so it
	/// scores higher. Under <see cref="Settings.WinNowOnly"/> only one inside this turn — every later win
	/// scores at most a turn's discount plus its tie-break (<see cref="Rollout.TurnEndCap"/>).
	/// </summary>
	Node? Winner(List<Node> nodes) =>
		nodes
			.Where(n =>
				options.WinNowOnly
					? n.Score
						> Evaluator.WinScore * RolloutSettings.TerminalDiscount
							+ Rollout.TurnEndCap(RolloutSettings)
							+ Evaluator.EndBoardCap
					: n.Score >= WinThreshold
			)
			.MaxBy(n => n.Score);

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
			return planned is Pick or Declare && Equals(ChoiceKey(choice), planned.Key)
				? planned
				: null;
		return
			planned is Move
			&& s.DistinctMoves(s.LegalActions())
				.FirstOrDefault(a => Equals(s.MoveKey(a), planned.Key))
				is { } move
			? new Move(move, planned.Key)
			: null;
	}
}
