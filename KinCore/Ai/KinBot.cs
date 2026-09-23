using ImmutableGameObjects;

namespace KinCore;

/// <summary>
/// How the bot values a position. **Stamped into every results file**, because a win rate is a
/// property of (game, bot) and not of the game — two numbers measured under different weights are
/// not comparable, and nothing else will remind you of that in six weeks.
/// </summary>
public record KinEvalWeights
{
	/// <summary>
	/// Bumped whenever a weight or the search changes. Old result files keep the old id.
	///
	/// **`/v3` marks a change to the GAME, not to the bot.** A win rate is a property of
	/// (game, bot), and combat v3 made units ephemeral — so a v3 number and a v2 number are not
	/// comparable even though the search and every weight here are untouched. Without this, run 14
	/// reads as a catastrophic regression against run 13 rather than as a different game.
	///
	/// **NOT bumped when the deck term was deleted on 2026-09-22, and that is deliberate.** The
	/// term had become a constant — with the dooms gone a battle cannot touch the run deck, so
	/// `Run.AfterBattle(s).Deck` was `run.Deck` for every line scored — and a constant added to
	/// every sibling cannot move an argmax. Verified rather than argued: `sim 30` before and after,
	/// and all 88 lines of output matched except the wall clock. Numbers measured either side of it
	/// ARE comparable, so bumping would have thrown that comparability away for nothing.
	/// </summary>
	public string Version { get; init; } = "bot-1/v3";

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

	/// <summary>Nodes one turn's search may expand. Hit only by a wide hand; see KinBot.</summary>
	public int NodeBudget { get; init; } = 20000;
}

/// <summary>
/// Plays a turn as well as it can see, which is exactly one turn.
///
/// **It enumerates every legal line and scores each by actually ending the turn on a copy.** That
/// is affordable here and nowhere near affordable in MTG: 3 energy, one unit per lane, five lanes
/// and no targeting at all means a turn has hundreds of options, not billions. Scoring by
/// simulation rather than by a hand-written board heuristic is what stops there being a second,
/// drifting account of what combat does — the same reason `KinPreviewer` runs the real transform.
///
/// **It sees one turn, and nothing now corrects for that.** The doom clock was the multi-turn race
/// the bot could not search, and the eval answered it by pricing the run deck through
/// `Run.AfterBattle` — a line that let an apocalypse land paid for the cards it would cost. With
/// the dooms deleted a battle cannot touch the run deck at all, so that term became a constant
/// added to every sibling line and was removed. **If a mechanic ever spans turns again, this is
/// where it has to be priced**, and a constant is not how.
/// </summary>
public static class KinBot
{
	/// <summary>
	/// Returns the state after the best sequence of plays this turn. **Does not end the turn** —
	/// the caller does, so the simulator and any future front end share one turn boundary.
	/// </summary>
	public static GameState PlayTurn(GameState state, KinEvalWeights? weights = null)
	{
		var w = weights ?? new KinEvalWeights();

		var budget = w.NodeBudget;
		var best = Search(state, w, ref budget);

		// **The companion's move is considered FIRST, and only first.** Staying put is one line and
		// each legal move is another, and each gets its own search of the plays after it. Letting a
		// move interleave anywhere in a line would multiply the search by every point it could go
		// — and moving before you play is where it almost always belongs, since the move decides
		// which lanes your cards should fill.
		//
		// ponytail: a move AFTER some plays is never tried, e.g. play into the companion's lane's
		// neighbour first. Search it everywhere if play shows the bot missing those lines.
		foreach (var lane in state.OpenLanes())
		{
			var move = new MoveCompanionAction { Lane = lane };
			if (!move.ValidateAdd(state).IsValid)
				continue;

			var (moved, _) = state.AddAction(move).ProcessAllActions();
			var laneBudget = w.NodeBudget;
			var line = Search(moved, w, ref laneBudget);

			if (line.Score > best.Score)
				best = line;
		}

		return best.State;
	}

