using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Picks a card for one seat: returns an INDEX into <paramref name="offer"/>.
/// <paramref name="pool"/> is what the seat has already drafted, so a picker can
/// weigh synergy and curve against what it is building.
///
/// A delegate rather than an interface: draft state is standalone records that never
/// enter a GameState, so the no-delegates serialization rule does not apply here —
/// same reasoning as DeckInfo.Builder in Decks/DeckRegistry.cs.
/// </summary>
public delegate int DraftPicker(IReadOnlyList<Card> offer, IReadOnlyList<Card> pool);

public static class DraftPickers
{
	/// <summary>
	/// How hard the trained picker commits to its colours, in points of win rate. Measured at
	/// 55.9% +/- 1.6 over 960 head-to-head games against the same picker with no lane term — see
	/// the laneWeight parameter on <see cref="Trained"/>.
	/// </summary>
	public const double DefaultLaneWeight = 2.0;

	/// Picks over which the lane term ramps from nothing to full strength — about a third of a
	/// booster pack, which is roughly when a real drafter knows what they are in.
	private const double LaneRampPicks = 10;

	/// Baseline — uniform random pick. The control group for measuring any other picker.
	public static DraftPicker Random(Random rng) => (offer, _) => rng.Next(offer.Count);

	/// <summary>
	/// Takes the most stats-per-mana, with a nudge away from an already top-heavy curve.
	///
	/// ponytail: efficiency only — no synergy, no removal quality, no archetype, because
	/// ManaCost and P/T are all Card actually carries (no rarity, no colors).
	/// Upgrade path: score from the GIH win rates in sim_results/precon_*.csv.
	/// </summary>
	public static int Curve(IReadOnlyList<Card> offer, IReadOnlyList<Card> pool)
	{
		var best = 0;
		var bestScore = Score(offer[0], pool);
		for (var i = 1; i < offer.Count; i++)
		{
			var score = Score(offer[i], pool);
			if (score > bestScore)
			{
				(best, bestScore) = (i, score);
			}
		}
		return best; // ties resolve to the first card — deterministic, no rng needed
	}

