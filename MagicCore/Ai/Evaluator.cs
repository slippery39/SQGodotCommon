using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Turns;

namespace MagicCore.Ai;

/// <summary>
/// How good a position is for one player — the score every MagicCore AI compares moves by.
/// Card-agnostic: it reads the board, life and hand, never card text.
/// </summary>
public static partial class Evaluator
{
	/// <summary>A non-land card in hand when nothing measures it — the flat price every AI began with.</summary>
	public const double FlatHandCard = 1.2;

	/// <summary>A land in hand: less than a spell, so playing even a surplus land scores a gain.</summary>
	public const double LandInHand = 0.3;

	/// <summary>A card exiled "you may play it" is worth this much of an unknown card (Shayne's 0.8, AI-9.3), the next one <see cref="ImpulseDiminish"/> times the last.</summary>
	public const double ImpulseShare = 0.8;

	public const double ImpulseDiminish = 0.5;

	static readonly AsyncLocal<HashSet<int>?> rootImpulse = new();

	static readonly AsyncLocal<Dictionary<int, int>?> rootOptionTurns = new();

	/// <summary>
	/// <paramref name="work"/> with the cards exiled "you may play" at <paramref name="root"/> remembered as real, and each
	/// player's <see cref="OptionTurns"/> read there (<see cref="Terms.RootOptionTurns"/>).
	/// </summary>
	public static T WithRootImpulse<T>(GameState root, Func<T> work)
	{
		var (outer, outerTurns) = (rootImpulse.Value, rootOptionTurns.Value);
		rootImpulse.Value = [.. ImpulseCards(root, -1).Select(c => c.Id)];
		rootOptionTurns.Value =
			IsOff(Terms.RootOptionTurns) || IsOff(Terms.HoldOption)
				? null
				: new[] { root.Player1Id(), root.Player2Id() }.ToDictionary(
					p => p,
					p => LiveOptionTurns(root, p)
				);
		try
		{
			return work();
		}
		finally
		{
			(rootImpulse.Value, rootOptionTurns.Value) = (outer, outerTurns);
		}
	}

	/// <summary>Cards in exile with a live play permission for <paramref name="player"/> (-1: anyone's).</summary>
	static IEnumerable<Card> ImpulseCards(GameState s, int player) =>
		new[] { s.Player1Id(), s.Player2Id() }
			.SelectMany(o => s.CardsIn(o, ZoneType.Exile))
			.Where(c =>
				c.GetComponent<PlayableFromExile>() is { } p
				&& (player < 0 || p.PlayerId == player)
				&& s.GetGame().TurnNumber <= p.UntilTurn
			);

	/// <summary>An impulse card made INSIDE the search (not there at the root): the sample's luck, never played (<see cref="Terms.ImpulseValue"/>).</summary>
	public static bool IsPhantomImpulse(int cardId, GameState s) =>
		!IsOff(Terms.ImpulseValue)
		&& rootImpulse.Value is { } root
		&& !root.Contains(cardId)
		&& s.HasObject(cardId)
		&& s.GetObject(cardId) is Card c
		&& c.GetComponent<PlayableFromExile>() is { Blind: true }
		&& s.IsIn(cardId, c.OwnerId, ZoneType.Exile);

	/// <summary>The legal moves for the search: without playing a phantom impulse card (<see cref="Terms.ImpulseValue"/>).</summary>
	public static ImmutableList<GameAction> SearchMoves(this GameState s) =>
		[
			.. s.LegalActions()
				.Where(a =>
					MagicRules.CardOf(a) is var id && (id == 0 || !IsPhantomImpulse(id, s))
				),
		];

	/// <summary>What the phantom impulse cards of <paramref name="player"/> are credited: a share of an unknown card each, fading.</summary>
	static double ImpulseCredit(GameState s, int player, HeldValues held) =>
		IsOff(Terms.ImpulseValue)
			? 0
			: ImpulseCards(s, player)
				.Where(c => IsPhantomImpulse(c.Id, s))
				.Select((_, i) => ImpulseShare * Math.Pow(ImpulseDiminish, i) * held.Unknown)
				.Sum();

	/// <summary>A land in hand that unlocks nothing (<see cref="Terms.LandUnlock"/>): a land to pitch or bin.</summary>
	public const double LandSurplus = 0.05;

	/// <summary>
	/// Position score for <paramref name="player"/>. Permanent value counts printed stats plus counters, not
	/// until-end-of-turn pumps — a pump is worth only what it changes before it wears off.
	/// <paramref name="held"/>: cards in hand priced by their measured value (AI-1) instead of the
	/// flat price; null — GreedyAi, the card-value sandbox — is the flat price.
	/// </summary>
	public static double Evaluate(GameState s, int player, HeldValues? held = null)
	{
		if (!s.IsOver())
			return Position(s, player, held);
		var end = s.WinnerId() == player ? WinScore : -WinScore;
		return IsOff(Terms.EndBoard)
			? end
			: end + EndBoardCap * Math.Tanh(Position(s, player, held) / EndBoardScale);
	}

	/// <summary>
	/// The most a finished game's board adds to or takes from its win or loss (<see cref="Terms.EndBoard"/>):
	/// small beside a turn's discount (Rollout.TurnEndCap and BeamSearchAi.Winner allow for it), so it only
	/// orders games that end alike.
	/// </summary>
	public const double EndBoardCap = 10;

	/// <summary>
	/// The position at which a finished game's tie-break is three-quarters spent. Wide: an ended game's
	/// position is ±60 and more (the dead player's life alone is −50), so squashed by the cap itself the
	/// boards differed by 1e-5 and past ~190 tied exactly again (the review, AI-17.6).
	/// </summary>
	public const double EndBoardScale = 100;

	static double Position(GameState s, int player, HeldValues? held)
	{
		var opponent = s.OpponentOf(player);
		var keywords = Keywords;
		var sides =
			Side(s, player, held, own: true, keywords).Total
			- Side(s, opponent, held, own: false, keywords).Total;
		return IsOff(Terms.Threat)
			? sides
			: sides + Pressure(s, player, opponent) - Pressure(s, opponent, player);
	}

