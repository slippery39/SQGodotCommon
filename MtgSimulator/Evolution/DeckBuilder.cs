using System.Collections.Immutable;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Builds and mutates constructed decklists.
///
/// Seeding and mutation live in one file because mutation reuses the seeder's weighted card
/// sampler; splitting them would duplicate it or export it just to be shared.
///
/// **A seed is not 36 random cards.** A human building a constructed deck starts from a
/// concept — a payoff card, a package that supports it, a curve — and fills around it. The
/// same shape is available here without hand-labelling a single card, because the trained
/// model already carries both halves: per-card win rates say what is good, and the measured
/// pair table says what wants to be together. Both were found unsupervised.
///
/// A seed is therefore: an ANCHOR (sampled by card value), a KERNEL of its best-evidenced
/// synergy partners, a CURVE TARGET, then a weighted fill. Archetypes fall out of the curve
/// and the kernel rather than being enumerated anywhere.
/// </summary>
public static class DeckBuilder
{
	/// <summary>
	/// Softmax temperature for card sampling, in percentage points of win rate — the same
	/// units DraftPickers uses. High, because seeding wants spread across the pool rather than
	/// the argmax: eight decks that all open on the best card in the format are one deck.
	/// </summary>
	public const double SeedTemperature = 4.0;

	/// Tighter than seeding: a mutation is a considered swap, not exploration.
	public const double MutateTemperature = 2.0;

	/// How many synergy partners form the kernel around the anchor.
	public const int KernelSize = 4;

	/// Points of score lost per point of mana cost away from the deck's curve target.
	public const double CurvePenalty = 1.5;

	/// <summary>
	/// Score added for a card with no constructed evidence yet, scaled by how unmeasured it is.
	///
	/// Without it the mode is purely exploitative: a card gets played because it has good data
	/// and has good data because it was played. Measured over 100 generations, only 343 of 785
	/// cards were ever tried, and cards in final decks had a median 11 310 games against 998
	/// for the rest.
	///
	/// Sized to roughly one point of win rate — enough to get an unknown card looked at, far
	/// too small to keep it in a deck it loses with. The presimulation is the bigger half of
	/// this fix; the bonus is what keeps late generations still sampling.
	/// </summary>
	public const double ExplorationBonus = 3.0;

	private const double MinCurveTarget = 2.0;
	private const double MaxCurveTarget = 4.5;

	/// <summary>
	/// A fresh decklist built around a randomly chosen concept.
	/// </summary>
	/// <param name="wildcard">
	/// The exploration arm. Samples its anchor UNIFORMLY, ignoring card value entirely, and
	/// leans harder on synergy. It will often be bad and get culled — that is the mechanism,
	/// not a failure of it. Without a slot that ignores what the model already believes, the
	/// field can only ever refine the cards the prior already liked, and a combo deck built
	/// from individually-mediocre pieces is unreachable.
	/// </param>
	public static Decklist Seed(
		string name,
		IReadOnlyList<Card> pool,
		ConstructedValues values,
		Random rng,
		bool wildcard = false
	)
	{
		var spells = pool.Where(c => !c.HasSubtype("Land")).ToList();
		if (spells.Count == 0)
			throw new ArgumentException("Card pool has no non-land cards.", nameof(pool));

		var curveTarget = MinCurveTarget + rng.NextDouble() * (MaxCurveTarget - MinCurveTarget);
		var lands = LandsForCurve(curveTarget, rng);

		var deck = Decklist.Empty(name) with { Lands = lands };

		// 1. Anchor — the concept the deck is about.
		var anchor = wildcard
			? spells[rng.Next(spells.Count)]
			: spells[
				DraftPickers.SampleSoftmax(
					spells.Select(c => values.CardDelta(c.Name)).ToArray(),
					SeedTemperature,
					rng
				)
			];
		deck = deck.WithCopies(anchor.Name, Decklist.MaxCopies);

		// 2. Kernel — what measurably wants to be alongside it.
		var byName = spells.ToDictionary(c => c.Name, StringComparer.Ordinal);
		foreach (var (partner, _) in values.TopPartners(anchor.Name, spells, KernelSize))
		{
			if (!byName.ContainsKey(partner))
				continue;
			deck = deck.WithCopies(partner, 3 + rng.Next(2));
			if (deck.SpellCount >= Decklist.DeckSize - lands)
				break;
		}

		// 3. Fill, in chunks — constructed decks play multiples, not 39 singletons.
		var synergyWeight = wildcard ? 2.0 : 1.0;
		return Fill(deck, spells, values, rng, curveTarget, synergyWeight, SeedTemperature);
	}

