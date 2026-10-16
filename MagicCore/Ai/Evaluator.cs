using ImmutableGameObjects;
using MagicCore.Cards;

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
		return Side(s, player, held, own: true) - Side(s, opponent, held, own: false);
	}

	static double Side(GameState s, int player, HeldValues? held, bool own)
	{
		var board = s.CardsIn(player, ZoneType.Battlefield).ToList();
		var creatures = board.Where(c => c.Is(CardType.Creature)).Sum(c => CreatureValue(s, c));
		// Every term is MONOTONIC — more of a good thing always scores more, tapering instead of
		// capping. A cap makes "more" tie with "less", and a tie silently picks the passive move:
		// life capped at 20 stopped all attacks past tapped blockers once a drain deck pushed the
		// opponent above it; lands capped at 7 made an 8th land worth less in play than in hand.
		var lands = Taper(board.Count(c => c.Is(CardType.Land)), 7, 1.5, 0.4);
		// Loyalty is spent, not banked: valued under half a card per counter, so trading three
		// counters for a card scores as a gain. At 0.8 the AI never once used its Jace.
		var walkers = board.Where(c => c.Is(CardType.Planeswalker)).Sum(c => 2 + c.Loyalty * 0.35);
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

	/// <summary>Full value per unit up to <paramref name="knee"/>, a smaller value per unit beyond.</summary>
	static double Taper(int n, int knee, double perUnit, double beyond) =>
		Math.Min(n, knee) * perUnit + Math.Max(0, n - knee) * beyond;

	static double CreatureValue(GameState s, Card c)
	{
		// Alive is decided by the real toughness (a pump that saved it counts); its VALUE by what
		// lasts past this turn — statics yes ("creatures you control have trample" stays), until-
		// end-of-turn pumps and grants no.
		if (s.Toughness(c) <= 0 || (s.Toughness(c) - c.Damage <= 0 && !s.IsIndestructible(c)))
			return 0; // the state-based check's own question: lethal damage doesn't kill indestructible
		var pumps = c.GetComponents<UntilEndOfTurn>().Where(u => u.UntilTurnOf == 0).ToList(); // "until your next turn" lasts
		var power = s.Power(c) - pumps.Sum(u => u.Power);
		var toughness = Math.Max(1, s.Toughness(c) - pumps.Sum(u => u.Toughness) - c.Damage);
		var keywords = new[]
		{
			"Flying",
			"Deathtouch",
			"Lifelink",
			"First strike",
			"Double strike",
			"Trample",
			"Menace",
		}.Count(k => s.Has(c, k) && !pumps.Any(u => u.Keyword == k));
		return 2 + power * 1.5 + toughness + keywords * 0.8;
	}
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