	/// <summary>One side's terms — <see cref="Evaluate"/> sums them, <see cref="Explain"/> lists them.</summary>
	public readonly record struct SideTerms(
		double Creatures,
		double Walkers,
		double Lands,
		double Others,
		double Hand,
		double Life,
		double Graveyard
	)
	{
		public double Total => Creatures + Walkers + Lands + Others + Hand + Life + Graveyard;
	}

	/// <summary>
	/// <see cref="Evaluate"/> term by term — ours, theirs (negated) and both clocks — summing to it, for a
	/// reader (the decision report, an inspector). The same <see cref="Side"/> and <see cref="Pressure"/>:
	/// never a second copy of the sum.
	/// </summary>
	public static IReadOnlyList<(string Term, double Value)> Explain(
		GameState s,
		int player,
		HeldValues? held = null
	)
	{
		if (s.IsOver())
			return [("game over", Evaluate(s, player, held))];
		var opponent = s.OpponentOf(player);
		var (ours, theirs) = (
			Side(s, player, held, own: true, Keywords),
			Side(s, opponent, held, own: false, Keywords)
		);
		var threat = !IsOff(Terms.Threat);
		return
		[
			("our creatures", ours.Creatures),
			("our walkers", ours.Walkers),
			("our lands", ours.Lands),
			("our other permanents", ours.Others),
			("our hand", ours.Hand),
			("our life", ours.Life),
			("our graveyard", ours.Graveyard),
			("their creatures", -theirs.Creatures),
			("their walkers", -theirs.Walkers),
			("their lands", -theirs.Lands),
			("their other permanents", -theirs.Others),
			("their hand", -theirs.Hand),
			("their life", -theirs.Life),
			("their graveyard", -theirs.Graveyard),
			("our clock on them", threat ? Pressure(s, player, opponent) : 0),
			("their clock on us", threat ? -Pressure(s, opponent, player) : 0),
		];
	}

	/// <summary>
	/// The evaluator's terms that can be switched off — each added by a measured story, so each can be
	/// measured without — and the look-ahead's beside them (<see cref="BlockSurvival"/>,
	/// <see cref="GangBlocks"/>).
	/// </summary>
	[Flags]
	public enum Terms
	{
		None = 0,

		/// <summary>The clock that counts blockers (AI-5.2, `+nothreat`).</summary>
		Threat = 1,

		/// <summary>Vigilance, haste, reach, hexproof, indestructible and ward among a creature's valued keywords (AI-5.3, `+nokeywords`).</summary>
		MoreKeywords = 2,

		/// <summary>
		/// Not the evaluator's own, but the look-ahead's beside it: double strike, deathtouch and marked
		/// damage in "blocks only what it survives" (<see cref="Lookahead.QuickAnswer"/>, AI-8.3, `+oldblocks`).
		/// </summary>
		BlockSurvival = 4,

		/// <summary>A trampler's excess over its blocker in the threat term (AI-8.4, `+notrample`).</summary>
		Trample = 8,

		/// <summary>A planeswalker priced by what its best ability makes, not a flat body (AI-8.2, `+walkercount`).</summary>
		WalkerUse = 16,

		/// <summary>
		/// A card in the opponent's hand that we KNOW (<see cref="Card.KnownTo"/> — a creature we bounced,
		/// a card they revealed) priced as itself, not as an unknown card (AI-9.5, Shayne, `+blindhand`) — a
		/// creature at what it is on the board less the mana to cast it again (AI-13.4).
		/// </summary>
		KnownHand = 32,

		/// <summary>
		/// A held instant or flash card of ours priced as an OPTION (AI-13, Shayne: "the expected value of
		/// playing the spell now vs holding for a better situation"): the expected best of the offers it
		/// meets over the turns left (<see cref="CastGains"/>, <see cref="OptionTurns"/>) — so casting it
		/// needs a gain at least that, and the bar falls as the game shortens (`+nooption`). A targeted
		/// sorcery-speed card too, at one turn (<see cref="TargetedOptions"/>); either one short of mana is
		/// discounted as any held card (<see cref="ManaShort"/>).
		/// </summary>
		HoldOption = 64,

		/// <summary>
		/// The look-ahead's, beside the evaluator: a blocker joins a block already declared when together
		/// they kill the attacker, or menace needs a second (<see cref="Lookahead.QuickAnswer"/>, AI-15,
		/// `+nogang`) — without it a menace attacker was unblockable in every look-ahead.
		/// </summary>
		GangBlocks = 128,

		/// <summary>
		/// The look-ahead's defender chump-blocks when facing lethal (story AI-17.3, `+nochump`) — without
		/// it a lone attacker at a player low on life looked lethal however many blockers they had, so a
		/// second attacker added nothing (Shayne's turn 21: the Technician alone "killed", the rollout's
		/// own defender chumped it, the lethal attack of both was never made).
		/// </summary>
		ChumpBlocks = 256,

		/// <summary>
		/// A finished game keeps its board as a tie-break under its win or loss (AI-17.6, Shayne: "we want to
		/// make the best play possible even if our search shows that we are winning or losing no matter what
		/// … players can always make mistakes"). Without it every finished game scored a flat ±WinScore, so
		/// every choice ending one tied: the look-ahead's defender, dead to an unblockable flier, blocked
		/// nothing, and our 2/2 attacked beside the flier for free (`+flatend`).
		/// </summary>
		EndBoard = 512,

		/// <summary>
		/// Below 6 life a lost point costs more the nearer 0 — (6 − life)² ÷ 2 on top: 0.5 at 5, 12.5 at 1
		/// (AI-17.6, Shayne: "As your life total gets lower you take on more risk of your opponent having
		/// something that can just kill you"). Without it 5 life → 1 cost 8 points, under a dead 2/2 and
		/// their weaker clock, and the AI drew two cards into a swing to 1 (`+flatlowlife`).
		/// </summary>
		LowLifeRisk = 1024,