	/// <summary>
	/// One mutated copy of a deck. Returns null when the operator could not produce a valid
	/// distinct deck (an empty pool of candidates, a land move that would break the range),
	/// which the caller treats as "no proposal this round" rather than an error.
	///
	/// Operators are deliberately small. A mutation has to be measurable against its parent
	/// over a few dozen games, and a large rewrite is indistinguishable from a fresh seed —
	/// it destroys the hill being climbed.
	/// </summary>
	/// <param name="history">
	/// What this deck slot has learned about its own cards. When supplied, cutting is
	/// synergy-aware: a card whose pairs underperform inside this deck is likelier to be cut,
	/// and a card carrying several winning pairs is protected. Null falls back to overall card
	/// quality alone, which is all that is available in generation 1.
	/// </param>
	public static Decklist? Mutate(
		Decklist deck,
		IReadOnlyList<Card> pool,
		ConstructedValues values,
		Random rng,
		DeckHistory? history = null
	)
	{
		var spells = pool.Where(c => !c.HasSubtype("Land")).ToList();
		var curveTarget = deck.AverageCost(
			spells.ToDictionary(c => c.Name, StringComparer.Ordinal)
		);

		// Weighted: swapping cards is the operator that actually explores the card pool, so it
		// gets most of the budget. Land moves are one integer and converge quickly.
		// Package size is rolled independently so the mutator is not ALWAYS hunting synergy:
		// 0 partners is "just put a good card in this slot", 1 is a pair, 2 is a triple. A
		// mutator that only ever proposes packages narrows the field to whatever the pair table
		// already believes, and pair evidence is the thinnest thing in the model.
		var roll = rng.Next(10);
		var mutated =
			roll < 5 ? Swap(deck, spells, values, rng, curveTarget, history)
			: roll < 7 ? Recount(deck, spells, values, rng, curveTarget, history)
			: roll < 9 ? Package(deck, spells, values, rng, history, partners: rng.Next(3))
			: AdjustLands(deck, spells, values, rng, curveTarget, history);

		if (mutated is null || mutated.Validate() is not null)
			return null;
		return Decklist.Difference(deck, mutated) > 0 ? mutated : null;
	}

	/// Remove k copies of one card, add k copies of another.
	private static Decklist? Swap(
		Decklist deck,
		IReadOnlyList<Card> spells,
		ConstructedValues values,
		Random rng,
		double curveTarget,
		DeckHistory? history
	)
	{
		var outgoing = PickWeakest(deck, values, rng, history);
		if (outgoing is null)
			return null;

		var k = Math.Min(deck.CopiesOf(outgoing), 1 + rng.Next(Decklist.MaxCopies));
		var trimmed = deck.WithCopies(outgoing, deck.CopiesOf(outgoing) - k);
		return Fill(trimmed, spells, values, rng, curveTarget, 1.0, MutateTemperature);
	}

	/// Shift one card's copy count by 1, compensating with another card.
	private static Decklist? Recount(
		Decklist deck,
		IReadOnlyList<Card> spells,
		ConstructedValues values,
		Random rng,
		double curveTarget,
		DeckHistory? history
	)
	{
		if (deck.DistinctSpells < 2)
			return null;

		var names = deck.Spells.Keys.ToList();
		var target = names[rng.Next(names.Count)];
		var up = rng.Next(2) == 0;

		if (up && deck.CopiesOf(target) >= Decklist.MaxCopies)
			return null;

		var adjusted = deck.WithCopies(target, deck.CopiesOf(target) + (up ? 1 : -1));

		// Adding a copy has to take a slot from somewhere; removing one frees a slot to fill.
		if (!up)
			return Fill(adjusted, spells, values, rng, curveTarget, 1.0, MutateTemperature);

		var donor = PickWeakest(adjusted, values, rng, history, exclude: target);
		return donor is null ? null : adjusted.WithCopies(donor, adjusted.CopiesOf(donor) - 1);
	}

