using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **The bot for `party-sim` — never for play.** Each turn it searches SEQUENCES of plays (a beam:
/// the best few part-turns, each extended by every step, card and Snare), so it finds combos whose
/// first card looks useless alone — Dash then a step, Gust then Rally. A plan is scored by what the
/// ENGINE says ending the turn leaves, AND by the turn after with no plays, so where monsters are
/// left standing matters. Still a heuristic: its win rate is a floor, not the game's.
/// </summary>
public static class PartyBot
{
	/// <summary>How much your health is worth next to a monster's HP — it is the run's other clock.</summary>
	public const double TrainerWeight = 1.5;

	/// <summary>A catch is worth this much HP.</summary>
	public const double CatchValue = 15;

	/// <summary>
	/// **A foe left at catchable HP, with a Snare to throw, is most of a catch** — so the bot stops
	/// hitting a foe it could Snare next turn instead of killing it (the first sim's bot never did).
	/// </summary>
	public const double CatchableValue = CatchValue * 0.6;

	/// <summary>Part-turns kept at each depth, and the most plays in one turn.</summary>
	public const int BeamWidth = 5;

	public const int MaxPlays = 6;

	/// <summary>How much the turn after counts, next to this one.</summary>
	public const double NextTurnWeight = 0.5;

	/// <summary>
	/// **Engine passes the bot has run, all threads** — `party-sim` prints it, so a fast sim can be
	/// checked for actually simulating.
	/// </summary>
	public static long Simulations;

	private record Node(GameState State, ImmutableList<GameAction> Plan, double Score);

	/// <summary>Plays one turn: the best plan it can find, then ends the turn. `log` hears each play.</summary>
	public static GameState PlayTurn(GameState s, Action<string>? log = null)
	{
		foreach (var action in BestPlan(s))
		{
			log?.Invoke($"    plays {Describe(s, action)}");
			s = Do(s, action);
		}
		return s.GetParty().IsOver ? s : Do(s, new EndPartyTurnAction());
	}

	/// <summary>
	/// **The beam search.** Depth by depth: extend every kept part-turn by every legal play, drop
	/// duplicates (stepping A then B = B then A), keep the best <see cref="BeamWidth"/>. The best plan
	/// seen at any depth wins — including playing nothing.
	/// </summary>
	private static ImmutableList<GameAction> BestPlan(GameState s)
	{
		var best = new Node(s, [], Score(s));
		var frontier = new List<Node> { best };

		for (var depth = 0; depth < MaxPlays && frontier.Count > 0; depth++)
		{
			var seen = new HashSet<string>();
			var children = new List<Node>();
			foreach (var node in frontier.Where(n => !n.State.GetParty().IsOver))
			foreach (var action in Candidates(node.State))
			{
				if (!action.ValidateAdd(node.State).IsValid)
					continue;
				var next = Do(node.State, action);
				if (!seen.Add(Signature(next)))
					continue;
				children.Add(new Node(next, node.Plan.Add(action), Score(next)));
			}

			frontier = [.. children.OrderByDescending(c => c.Score).Take(BeamWidth)];
			if (frontier.Count > 0 && frontier[0].Score > best.Score + 0.01)
				best = frontier[0];
		}
		return best.Plan;
	}

	private static GameState Do(GameState s, GameAction action)
	{
		Interlocked.Increment(ref Simulations);
		return s.AddAction(action).ProcessAllActions().State;
	}

	/// <summary>Every step, every distinct card on every drop, every Snare.</summary>
	private static IEnumerable<GameAction> Candidates(GameState s)
	{
		foreach (var ally in s.LivingAllies())
		foreach (var to in new[] { ally.Space - 1, ally.Space + 1 })
			yield return new MoveAllyAction { AllyId = ally.Id, Space = to };

		// Two copies of Guard do the same thing — try one.
		foreach (var card in s.CardsIn(ZoneType.Hand).DistinctBy(c => c.Name))
			for (var space = 0; space < PartyBattle.Spaces; space++)
				foreach (var foeRow in new[] { false, true })
					yield return new PlayPartyCardAction
					{
						CardId = card.Id,
						Space = space,
						FoeRow = foeRow,
					};

		foreach (var foe in s.LivingFoes())
			yield return new UseSnareAction { FoeId = foe.Id };
	}

	/// <summary>
	/// **What ending the turn now leaves — and the turn after, if nothing is played** — by the rules
	/// themselves. A loss is worse than anything; a win better.
	/// </summary>
	private static double Score(GameState s)
	{
		if (Terminal(s) is { } now)
			return now;

		var after = Do(s, new EndPartyTurnAction());
		if (Terminal(after) is { } then)
			return then;

		var later = Do(after, new EndPartyTurnAction());
		var laterValue = Terminal(later) is { } end ? end : Value(later);
		return (1 - NextTurnWeight) * Value(after) + NextTurnWeight * laterValue;
	}

	private static double? Terminal(GameState s) =>
		s.GetParty() is { IsOver: true } party ? (party.Won ? 10_000 + Value(s) : -10_000) : null;

	private static double Value(GameState s)
	{
		var party = s.GetParty();
		var snaring =
			party.Snares > 0
				? s.LivingFoes().Count(f => f.Catchable && f.Hp <= f.CatchAt()) * CatchableValue
				: 0;
		return s.Allies().Where(a => !a.IsKnockedOut).Sum(a => a.Hp)
			- s.LivingFoes().Sum(f => f.Hp)
			+ party.TrainerHp * TrainerWeight
			- party.LeaderHp
			+ s.CaughtFoes().Count() * CatchValue
			+ snaring;
	}

	/// <summary>Enough of a state to tell two part-turns apart — the beam keeps one of each.</summary>
	private static string Signature(GameState s)
	{
		var party = s.GetParty();
		return string.Join(
			"|",
			s.Allies()
				.Select(a =>
					$"{a.Id}:{a.Space}:{a.Hp}:{a.Block}:{a.BonusPower}:{a.BonusThorns}:{a.Momentum}:{a.StepsLeft}:{a.HasActed}:{a.PatternIndex}"
				)
				.Concat(
					s.GetChildren(s.GetWellKnownId(PartyState.BattleKey))
						.OfType<Foe>()
						.Select(f =>
							$"{f.Id}:{f.Space}:{f.Hp}:{f.Block}:{f.Staggered}:{f.OffBalance}:{f.Caught}"
						)
				)
				.Append($"{party.Energy}:{party.Snares}:{party.TrainerHp}:{party.LeaderHp}")
				.Append(string.Join(",", s.CardsIn(ZoneType.Hand).Select(c => c.Id)))
		);
	}

	private static string Describe(GameState s, GameAction action) =>
		action switch
		{
			MoveAllyAction m => $"step: {s.GetObject(m.AllyId).Name} to space {m.Space}",
			PlayPartyCardAction p =>
				$"{s.GetObject(p.CardId).Name} on {(p.FoeRow ? "foe" : "your")} space {p.Space}",
			UseSnareAction u => $"SNARE at the {s.GetObject(u.FoeId).Name}",
			_ => action.GetType().Name,
		};
}