		/// <summary>
		/// The look-ahead's, beside the evaluator: <see cref="Lookahead.Settle"/> answers one target or a
		/// single "you may" by its chooser's best (AI-17.6, review item 1) — before, the first legal target
		/// and "no" (`+quicksettle`). Two levels deep and for every single pick under <see cref="SettleDeep"/>.
		/// </summary>
		SettleChoices = 2048,

		/// <summary>
		/// A card in a graveyard that can return itself (an activated ability usable there that returns its
		/// source) priced as a card in hand less the mana to bring it back (<see cref="RecastPerMana"/>) —
		/// AI-17.6, Shayne: "basically like paying a cost to be able to have access to a card. So that should
		/// be priced in." Without it every graveyard card was worth nothing, so Way of the Deathbringer's −2
		/// sacrificed Tinybones and kept Blessed Ghoul ("{2}{W/B}: Return this card from your graveyard to your
		/// hand"), where the Ghoul comes back to do it again (`+deadgraveyard`).
		/// </summary>
		GraveyardAccess = 4096,

		/// <summary>
		/// The look-ahead's: <see cref="SettleChoices"/> two choices deep, and for every single pick — Lich's
		/// Relic's "you may pay {2}. When you do, destroy target …" read as paying for nothing one level down,
		/// and an edict's sacrifice took the first listed (AI-17.6, the review of item 1) — `+shallowsettle`.
		/// </summary>
		SettleDeep = 8192,

		/// <summary>
		/// A planeswalker priced by its value over its lifetime (AI-9.3, Shayne: "value over time engines"):
		/// what its abilities make each turn it lives, the turns it lives judged by the pressure on it, and
		/// the damage it soaks (<see cref="WalkerLifetime"/>) — without it, half of one best activation
		/// (AI-8.2's price, `+walkernow`).
		/// </summary>
		WalkerLifetime = 16384,

		/// <summary>
		/// The simulated opponent's trick in its OWN combat, once we have blocked — a spell, or a mana-costed
		/// ability (Astute Terraformer's pump) — so a block that loses to open mana is seen (AI-17.10, Shayne's
		/// turn 3: the AI blocked, he pumped, and it lost a creature for nothing; `+notheircombat`).
		/// </summary>
		TheirCombatTricks = 32768,

		/// <summary>
		/// A simulated attacker judges each attack against the defender the rollout will actually play (the
		/// blocks <see cref="Rollout.OpponentStep"/> chooses), not the look-ahead's weaker one — so it stops
		/// attacking into blocks it didn't see (AI-19.21: Shayne, "I wouldn't attack"; `+nodefender`).
		/// </summary>
		TrueDefender = 65536,

		/// <summary>
		/// A surveil waiting on the stack counts for what it is worth, a third of a card (<see cref="SurveilShare"/>,
		/// Shayne) - the rollout's one sampled top card cannot show it, so it tied with a mode that does nothing
		/// (AI-17.11, Dominion Assault Force: "surveiling here is 100% the correct play"; `+nosurveilprice`).
		/// </summary>
		SurveilPrice = 131072,

		/// <summary>
		/// The search never passes while a land play is legal (Shayne, 2026-11-08: turn 1, three Forests in hand, the AI
		/// passed and discarded to hand size - "pretty much a 0% chance this was correct"). The land costs nothing to
		/// play first, whatever else follows, but a 4-turn rollout scores the mana it gives only by the land's flat
		/// price, and a discarded card with a graveyard ability (Photosynthetic Physician) came back as an Officer
		/// inside the horizon - so passing scored 5.85 better (`+nolanddrop`).
		/// </summary>
		LandDrop = 262144,

		/// <summary>
		/// The search is not offered a station activation once the Spacecraft has its threshold (Shayne, 2026-11-09: 17 counters
		/// on a Sarcophagus that animates at 10). The evaluator cannot price a counter that adds nothing, and the useless
		/// activation was a free first step on a won line (`+nostationcap`).
		/// </summary>
		StationCap = 524288,

		/// <summary>
		/// A land in our hand is worth what it unlocks (AI-9.1, Shayne 2026-11-09), not a flat 0.3: a card we hold that
		/// we could not cast without it, or more cards cast by next turn (<see cref="LandUnlock"/>); a surplus land is
		/// worth <see cref="LandSurplus"/> (`+nolandunlock`).
		/// </summary>
		LandUnlock = 1048576,

		/// <summary>
		/// A surveil bins a land that <see cref="LandUnlock"/> says unlocks nothing, and keeps one that does - a rule, not
		/// a rollout (AI-17.20, Shayne 2026-11-09): the rollout keeps one sampled top card, so binning never gained
		/// (`+nosurveillands`). Spells on top are still branched.
		/// </summary>
		SurveilLands = 2097152,

		/// <summary>
		/// A surveil bins a spell priced below the expected replacement and keeps one at or above it (AI-17.20, the
		/// spells half): the next draw is a known card or an unknown one, <see cref="HeldValues.Unknown"/> on average
		/// (`+nosurveilspells`). Needs the held table; every card on top is then decided, none branched.
		/// </summary>
		SurveilSpells = 4194304,

		/// <summary>
		/// "Exile the top card, you may play it" is worth a RANDOM card, not the one the search sampled (AI-17.16, Shayne
		/// 2026-11-09: "it shouldn't matter what the exact top card is"): inside the search a card exiled this way after the
		/// root can't be played - the sample's luck - and is credited <see cref="ImpulseShare"/> of an unknown card, each
		/// further one <see cref="ImpulseDiminish"/> times the one before (you can only play so much). Cards already exiled
		/// at the root are real and play normally (`+noimpulsevalue`).
		/// </summary>
		ImpulseValue = 8388608,

