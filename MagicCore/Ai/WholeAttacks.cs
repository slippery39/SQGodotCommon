using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;

namespace MagicCore.Ai;

/// <summary>
/// An attack chosen as ONE decision (story AI-17.3, Shayne: "we aren't choosing to attack with creatures
/// individually in isolation, but rather we are launching an attack as one decision"). The engine asks
/// "attack with X?" a creature at a time; here the whole declaration is a creature → target map, a few
/// candidates are offered, each judged against the defender's blocks, and the chosen one answers every
/// question of the declaration — as <c>MagicTableManager.DeclareCombat</c> answers them for a person.
/// </summary>
public static class WholeAttacks
{
	/// <summary>
	/// An attack: each creature's target (its player or a planeswalker) — the <paramref name="Creatures"/>
	/// it decides for, asked from where it was chosen; one of them not in <paramref name="Targets"/> stays home.
	/// </summary>
	public sealed record Plan(
		ImmutableDictionary<int, int> Targets,
		ImmutableHashSet<int> Creatures
	)
	{
		public override string ToString() => $"attack {string.Join(", ", Targets)}";
	}

	/// <summary>
	/// Is this the declaration's first question — nothing answered yet, "don't attack" included (an
	/// answer of None is still an answer: Declarations.Read leaves it out, the context doesn't)?
	/// </summary>
	public static bool IsFirst(GameState s, DeclareAttackerChoice choice) =>
		s.ActionStack.Peek() is PipelineAction declaring
		&& !declaring.PipelineContext.Keys.Any(k => k.StartsWith(DeclareAttackerChoice.KeyPrefix));

	/// <summary>
	/// The candidate attacks (Shayne, 2026-10-29: a few, not every subset) for the questions from this one
	/// on: everyone, no one, each creature alone, everyone but one — each creature at its own best target
	/// (<see cref="Assigned"/>) — and everyone with one creature at another target it could attack. That
	/// last set lets the search's look-ahead judge the targets too: the cheap judgment sees one combat, not
	/// what a walker does next turn (a Jace at 3 that would draw is worth a 2/2's attack — AI-8.1).
	/// "Everyone" first, so a tie with a smaller lethal attack takes the whole one.
	/// </summary>
	/// <param name="otherTargets">The "one creature at another target" set — the search's tree only, whose
	/// rollouts judge it; the look-ahead's own choice (<see cref="Best"/>) skips it, for its cost.</param>
	public static IReadOnlyList<Plan> Candidates(
		GameState s,
		int who,
		HeldValues? held,
		bool otherTargets = true
	)
	{
		var (targets, offered) = Assigned(s, who, held);
		var creatures = targets.Keys.ToList();
		var asked = offered.Keys.ToImmutableHashSet();
		Plan Of(IEnumerable<int> attacking) =>
			new(attacking.ToImmutableDictionary(c => c, c => targets[c]), asked);
		var plans = new List<Plan> { Of(creatures), Of([]) };
		if (creatures.Count > 1)
		{
			plans.AddRange(creatures.Select(c => Of([c])));
			if (creatures.Count > 2)
				plans.AddRange(creatures.Select(c => Of(creatures.Where(x => x != c))));
		}
		foreach (var c in otherTargets ? creatures : [])
		foreach (var other in offered[c].Where(t => t != targets[c]))
			plans.Add(new(Of(creatures).Targets.SetItem(c, other), asked));
		return plans;
	}

	/// <summary>
	/// Each creature's own best target, given the others' (Shayne: "a planeswalker on 4 loyalty. Your 4
	/// power creature might want to attack that one, and your 2 and 3 power creature might want to attack
	/// their face"): the biggest first, each target tried with those before it attacking, the attack
	/// settled — so a second creature doesn't pile onto a walker the first already kills. And every
	/// creature's offered targets, the questions from this one on (with no walker, nothing to weigh).
	/// ponytail: one assignment, made with everyone attacking and used by every candidate; assign per
	/// candidate if a smaller attack is shown aiming badly.
	/// </summary>
	static (
		ImmutableDictionary<int, int> Targets,
		Dictionary<int, IReadOnlyList<int>> Offered
	) Assigned(GameState s, int who, HeldValues? held)
	{
		var options = new Dictionary<int, IReadOnlyList<int>>();
		var at = s;
		while (at.GetPendingChoice() is DeclareAttackerChoice ask && ask.PlayerId == who)
		{
			options[ask.CreatureId] =
			[
				.. ask.Options.Select(o => o.Id).Where(id => id != CombatOption.None),
			];
			at = at.Choose([CombatOption.None]);
		}
		var chosen = ImmutableDictionary<int, int>.Empty;
		if (options.Values.All(o => o.Count <= 1)) // the player alone (no walker): nothing to weigh
			return (
				options
					.Where(kv => kv.Value.Count == 1)
					.ToImmutableDictionary(kv => kv.Key, kv => kv.Value[0]),
				options
			);
		foreach (
			var creature in options
				.Keys.OrderByDescending(c => Turns.Combat.Assigns(s, s.GetCard(c)))
				.ThenBy(c => c)
		)
			chosen = chosen.Add(
				creature,
				options[creature]
					.Select(t =>
						(
							Target: t,
							Score: Evaluator.Evaluate(
								Lookahead.Settle(
									Declare(s, who, new(chosen.Add(creature, t), [.. options.Keys]))
								),
								who,
								held
							)
						)
					)
					.OrderByDescending(x => x.Score)
					.ThenBy(x => x.Target) // a tie: the player (the opponent's id is the lowest)
					.Select(x => x.Target)
					.FirstOrDefault(CombatOption.None)
			);
		return (chosen.Where(kv => kv.Value != CombatOption.None).ToImmutableDictionary(), options);
	}

