using MtgCore;

namespace MtgSimulator;

/// <summary>
/// What a card is worth IN CONSTRUCTED, learned from constructed games, starting from what the
/// draft model knows.
///
/// **Limited and constructed disagree systematically, and this type exists to stop the mode
/// rediscovering the draft model.** The draft model measures a card in a 40-card, 23-spell,
/// near-singleton, drafted deck. Constructed is 60 cards, 4-ofs, curated. Big vanilla creatures
/// and grindy card advantage rate well in limited and poorly in constructed; narrow combo
/// pieces and cheap situational answers rate near zero in limited and can define a constructed
/// format. Seeding from a limited prior and never correcting it produces exactly the failure
/// this mode has to avoid — more consistent draft decks.
///
/// Fitness is already immune, because it is the win rate measured in constructed games. The
/// exposure is REACHABILITY: hill climbing from limited-good starting points may never find the
/// combo deck. So the mode counts its own games and shrinks toward what it measures.
///
/// **The blend needs no decay schedule.** It is <see cref="DraftTrainingData.Shrink"/> with the
/// DRAFT-derived rate passed as the prior instead of the global 0.5:
///
///   few constructed games  ⇒ value ≈ the draft prior (early generations, nothing else to go on)
///   many constructed games ⇒ value ≈ the measured constructed value
///
/// With no draft model at all every card sits at the global prior and scores 0, so seeding is
/// quality-blind and the table builds from scratch. That is a supported mode and the honest
/// control arm if the limited prior is ever suspected of dominating a result.
///
/// Stored as <see cref="DraftTrainingData"/> rather than a parallel type so Merge, Shrink and
/// ExpectedPairRate all apply unchanged, and so the two tables can be diffed directly — which
/// is the acceptance test for whether any of this is working.
/// </summary>
public sealed class ConstructedValues
{
	/// Matches DraftPickers' card shrink. Cards carry ~10x the data pairs do.
	public const int CardShrinkK = 25;
	public const int PairShrinkK = 200;

	/// <summary>
	/// Minimum games before a PAIR is allowed to influence anything.
	///
	/// **This was 200 and that silently disabled every synergy path in the mode.** The CSC
	/// draft table's BUSIEST pair has 166 games (median 53, p99 108), so nothing could ever
	/// clear it: `SynergyDelta` returned 0 for all 83 028 pairs, `TopPartners` came back empty
	/// so the seeder's kernel never formed, and both fill and cut scoring collapsed to card
	/// quality plus curve. The decks it produced were piles of individually-good cards with
	/// visible anti-synergies, which is how it was caught — from the output, not the code.
	///
	/// The 200 came from copying `pairShrinkK`, which is a shrinkage constant and a completely
	/// different quantity. The unit test that "covered" this used inline data with 4 000 games,
	/// so it passed while no real pair could clear the gate — a test measuring nothing, exactly
	/// the trap this codebase keeps rediscovering.
	///
	/// Measured distributions this is now calibrated against:
	///
	/// | table | pairs | p50 | p99 | max | >= 50 games |
	/// |---|---|---|---|---|---|
	/// | CSC draft | 83 028 | 53 | 108 | 166 | 56% |
	/// | constructed (one run) | 3 484 | 119 | 5 768 | 13 704 | 56% |
	///
	/// 50 keeps the better-evidenced half of both. It is deliberately a SECOND line of defence
	/// rather than the only one: `Shrink(..., pairShrinkK: 200)` already pulls a 53-game pair
	/// 79% of the way back to its independence baseline before the gate is consulted at all.
	///
	/// The CLAUDE.md warning it was over-reacting to is about SUMMING ~27 thin pair deltas to
	/// rank one draft pick. Taking the top few partners of one anchor is a different operation
	/// with a different noise profile — that is the confidence-gating the same document lists
	/// as untested improvement path #1.
	/// </summary>
	public const int MinPairGames = 50;

	private readonly DraftTrainingData _constructed;
	private readonly DraftTrainingData? _draft;

	private readonly Dictionary<string, double> _draftRates;
	private readonly Dictionary<string, CardStat> _constructedCards;
	private readonly Dictionary<string, PairStat> _constructedPairs;
	private readonly Dictionary<string, PairStat> _draftPairs;