	/// <summary>
	/// Picks from trained win-rate data: the card's own games-in-hand rate plus its average
	/// synergy with what this seat has already drafted, sampled with a softmax so the same
	/// seed does not always produce the identical deck.
	///
	/// Scores are in percentage points, so a card score of +3 means "decks drawing this card
	/// won 3 points more often than average" and a synergy score of +3 means "this pair won
	/// 3 points more often than the two cards' individual rates predict".
	/// <paramref name="temperature"/> is in those same units: 0 picks the argmax, ~2 splits
	/// near-ties, high is near-random. Cards absent from the training data score 0 (i.e.
	/// exactly average), so an incomplete model degrades gracefully instead of ignoring
	/// unseen cards.
	///
	/// The synergy term is discounted by how much rarer it is to draw BOTH halves of a pair
	/// than to draw one card (measured from the data, ~0.44), so it is commensurate with the
	/// card term rather than being compared across different conditional events.
	///
	/// <paramref name="synergyWeight"/> nonetheless defaults to 0, on measurement. Across
	/// 16 800 training games (median 464 games per pair) every non-zero weight cost win rate:
	/// 0 -> 82.2%, 0.5 -> 80.2%, 1 -> 74.6%, 2 -> 62.8%, 4 -> 54.5% over 288 games each.
	/// The metric finds real interactions (Faithless Looting + Tarmogoyf, Goblin Grenade +
	/// Goblin Matron), but summing ~27 pair estimates accumulates more noise than signal.
	/// Re-run the weight sweep before raising this; the intuition has been wrong twice.
	/// </summary>
	/// <param name="laneWeight">
	/// Points of win rate a card gains for being inside the seat's two committed colours, and loses
	/// for being outside them. **Colour commitment is the core drafting skill and the picker had no
	/// notion of it**: measured over 200 seats on CSC, colour concentration was 0.22 where 0.20 is a
	/// perfectly even five-colour spread, and 18% of seats finished unable to field 23 playables in
	/// their own best identity. Deck assembly can only choose among what was drafted, so no fix
	/// downstream reaches this.
	///
	/// **Measured before being switched on**, the same rule <paramref name="synergyWeight"/> earned
	/// the hard way. Half the seats drafting with the term and half without, AT THE SAME TABLE so
	/// both are competing for the same cards, then a full round robin: **537-423, 55.9% +/- 1.6 over
	/// 960 games** at weight 2. Measuring a lane term against itself would have shown nothing.
	///
	/// The depth effect is larger and was never in doubt — seats unable to field 23 playables in
	/// their own best identity fall from 18% to 1%, and colour concentration rises from 0.22 (a
	/// perfectly even five-colour spread is 0.20) toward a real two-colour deck.
	///
	/// **More commitment is NOT better, and the tidy metrics say it is.** Weight 4 measured
	/// **494-466, 51.5% +/- 1.6** on the same 960 games — 4.4 points worse than weight 2 and barely
	/// above even — while its colour concentration (0.33 against 0.26) and lane depth both looked
	/// strictly better. Over-committing means passing the bombs, and every metric that is cheap to
	/// read rewards it anyway. This is the synergyWeight trap wearing a different hat: measure the
	/// WIN RATE, at the large sample, or do not move this number.
	/// </param>
	public static DraftPicker Trained(
		DraftTrainingData data,
		Random rng,
		double temperature = 2.0,
		double synergyWeight = 0.0,
		int shrinkK = 25,
		// Pairs carry ~10x less data than cards, so they are damped ~10x harder.
		int pairShrinkK = 200,
		double laneWeight = DefaultLaneWeight
	)
	{
		var prior = data.Prior;
		var pairs = data.Pairs.ToDictionary(p => PairKey(p.A, p.B), StringComparer.Ordinal);

		// Shrunk once up front: these feed both the card term and every pair's baseline.
		var cardRates = data.Cards.ToDictionary(
			c => c.Name,
			c => DraftTrainingData.Shrink(c.Wins, c.Games, prior, shrinkK),
			StringComparer.Ordinal
		);

		// How much rarer it is to draw BOTH halves of a pair than to draw one card, measured
		// from the data (~0.44 in practice). A card's win rate is conditioned on that card
		// being drawn; a pair's on both being drawn. Without this discount the two are
		// measured on different events and adding them over-weights synergy.
		//
		// Deliberately ONE global scalar rather than per-card factors. Per-card P(drawn) is
		// endogenous — a card that wins games faster is drawn less often, so scaling by it
		// penalises exactly the best cards. It is also applied to synergy only: scaling both
		// terms would compress the whole score range and silently make `temperature` behave
		// far more randomly, which is a bigger effect than the correction itself.
		var pairDrawRatio = PairDrawRatio(data);

		double RateOf(string name) => cardRates.GetValueOrDefault(name, prior);

		double CardDelta(string name) => 100.0 * (RateOf(name) - prior);

		double SynergyDelta(string name, IReadOnlyList<Card> pool)
		{
			var total = 0.0;
			// Only the picks that will actually make the deck can ever be drawn alongside
			// this card, so pairing against the whole 45-card pool would inflate the term.
			var deckBound = Math.Min(pool.Count, Draft.DefaultMaxSpells);
			for (var i = 0; i < deckBound; i++)
			{
				var key = PairKey(name, pool[i].Name);
				if (!pairs.TryGetValue(key, out var s))
					continue;

				// Baseline is what these two cards should do together on their own merits,
				// NOT the global prior — otherwise a strong card makes every pair it appears
				// in look synergistic. Shrinking toward that same baseline means a pair with
				// no data lands on exactly 0 synergy rather than being dragged toward the
				// global average, which would penalise untested pairs of strong cards.
				var expected = DraftTrainingData.ExpectedPairRate(
					RateOf(name),
					RateOf(pool[i].Name),
					prior
				);
				var actual = DraftTrainingData.Shrink(s.Wins, s.Games, expected, pairShrinkK);
				total += 100.0 * (actual - expected);
			}

			// Summed, not averaged: every pair in the deck contributes its own expected points.
			// The draw-frequency discount is applied once, to the whole sum.
			return pairDrawRatio * total;
		}

		/// <summary>
		/// The two colours this seat has invested the most VALUE in — not the most cards. A seat
		/// holding four strong red cards is redder than one holding six weak ones, and counting
		/// cards would let a pile of late-pack filler define the lane.
		/// </summary>
		List<ManaColor> CommittedColors(IReadOnlyList<Card> pool)
		{
			var committed = new Dictionary<ManaColor, double>();
			foreach (var color in ManaPool.Colors)
				committed[color] = 0;

			foreach (var card in pool)
			{
				// Only cards that were worth playing argue for their colour; a below-average card
				// is not a reason to stay in a lane.
				var worth = Math.Max(0, CardDelta(card.Name));
				foreach (var color in ManaPool.Colors)
					if (card.ColorPips[color] > 0)
						committed[color] += worth;
			}

			return
			[
				.. committed
					.OrderByDescending(kv => kv.Value)
					.ThenBy(kv => ManaPool.Colors.IndexOf(kv.Key))
					.Take(2)
					.Select(kv => kv.Key),
			];
		}

		return (offer, pool) =>
		{
			// Ramped in over the first picks: a seat with three cards has no lane worth defending,
			// and committing to one on pick two is how a drafter ends up fighting the whole table
			// for a colour nobody passed them. By mid-pack-one the term is at full strength.
			var ramp = laneWeight == 0 ? 0 : Math.Min(1.0, (double)pool.Count / LaneRampPicks);
			var lane = ramp > 0 ? CommittedColors(pool) : [];

			var scores = new double[offer.Count];
			for (var i = 0; i < offer.Count; i++)
			{
				var card = offer[i];
				scores[i] = CardDelta(card.Name) + synergyWeight * SynergyDelta(card.Name, pool);

				if (ramp <= 0)
					continue;

				// Colourless cards are in every lane, so they are neither rewarded nor punished —
				// the term is about COMMITMENT, and a card that costs nothing commits nothing.
				if (card.ColorPips.IsEmpty)
					continue;

				var fits = ManaPool.Colors.All(c => card.ColorPips[c] == 0 || lane.Contains(c));
				scores[i] += laneWeight * ramp * (fits ? 1 : -1);
			}

			return SampleSoftmax(scores, temperature, rng);
		};
	}