	/// <summary>
	/// Every question of the declaration answered from <paramref name="plan"/>; a creature whose target is
	/// no longer offered (a walker one attacker already took, under Tomik) attacks the player.
	/// </summary>
	public static GameState Declare(GameState s, int who, Plan plan) =>
		Declare(s, who, plan, (at, ids) => at.Choose(ids));

	/// <summary><see cref="Declare(GameState,int,Plan)"/>, each answer made by <paramref name="answer"/> — a traced rollout's.</summary>
	public static GameState Declare(
		GameState s,
		int who,
		Plan plan,
		Func<GameState, int[], GameState> answer
	)
	{
		while (s.GetPendingChoice() is DeclareAttackerChoice ask && ask.PlayerId == who)
			s = answer(s, [Answer(s, who, plan, ask)]);
		return s;
	}

	/// <summary>One question of the declaration answered from <paramref name="plan"/> — the one rule <see cref="Declare(GameState,int,Plan)"/> and the search share.</summary>
	public static int Answer(GameState s, int who, Plan plan, DeclareAttackerChoice ask)
	{
		var offered = ask.Options.Select(o => o.Id).ToList();
		var target = plan.Targets.GetValueOrDefault(ask.CreatureId, CombatOption.None);
		return offered.Contains(target) ? target
			: target == CombatOption.None ? CombatOption.None
			: offered.Contains(s.OpponentOf(who)) ? s.OpponentOf(who)
			: CombatOption.None;
	}

	/// <summary>"Attack: Tether Technician → You; Chandra's Emberling → Jace" — for the search's explanations.</summary>
	public static string Describe(GameState s, Plan plan) =>
		plan.Targets.Count == 0
			? "No attack"
			: "Attack: "
				+ string.Join(
					"; ",
					plan.Targets.Select(kv =>
						$"{s.GetCard(kv.Key).Name} → {(kv.Value == s.Player1Id() || kv.Value == s.Player2Id() ? s.GetPlayer(kv.Value).Name : s.GetCard(kv.Value).Name)}"
					)
				);

	/// <summary>
	/// The attack answered by the defender as the rollout plays it (<see cref="Rollout.OpponentStep"/>), when
	/// <paramref name="defender"/> says so — else left to <see cref="Lookahead.Settle"/>'s weaker one (AI-19.21).
	/// </summary>
	static GameState Blocked(GameState s, int who, Rollout.Settings? defender)
	{
		if (defender is null || Evaluator.IsOff(Evaluator.Terms.TrueDefender))
			return s;
		// Drive the combat to its blocks: an attacker's own trigger asks a choice (Tal Shiar Operative's "choose
		// one"), and a defender with an instant-speed ability holds priority in declare-attackers (Chang) - a
		// probe that stopped at either silently fell back to the weak defender (AI-17.14).
		for (var i = 0; i < 40 && !s.IsOver(); i++)
		{
			if (s.GetPendingChoice() is DeclareAttackerChoice) // a creature the plan left out: not ours to re-pick
				break;
			if (s.GetPendingChoice() is not null)
				s = Rollout.Untraced(() => Rollout.OpponentStep(s, s.DecidingPlayerId(), defender));
			else if (s.GetGame().Step <= Step.DeclareAttackers)
				s = s.Act(new PassPriorityAction { PlayerId = s.DecidingPlayerId() }).AutoPass();
			else
				break;
		}
		return s;
	}

	/// <summary>How many of the look-ahead's best attacks the true defender re-judges (with "no attack" always).</summary>
	public const int Shortlist = 3;

	/// <summary>
	/// The candidate whose attack scores best for <paramref name="who"/>. Every candidate is settled against the
	/// look-ahead's cheap defender; with <paramref name="defender"/> (<see cref="Evaluator.Terms.TrueDefender"/>)
	/// only the best <see cref="Shortlist"/> and "no attack" are judged again against the blocks the rollout will
	/// play (AI-19.21). The cheap defender over-rates attacks, so the ones it over-rates are the ones on the
	/// shortlist to be corrected. Re-judging every plan cost 2.3x a decision (6.8x on a big board, #305); the best 3
	/// cost ~3.1x there. The best 1 is cheaper (1.9x) and flips the Chang plays, but misses the lethal attack Shayne's
	/// Jace token position holds (it flips to Pass), so it stays 3.
	/// ponytail: a plan good only against the real blocks yet low under the cheap ones is missed; widen the list.
	/// </summary>
	public static Plan Best(
		GameState s,
		int who,
		HeldValues? held,
		Rollout.Settings? defender = null
	)
	{
		double Score(Plan plan, bool real) =>
			Evaluator.Evaluate(
				Lookahead.Settle(
					real ? Blocked(Declare(s, who, plan), who, defender) : Declare(s, who, plan)
				),
				who,
				held
			);
		var cheap = Candidates(s, who, held, otherTargets: false)
			.Select((plan, i) => (plan, i, Score: Score(plan, false)))
			.OrderByDescending(x => x.Score)
			.ThenBy(x => x.i)
			.ToList();
		if (defender is null || Evaluator.IsOff(Evaluator.Terms.TrueDefender))
			return cheap[0].plan;
		var stay = cheap.First(x => x.plan.Targets.Count == 0);
		return cheap
			.Take(Shortlist)
			.Append(stay)
			.DistinctBy(x => x.i)
			.Select(x => (x.plan, x.i, Score: Score(x.plan, true)))
			.OrderByDescending(x => x.Score)
			.ThenBy(x => x.i)
			.First()
			.plan;
	}
}
