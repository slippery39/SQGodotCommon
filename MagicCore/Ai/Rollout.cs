using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;

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
/// The simulated opponent is <see cref="Settings.Opponent"/> (story AI-3) — the one place it is decided
/// is <see cref="OpponentStep"/>, which both the rollouts and the search tree go through.
///
/// A rollout draws the cards on top of the library it is given: what the search may know of them is
/// decided at its root (<see cref="Hidden.Sample"/>) — the real order, or what isn't known shuffled.
/// </summary>
public static class Rollout
{
	/// <param name="LookaheadTurns">Turns played after the current one is finished.</param>
	/// <param name="ActionsPerTurn">
	/// Our non-land plays per simulated turn (and a casting opponent's spells). MtgSimulator's default
	/// of 1 is an asymmetry — the opponent attacks with everything — kept as the starting point and
	/// measured from there.
	/// </param>
	/// <param name="Opponent">What the simulated opponent does — <see cref="OpponentModel"/>.</param>
	/// <param name="Ours">How our own side plays in a rollout — <see cref="OurPolicy"/>.</param>
	public sealed record Settings(
		int LookaheadTurns = 2,
		int ActionsPerTurn = 1,
		double TerminalDiscount = 0.95,
		int DecisionLimit = 2_000,
		HeldValues? Held = null,
		OpponentModel Opponent = OpponentModel.BoardOnly,
		OurPolicy Ours = OurPolicy.Cheap,
		Attacks Attacks = Attacks.EachAlone,
		Targets Targets = Targets.FirstLegal
	);

	/// <summary>How the rollout judges an attack declaration — both sides' (story AI-3.8).</summary>
	public enum Attacks
	{
		/// <summary>Each creature alone, the ones after it staying home in the look-ahead.</summary>
		EachAlone,

		/// <summary>
		/// Also "this creature and every one after it attack the player", taken if it beats the best
		/// alone — two 3/3s into one blocker for lethal (Shayne).
		/// </summary>
		AllInToo,
	}

	/// <summary>How the rollout answers a target choice — a trigger's, as it goes on the stack (story AI-3.8).</summary>
	public enum Targets
	{
		/// <summary>The first legal target (<see cref="Lookahead.QuickAnswer"/>).</summary>
		FirstLegal,

		/// <summary>Each legal target settled, the best for whoever chooses it — as GreedyAi answers one pick (Shayne).</summary>
		OwnersBest,
	}

	/// <summary>Our own side's rollout policy (story AI-3.7) — the opponent's is its model, apart.</summary>
	public enum OurPolicy
	{
		/// <summary>The land, then at most ActionsPerTurn plays a turn, each the best that leaves us no worse; sorcery speed only (<see cref="OurPlay"/>).</summary>
		Cheap,

		/// <summary>
		/// <see cref="GreedyAi"/> decides every decision of ours — any play at any speed that beats
		/// passing, as many as do, and every choice by its own answer (Shayne). One ply at every option:
		/// dear.
		/// </summary>
		Greedy,
	}

	/// <summary>The simulated opponent, measured against each other (story AI-3).</summary>
	public enum OpponentModel
	{
		/// <summary>Attacks and blocks with its board; never casts, never plays a land.</summary>
		BoardOnly,

		/// <summary>Also plays a land, up to ActionsPerTurn spells and its loyalty abilities in its own turn, by OUR policy — from its real hand.</summary>
		CastsFromHand,

