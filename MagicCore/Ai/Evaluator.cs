using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Turns;

namespace MagicCore.Ai;

/// <summary>
/// How good a position is for one player — the score every MagicCore AI compares moves by.
/// Card-agnostic: it reads the board, life and hand, never card text.
/// </summary>
public static class Evaluator
{
	/// <summary>A non-land card in hand when nothing measures it — the flat price every AI began with.</summary>
	public const double FlatHandCard = 1.2;

	/// <summary>A land in hand: less than a spell, so playing even a surplus land scores a gain.</summary>
	public const double LandInHand = 0.3;

	/// <summary>
	/// Position score for <paramref name="player"/>. Permanent value counts printed stats plus counters, not
	/// until-end-of-turn pumps — a pump is worth only what it changes before it wears off.
	/// <paramref name="held"/>: cards in hand priced by their measured value (AI-1) instead of the
	/// flat price; null — GreedyAi, the card-value sandbox — is the flat price.
	/// </summary>
	public static double Evaluate(GameState s, int player, HeldValues? held = null)
	{
		if (s.IsOver())
			return s.WinnerId() == player ? 10_000 : -10_000;
		var opponent = s.OpponentOf(player);
		var keywords = IsOff(Terms.MoreKeywords) ? FirstKeywords : ValuedKeywords;
		var sides =
			Side(s, player, held, own: true, keywords)
			- Side(s, opponent, held, own: false, keywords);
		return IsOff(Terms.Threat)
			? sides
			: sides + Pressure(s, player, opponent) - Pressure(s, opponent, player);
	}

	/// <summary>
	/// The evaluator's terms that can be switched off — each added by a measured story, so each can be
	/// measured without — and the look-ahead's one beside them (<see cref="BlockSurvival"/>).
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
	}

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
	/// ponytail: otherwise one blocker stops one attacker whatever their sizes; menace and first strike
	/// are ignored, and a keyword granted until end of turn still counts (trample excepted). Blockers go
	/// to attackers in order, never two to one trampler, and a non-trampler may take the blocker a
	/// trampler would have met — so a trampler beside a bigger attacker can be overstated. Count them
	/// if a misjudged race is ever shown.
	/// </summary>
	static double Pressure(GameState s, int attacker, int defender)
	{
		var life = s.GetPlayer(defender).Life;
		if (life <= 0)
			return 0;
		var attackers = new List<(Card Card, int Power)>();
		foreach (var c in s.CardsIn(attacker, ZoneType.Battlefield))
			if (
				c.Is(CardType.Creature)
				&& Alive(s, c)
				&& (!s.Has(c, "Defender") || s.Has(c, Combat.AttacksDespiteDefender))
				&& s.Power(c) - Pumps(c).Power is > 0 and var power
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
				blockers.Add(c.IsTapped ? c with { IsTapped = false } : c);
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
		return PressureWeight * 20.0 * through / life;

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

	static double Side(GameState s, int player, HeldValues? held, bool own, string[] keywords)
	{
		var board = s.CardsIn(player, ZoneType.Battlefield).ToList();
		var creatures = board
			.Where(c => c.Is(CardType.Creature))
			.Sum(c => CreatureValue(s, c, keywords));
		// Every term is MONOTONIC — more of a good thing always scores more, tapering instead of
		// capping. A cap makes "more" tie with "less", and a tie silently picks the passive move:
		// life capped at 20 stopped all attacks past tapped blockers once a drain deck pushed the
		// opponent above it; lands capped at 7 made an 8th land worth less in play than in hand.
		var lands = Taper(board.Count(c => c.Is(CardType.Land)), 7, 1.5, 0.4);
		var walkers = board.Where(c => c.Is(CardType.Planeswalker)).Sum(Walker);
		var others =
			board.Count(c => !c.Is(CardType.Creature | CardType.Land | CardType.Planeswalker))
			* 2.0;
		var life = s.GetPlayer(player).Life;
		var lifeValue = life <= 0 ? -50 : Taper(life, 20, 1.0, 0.5) + (life < 6 ? life - 6 : 0); // low life hurts more
		// A land in hand is worth less than a spell, so playing even a surplus land scores a gain.
		// Floating mana scores nothing: it empties at the end of the step, which Settle never reaches.
		// With held values the opponent's hand is BLIND — one price per card, land or not (HeldValues).
		var hand = s.CardsIn(player, ZoneType.Hand)
			.Sum(c =>
				held is not null && !own ? held.Unknown
				: c.Is(CardType.Land) ? LandInHand
				: held is null ? FlatHandCard
				: held.Of(c.Name)
			);
		return creatures + walkers + lands + others + hand + lifeValue;
	}

	/// <summary>
	/// A planeswalker's own worth: a body and its loyalty. Loyalty is spent, not banked — valued under
	/// half a card per counter, so trading three counters for a card scores as a gain. At 0.8 the AI
	/// never once used its Jace.
	/// </summary>
	public static double Walker(Card c) => 2 + c.Loyalty * 0.35;

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
		return 2 + power * 1.5 + toughness + keywords * 0.8;
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
		&& (c.Keywords.Contains(keyword) || !pumps.Any(u => u.Keyword == keyword))
		&& (keyword != "Haste" || Combat.IsSummoningSick(s, c));
}

/// <summary>
/// What a card in hand is worth when HELD: a fraction of its measured value (<see cref="CardValues"/>),
/// never less than the flat price — at 0 the search, whose ties go to acting, would throw zero-valued
/// tricks away. Story AI-1: the first, STATIC price of the option a held spell is (AI-5 prices it by
/// what may still come, time, mana, plan). Our own cards are priced by name. The opponent's are
/// hidden, so EVERY one of them — land or not — gets one blind price: a limited deck's share of lands
/// at the land price, the rest at the average held value of a measured card. No peeking, and "they
/// draw a card" costs on the same scale as our own cards are worth.
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
