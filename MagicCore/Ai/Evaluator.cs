using ImmutableGameObjects;
using MagicCore.Cards;

namespace MagicCore.Ai;

/// <summary>
/// How good a position is for one player — the score every MagicCore AI compares moves by.
/// Card-agnostic: it reads the board, life and hand, never card text.
/// </summary>
public static class Evaluator
{
	/// <summary>
	/// Position score for <paramref name="player"/>. Permanent value counts printed stats plus counters, not
	/// until-end-of-turn pumps — a pump is worth only what it changes before it wears off.
	/// </summary>
	public static double Evaluate(GameState s, int player)
	{
		if (s.IsOver())
			return s.WinnerId() == player ? 10_000 : -10_000;
		var opponent = s.OpponentOf(player);
		return Side(s, player) - Side(s, opponent);
	}

	static double Side(GameState s, int player)
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
		var hand = s.CardsIn(player, ZoneType.Hand).Sum(c => c.Is(CardType.Land) ? 0.3 : 1.2);
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