		/// <summary>
		/// CastsFromHand, and in OUR turn it also responds at instant speed — while a spell or ability
		/// of ours is on the stack, and in our combat — with whichever spell beats passing most, both
		/// settled; as often as one does (story AI-3.6, Shayne). Our own rollout policy never responds.
		/// </summary>
		Responds,
	}

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
		var greedy = settings.Ours == OurPolicy.Greedy ? new GreedyAi(me, settings.Held) : null;
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
			if (greedy is not null)
			{
				s = greedy.Play(s);
				continue;
			}
			if (s.GetPendingChoice() is { } choice)
			{
				s = s.Choose(Answer(s, choice, who, settings));
				continue;
			}
			var spent = plays >= settings.ActionsPerTurn;
			var play = !(landDone && spent)
				? OurPlay(s, me, spent ? null : AnyPlay, ref landDone, settings)
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
	/// One decision of the simulated opponent — the ONE place the opponent model is decided: attacks
	/// and blocks judged, every other choice the cheap answer, (CastsFromHand, Responds) its own
	/// sorcery-speed plays, and (Responds) its instant-speed answers in our turn; every other priority
	/// passed. The search answers the opponent through here too, so its tree and its rollouts agree.
	/// </summary>
	public static GameState OpponentStep(GameState s, int who, Settings settings) =>
		s.GetPendingChoice() is { } choice ? s.Choose(Answer(s, choice, who, settings))
		: settings.Opponent != OpponentModel.BoardOnly && TheirPlay(s, who, settings) is { } play
			? s.Act(play)
		: settings.Opponent == OpponentModel.Responds
		&& TheirResponse(s, who, settings) is { } response
			? s.Act(response)
		: Pass(s, who);

	/// <summary>
	/// Responds: in our turn, while our spell or ability is on top of the stack or our attackers or
	/// blockers are declared, the instant-speed spell whose settled result beats passing (settled the
	/// same way) most — none if none beats it. Each response is judged at the next window again, so
	/// there is no limit but its mana. ponytail: spells only — a free activated ability that beats
	/// passing could repeat without end; add abilities with a count if a deck's answer is one.
	/// </summary>
	static GameAction? TheirResponse(GameState s, int who, Settings settings)
	{
		var game = s.GetGame();
		var ours = s.OpponentOf(who);
		var window =
			game.ActivePlayerId == ours
			&& (
				Stops.TopOfStackControllerId(s) == ours
				|| (
					s.StackIsEmpty()
					&& game.Step is Step.DeclareAttackers or Step.DeclareBlockers
					&& Turns.Combat.Attackers(s).Any() // a combat with no attack is no window
				)
			);
		if (!window)
			return null;
		var spells = s.DistinctMoves(s.LegalActions()).OfType<CastSpellAction>().ToList();
		if (spells.Count == 0)
			return null;
		var passing = Evaluator.Evaluate(Lookahead.Settle(Pass(s, who)), who, settings.Held);
		var (best, score) = BestSettled(s, who, spells, settings);
		return score > passing ? best : null;
	}

	/// <summary>
	/// CastsFromHand: the opponent plays by OUR policy — its land, then up to ActionsPerTurn spells, and
	/// each planeswalker's loyalty ability (once a turn by the rules, so it is not counted). Counted from
	/// the turn's tally rather than kept, because OpponentStep holds no state — the search tree calls it
	/// too. So the two sides differ in two ways: in the search's own turn ours counts from the rollout's
	/// start and theirs from the turn's start, and ours counts a loyalty or other ability as a play.
	/// ponytail: no other activated abilities — the tally counts no activations, so a free one would
	/// repeat until the decision limit; count them in the tally if a deck's plan is an ability.
	/// </summary>
	static GameAction? TheirPlay(GameState s, int who, Settings settings)
	{
		var tally = s.GetPlayer(who).ThisTurn;
		var turn = s.GetGame().TurnNumber;
		// No land in hand: no land drop to look for (a land played from another zone is missed).
		var landDone =
			tally.LandsPlayed >= 1 + tally.ExtraLandDrops
			|| !s.CardsIn(who, ZoneType.Hand).Any(c => c.Is(CardType.Land));
		var playable =
			tally.SpellsCast < settings.ActionsPerTurn ? SpellOrLoyalty
			: s.CardsIn(who, ZoneType.Battlefield)
				.Any(c => c.Is(CardType.Planeswalker) && c.LoyaltyActivatedOnTurn != turn)
				? Loyalty
			: null;
		return landDone && playable is null
			? null
			: OurPlay(s, who, playable, ref landDone, settings);
	}

	static readonly Func<GameAction, bool> AnyPlay = a => a is not PassPriorityAction;
	static readonly Func<GameAction, bool> SpellOrLoyalty = a =>
		a is CastSpellAction or ActivateLoyaltyAction;
	static readonly Func<GameAction, bool> Loyalty = a => a is ActivateLoyaltyAction;