		/// <summary>
		/// A card we hold is worth less the further we are from casting it (AI-17.21, Shayne 2026-11-09: "the Borg is a
		/// fine 5 drop if you can get it out, but its not worth keeping over a land when you have only 2 lands in play"):
		/// its held price times <see cref="ManaShortShare"/> per mana source short of its mana value past the first, counting
		/// our lands in hand. One short is free - the next land draw ("if you had 4 lands, maybe you don't landcycle because
		/// you could draw a land and play it"); discounting it made one sampled land at the horizon swing a hand of 4-drops
		/// by 11 points (the Tasha position). Without it a 5-drop on 2 lands was priced as castable, and landcycling it
		/// lost (`+nomanashort`).
		/// </summary>
		ManaShort = 16777216,

		/// <summary>
		/// A held option's turns left (<see cref="OptionTurns"/>) are read at the search's root, not in each line it
		/// compares (AI-13.9): read per line, any line that shortened the game - an attack - also marked down its
		/// attacker's held instants, so the simulated Shayne at 4 life would not swing his hasty 2/1 at the AI on 4
		/// (`PatrolRacesIntoDrain`, once TRK's instants had offers). The bar still falls as the game shortens, decision
		/// by decision (`+liveoptionturns`).
		/// </summary>
		RootOptionTurns = 134217728,

		/// <summary>
		/// The look-ahead's, beside the evaluator: the simulated opponent uses a mana-costed ability of its own permanents
		/// in its own turn when it beats passing (<see cref="Rollout"/>'s TheirAbility, AI-17.22) - without it a visible
		/// "each opponent loses 2 life" never fired, and the AI raced at 4 into it (`+notheirabilities`).
		/// </summary>
		TheirAbilities = 33554432,

		/// <summary>
		/// The next attack meets each creature as the cleanup leaves it (AI-17.9, Shayne: note it, fix later): La'An's
		/// "can't block this turn", a flier or "can't be blocked" until end of turn no longer read as lasting into next
		/// turn's clock - as its toughness and power pumps already weren't (`+thisturngrants`).
		/// </summary>
		NextTurnGrants = 67108864,

		/// <summary>
		/// Burn at their face waits for a kill within two turns (AI-17.6's burn rule, Shayne 2026-11-09: "save it until
		/// you can actually kill the opponent"): the search leaves out a damage-only spell at their face, or a
		/// damage-only enters-trigger permanent with nothing else of theirs to hit, unless the reachable damage this
		/// turn and next kills (<see cref="BeamSearchAi"/>'s KillsWithinTwoTurns) - their low life priced the chip
		/// damage above the card (`+faceburn`).
		/// </summary>
		SaveBurn = 268435456,

		/// <summary>
		/// A sorcery-speed card with a target to wait for - removal, burn, an Aura, a creature whose enters needs a target -
		/// held as an option too, as instants are (<see cref="CastGains.IsOption"/>; Shayne 2026-11-09, AI-13.10): worth its
		/// offers' average, ONE future turn - holding it costs a turn of tempo, where an instant keeps the best of the turns
		/// left (`+instantoptions`).
		/// </summary>
		TargetedOptions = 536870912,
	}

	/// <summary>
	/// A held card's price kept per mana source it is short past the first (<see cref="Terms.ManaShort"/>) - chosen, not
	/// measured: two short keeps 0.6 of it, three short (Shayne's 5-drop on 2 lands) 0.36.
	/// </summary>
	public const double ManaShortShare = 0.6;

	/// <summary>
	/// What <paramref name="player"/> could tap for a held card: mana sources in play plus lands in hand.
	/// ponytail: mana value only - colours, X and one land drop a turn are not counted; LandUnlock's pip check if a
	/// position shows a card short only on colour.
	/// </summary>
	static int ManaWithin(GameState s, int player) =>
		s.CardsIn(player, ZoneType.Battlefield).Count(c => ManaAbilities.ColorsOf(s, c).Any())
		+ s.CardsIn(player, ZoneType.Hand).Count(c => c.Is(CardType.Land));

	/// <summary>
	/// What casting a known creature again costs them, a point of mana at this (AI-13.4) — chosen, not
	/// measured: a land in play is worth 1.5, but this mana is spent once.
	/// </summary>
	public const double RecastPerMana = 0.5;

	/// <summary>The most turns of offers an option counts — beyond, the game's length is a guess (AI-13).</summary>
	public const int MaxOptionTurns = 4;

	/// <summary>
	/// The turns of offers a held card of <paramref name="player"/>'s has left: the sooner of the two
	/// clocks (<see cref="TurnsToKill"/>), 1 to <see cref="MaxOptionTurns"/>.
	/// ponytail: the clocks of the board as it stands; a game's real length moves with every play.
	/// </summary>
	public static int OptionTurns(GameState s, int player) =>
		!IsOff(Terms.RootOptionTurns)
		&& rootOptionTurns.Value is { } root
		&& root.TryGetValue(player, out var turns)
			? turns
			: LiveOptionTurns(s, player);

	static int LiveOptionTurns(GameState s, int player)
	{
		var opponent = s.OpponentOf(player);
		var left = Math.Min(TurnsToKill(s, opponent, player), TurnsToKill(s, player, opponent));
		return Math.Clamp(left, 1, MaxOptionTurns);
	}

	/// <summary>The keywords a creature's value counts here (<see cref="Terms.MoreKeywords"/>).</summary>
	static string[] Keywords => IsOff(Terms.MoreKeywords) ? FirstKeywords : ValuedKeywords;

	/// <summary>Whether <paramref name="term"/> is switched off here (<see cref="Without{T}"/>).</summary>
	public static bool IsOff(Terms term) => (off.Value & term) != 0;

	/// <summary>
	/// <paramref name="work"/> evaluated without <paramref name="terms"/> — a search measured without them,
	/// or the card-value sandbox. Flows with the execution context into the search's parallel workers,
	/// as MagicRules.CountingSteps does.
	/// </summary>
	public static T Without<T>(Terms terms, Func<T> work)
	{
		var outer = off.Value;
		off.Value = outer | terms;
		try
		{
			return work();
		}
		finally
		{
			off.Value = outer;
		}
	}