	private readonly double _prior;

	public ConstructedValues(DraftTrainingData constructed, DraftTrainingData? draft)
	{
		_constructed = constructed;
		_draft = draft;

		// The base rate every delta is quoted against. Constructed data owns it once it exists,
		// because that is the population being scored; before then, fall back to the draft
		// model's, and to 0.5 if there is no model at all.
		_prior =
			constructed.Perspectives > 0 ? constructed.Prior
			: draft is not null ? draft.Prior
			: 0.5;

		var draftPrior = draft?.Prior ?? 0.5;
		_draftRates =
			draft?.Cards.ToDictionary(
				c => c.Name,
				c => DraftTrainingData.Shrink(c.Wins, c.Games, draftPrior, CardShrinkK),
				StringComparer.Ordinal
			) ?? new Dictionary<string, double>(StringComparer.Ordinal);

		_constructedCards = constructed.Cards.ToDictionary(c => c.Name, StringComparer.Ordinal);
		_constructedPairs = constructed.Pairs.ToDictionary(
			p => DraftPickers.PairKey(p.A, p.B),
			StringComparer.Ordinal
		);
		_draftPairs =
			draft?.Pairs.ToDictionary(p => DraftPickers.PairKey(p.A, p.B), StringComparer.Ordinal)
			?? new Dictionary<string, PairStat>(StringComparer.Ordinal);
	}

	public DraftTrainingData Data => _constructed;

	public int ConstructedDeckGames => _constructed.Perspectives;

	public bool HasDraftPrior => _draft is not null;

	/// <summary>
	/// The card's win rate, blended. This is the one place the limited→constructed shrink
	/// happens; everything else is expressed in terms of it.
	/// </summary>
	public double RateOf(string name)
	{
		var prior = _draftRates.GetValueOrDefault(name, _prior);
		return _constructedCards.TryGetValue(name, out var c)
			? DraftTrainingData.Shrink(c.Wins, c.Games, prior, CardShrinkK)
			: prior;
	}

	/// <summary>
	/// How much better than average a card is, in PERCENTAGE POINTS — the same units
	/// DraftPickers scores in, so a softmax temperature means the same thing here.
	/// </summary>
	public double CardDelta(string name) => 100.0 * (RateOf(name) - _prior);

	/// <summary>
	/// How unmeasured a card is in CONSTRUCTED, from 1 (never played) to 0 (well established).
	/// Drives the exploration bonus, so a card nobody has tried gets looked at rather than
	/// waiting for data it can only get by being looked at.
	///
	/// Uses the same shrink constant as the card rate, so "measured enough to trust" means the
	/// same thing in both places.
	/// </summary>
	public double Unmeasured(string name) =>
		_constructedCards.TryGetValue(name, out var c)
			? (double)CardShrinkK / (c.Games + CardShrinkK)
			: 1.0;

	/// <summary>
	/// How much better two cards do together than their individual rates predict, in
	/// percentage points. Positive is real synergy.
	///
	/// Baseline is <see cref="DraftTrainingData.ExpectedPairRate"/> — combining each card's
	/// effect in log-odds space — and NOT the global prior. Measuring against the prior just
	/// re-reports card quality: pair a bomb with anything and the pair looks great.
	///
	/// Returns 0 for any pair without <see cref="MinPairGames"/> games behind it, in either
	/// table. Shrinking toward the expectation would already put a thin pair near 0; the gate
	/// makes it exactly 0 so a handful of lucky games cannot anchor a whole deck.
	/// </summary>
	public double SynergyDelta(string a, string b)
	{
		if (string.Equals(a, b, StringComparison.Ordinal))
			return 0;

		var key = DraftPickers.PairKey(a, b);
		var hasConstructed = _constructedPairs.TryGetValue(key, out var cp);
		var hasDraft = _draftPairs.TryGetValue(key, out var dp);

		var constructedGames = hasConstructed ? cp!.Games : 0;
		var draftGames = hasDraft ? dp!.Games : 0;
		if (constructedGames + draftGames < MinPairGames)
			return 0;

		var expected = DraftTrainingData.ExpectedPairRate(RateOf(a), RateOf(b), _prior);

		// Same blend as cards: the draft pair is the prior, constructed evidence moves off it.
		var draftRate =
			draftGames > 0
				? DraftTrainingData.Shrink(dp!.Wins, dp.Games, expected, PairShrinkK)
				: expected;
		var blended =
			constructedGames > 0
				? DraftTrainingData.Shrink(cp!.Wins, cp.Games, draftRate, PairShrinkK)
				: draftRate;

		return 100.0 * (blended - expected);
	}