	static GameState Pass(GameState s, int who) =>
		s.Act(
			s.GetGame().Step == Step.PreGame
				? new KeepHandAction { PlayerId = who }
				: new PassPriorityAction { PlayerId = who }
		);

	/// <summary>
	/// Our policy: on our own turn at sorcery speed, the land first, then the best-scoring
	/// <paramref name="playable"/> move (none: the turn's plays are spent) if it leaves us no worse. "No worse", not "better" as GreedyAi asks:
	/// a setup play scores flat, and MtgSimulator found strict improvement stops at exactly the play a
	/// rollout exists to see. Never responds.
	/// </summary>
	static GameAction? OurPlay(
		GameState s,
		int me,
		Func<GameAction, bool>? playable,
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
		if (playable is null)
			return null;
		var candidates = moves.Where(playable).ToList();
		if (candidates.Count == 0)
			return null;
		var before = Evaluator.Evaluate(s, me, settings.Held);
		var (move, score) = BestSettled(s, me, candidates, settings);
		return score >= before ? move : null;
	}

	/// <summary>The move whose settled result scores best for <paramref name="who"/> — our policy's and a responding opponent's one way to judge a play.</summary>
	static (GameAction Move, double Score) BestSettled<T>(
		GameState s,
		int who,
		IEnumerable<T> candidates,
		Settings settings
	)
		where T : GameAction =>
		candidates
			.Select(a =>
				((GameAction)a, Evaluator.Evaluate(Lookahead.Settle(s.Act(a)), who, settings.Held))
			)
			.MaxBy(x => x.Item2);

	/// <summary>
	/// Attacks and blocks are judged by what they lead to, for whoever declares them — so a player
	/// facing lethal chump-blocks. Each creature is judged alone (the rest declare nothing in the
	/// look-ahead) unless <see cref="Attacks.AllInToo"/> weighs "all attack" too. A target choice takes
	/// its owner's best under <see cref="Targets.OwnersBest"/>; every other choice the cheap answer.
	/// </summary>
	static IEnumerable<int> Answer(GameState s, ChoiceAction choice, int who, Settings settings) =>
		choice switch
		{
			DeclareAttackerChoice attacker when settings.Attacks == Attacks.AllInToo =>
				AllInOrAlone(s, attacker, who, settings),
			DeclareAttackerChoice or DeclareBlockerChoice =>
			[
				BestAlone(s, choice, who, settings).Id,
			],
			// Each legal target (or none, when optional) settled, the best for its owner — GreedyAi's
			// answer to one pick, with the search's held values.
			ChooseTargetChoice { MaxChoices: 1 } when settings.Targets == Targets.OwnersBest =>
				new GreedyAi(who, settings.Held).Answer(s, choice),
			_ => Lookahead.QuickAnswer(s, choice),
		};

	/// <summary>One attacker's or blocker's best option, judged alone: each settled for <paramref name="who"/>.</summary>
	static (int Id, double Score) BestAlone(
		GameState s,
		ChoiceAction choice,
		int who,
		Settings settings
	) =>
		choice
			.Options.Select(o =>
				(o.Id, Evaluator.Evaluate(Lookahead.Settle(s.Choose([o.Id])), who, settings.Held))
			)
			.MaxBy(x => x.Item2);

	/// <summary>
	/// AllInToo: the best answer judged alone, unless "this one and every one after attack the player"
	/// settles better — then attack the player (the next creature's judgment sees the same all-in line).
	/// ponytail: the look-ahead's defender blocks only what it survives — it never chump-blocks — so an
	/// all-in can look lethal where a defender would chump; the rollout's own blocks (Answer) do chump.
	/// Settle the all-in line with those blocks if all-in proves too eager.
	/// </summary>
	static IEnumerable<int> AllInOrAlone(
		GameState s,
		DeclareAttackerChoice attacker,
		int who,
		Settings settings
	)
	{
		var (alone, aloneScore) = BestAlone(s, attacker, who, settings);
		var player = s.OpponentOf(attacker.PlayerId);
		var allIn = Evaluator.Evaluate(
			Lookahead.Settle(s.Choose([player]), allAttack: true),
			who,
			settings.Held
		);
		return [allIn > aloneScore ? player : alone];
	}
}