	static readonly AsyncLocal<Terms> off = new();

	/// <summary>
	/// Threat (story AI-5.2): how soon <paramref name="attacker"/>'s board kills <paramref name="defender"/>
	/// — <see cref="PressureWeight"/> × 20 ÷ the turns it needs: life over the power that gets past the
	/// defender's blockers in the next attack. Symmetric: the term that makes the AI respect a clock on
	/// itself makes it press one on them (MtgSimulator's race pressure, which never counted blockers).
	/// Monotonic, uncapped: more lasting power, less life, fewer blockers — more pressure. An opposing
	/// 2/2 at 4 life with no blocker weighs 5; against a blocker, nothing.
	/// Power is what lasts (pumps until end of turn wear off before the next attack); a creature dying
	/// to its damage neither attacks nor blocks; who can attack or block is Combat's rule.
	/// A trampler (AI-8.4) is met by the blocker that absorbs most, and gets past it what that blocker
	/// can't absorb by the next attack — its lasting toughness, or 1 against deathtouch: Yuriko's 2/3
	/// "stopped" a 7/5 trampler at 3 life, scoring a lethal board as no clock at all.
	/// Each creature is met as the cleanup leaves it (AI-17.9, <see cref="NextTurn"/>).
	/// ponytail: otherwise one blocker stops one attacker whatever their sizes; menace and first strike
	/// are ignored. Blockers go
	/// to attackers in order, never two to one trampler, and a non-trampler may take the blocker a
	/// trampler would have met — so a trampler beside a bigger attacker can be overstated. Count them
	/// if a misjudged race is ever shown.
	/// </summary>
	static double Pressure(GameState s, int attacker, int defender)
	{
		var life = s.GetPlayer(defender).Life;
		if (life <= 0)
			return 0;
		return PressureWeight * 20.0 * Through(s, attacker, defender) / life;
	}

	/// <summary>
	/// Turns <paramref name="attacker"/>'s board needs to kill <paramref name="defender"/> at the damage
	/// that gets past its blockers now — the threat term's clock (int.MaxValue: no clock).
	/// </summary>
	public static int TurnsToKill(GameState s, int attacker, int defender)
	{
		var life = s.GetPlayer(defender).Life;
		var through = Through(s, attacker, defender);
		return life <= 0 ? 0
			: through <= 0 ? int.MaxValue
			: (life + through - 1) / through;
	}

	/// <summary>
	/// The power that gets past <paramref name="defender"/>'s blockers in <paramref name="attacker"/>'s next attack
	/// — each attacker's power changed by <paramref name="bonus"/> (a walker's pump or −N/−0, AI-9.3).
	/// </summary>
	internal static int Through(GameState s, int attacker, int defender, int bonus = 0)
	{
		var attackers = new List<(Card Card, int Power)>();
		foreach (var now in s.CardsIn(attacker, ZoneType.Battlefield))
			if (
				now.Is(CardType.Creature)
				&& Alive(s, now)
				&& NextTurn(now) is var c
				&& (!s.Has(c, "Defender") || s.Has(c, Combat.AttacksDespiteDefender))
				&& s.Power(c) - Pumps(c).Power + bonus is > 0 and var power
			)
				attackers.Add((c, power));
		if (attackers.Count == 0)
			return 0;
		// A tapped blocker blocks the next attack only if the defender untaps first: the attacker's own
		// combat this turn is over, so its next attack comes after the defender's turn.
		var game = s.GetGame();
		var untapsFirst = game.ActivePlayerId == attacker && game.Step >= Step.EndCombat;
		var blockers = new List<Card>();
		foreach (var c in s.CardsIn(defender, ZoneType.Battlefield))
			if (c.Is(CardType.Creature) && Alive(s, c) && (untapsFirst || !c.IsTapped))
				blockers.Add(NextTurn(c.IsTapped ? c with { IsTapped = false } : c));
		// The defender blocks the biggest it can — fliers first, which only fliers and reach stop.
		var through = 0;
		foreach (
			var (card, power) in attackers
				.OrderByDescending(a => s.Has(a.Card, "Flying"))
				.ThenByDescending(a => a.Power)
		)
		{
			if (!IsTrampler(card))
			{
				var blocker = blockers.FindIndex(b => Combat.CanBlock(s, b, card));
				if (blocker < 0)
					through += power;
				else
					blockers.RemoveAt(blocker);
				continue;
			}
			// What a blocker absorbs by the NEXT attack, as power is: pumps and marked damage gone.
			var deathtouch = s.Has(card, "Deathtouch");
			int Absorbs(Card b) =>
				deathtouch ? 1 : Math.Max(1, s.Toughness(b) - Pumps(b).Toughness);
			var best = blockers.Where(b => Combat.CanBlock(s, b, card)).MaxBy(Absorbs);
			if (best is null)
				through += power;
			else
			{
				through += Math.Max(0, power - Absorbs(best));
				blockers.Remove(best);
			}
		}
		return through;

		// Trample that lasts to the next attack (AI-8.4) — not one granted until end of turn.
		bool IsTrampler(Card c) =>
			!IsOff(Terms.Trample)
			&& s.Has(c, "Trample")
			&& Lasting(
				s,
				c,
				"Trample",
				[.. c.GetComponents<UntilEndOfTurn>().Where(u => u.UntilTurnOf == 0)]
			);
	}