	/// <summary>
	/// How well two cards do TOGETHER in absolute terms, in percentage points against the base
	/// rate — not against what independence predicts.
	///
	/// **This is the selection criterion; <see cref="SynergyDelta"/> is not.** Synergy measures
	/// interaction, which is a different question and a trap for choosing cards: for two
	/// individually-terrible cards `ExpectedPairRate` predicts a catastrophic rate, so a pair
	/// that merely performs badly scores as strong POSITIVE synergy. Measured on a real run,
	/// Dragonstorm + Tendrils of Agony read +8.91pp of "synergy" against a baseline expecting
	/// −35.4pp — i.e. about −26pp in absolute terms, while looking like a discovery.
	///
	/// The worst cards in a format are the EASIEST to show synergy for, because their baselines
	/// are the most pessimistic. Ranking on that systematically selects junk. Absolute joint
	/// performance has no such bias and needs no separate card-quality gate: a card that is bad
	/// alone but genuinely enables the anchor still scores well, and one that is bad both alone
	/// and together does not.
	/// </summary>
	public double JointDelta(string a, string b)
	{
		if (string.Equals(a, b, StringComparison.Ordinal))
			return 0;

		var key = DraftPickers.PairKey(a, b);
		var hasConstructed = _constructedPairs.TryGetValue(key, out var cp);
		var hasDraft = _draftPairs.TryGetValue(key, out var dp);

		var constructedGames = hasConstructed ? cp!.Games : 0;
		var draftGames = hasDraft ? dp!.Games : 0;
		if (constructedGames + draftGames < MinPairGames)
			return double.NaN; // no evidence — caller decides what that means

		// Blend the same way card rates do: draft evidence is the prior, constructed displaces
		// it. Shrink toward the two cards' own average so a thin pair says "about what these
		// cards do", not "about what the format does".
		var solo = (RateOf(a) + RateOf(b)) / 2.0;
		var draftRate =
			draftGames > 0 ? DraftTrainingData.Shrink(dp!.Wins, dp.Games, solo, PairShrinkK) : solo;
		var blended =
			constructedGames > 0
				? DraftTrainingData.Shrink(cp!.Wins, cp.Games, draftRate, PairShrinkK)
				: draftRate;

		return 100.0 * (blended - _prior);
	}

	/// <summary>
	/// How well a card performs alongside this particular deck, in percentage points.
	///
	/// **Averaged, not summed, and that is the whole point.** The previous version summed
	/// `SynergyDelta` over every card weighted by copies, which is unbounded in deck size: a
	/// 10-card, 40-copy deck produced synergy scores near +290 against card values in the ±25
	/// range, so card quality became rounding error. Deck B scored Dragonstorm (+261 combined)
	/// ABOVE Ancestral Recall (+240) and could neither stop adding junk nor cut what it had,
	/// because the same number drives adding and cutting.
	///
	/// Averaging keeps this in the same units as <see cref="CardDelta"/> so the two are
	/// commensurate and a weight of 1.0 means "these matter equally". Copies still weight the
	/// average — four copies of a partner really is four times the chance of drawing it.
	///
	/// Pairs with no evidence are skipped rather than counted as zero: a card should not be
	/// penalised for the pairs nobody has measured yet. With no evidence at all this returns 0
	/// and the caller falls back to card value alone.
	/// </summary>
	public double DeckFit(string name, Decklist deck)
	{
		var total = 0.0;
		var weight = 0;
		foreach (var (other, copies) in deck.Spells)
		{
			if (string.Equals(other, name, StringComparison.Ordinal))
				continue;
			var joint = JointDelta(name, other);
			if (double.IsNaN(joint))
				continue;
			total += joint * copies;
			weight += copies;
		}
		return weight == 0 ? 0 : total / weight;
	}