	/// <summary>
	/// median P(both drawn | both in deck) / median P(drawn | in deck) — how much rarer a
	/// pair's payoff is than a single card's. Medians rather than means so a handful of
	/// thin-sample entries cannot swing it. Returns 1.0 for data written before DeckGames
	/// existed, which reproduces the old unscaled behaviour.
	/// </summary>
	internal static double PairDrawRatio(DraftTrainingData data)
	{
		var cardDraw = Median(
			data.Cards.Where(c => c.DeckGames > 0).Select(c => (double)c.Games / c.DeckGames)
		);
		var pairDraw = Median(
			data.Pairs.Where(p => p.DeckGames > 0).Select(p => (double)p.Games / p.DeckGames)
		);
		return cardDraw > 0 && pairDraw > 0 ? pairDraw / cardDraw : 1.0;
	}

	private static double Median(IEnumerable<double> values)
	{
		var sorted = values.OrderBy(v => v).ToList();
		return sorted.Count == 0 ? 0.0 : sorted[sorted.Count / 2];
	}

	/// <summary>
	/// Order-independent key so (A,B) and (B,A) are the same pair. The separator is NUL because no
	/// card name can contain one, which makes the key collision-proof for free.
	///
	/// **Written as the escape `\0`, never as a literal NUL byte.** It was a literal one, and git
	/// treats any file with a NUL in its first 8 KB as BINARY — so `git diff` on this file printed
	/// `Bin 9044 -> 13002 bytes` and showed nothing. Every change to the picker had been invisible
	/// to review. The runtime value is identical; only the bytes on disk differ.
	/// </summary>
	internal static string PairKey(string a, string b) =>
		string.CompareOrdinal(a, b) <= 0 ? $"{a}\0{b}" : $"{b}\0{a}";

	/// <summary>
	/// Samples an index with probability proportional to exp(score / temperature).
	/// Shifts by the max before exponentiating (standard guard against overflow), and
	/// falls back to argmax at or below zero temperature.
	/// </summary>
	internal static int SampleSoftmax(double[] scores, double temperature, Random rng)
	{
		if (temperature <= 0)
			return ArgMax(scores);

		var max = scores.Max();
		var weights = new double[scores.Length];
		var total = 0.0;
		for (var i = 0; i < scores.Length; i++)
		{
			weights[i] = Math.Exp((scores[i] - max) / temperature);
			total += weights[i];
		}

		// Guards against a non-finite total from an extreme score or temperature.
		if (double.IsNaN(total) || double.IsInfinity(total) || total <= 0)
			return ArgMax(scores);

		var roll = rng.NextDouble() * total;
		for (var i = 0; i < weights.Length; i++)
		{
			roll -= weights[i];
			if (roll <= 0)
				return i;
		}
		return weights.Length - 1; // floating-point slack on the last bucket
	}

	private static int ArgMax(double[] scores)
	{
		var best = 0;
		for (var i = 1; i < scores.Length; i++)
			if (scores[i] > scores[best])
				best = i;
		return best;
	}

	private static double Score(Card card, IReadOnlyList<Card> pool)
	{
		var body = card.GetComponent<CreatureComponent>();
		// Non-creatures get a flat body value — a removal spell or a 3-drop creature are
		// roughly interchangeable at this level of fidelity.
		var raw = body is null ? 3.0 : body.Power + 0.5 * body.Toughness;
		var score = raw - card.ManaCost;
		if (card.ManaCost >= 5 && pool.Count(c => c.ManaCost >= 5) >= 5)
			score -= 3;
		return score;
	}
}