	/// <summary>
	/// A creature as the cleanup leaves it (<see cref="Terms.NextTurnGrants"/>): every "until end of turn" effect gone and
	/// a copy until end of turn undone, as <c>Combat.CleanupPermanents</c> does.
	/// ponytail: control taken until end of turn still counts for the taker, and a lord that lost its abilities this
	/// turn still grants nothing (the board's statics are this turn's); read both from a cleaned board if a race is
	/// misjudged.
	/// </summary>
	static Card NextTurn(Card c)
	{
		if (
			IsOff(Terms.NextTurnGrants)
			|| !c.Components.Any(x => x is UntilEndOfTurn { UntilTurnOf: 0 } or CopyUntilEndOfTurn)
		)
			return c;
		var back = c.WithoutCopyUntilEndOfTurn();
		return back with
		{
			Components = back.Components.RemoveAll(x => x is UntilEndOfTurn { UntilTurnOf: 0 }),
		};
	}

	/// <summary>Not dying to what is marked on it now: the state-based check's own question.</summary>
	static bool Alive(GameState s, Card c)
	{
		var toughness = s.Toughness(c);
		return toughness > 0 && (toughness - c.Damage > 0 || s.IsIndestructible(c));
	}

	/// <summary>What its pumps until end of turn add — gone before the next turn ("until your next turn" lasts).</summary>
	static (int Power, int Toughness) Pumps(Card c)
	{
		var (power, toughness) = (0, 0);
		foreach (var component in c.Components)
			if (component is UntilEndOfTurn { UntilTurnOf: 0 } pump)
				(power, toughness) = (power + pump.Power, toughness + pump.Toughness);
		return (power, toughness);
	}

	/// <summary>
	/// Half: at a healthy life a forecast must not outweigh real damage — a free attack for 2 at 29 life
	/// (+1.0) must beat the 1 power it leaves unblocked (−0.5 here, −1.0 at full weight: a tie, and a
	/// tie takes the passive move). Near death it still dominates: a lethal swing-back at 3 life is −10.
	/// </summary>
	public const double PressureWeight = 0.5;

	static SideTerms Side(GameState s, int player, HeldValues? held, bool own, string[] keywords)
	{
		var board = s.CardsIn(player, ZoneType.Battlefield).ToList();
		var creatures = board
			.Where(c => c.Is(CardType.Creature))
			.Sum(c => CreatureValue(s, c, keywords));
		// Every term is MONOTONIC — more of a good thing always scores more, tapering instead of
		// capping. A cap makes "more" tie with "less", and a tie silently picks the passive move:
		// life capped at 20 stopped all attacks past tapped blockers once a drain deck pushed the
		// opponent above it; lands capped at 7 made an 8th land worth less in play than in hand.
		var lands = LandsValue(board.Count(c => c.Is(CardType.Land)));
		var walkers = board.Where(c => c.Is(CardType.Planeswalker)).Sum(c => Walker(s, c, held));
		var others = board
			.Where(c => !c.Is(CardType.Creature | CardType.Land | CardType.Planeswalker))
			.Sum(c => Other(s, c, keywords));
		var lifeValue = LifeValue(s.GetPlayer(player).Life);
		// A land in hand is worth less than a spell, so playing even a surplus land scores a gain.
		// Floating mana scores nothing: it empties at the end of the step, which Settle never reaches.
		// With held values the opponent's hand is BLIND — one price per card, land or not (HeldValues) —
		// except a card we know: a bounced creature is not "a card" (KnownHand, Shayne).
		// ponytail: under Hidden.Hand.Real (the default) a reveal played INSIDE the look-ahead prices
		// their real hidden cards — a peek; Hidden.Hand.ShuffleUnseen is honest. Price a card known only
		// at the search's root if a reveal spell is ever misjudged.
		// counted only when a held card could be short (lazily: Evaluate is the search's innermost loop)
		int? mana = null;
		bool Short(Card c) =>
			own
			&& !IsOff(Terms.ManaShort)
			&& c.ManaCost.ManaValue - 1 > (mana ??= ManaWithin(s, player));
		// a held price kept ManaShortShare per source short past the first - an option's too (AI-13.10's review: a 6-mana
		// targeted sorcery on two lands was priced as castable)
		double ShortOf(Card c, double price) =>
			Short(c)
				? Math.Max(
					FlatHandCard,
					price * Math.Pow(ManaShortShare, c.ManaCost.ManaValue - 1 - mana!.Value)
				)
				: price;
		var hand = s.CardsIn(player, ZoneType.Hand)
			.Sum(c =>
				held is not null
				&& !own
				&& !(c.KnownTo.HasFlag(Knowers.Opponent) && !IsOff(Terms.KnownHand))
					? held.Unknown
				: c.Is(CardType.Land)
					? (
						own && !IsOff(Terms.LandUnlock) && !LandUnlock.Unlocks(s, player, c)
							? LandSurplus
							: LandInHand
					)
				: held is null ? FlatHandCard
				: own
				&& !IsOff(Terms.HoldOption)
				&& CastGains.Fra.TryGetValue(c.Name, out var offers)
				&& CastGains.IsOption(s, c)
					// holding an instant costs nothing - it is cast in their turn; holding a sorcery-speed card costs a
					// turn of tempo, so it counts one future turn, its average use (AI-13.10, Shayne: "a tempo cost")
					? ShortOf(
						c,
						CastGains.ExpectedBest(
							offers,
							Priority.AtInstantSpeed(s, c) ? OptionTurns(s, player) : 1
						)
					)
				// a creature of theirs we KNOW they hold (one we bounced): what it is on the board, less
				// the mana to cast it again — a bounce gains the tempo, not the creature (AI-13.4, Shayne)
				: !own && c.Is(CardType.Creature) && !IsOff(Terms.KnownHand)
					? Math.Max(
						held.Of(c.Name),
						CreatureValue(s, c, keywords) - RecastPerMana * c.ManaCost.ManaValue
					)
				: ShortOf(c, held.Of(c.Name))
			);
		if (held is not null)
			hand += ImpulseCredit(s, player, held);
		var graveyard = IsOff(Terms.GraveyardAccess)
			? 0
			: s.CardsIn(player, ZoneType.Graveyard)
				.Sum(c =>
					ReturnCost(s, c) is { } cost
						? Math.Max(
							0,
							(
								c.Is(CardType.Land) ? LandInHand
								: held is null ? FlatHandCard
								: held.Of(c.Name)
							)
								- RecastPerMana * cost.ManaValue
						)
						: 0
				);
		return new(creatures, walkers, lands, others, hand, lifeValue, graveyard);
	}