	/// <summary>
	/// Depth-first over every legal line. Every node is a valid stopping point — playing nothing
	/// more is always allowed — so the node's own score competes with its children's.
	/// </summary>
	private static (double Score, GameState State) Search(
		GameState s,
		KinEvalWeights w,
		ref int budget
	)
	{
		var best = (Score: ScoreEndingTurnHere(s, w), State: s);

		if (--budget <= 0 || s.GetBattle().IsOver)
			return best;

		foreach (var action in Candidates(s))
		{
			if (!action.ValidateAdd(s).IsValid)
				continue;

			var (next, _) = s.AddAction(action).ProcessAllActions();
			var child = Search(next, w, ref budget);

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
	///
	/// **Combat v3 lets you play into a HELD lane, and this deliberately does not offer it.** Not
	/// an oversight: the board is empty at the start of every turn, so the only thing holding a
	/// lane mid-turn is something this search placed a moment ago — and replacing that is strictly
	/// worse than not having played it, since you paid twice for one lane. The companion's lane is
	/// refused outright. So open lanes ARE the legal, non-dominated set.
	///
	/// **This stops being true in phase 6**, when a persistent unit can survive into a turn you did
	/// not place it on and overwriting it becomes a real choice. Widen it then — and measure the
	/// node budget when you do, because the branching factor is what pays for it.
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
				// **A rite that reads a lane is a targeted card** — the lane it is dropped on IS
				// the choice — so each lane is a different play and all five have to be offered.
				// Not just the open ones: Gallows Feast wants a lane you are HOLDING.
				if (card.Effects.Any(e => KinTargeting.IsLaneScoped(e.Target)))
				{
					for (var lane = 0; lane < KinBattle.LaneCount; lane++)
						yield return new PlayCardAction { CardId = card.Id, Lane = lane };
				}
				else
				{
					yield return new PlayCardAction { CardId = card.Id };
				}

				continue;
			}

			foreach (var lane in openLanes)
				yield return new PlayCardAction { CardId = card.Id, Lane = lane };

			// **Devour is the one reason to play into a lane you already hold.** The comment above
			// is right that overwriting is otherwise strictly worse — you paid twice for one lane —
			// but a Devour card is buying a DEATH with that second payment, and every `Loss` read
			// in the pool pays for it. Without this the bot never devours and `sim` measures the
			// keyword as a blank.
			if (card.Devours)
			{
				foreach (var lane in s.HeldLanes())
					yield return new PlayCardAction { CardId = card.Id, Lane = lane };
			}
		}
	}

	/// <summary>
	/// What this line is worth if the turn ends right now — combat, deaths and the Opponent's
	/// reinforcement all resolved by the engine itself on a throwaway copy.
	/// </summary>
	private static double ScoreEndingTurnHere(GameState s, KinEvalWeights w)
	{
		if (s.GetBattle().IsOver)
			return Score(s, w);

		var (after, _) = s.AddAction(new EndTurnAction()).ProcessAllActions();
		return Score(after, w);
	}

	private static double Score(GameState s, KinEvalWeights w)
	{
		var battle = s.GetBattle();
		if (battle.PlayerIsDead)
			return w.Death;

		var score = 0.0;

		if (battle.OpponentDefeated)
			score += w.Win;

		score -= s.GetOpponent().Health * w.OpponentHealth;
		score += s.GetPlayer().Life * w.Life;

		// **Under v3 this prices the COMPANION and almost nothing else, and that is correct.** The
		// score is taken after `EndTurnAction` has run, and by then your units have withdrawn — so
		// a board is worth nothing once the turn is over, which is exactly what v3 made true. What
		// a unit was WORTH is already counted: damage it dealt shows up in OpponentHealth and
		// EnemyHealth, damage it soaked shows up in Life. Do not re-add a board term to make the
		// bot "value its board"; it would be paying twice for the same turn.
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

		return score;
	}
}
