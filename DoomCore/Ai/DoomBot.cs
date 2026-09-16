using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// How the bot values a position. **Stamped into every results file**, because a win rate is a
/// property of (game, bot) and not of the game — two numbers measured under different weights are
/// not comparable, and nothing else will remind you of that in six weeks.
/// </summary>
public record DoomEvalWeights
{
	/// <summary>Bumped whenever a weight or the search changes. Old result files keep the old id.</summary>
	public string Version { get; init; } = "bot-1";

	public double Win { get; init; } = 250;
	public double Death { get; init; } = -5000;

	/// <summary>Per point of Opponent health remaining. Killing it is the only way to win.</summary>
	public double OpponentHealth { get; init; } = 3;

	/// <summary>Per point of player life. Life is the run's only fail state, so it outprices board.</summary>
	public double Life { get; init; } = 2;

	public double UnitPower { get; init; } = 1.5;
	public double UnitToughness { get; init; } = 1;
	public double EnemyHealth { get; init; } = 1;
	public double EnemyAttack { get; init; } = 1.5;

	/// <summary>Per point of deck value. This is what prices taking the apocalypse vs outrunning it.</summary>
	public double DeckCard { get; init; } = 1.0;

	/// <summary>Nodes one turn's search may expand. Hit only by a wide hand; see DoomBot.</summary>
	public int NodeBudget { get; init; } = 20000;
}

/// <summary>
/// Plays a turn as well as it can see, which is exactly one turn.
///
/// **It enumerates every legal line and scores each by actually ending the turn on a copy.** That
/// is affordable here and nowhere near affordable in MTG: 3 energy, one unit per lane, five lanes
/// and no targeting at all means a turn has hundreds of options, not billions. Scoring by
/// simulation rather than by a hand-written board heuristic is what stops there being a second,
/// drifting account of what combat does — the same reason `DoomPreviewer` runs the real transform.
///
/// **It sees one turn.** The doom clock is a multi-turn race and the bot does not search it; what
/// keeps it from being blind is that the eval prices the RUN DECK through <see cref="Run.AfterBattle"/>,
/// so a line that lets the apocalypse land pays for the cards it would cost, with the real
/// transform doing the pricing.
/// </summary>
public static class DoomBot
{
	/// <summary>
	/// Returns the state after the best sequence of plays this turn. **Does not end the turn** —
	/// the caller does, so the simulator and any future front end share one turn boundary.
	/// </summary>
	public static GameState PlayTurn(GameState state, Run run, DoomEvalWeights? weights = null)
	{
		var w = weights ?? new DoomEvalWeights();
		var budget = w.NodeBudget;
		return Search(state, run, w, ref budget).State;
	}

	/// <summary>
	/// Depth-first over every legal line. Every node is a valid stopping point — playing nothing
	/// more is always allowed — so the node's own score competes with its children's.
	/// </summary>
	private static (double Score, GameState State) Search(
		GameState s,
		Run run,
		DoomEvalWeights w,
		ref int budget
	)
	{
		var best = (Score: ScoreEndingTurnHere(s, run, w), State: s);

		if (--budget <= 0 || s.GetBattle().IsOver)
			return best;

		foreach (var action in Candidates(s))
		{
			if (!action.ValidateAdd(s).IsValid)
				continue;

			var (next, _) = s.AddAction(action).ProcessAllActions();
			var child = Search(next, run, w, ref budget);

			if (child.Score > best.Score)
				best = child;

			if (budget <= 0)
				break;
		}

		return best;
	}

	/// <summary>
	/// Every play worth trying from here: affordable cards into open lanes.
	///
	/// **Identical cards collapse to one candidate.** A starter hand holds four Scavengers, and
	/// without this the search explores 24 orderings of the same board. Lanes do NOT collapse —
	/// each holds a different enemy, so the lane is the decision.
	/// </summary>
	private static IEnumerable<GameAction> Candidates(GameState s)
	{
		var energy = s.GetPlayer().Energy;
		var openLanes = s.OpenLanes().ToList();
		var seen = new HashSet<string>();

		foreach (var card in s.CardsIn(ZoneType.Hand))
		{
			if (card.Cost > energy)
				continue;

			var unit = card.GetComponent<UnitComponent>();
			var signature =
				$"{card.Name}|{card.Cost}|{unit?.Power}/{unit?.Toughness}|{card.Effects.Count}";

			if (!seen.Add(signature))
				continue;

			if (unit is null)
			{
				yield return new PlayCardAction { CardId = card.Id };
				continue;
			}

			foreach (var lane in openLanes)
				yield return new PlayCardAction { CardId = card.Id, Lane = lane };
		}
	}

	/// <summary>
	/// What this line is worth if the turn ends right now — combat, deaths, the Opponent's
	/// reinforcement and any doom firing all resolved by the engine itself on a throwaway copy.
	/// </summary>
	private static double ScoreEndingTurnHere(GameState s, Run run, DoomEvalWeights w)
	{
		if (s.GetBattle().IsOver)
			return Score(s, run, w);

		var (after, _) = s.AddAction(new EndTurnAction()).ProcessAllActions();
		return Score(after, run, w);
	}

	private static double Score(GameState s, Run run, DoomEvalWeights w)
	{
		var battle = s.GetBattle();
		if (battle.PlayerIsDead)
			return w.Death;

		var score = 0.0;

		if (battle.OpponentDefeated)
			score += w.Win;

		score -= s.GetOpponent().Health * w.OpponentHealth;
		score += s.GetPlayer().Life * w.Life;

		foreach (var card in s.Units())
		{
			var unit = card.Unit();
			if (unit.IsDead)
				continue;

			score += unit.Power * w.UnitPower + unit.RemainingToughness * w.UnitToughness;
		}

		foreach (var enemy in s.LivingEnemies())
		{
			score -= enemy.Health * w.EnemyHealth;
			score -= (enemy.Intent == IntentKind.Attack ? enemy.IntentAmount : 0) * w.EnemyAttack;
		}

		// THE DOOM TERM. Replaying the firings through the real transform is what makes the bot
		// play the countdown race at all: a line that eats an apocalypse pays here, in the cards it
		// actually costs, priced by the same code the game uses. Firings already banked appear in
		// every sibling line, so they cancel and only a NEW firing moves the comparison.
		var deck = run.AfterBattle(s).Deck;
		if (deck.IsEmpty)
			return w.Death; // Flood can empty a deck outright, and an empty deck is a lost run.

		score += deck.Sum(CardValue) * w.DeckCard;

		return score;
	}

	/// <summary>
	/// What a deck entry is worth, roughly.
	///
	/// ponytail: crude stat sum — it only has to rank "lost a card" against "gained one" so the
	/// doom term has a sign. Replace it with the measured per-card numbers out of
	/// `doom_sim_results/` once there are some, which is the whole point of running the sim.
	/// </summary>
	private static double CardValue(RunCard card) =>
		card.IsUnit ? card.Power + card.Toughness - card.Cost : 4 - card.Cost;
}