	/// <summary>Lands in play: 1.5 each to 7, 0.4 beyond — the ONE land price (a walker's mana reads it too).</summary>
	static double LandsValue(int lands) => Taper(lands, 7, 1.5, 0.4);

	/// <summary>A player's life: the ONE life price (a walker's damage, life and soak read it too, AI-9.3).</summary>
	static double LifeValue(int life) =>
		life <= 0
			? -50
			: Taper(life, 20, 1.0, 0.5)
				+ (life < 6 ? life - 6 : 0) // low life hurts more
				- (life < 6 && !IsOff(Terms.LowLifeRisk) ? (6 - life) * (6 - life) / 2.0 : 0);

	/// <summary>
	/// The mana cost of the cheapest ability that returns this graveyard card itself (Blessed Ghoul's
	/// "{2}{W/B}: Return this card from your graveyard to your hand"), or null.
	/// ponytail: the mana only — an extra cost (a finality counter, "activate only if") is not priced, and a
	/// graveyard ability that exiles the card for an effect (Campus Crier) gives it no value; price them if
	/// such a card is ever misjudged.
	/// </summary>
	static ManaCost? ReturnCost(GameState s, Card c) =>
		s.ActivatedAbilitiesOf(c)
			.Where(a =>
				a.From == ZoneType.Graveyard
				&& a.Effects.Any(e =>
					e is ReturnFromGraveyard { What: var what } && what == Ref.Source
				)
			)
			.Select(a => a.Cost)
			.MinBy(cost => cost.ManaValue);

	/// <summary>
	/// A planeswalker's worth (AI-8.2, Shayne: loyalty "only matters as to what it can actually provide
	/// if you spend it"): HALF of what its best affordable loyalty ability makes — next turn's, and it
	/// can be attacked first — plus 0.35 a counter. Counted as loyalty alone, casting Way of the
	/// Wildspeaker (Jace +7, and "−4: a 4/4 Beast") scored +1.23 and lost to a 2-mana pump; the Beast it
	/// enables scores +13.40. An ability whose effect isn't priced here (<see cref="Makes"/>) leaves the
	/// old flat body of 2. Loyalty is spent, not banked — under half a card a counter: at 0.8 the AI
	/// never once used its Jace.
	/// </summary>
	public static double Walker(GameState s, Card c, HeldValues? held)
	{
		var loyalty = c.Loyalty * 0.35;
		if (IsOff(Terms.WalkerUse))
			return 2 + loyalty;
		if (!IsOff(Terms.WalkerLifetime))
			return WalkerLifetime(s, c, held);
		var best = 0.0;
		foreach (var a in Loyalty.AbilitiesOf(s, c))
			// "Activate only if" isn't checked here: a gated ability stays unpriced (the flat body).
			if (
				a.OnlyIf is null
				&& (a.MinusX ? c.Loyalty > 0 : a.Cost >= 0 || -a.Cost <= c.Loyalty)
			)
				best = Math.Max(best, Makes(a, held));
		// Never below the old body: more loyalty only ever adds affordable abilities, so the value
		// only rises with it (monotonic, as every term is). Half a card's draw stays under the body.
		return Math.Max(2, WalkerPotential * best) + loyalty;
	}

	/// <summary>What a walker's best ability is worth while unspent: half — it is next turn's.</summary>
	public const double WalkerPotential = 0.5;

	/// <summary>
	/// What a loyalty ability makes, read from its effects (never its text): a creature token's value,
	/// a card drawn at a held card's price. Anything else is 0 — not priced, so the walker keeps its
	/// flat body. ponytail: damage, mana, life, pumps and exile are unpriced, as is a token or draw
	/// wrapped in "you may" or "if", a token's counters, and a count that depends on the board (taken as
	/// 1); price one when a misjudged walker shows it.
	/// </summary>
	static double Makes(LoyaltyAbilityComponent a, HeldValues? held) =>
		a.Effects.Sum(e =>
			e switch
			{
				CreateTokens t
					when t.ControlledBy is null && t.Token.TypeLine.Contains("Creature") => Amount(
					t.Count
				) * TokenValue(t.Token),
				DrawCards d when d.Who == Ref.You => Amount(d.Amount)
					* (held?.Unknown ?? FlatHandCard),
				_ => 0,
			}
		);

	/// <summary>A count's number where it is fixed; 1 where it depends on the game — plus its Plus (Sickbay).</summary>
	static int Amount(Count n) => (n.Kind == CountOf.Fixed ? n.Fixed * n.Sign : 1) + n.Plus;

	/// <summary>A creature token as <see cref="CreatureValue"/> prices a fresh creature: printed stats and keywords.</summary>
	static double TokenValue(CardData token) =>
		Body(
			int.TryParse(token.Power, out var p) ? p : 0,
			int.TryParse(token.Toughness, out var t) ? t : 0,
			token.Keywords.Count(Keywords.Contains)
		);

	/// <summary>A creature's value from its lasting stats and valued keywords — the ONE formula.</summary>
	static double Body(int power, int toughness, int keywords) =>
		2 + power * 1.5 + toughness + keywords * 0.8;

	/// <summary>Full value per unit up to <paramref name="knee"/>, a smaller value per unit beyond.</summary>
	static double Taper(int n, int knee, double perUnit, double beyond) =>
		Math.Min(n, knee) * perUnit + Math.Max(0, n - knee) * beyond;

	/// <summary>The keywords a creature's value counts, 0.8 each — the first seven, then six more (AI-5.3, Shayne).</summary>
	static readonly string[] FirstKeywords =
	[
		"Flying",
		"Deathtouch",
		"Lifelink",
		"First strike",
		"Double strike",
		"Trample",
		"Menace",
	];