	/// <summary>
	/// The best-evidenced synergy partners for one card, strongest first — the kernel a seeded
	/// deck is built around.
	/// </summary>
	/// <summary>
	/// The partners that actually WIN alongside this card, best first.
	///
	/// Ranked on <see cref="JointDelta"/> — absolute joint performance — rather than on
	/// synergy. Ranking on synergy selected the worst cards in the format, because their
	/// independence baselines are the most pessimistic and so the easiest to beat. Requiring
	/// the pair to beat the base rate outright also removes the need for a separate
	/// card-quality gate: a bad-alone-but-genuinely-enabling card still qualifies, a
	/// bad-alone-and-bad-together one cannot.
	/// </summary>
	public IReadOnlyList<(string Name, double Joint)> TopPartners(
		string anchor,
		IReadOnlyList<Card> pool,
		int count
	) =>
		pool.Where(c => !string.Equals(c.Name, anchor, StringComparison.Ordinal))
			.Select(c => (c.Name, Joint: JointDelta(anchor, c.Name)))
			.Where(e => !double.IsNaN(e.Joint) && e.Joint > 0)
			.OrderByDescending(e => e.Joint)
			.ThenBy(e => e.Name, StringComparer.Ordinal) // deterministic ties
			.Take(count)
			.ToList();

	/// <summary>
	/// Cards whose constructed value has moved furthest from their draft value, both
	/// directions. THIS IS THE ACCEPTANCE TEST for the whole limited-vs-constructed concern:
	/// an empty or near-empty list means constructed data is not displacing the prior and the
	/// mode really is just building draft decks.
	///
	/// <c>Move</c> is CENTRED on the median movement, and that is not cosmetic. Both tables
	/// are games-in-hand rates, but a card is only credited for a game in which it was DRAWN,
	/// and constructed games end faster than limited ones — so the mean games-in-hand win rate
	/// differs between the two formats by several points even though both priors are 0.50.
	/// Measured on the first real run: draft 0.5162 against constructed 0.4439, a 7.2pp
	/// offset, which showed up as EVERY card having "moved down" by ~9pp.
	///
	/// Uncentred, the faller list is just the offset and the riser list is the cards that beat
	/// it — which reads as a dramatic finding and is an artifact. The ranking is unaffected
	/// (<see cref="SpearmanAgainstDraft"/> is rank-based), so only the magnitudes needed
	/// fixing. <c>RawMove</c> is kept so the offset itself stays visible.
	/// </summary>
	public IReadOnlyList<(
		string Name,
		double DraftPP,
		double ConstructedPP,
		double Move,
		double RawMove
	)> Movers(int minGames = 100)
	{
		if (_draft is null)
			return [];

		var draftPrior = _draft.Prior;
		var rows = _constructed
			.Cards.Where(c => c.Games >= minGames && _draftRates.ContainsKey(c.Name))
			.Select(c =>
			{
				var draftPP = 100.0 * (_draftRates[c.Name] - draftPrior);
				var constructedPP = CardDelta(c.Name);
				return (c.Name, draftPP, constructedPP, RawMove: constructedPP - draftPP);
			})
			.ToList();

		if (rows.Count == 0)
			return [];

		var offset = Median(rows.Select(r => r.RawMove).ToList());

		return rows.Select(r => (r.Name, r.draftPP, r.constructedPP, r.RawMove - offset, r.RawMove))
			.OrderByDescending(e => e.Item4)
			.ToList();
	}

	/// The systematic gap between the two tables — an artifact of comparing games-in-hand
	/// rates across formats with different game lengths, not a finding about any card.
	public double FormatOffset(int minGames = 100)
	{
		var rows = Movers(minGames);
		return rows.Count == 0 ? 0 : Median(rows.Select(r => r.RawMove).ToList());
	}