	/// <summary>
	/// Bring in a card together with its best measured synergy partners, in one move.
	///
	/// **This exists because single-card hill climbing cannot cross a synergy valley.** Atog
	/// alone is a bad card; artifacts without Atog are unremarkable. Every one-card step from a
	/// normal deck toward the Atog combo makes the deck WORSE, so it is rejected, and the combo
	/// is unreachable no matter how good the scoring function is. "Consistent piles of
	/// individually-strong cards" is precisely the set of decks reachable by one-card steps —
	/// which is exactly what the first four runs produced.
	///
	/// So this is a search-operator fix, not a scoring fix. The pair table is used to GENERATE
	/// the proposal rather than to score it, which is a much lighter demand on thin pair data:
	/// a wrong package is simply rejected by the win rate a generation later, whereas a wrong
	/// score silently biases every decision.
	/// </summary>
	/// <param name="partners">
	/// How many synergy partners come in with the anchor. **0 means "just add a good card"** —
	/// the anchor is chosen on card value alone and no pair evidence is consulted. That arm
	/// exists so the mutator is not permanently hunting synergy: pair data is the thinnest part
	/// of the model, and a mutator that only proposes packages can never fill a slot with a
	/// plainly strong card that happens to have no measured partners.
	/// </param>
	private static Decklist? Package(
		Decklist deck,
		IReadOnlyList<Card> spells,
		ConstructedValues values,
		Random rng,
		DeckHistory? history,
		int partners
	)
	{
		var outside = spells.Where(c => deck.CopiesOf(c.Name) == 0).ToList();
		if (outside.Count == 0)
			return null;

		var anchor = outside[
			DraftPickers.SampleSoftmax(
				outside.Select(c => values.CardDelta(c.Name)).ToArray(),
				MutateTemperature,
				rng
			)
		];

		// Partners must be outside the deck too — a "package" whose halves are already present
		// is just an expensive Recount. Ranked on absolute joint performance, so a partner has
		// to actually WIN alongside the anchor rather than merely beat a pessimistic baseline.
		var chosen =
			partners == 0
				? []
				: values
					.TopPartners(anchor.Name, spells, KernelSize)
					.Select(p => p.Name)
					.Where(n => deck.CopiesOf(n) == 0)
					.Take(partners)
					.ToList();

		var incoming = chosen.Prepend(anchor.Name).ToList();
		var copies = incoming.ToDictionary(n => n, _ => 2 + rng.Next(2), StringComparer.Ordinal);
		var needed = copies.Values.Sum();

		// Free the slots first, so the package lands as one atomic change.
		var trimmed = deck;
		var guard = 0;
		while (trimmed.SpellCount > Decklist.DeckSize - trimmed.Lands - needed)
		{
			if (guard++ > Decklist.DeckSize)
				return null;
			var cut = PickWeakest(trimmed, values, rng, history);
			if (cut is null)
				return null;
			trimmed = trimmed.WithCopies(cut, trimmed.CopiesOf(cut) - 1);
		}

		foreach (var (name, count) in copies)
			trimmed = trimmed.WithCopies(name, count);

		// Trimming works one copy at a time and can overshoot, so top back up.
		return trimmed.SpellCount < Decklist.DeckSize - trimmed.Lands
			? Fill(
				trimmed,
				spells,
				values,
				rng,
				trimmed.AverageCost(spells.ToDictionary(c => c.Name, StringComparer.Ordinal)),
				1.0,
				MutateTemperature
			)
			: trimmed;
	}

	/// Move the mana base by one, compensating with a spell.
	private static Decklist? AdjustLands(
		Decklist deck,
		IReadOnlyList<Card> spells,
		ConstructedValues values,
		Random rng,
		double curveTarget,
		DeckHistory? history
	)
	{
		var up = rng.Next(2) == 0;
		var lands = deck.Lands + (up ? 1 : -1);
		if (lands < Decklist.MinLands || lands > Decklist.MaxLands)
			return null;

		var adjusted = deck with { Lands = lands };
		if (!up)
			return Fill(adjusted, spells, values, rng, curveTarget, 1.0, MutateTemperature);

		var donor = PickWeakest(adjusted, values, rng, history);
		return donor is null ? null : adjusted.WithCopies(donor, adjusted.CopiesOf(donor) - 1);
	}