	static readonly string[] ValuedKeywords =
	[
		.. FirstKeywords,
		"Vigilance",
		"Haste",
		"Reach",
		"Hexproof",
		"Indestructible",
		"Ward",
	];

	/// <summary>An ended game's score: won, or lost (negative) — every "is it a win" threshold is read from it.</summary>
	public const double WinScore = 10_000;

	/// <summary>A creature's value as the evaluator counts it on the board — which one to chump with (Lookahead).</summary>
	public static double ValueOf(GameState s, Card c) => CreatureValue(s, c, Keywords);

	/// <summary>
	/// A permanent that is no creature, land or planeswalker: 2 — and a Spacecraft below its station
	/// threshold, that plus its share of the creature it becomes (charge counters ÷ threshold), so
	/// stationing scores (MC-5.2). Shayne: a simple formula, to iterate on — the creature tapped for it
	/// is priced only by the clock's untapped blockers. ponytail: the creature read off the card as it
	/// is, so its threshold keywords (flying) aren't counted until it has them.
	/// </summary>
	static double Other(GameState s, Card c, string[] keywords) =>
		c.GetComponent<StationComponent>() is { } station
			? 2.0
				+ (double)c.ChargeCounters
					/ station.CreatureAt
					* Math.Max(0, CreatureValue(s, c, keywords) - 2.0)
			: 2.0;

	static double CreatureValue(GameState s, Card c, string[] valued)
	{
		// Alive is decided by the real toughness (a pump that saved it counts); its VALUE by what
		// lasts past this turn — statics yes ("creatures you control have trample" stays), until-
		// end-of-turn pumps and grants no.
		if (s.Toughness(c) <= 0 || (s.Toughness(c) - c.Damage <= 0 && !s.IsIndestructible(c)))
			return 0; // the state-based check's own question: lethal damage doesn't kill indestructible
		var pumps = c.GetComponents<UntilEndOfTurn>().Where(u => u.UntilTurnOf == 0).ToList(); // "until your next turn" lasts
		var power = s.Power(c) - pumps.Sum(u => u.Power);
		var toughness = Math.Max(1, s.Toughness(c) - pumps.Sum(u => u.Toughness) - c.Damage);
		var keywords = valued.Count(k => Lasting(s, c, k, pumps));
		return Body(power, toughness, keywords);
	}

	/// <summary>
	/// A keyword it keeps: printed, or granted by more than a pump that ends this turn. Haste only while
	/// the creature is new — after its first turn haste does nothing (AI-5.3).
	/// ponytail: keywords read by name — a ward or hexproof given by a scripted component instead of the
	/// keyword ("Hexproof from …", a ward trigger with another cost) scores nothing; add them if a card
	/// with one is ever misjudged.
	/// </summary>
	static bool Lasting(GameState s, Card c, string keyword, List<UntilEndOfTurn> pumps) =>
		s.Has(c, keyword)
		&& (
			c.Keywords.Contains(keyword)
			|| c.HasKeywordCounter(keyword) // Drivnod's indestructible counter stays, pumped or not
			|| !pumps.Any(u => u.Keyword == keyword)
		)
		&& (keyword != "Haste" || Combat.IsSummoningSick(s, c));
}

/// <summary>
/// What a card in hand is worth when HELD: a fraction of its measured value (<see cref="CardValues"/>),
/// never less than the flat price — at 0 the search, whose ties go to acting, would throw zero-valued
/// tricks away. Story AI-1: the first, STATIC price of the option a held spell is (AI-5 prices it by
/// what may still come, time, mana, plan). Our own cards are priced by name, and so is an opponent's
/// card we KNOW (a creature we bounced — <see cref="Evaluator.Terms.KnownHand"/>, AI-9.5). The rest of
/// theirs are hidden, so each — land or not — gets one blind price: a limited deck's share of lands
/// at the land price, the rest at the average held value of a measured card. "They draw a card" costs
/// on the same scale as our own cards are worth.
/// Constant per card: it never moves with mana, which is how MtgSimulator's hand term made land drops
/// score negative (.claude/rules/sim-ai.md). A class, not a record: a `with` copy would keep a stale
/// blind price.
/// </summary>
public sealed class HeldValues
{
	/// <summary>17 lands in 40 — limited's usual share, for a card we can't see.</summary>
	public const double LandShare = 17.0 / 40;

	public HeldValues(IReadOnlyDictionary<string, CardValues.Entry> values, double fraction)
	{
		Values = values;
		Fraction = fraction;
		var spell = values
			.Values.Where(e => e.NotMeasured is null)
			.Select(e => Price(e, fraction))
			.DefaultIfEmpty(Evaluator.FlatHandCard)
			.Average();
		Unknown = LandShare * Evaluator.LandInHand + (1 - LandShare) * spell;
	}

	public IReadOnlyDictionary<string, CardValues.Entry> Values { get; }

	public double Fraction { get; }

	/// <summary>
	/// The tracked FRA table at <paramref name="fraction"/>. Throws if the table is missing: an empty one
	/// would price every card flat and make held values silently do nothing.
	/// </summary>
	public static HeldValues Fra(double fraction) =>
		CardValues.Fra.Count > 0
			? new(CardValues.Fra, fraction)
			: throw new InvalidOperationException(
				$"No card-value table embedded — run: dotnet run --project MagicConsole -- --card-values"
			);

	/// <summary>Our own card, by name; an unmeasured one (a land, a card the fixture can't cast) is flat.</summary>
	public double Of(string name) => Price(Values.GetValueOrDefault(name), Fraction);

	/// <summary>A card we can't see, land or spell.</summary>
	public double Unknown { get; }

	static double Price(CardValues.Entry? e, double fraction) =>
		e is { NotMeasured: null }
			? Math.Max(Evaluator.FlatHandCard, fraction * e.Value)
			: Evaluator.FlatHandCard;
}