	private static double Median(IReadOnlyList<double> values)
	{
		if (values.Count == 0)
			return 0;
		var sorted = values.OrderBy(v => v).ToList();
		return sorted.Count % 2 == 1
			? sorted[sorted.Count / 2]
			: (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2.0;
	}

	/// <summary>
	/// Spearman rank correlation between the draft and constructed valuations over shared
	/// cards. ~1.0 means nothing has moved — see Movers.
	/// </summary>
	public double SpearmanAgainstDraft(int minGames = 100)
	{
		var rows = Movers(minGames);
		if (rows.Count < 3)
			return double.NaN;

		var draftRanks = Ranks(rows.Select(r => r.DraftPP).ToList());
		var constructedRanks = Ranks(rows.Select(r => r.ConstructedPP).ToList());

		var n = rows.Count;
		var meanRank = (n - 1) / 2.0;
		double num = 0,
			dx = 0,
			dy = 0;
		for (var i = 0; i < n; i++)
		{
			var a = draftRanks[i] - meanRank;
			var b = constructedRanks[i] - meanRank;
			num += a * b;
			dx += a * a;
			dy += b * b;
		}
		return dx > 0 && dy > 0 ? num / Math.Sqrt(dx * dy) : double.NaN;
	}

	/// Average rank for ties, so a table with repeated values does not skew the correlation.
	private static double[] Ranks(IReadOnlyList<double> values)
	{
		var order = Enumerable.Range(0, values.Count).OrderBy(i => values[i]).ToList();
		var ranks = new double[values.Count];
		var i2 = 0;
		while (i2 < order.Count)
		{
			var j = i2;
			while (j + 1 < order.Count && values[order[j + 1]] == values[order[i2]])
				j++;
			var avg = (i2 + j) / 2.0;
			for (var k = i2; k <= j; k++)
				ranks[order[k]] = avg;
			i2 = j + 1;
		}
		return ranks;
	}
}

/// <summary>
/// Load/save for the constructed table. Same counts-not-rates format as the draft model, so
/// runs merge and the scoring formula can be retuned without re-simulating.
/// </summary>
public static class ConstructedValuesStore
{
	public static string PathFor(string setCode) =>
		Path.Combine("sim_results", $"constructed_values_{setCode.ToLowerInvariant()}.json");

	/// <summary>
	/// Builds the table for a set: whatever constructed data exists, over whatever draft model
	/// exists. Both halves are optional and both degrade correctly when absent.
	/// </summary>
	public static ConstructedValues Load(string setCode, bool useDraftPrior = true)
	{
		var constructed = DraftTrainingStore.Load(PathFor(setCode)) ?? DraftTrainingData.Empty;
		var draft = useDraftPrior ? LoadDraftPrior(setCode) : null;
		return new ConstructedValues(constructed, draft);
	}

	/// <summary>
	/// The seeding prior for a set. For the combined pool this MERGES every set's model, since
	/// no single one covers the whole card list and an unmerged model would score two thirds
	/// of the pool at exactly the prior.
	/// </summary>
	public static DraftTrainingData? LoadDraftPrior(string setCode, bool enabled = true)
	{
		if (!enabled)
			return null;
		if (!string.Equals(setCode, SetRegistry.CombinedCode, StringComparison.OrdinalIgnoreCase))
			return DraftTrainingStore.Load(DraftTrainingStore.PathFor(setCode));

		DraftTrainingData? merged = null;
		foreach (var set in SetRegistry.All)
		{
			var data = DraftTrainingStore.Load(DraftTrainingStore.PathFor(set.Code));
			if (data is null)
				continue;
			merged = merged is null ? data : DraftTrainingData.Merge(merged, data);
		}
		return merged;
	}

	/// Merges this run's counts into whatever is on disk, so runs accumulate.
	public static void SaveMerged(DraftTrainingData fresh, string setCode)
	{
		var path = PathFor(setCode);
		var merged = DraftTrainingData.Merge(
			DraftTrainingStore.Load(path) ?? DraftTrainingData.Empty,
			fresh
		);

		var dir = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(dir))
			Directory.CreateDirectory(dir);
		File.WriteAllText(
			path,
			System.Text.Json.JsonSerializer.Serialize(
				merged,
				new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
			)
		);
	}
}
