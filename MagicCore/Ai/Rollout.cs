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
	/// <param name="Progress">
	/// "Progress this turn" (story AI-10, Shayne): a won line also counts the opponent's life lost by the
	/// end of the search's own turn, so of two wins on the same turn the one that presses NOW scores
	/// higher — both used to score 10,000 × 0.95², and the tie picked "Don't attack". No worse over
	/// 288 games (51.4% ± 1.2pp, AI-10.2); off is beam+noprogress. Since AI-16 it is the whole
	/// evaluation at that moment (<see cref="ProgressBy.TurnEnd"/>), not their life alone — see there.
	/// </param>
	/// <param name="Mays">
	/// A "you may" (a choice of none or one: "you may sacrifice a creature", a yes/no, "you may discard",
	/// a library search) is answered with its chooser's best — none and each option settled, as
	/// <see cref="GreedyAi.Answer"/> does — not with nothing (AI-17.2). Declined, Way of the Deathbringer's
	/// "−2: you may sacrifice a creature … a 4/4" was "−2 loyalty for nothing", the worst of its options,
	/// and the search's plan drew a card instead. An optional target ("up to one") is one too, whatever
	/// <see cref="Targets"/> says; a pay-to-keep is not (the cheap answer pays). ponytail: only the
	/// rollout's answers — <see cref="Lookahead.Settle"/>, which judges each play the policies weigh,
	/// still declines (AI-17's review); and a library search settles every card it offers. Off is
	/// beam+nomays.
	/// </param>
	/// <param name="LostLines">
	/// A lost line counts the board as the searched turn ends too (AI-16.4, Shayne: "if the AI can only find
	/// losing lines, the AI should still try to maximize their board score") — a later loss still first
	/// ("it gives your opponent the chance to make a mistake"), then the better board, squashed under
	/// <see cref="TurnEndCap"/> as a won line's is. Every lost line on one turn used to tie, and the tie
	/// went to the first move that acts (<see cref="BeamSearchAi"/>'s pass bias). A line lost INSIDE the
	/// searched turn has no turn end; since AI-17.6 its own board breaks the tie (Evaluator.Terms.EndBoard).
	/// Off is beam+nolostlines (with lifeprogress and
	/// noprogress, which reproduce the search before AI-16).
	/// </param>
	/// <param name="KnownOnly">
	/// A casting or responding opponent plays only the cards in its hand WE know (<see cref="Card.KnownTo"/>:
	/// one we bounced, saw, or watched return from a graveyard) — board-only otherwise (Shayne, AI-10.4:
	/// "the opponent shouldn't just run into it"). Its unknown hand, lands included, stays unplayed.
	/// The default since AI-10.4, with <see cref="OpponentModel.Responds"/>: board-only plus what we
	/// know — no stronger over 96 games (49.0% ± 1.0pp), +1% cost, and it stops an Aura run into a known
	/// removal spell. Off (the whole hand) is beam+casts or beam+responds; board-only, beam+boardonly.
	/// </param>
	/// <param name="TheirWalkers">
	/// The simulated opponent uses its best planeswalker ability every turn it can, whatever its model
	/// (<see cref="TheirLoyalty"/>, story AI-8.1). False: only a casting opponent does, and only when
	/// it leaves it no worse — which by the evaluator a Jace drawing its card never did.
	/// </param>
	public sealed record Settings(
		int LookaheadTurns = 2,
		int ActionsPerTurn = 1,
		double TerminalDiscount = 0.95,
		int DecisionLimit = 2_000,
		HeldValues? Held = null,
		OpponentModel Opponent = OpponentModel.Responds,
		OurPolicy Ours = OurPolicy.Cheap,
		Attacks Attacks = Attacks.AsOne,
		Targets Targets = Targets.OwnersBest,
		bool TheirWalkers = true,
		ProgressBy Progress = ProgressBy.TurnEnd,
		bool KnownOnly = true,
		bool LostLines = true,
		bool Mays = true
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

		/// <summary>
		/// The attack chosen as ONE decision at the declaration's first question (AI-17.3, Shayne): a few
		/// candidate attacks, each creature at its own best target, each settled against the defender's
		/// blocks — <see cref="WholeAttacks"/>, as the search's tree chooses (BeamSearchAi.Settings.
		/// WholeAttacks). The default since AI-17.6 (Shayne, AI-17.3: "if we ever find a test case that needs
		/// it to be in the look ahead then we will have to reconsider"): at 5 life with one 3/3 against three
		/// 2/2s, each 2/2 alone dies to the 3/3, so a look-ahead judging them one by one never imagined the
		/// swing to 1 life and the AI drew two cards instead of casting a blocker. +27.6% a position (AI-17.3)
		/// — beam+eachalonelookahead is the old.
		/// </summary>
		AsOne,
	}

	/// <summary>How the rollout answers a target choice — a trigger's, as it goes on the stack (story AI-3.8).</summary>
	public enum Targets
	{
		/// <summary>The first legal target (<see cref="Lookahead.QuickAnswer"/>).</summary>
		FirstLegal,

		/// <summary>
		/// Each legal target settled, the best for whoever chooses it — as GreedyAi answers one pick
		/// (Shayne). The default since AI-17.3: under FirstLegal the simulated opponent's Fateshaper
		/// Aspirant put its +1/+1 counter and indestructible on OUR creature, and that misplay made a
		/// bounce look right at #547 (Shayne held there). AI-3.8 had measured it no different (192 games,
		/// 2 decisions in 288). The first legal one is beam+firsttarget.
		/// </summary>
		OwnersBest,
	}

	/// <summary>What a won line counts besides how soon it wins — <see cref="Settings.Progress"/>.</summary>
	public enum ProgressBy
	{
		/// <summary>Nothing: won lines on the same turn tie (before AI-10) — beam+noprogress.</summary>
		None,

		/// <summary>The opponent's life as the search's own turn ends, a point a life (AI-10) — beam+lifeprogress.</summary>
		TheirLife,

		/// <summary>
		/// Our evaluation as the search's own turn ends (AI-16): everything this turn did, both sides —
		/// so a won line that wastes our life or a card scores under one that doesn't. Their life alone
		/// burned our own face: hit there, they took 6 in the attack (9 → 3); hit at their face, they
		/// blocked everything and stayed at 6 — the self-burn "pressed harder", and our 3 life counted
		/// for nothing in a won line. Squashed under <see cref="TurnEndCap"/>, so a sooner win still beats
		/// a later one, and never clamped, so no two lines tie at the cap. A win inside the search's own
		/// turn has no turn end: it counts their life (how far under 0), as TheirLife. No worse than
		/// TheirLife over 192 games (51.6% ± 0.9pp, AI-16.3).
		/// </summary>
		TurnEnd,
	}

	/// <summary>
	/// The most a won line's progress may add or take away: under half the smallest gap between two
	/// turns' discounted wins within the horizon (the last turn's), so it never reorders turns —
	/// 214 at the defaults (0.95, two turns), 193 at the position suite's four. With a finished game's
	/// own board tie-break (<see cref="Evaluator.EndBoardCap"/>, 10) two lines still move at most 2 × (cap +
	/// 10) — 448 against a 475 gap at the defaults, 406 against 428 at four turns.
	/// </summary>
	public static double TurnEndCap(Settings settings) =>
		0.45
		* Evaluator.WinScore
		* Math.Pow(settings.TerminalDiscount, settings.LookaheadTurns - 1)
		* (1 - settings.TerminalDiscount);

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
		/// <summary>Attacks and blocks with its board, and — while <see cref="Settings.TheirWalkers"/> is on — uses its planeswalkers; never casts, never plays a land.</summary>
		BoardOnly,

		/// <summary>Also plays a land and up to ActionsPerTurn spells in its own turn, by OUR policy — from its hand (only the cards we know under <see cref="Settings.KnownOnly"/>, the default); its walkers act by their effect while <see cref="Settings.TheirWalkers"/> is on, by OUR policy otherwise.</summary>
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
		var them = s.OpponentOf(me);
		GameState? turnEnd = null; // the root turn's end: the same moment for every candidate
		// ponytail: a rollout that hits DecisionLimit is scored where it stopped, mid-turn, beside
		// rollouts scored at the horizon. Only a loop that never advances reaches it; flag or drop
		// such a candidate if one is ever seen.
		for (var i = 0; i < settings.DecisionLimit && !s.IsOver(); i++)
		{
			if (s.GetGame().TurnNumber >= horizon)
				break;
			if (s.GetGame().TurnNumber != turn)
				(turn, plays, landDone) = (s.GetGame().TurnNumber, 0, false);
			if (turnEnd is null && turn > rootTurn)
				turnEnd = s;

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
				s = Choose(s, choice, who, settings);
				continue;
			}
			var spent = plays >= settings.ActionsPerTurn;
			var play = !(landDone && spent)
				? OurPlay(s, me, spent ? null : AnyPlay, ref landDone, settings)
				: null;
			if (play is not null)
			{
				s = Do(s, who, play);
				plays += play is PlayLandAction ? 0 : 1;
				continue;
			}
			s = Pass(s, who); // nothing worth playing
		}

		var score = Evaluator.Evaluate(s, me, settings.Held);
		if (!s.IsOver())
			return score;
		score *= Math.Pow(settings.TerminalDiscount, s.GetGame().TurnNumber - rootTurn);
		// under half a turn's discount (TurnEndCap), so a sooner win (or a later loss) still comes first, but no tie
		if (s.WinnerId() == me)
			return score + Progress(turnEnd ?? s, me, them, settings);
		return settings.LostLines && turnEnd is not null
			? score + TurnEndBoard(turnEnd, me, settings) // lost after our turn: the board we kept
			: score;
	}

	/// <summary>A won line's tie-break, read as the search's own turn ends (or the game, if it ends first).</summary>
	static double Progress(GameState s, int me, int them, Settings settings) =>
		settings.Progress switch
		{
			ProgressBy.TheirLife => -s.GetPlayer(them).Life,
			ProgressBy.TurnEnd when !s.IsOver() => TurnEndBoard(s, me, settings),
			ProgressBy.TurnEnd => -s.GetPlayer(them).Life, // won within the turn: how far under 0 they went
			_ => 0,
		};

	/// <summary>Our evaluation as the search's own turn ends, squashed under <see cref="TurnEndCap"/> — never clamped, so no two lines tie at it.</summary>
	static double TurnEndBoard(GameState s, int me, Settings settings) =>
		TurnEndCap(settings)
		* Math.Tanh(Evaluator.Evaluate(s, me, settings.Held) / TurnEndCap(settings));

	/// <summary>
	/// One decision of the simulated opponent — the ONE place the opponent model is decided: attacks
	/// and blocks judged, a "you may" its best (<see cref="Settings.Mays"/>), every other choice the cheap
	/// answer, (CastsFromHand, Responds) its own
	/// sorcery-speed plays, its planeswalkers' best abilities (TheirWalkers), and (Responds) its
	/// instant-speed answers in our turn; every other priority passed. The search answers the opponent through here too, so its tree and its rollouts agree.
	/// </summary>
	public static GameState OpponentStep(GameState s, int who, Settings settings) =>
		s.GetPendingChoice() is { } choice ? Choose(s, choice, who, settings)
		: settings.Opponent != OpponentModel.BoardOnly && TheirPlay(s, who, settings) is { } play
			? Do(s, who, play)
		: settings.TheirWalkers && TheirLoyalty(s, who, settings) is { } loyalty
			? Do(s, who, loyalty)
		: settings.Opponent == OpponentModel.Responds
		&& TheirResponse(s, who, settings) is { } response
			? Do(s, who, response)
		: Pass(s, who);

	[ThreadStatic]
	static Action<GameState, int, AiDecision>? trace;

	/// <summary>
	/// <paramref name="work"/> with every move the rollout and the simulated opponent make told to
	/// <paramref name="onMove"/> (the state before, who, the move) — what a look-ahead imagined, for
	/// <see cref="BeamSearchAi.Explain"/> (AI-17). Per thread: Explain replays on one, and a search's
	/// parallel workers never see it — off, a move costs a field read. ponytail: our GreedyAi policy's
	/// plays and the forced passes (AutoPass) aren't told; their effects still show.
	/// </summary>
	public static T Traced<T>(Action<GameState, int, AiDecision> onMove, Func<T> work)
	{
		var outer = trace;
		trace = onMove;
		try
		{
			return work();
		}
		finally
		{
			trace = outer;
		}
	}

	/// <summary>A choice answered — a declaration's first attacker by a whole attack (<see cref="Attacks.AsOne"/>), the rest by <see cref="Answer"/>.</summary>
	static GameState Choose(GameState s, ChoiceAction choice, int who, Settings settings) =>
		choice is DeclareAttackerChoice ask
		&& settings.Attacks == Attacks.AsOne
		&& WholeAttacks.IsFirst(s, ask)
			? WholeAttacks.Declare(
				s,
				who,
				WholeAttacks.Best(s, who, settings.Held),
				(at, ids) => Do(at, who, ids)
			)
			: Do(s, who, Answer(s, choice, who, settings));

	static GameState Do(GameState s, int who, GameAction action)
	{
		if (trace is not { } told)
			return s.Act(action);
		var d = AiDecision.Acting(s, action);
		told(s, who, d);
		return d.State;
	}

	static GameState Do(GameState s, int who, IEnumerable<int> answer)
	{
		if (trace is not { } told)
			return s.Choose(answer);
		var d = AiDecision.Answering(s, [.. answer]);
		told(s, who, d);
		return d.State;
	}

	/// <summary>
	/// The opponent's planeswalkers act (AI-8.1, Shayne: "assume the opponent will use its best
	/// planeswalker ability"): in its own turn at sorcery speed — after a casting model's own plays —
	/// the loyalty ability whose EFFECT is best, never weighed against not activating. Judged by what it
	/// does, not what it costs the walker: the walker's own worth is added back, since by the evaluator
	/// alone cashing a Jace at 3 for its card lost to a surveil (verified: it surveiled). So a Jace at 3
	/// draws next turn and at 1 or 2 surveils — what it WOULD do is what makes it worth attacking. A
	/// walker is on the board, so even the board-only opponent uses it; once a turn by the rules.
	/// ponytail: a minus that kills its own walker (a Jace at 1 surveiling) is taken like any other —
	/// a real player might keep the token for the next Empower; weigh that if a walker misjudged so shows.
	/// </summary>
	/// <summary>A planeswalker of <paramref name="who"/>'s that hasn't used its loyalty this turn.</summary>
	static bool HasUnusedWalker(GameState s, int who) =>
		s.CardsIn(who, ZoneType.Battlefield)
			.Any(c =>
				c.Is(CardType.Planeswalker) && c.LoyaltyActivatedOnTurn != s.GetGame().TurnNumber
			);

	static GameAction? TheirLoyalty(GameState s, int who, Settings settings)
	{
		if (!Priority.AtSorcerySpeed(s, who) || !HasUnusedWalker(s, who))
			return null;
		return s.DistinctMoves(s.LegalActions())
			.OfType<ActivateLoyaltyAction>()
			.MaxBy(a => WhatItDoes(s, a, who, settings));
	}

	static double WhatItDoes(GameState s, ActivateLoyaltyAction a, int who, Settings settings)
	{
		var after = Lookahead.Settle(s.Act(a));
		var walkerAfter =
			after.HasObject(a.CardId) && after.IsOnBattlefield(a.CardId)
				? Evaluator.Walker(after, after.GetCard(a.CardId), settings.Held)
				: 0;
		return Evaluator.Evaluate(after, who, settings.Held)
			+ Evaluator.Walker(s, s.GetCard(a.CardId), settings.Held)
			- walkerAfter;
	}

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
		var spells = s.DistinctMoves([.. s.LegalActions().Where(Allowed(s, who, settings))])
			.OfType<CastSpellAction>()
			.ToList();
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
		// No land in hand: no land drop to look for (a land played from another zone is missed).
		var landDone =
			tally.LandsPlayed >= 1 + tally.ExtraLandDrops
			|| !s.CardsIn(who, ZoneType.Hand).Any(c => c.Is(CardType.Land));
		// With TheirWalkers on, a walker is TheirLoyalty's alone — judged by its effect, after the plays.
		var playable =
			tally.SpellsCast < settings.ActionsPerTurn
				? (settings.TheirWalkers ? Spell : SpellOrLoyalty)
			: !settings.TheirWalkers && HasUnusedWalker(s, who) ? Loyalty
			: null;
		return landDone && playable is null
			? null
			: OurPlay(s, who, playable, ref landDone, settings, Allowed(s, who, settings));
	}

	/// <summary>
	/// What the opponent may play under <see cref="Settings.KnownOnly"/>: a card we know, or one from
	/// anywhere but its hand. Applied BEFORE DistinctMoves, which keeps one move per name — after it, an
	/// unknown copy could stand in for a known one and be dropped.
	/// ponytail: no unknown land either, so its mana doesn't grow in the look-ahead (as board-only);
	/// allow one land a turn if a known card it can't yet afford is ever misjudged.
	/// </summary>
	static Func<GameAction, bool> Allowed(GameState s, int who, Settings settings) =>
		!settings.KnownOnly
			? _ => true
			: a =>
				MagicRules.CardOf(a) is var id
				&& (
					id == 0
					|| !s.IsIn(id, who, ZoneType.Hand)
					|| s.GetCard(id).KnownTo.HasFlag(Knowers.Opponent)
				);

	static readonly Func<GameAction, bool> AnyPlay = a => a is not PassPriorityAction;
	static readonly Func<GameAction, bool> Spell = a => a is CastSpellAction;
	static readonly Func<GameAction, bool> SpellOrLoyalty = a =>
		a is CastSpellAction or ActivateLoyaltyAction;
	static readonly Func<GameAction, bool> Loyalty = a => a is ActivateLoyaltyAction;

	static GameState Pass(GameState s, int who) =>
		Do(
			s,
			who,
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
		Settings settings,
		Func<GameAction, bool>? allowed = null
	)
	{
		if (!Priority.AtSorcerySpeed(s, me))
			return null;
		var moves = s.DistinctMoves(
			allowed is null ? s.LegalActions() : [.. s.LegalActions().Where(allowed)]
		);
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
	/// facing lethal chump-blocks. Each creature is judged alone (in the look-ahead the rest don't attack,
	/// and block only what they survive or join a block already made — Lookahead.Joins, AI-15) unless <see cref="Attacks.AllInToo"/> weighs "all attack" too. A target choice takes
	/// its owner's best under <see cref="Targets.OwnersBest"/>; a choice of none or one — a "you may", an
	/// optional target — its chooser's best under <see cref="Settings.Mays"/>, a pay-to-keep excepted (the
	/// cheap answer pays); every other choice the cheap answer.
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
			{ MinChoices: 0, MaxChoices: 1 } and not PayToKeepChoice when settings.Mays =>
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
	/// Measured WORSE (AI-3.8) while the look-ahead's defender never chump-blocked, so an all-in looked
	/// lethal where a defender would chump; since AI-17.3 it chumps facing lethal (Lookahead.Chumps) —
	/// re-measure before reading that result as a ceiling.
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
