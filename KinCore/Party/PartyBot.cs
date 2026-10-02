using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **The bot for `party-sim` — never for play.** Each turn it searches SEQUENCES of plays (a beam:
/// the best few part-turns, each extended by every card), so it finds combos whose first
/// card looks useless alone — Charge then Rally, Gust then a Strike. A plan is scored by what the
/// ENGINE says ending the turn leaves, AND by the turn after with no plays, so the order the line is
/// left in matters. Still a heuristic: its win rate is a floor, not the game's. Not yet re-tuned
/// for THE RELAY (exploring).
/// </summary>
public static class PartyBot
{
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
		s = s.AddAction(action).ProcessAllActions().State;

		// ponytail: answers a hand choice with the first cards offered; score the options if the
		// bot's discards ever matter to a measurement.
		while (s.GetPendingChoice() is { } choice)
			s = s.ResolveChoice(
				[.. choice.Options.Take(choice.MinChoices).Select(o => o.Id)]
			).State;
		return s;
	}

	/// <summary>Every distinct card on every place in either line.</summary>
	private static IEnumerable<GameAction> Candidates(GameState s)
	{
		// Two copies of Guard do the same thing — try one.
		foreach (var card in s.CardsIn(ZoneType.Hand).DistinctBy(c => c.Name))
		{
			// No target: one play, not ten identical ones.
			if (!card.NeedsTarget())
			{
				yield return new PlayPartyCardAction { CardId = card.Id };
				continue;
			}
			for (var space = 0; space < PartyBattle.MaxLine; space++)
				foreach (var foeRow in new[] { false, true })
					yield return new PlayPartyCardAction
					{
						CardId = card.Id,
						Space = space,
						FoeRow = foeRow,
					};
		}
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
		return s.Allies().Where(a => !a.IsKnockedOut).Sum(a => a.Hp)
			- s.LivingFoes().Sum(f => f.Hp);
	}

	/// <summary>Enough of a state to tell two part-turns apart — the beam keeps one of each.</summary>
	private static string Signature(GameState s)
	{
		var party = s.GetParty();
		return string.Join(
			"|",
			s.Allies()
				.Select(a =>
					$"{a.Id}:{a.Position}:{a.Hp}:{a.Block}:{a.BonusPower}:{a.BonusThorns}:{a.HasActed}:{a.PatternIndex}"
				)
				.Concat(
					s.GetChildren(s.GetWellKnownId(PartyState.BattleKey))
						.OfType<Foe>()
						.Select(f =>
							$"{f.Id}:{f.Position}:{f.Hp}:{f.Block}:{f.Staggered}:{f.OffBalance}"
						)
				)
				.Append($"{party.Energy}:{party.NextCardFree}:{party.XPaid}")
				.Append(string.Join(",", s.CardsIn(ZoneType.Hand).Select(c => c.Id)))
		);
	}

	private static string Describe(GameState s, GameAction action) =>
		action switch
		{
			PlayPartyCardAction p =>
				$"{s.GetObject(p.CardId).Name} on {(p.FoeRow ? "their" : "your")} line at {p.Space}",
			_ => action.GetType().Name,
		};
}