	/// <summary>
	/// Adds cards until the deck is legal, sampling by value + synergy with what is already
	/// there − distance from the curve target.
	///
	/// Chunks of 2-4 rather than one at a time: constructed decks play multiples, and a fill
	/// that adds 39 singletons produces a pile that draws its own cards a fifth as often.
	/// </summary>
	private static Decklist? Fill(
		Decklist deck,
		IReadOnlyList<Card> spells,
		ConstructedValues values,
		Random rng,
		double curveTarget,
		double synergyWeight,
		double temperature
	)
	{
		var guard = 0;
		while (deck.SpellCount < Decklist.DeckSize - deck.Lands)
		{
			if (guard++ > Decklist.DeckSize * 2)
				return null; // pool too small to fill legally

			var need = Decklist.DeckSize - deck.Lands - deck.SpellCount;

			var candidates = spells.Where(c => deck.CopiesOf(c.Name) < Decklist.MaxCopies).ToList();
			if (candidates.Count == 0)
				return null;

			var scores = new double[candidates.Count];
			for (var i = 0; i < candidates.Count; i++)
			{
				var card = candidates[i];
				scores[i] =
					values.CardDelta(card.Name)
					+ synergyWeight * values.DeckFit(card.Name, deck)
					- CurvePenalty * Math.Abs(card.ManaCost - curveTarget)
					+ ExplorationBonus * values.Unmeasured(card.Name);
			}

			var chosen = candidates[DraftPickers.SampleSoftmax(scores, temperature, rng)];
			var room = Decklist.MaxCopies - deck.CopiesOf(chosen.Name);
			var add = Math.Min(Math.Min(2 + rng.Next(3), room), need);
			deck = deck.WithCopies(chosen.Name, deck.CopiesOf(chosen.Name) + add);
		}

		return deck;
	}

	/// <summary>
	/// A card to cut, sampled toward the deck's weakest contributors. Sampled rather than
	/// argmin so repeated mutation of one deck does not propose the identical cut every time.
	/// </summary>
	private static string? PickWeakest(
		Decklist deck,
		ConstructedValues values,
		Random rng,
		DeckHistory? history = null,
		string? exclude = null
	)
	{
		var names = deck
			.Spells.Keys.Where(n => !string.Equals(n, exclude, StringComparison.Ordinal))
			.ToList();
		if (names.Count == 0)
			return null;

		// **Never cut a card that is measurably carrying this deck.** Softmax alone still cuts
		// a strong card occasionally, and over 100 generations "occasionally" is constantly —
		// which is how Ancestral Recall left decks it was winning in. Restrict the candidates
		// to below-average performers and only fall back to the full list when every card is
		// pulling its weight (in which case the deck has no obvious weak link and any cut is a
		// guess anyway).
		// The filter and the ranking below MUST score identically. Scoring the filter on local
		// history alone made a card's global value unreachable: inside its own deck a card's
		// win rate sits near that deck's own rate, so its local delta is ~0 and — with the
		// imputed pair term — it lands at or above the local mean and is excluded from the cut
		// candidates entirely. Dragonstorm, the WORST card in a 780-card pool at -22.86pp,
		// survived 2 096 games in a deck that way, and Thoughtcast held slots in three decks
		// with no artifacts. Ancestral Recall meanwhile never got in, because the slots were
		// locked by junk that could not be cut.
		double Score(string n) =>
			values.CardDelta(n) + values.DeckFit(n, deck) + (history?.KeepScore(n, deck) ?? 0);

		if (names.Count > 2)
		{
			var scored = names.Select(n => (Name: n, Score: Score(n))).ToList();
			var mean = scored.Average(e => e.Score);
			var below = scored.Where(e => e.Score < mean).Select(e => e.Name).ToList();
			if (below.Count > 0)
				names = below;
		}

		// Negated, so the softmax favours the LOW scorers.
		//
		// `history` is what makes cutting synergy-aware, and it is the per-deck table rather
		// than the global one on purpose: it asks "is this combination pulling its weight in
		// THIS deck", which is dense enough to answer (a 4-of is drawn most games) where the
		// global table is spread over 83 000 pairs at a median of 53 games. A card carrying
		// several winning pairs survives even on an unremarkable solo rate — which is exactly
		// what a synergy piece looks like, and what pure card quality cuts first.
		var scores = names.Select(n => -Score(n)).ToArray();
		return names[DraftPickers.SampleSoftmax(scores, MutateTemperature, rng)];
	}

