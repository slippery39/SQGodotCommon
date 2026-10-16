using ImmutableGameObjects;
using MagicCore.Actions;

namespace MagicCore.Ai;

/// <summary>
/// Scores a position by playing it forward to a fixed turn and evaluating where it ends — how the
/// search compares its candidate moves (MtgSimulator's MultiTurnBeamSearch: ScoreAfterCompletingTurn
/// with MultiTurnGreedyRollout). Every decision on the way is answered by a cheap policy, ours or the
/// simulated opponent's, so a rollout never searches.
///
/// Carried from MtgSimulator, each measured there (.claude/rules/sim-ai.md):
/// - the horizon is an ABSOLUTE turn, the same for every candidate of one search — a line that
///   dawdles inside its turn must not push a recurring cost out of the window;
/// - a win or loss decays per turn since the SEARCH began (not since this rollout began), so a sooner
///   win and a later loss score better, and a decided game stays far above any board score;
/// - every choice is answered by its OWNER, for the owner's good — never by whose turn it is.
///
/// The simulated opponent is BoardOnly: it attacks and blocks with its board, never casts and never
/// plays a land. AI-3 makes that swappable (cast from hand, respond) — the one place it is decided is
/// <see cref="OpponentStep"/>, which both the rollouts and the search tree go through.
///
/// ponytail: the libraries are read in their REAL order, so a rollout draws the cards really on top
/// — the AI sees its own future. AI-3 adds "shuffle what the player can't know" as a second mode.
/// </summary>
public static class Rollout
{
	/// <param name="LookaheadTurns">Turns played after the current one is finished.</param>
	/// <param name="ActionsPerTurn">
	/// Our non-land plays per simulated turn. MtgSimulator's default of 1 is an asymmetry — the
	/// opponent attacks with everything — kept as the starting point and measured from there.
	/// </param>
	public sealed record Settings(
		int LookaheadTurns = 2,
		int ActionsPerTurn = 1,
		double TerminalDiscount = 0.95,
		int DecisionLimit = 2_000,
		HeldValues? Held = null
	);

	public static readonly Settings Default = new();

	/// <summary>The turn a search's rollouts all stop at: the current one finished, then the lookahead.</summary>
	public static int Horizon(GameState root, Settings settings) =>
		root.GetGame().TurnNumber + 1 + settings.LookaheadTurns;

	/// <summary>
	/// <paramref name="me"/>'s score once the game reaches <paramref name="horizon"/> (or ends — a
	/// win or loss decayed by the turns since the search's root, which the horizon fixes).
	/// </summary>
	public static double Score(GameState s, int me, int horizon, Settings settings)
	{
		var rootTurn = horizon - 1 - settings.LookaheadTurns;
		var (turn, plays, landDone) = (s.GetGame().TurnNumber, 0, false);
		// ponytail: a rollout that hits DecisionLimit is scored where it stopped, mid-turn, beside
		// rollouts scored at the horizon. Only a loop that never advances reaches it; flag or drop
		// such a candidate if one is ever seen.
		for (var i = 0; i < settings.DecisionLimit && !s.IsOver(); i++)
		{
			if (s.GetGame().TurnNumber >= horizon)
				break;
			if (s.GetGame().TurnNumber != turn)
				(turn, plays, landDone) = (s.GetGame().TurnNumber, 0, false);

			var who = s.DecidingPlayerId();
			if (who != me)
			{
				s = OpponentStep(s, who, settings);
				continue;
			}
			if (s.GetPendingChoice() is { } choice)
			{
				s = s.Choose(Answer(s, choice, who, settings));
				continue;
			}
			var play = !(landDone && plays >= settings.ActionsPerTurn)
				? OurPlay(s, me, plays >= settings.ActionsPerTurn, ref landDone, settings)
				: null;
			if (play is not null)
			{
				s = s.Act(play);
				plays += play is PlayLandAction ? 0 : 1;
				continue;
			}
			s = Pass(s, who); // nothing worth playing
		}

		var score = Evaluator.Evaluate(s, me, settings.Held);
		return s.IsOver()
			? score * Math.Pow(settings.TerminalDiscount, s.GetGame().TurnNumber - rootTurn)
			: score;
	}

	/// <summary>
	/// One decision of the simulated opponent — the ONE place the opponent model is decided (BoardOnly:
	/// attacks and blocks judged, every other choice the cheap answer, every priority passed). The
	/// search answers the opponent through here too, so its tree and its rollouts agree.
	/// </summary>
	public static GameState OpponentStep(GameState s, int who, Settings settings) =>
		s.GetPendingChoice() is { } choice
			? s.Choose(Answer(s, choice, who, settings))
			: Pass(s, who);

	static GameState Pass(GameState s, int who) =>
		s.Act(
			s.GetGame().Step == Step.PreGame
				? new KeepHandAction { PlayerId = who }
				: new PassPriorityAction { PlayerId = who }
		);

	/// <summary>
	/// Our policy: on our own turn at sorcery speed, the land first, then (unless the turn's plays are
	/// spent) the best-scoring play if it leaves us no worse. "No worse", not "better" as GreedyAi asks:
	/// a setup play scores flat, and MtgSimulator found strict improvement stops at exactly the play a
	/// rollout exists to see. Never responds.
	/// </summary>
	static GameAction? OurPlay(
		GameState s,
		int me,
		bool playsSpent,
		ref bool landDone,
		Settings settings
	)
	{
		if (!Priority.AtSorcerySpeed(s, me))
			return null;
		var moves = s.DistinctMoves(s.LegalActions());
		if (moves.OfType<PlayLandAction>().FirstOrDefault() is { } land)
			return land;
		landDone = true; // none now: none this turn worth asking for again
		if (playsSpent)
			return null;
		var candidates = moves.Where(a => a is not PassPriorityAction).ToList();
		if (candidates.Count == 0)
			return null;
		var before = Evaluator.Evaluate(s, me, settings.Held);
		var (move, score) = candidates
			.Select(a => (a, Evaluator.Evaluate(Lookahead.Settle(s.Act(a)), me, settings.Held)))
			.MaxBy(x => x.Item2);
		return score >= before ? move : null;
	}

	/// <summary>
	/// Attacks and blocks are judged by what they lead to, for whoever declares them — so a player
	/// facing lethal chump-blocks. Every other choice takes the cheap answer (first legal target …).
	/// ponytail: each creature is judged ALONE (the rest declare nothing in the look-ahead), so an
	/// attack that only works together — two 3/3s into one blocker for lethal — is never found.
	/// MtgSimulator's FindLethalAttacks covers that before the search; port it if rollouts need it.
	/// </summary>
	static IEnumerable<int> Answer(GameState s, ChoiceAction choice, int who, Settings settings) =>
		choice switch
		{
			DeclareAttackerChoice or DeclareBlockerChoice =>
			[
				choice
					.Options.Select(o => o.Id)
					.MaxBy(id =>
						Evaluator.Evaluate(Lookahead.Settle(s.Choose([id])), who, settings.Held)
					),
			],
			_ => Lookahead.QuickAnswer(s, choice),
		};
}