	/// <summary>
	/// Land count for a curve, jittered.
	///
	/// The mana base is a scalar here — one land in the engine, no colours — but it is NOT an
	/// inert knob. The fixed three-land opening hand does not neutralise it: what decides
	/// whether you keep hitting drops is the density of the REMAINDER of the library, which is
	/// 17-in-53 (32%) at 20 lands and 23-in-53 (43%) at 26. See Draft.DefaultMaxSpells for the
	/// same argument measured in limited.
	/// </summary>
	private static int LandsForCurve(double curveTarget, Random rng)
	{
		var scaled =
			Decklist.MinLands
			+ (curveTarget - MinCurveTarget)
				/ (MaxCurveTarget - MinCurveTarget)
				* (Decklist.MaxLands - Decklist.MinLands);
		return Math.Clamp(
			(int)Math.Round(scaled) + rng.Next(-1, 2),
			Decklist.MinLands,
			Decklist.MaxLands
		);
	}

	/// <summary>
	/// Seeds a whole field, rejecting any deck too similar to one already placed.
	///
	/// The diversity constraint is enforced HERE and again at mutation acceptance, which is
	/// what stops the field converging. It is a hard constraint rather than a fitness penalty
	/// because the requirement is categorical ("no two decks share more than X") and a penalty
	/// would let a strong deck buy its way past it.
	///
	/// The threshold relaxes if a slot cannot be filled, rather than looping forever — on a
	/// small pool, eight genuinely distinct 60-card decks may not exist, and reporting the
	/// achieved diversity is more useful than hanging.
	/// </summary>
	public static IReadOnlyList<Decklist> SeedField(
		int count,
		IReadOnlyList<Card> pool,
		ConstructedValues values,
		Random rng,
		double minDifference,
		bool includeWildcard = true
	)
	{
		var field = new List<Decklist>(count);
		for (var i = 0; i < count; i++)
		{
			var wildcard = includeWildcard && i == count - 1;
			var name = wildcard ? "Wildcard" : $"Deck {(char)('A' + i)}";
			field.Add(SeedDistinct(name, pool, values, rng, field, minDifference, wildcard));
		}
		return field;
	}

	/// <summary>
	/// One deck that differs from every deck in <paramref name="others"/> by at least
	/// <paramref name="minDifference"/>. Falls back to the most distinct attempt seen.
	/// </summary>
	public static Decklist SeedDistinct(
		string name,
		IReadOnlyList<Card> pool,
		ConstructedValues values,
		Random rng,
		IReadOnlyList<Decklist> others,
		double minDifference,
		bool wildcard = false,
		int attempts = 30
	)
	{
		Decklist? best = null;
		var bestGap = -1.0;

		for (var i = 0; i < attempts; i++)
		{
			var candidate = Seed(name, pool, values, rng, wildcard);
			if (candidate.Validate() is not null)
				continue;

			var gap = MinDifference(candidate, others);
			if (gap >= minDifference)
				return candidate;
			if (gap > bestGap)
				(best, bestGap) = (candidate, gap);
		}

		return best ?? Seed(name, pool, values, rng, wildcard);
	}

	/// Smallest difference between a deck and any of a field. 1.0 against an empty field.
	public static double MinDifference(Decklist deck, IReadOnlyList<Decklist> others) =>
		others.Count == 0
			? 1.0
			: others
				.Where(o => !ReferenceEquals(o, deck))
				.Select(o => Decklist.Difference(deck, o))
				.DefaultIfEmpty(1.0)
				.Min();
}
