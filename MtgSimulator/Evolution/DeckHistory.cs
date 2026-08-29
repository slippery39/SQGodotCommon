namespace MtgSimulator;

/// <summary>
/// What one deck slot has learned about its OWN cards, from its own games.
///
/// The global pair table cannot answer "is this combination pulling its weight in THIS deck":
/// it is spread over 83 000 pairs at a median of 53 games each, and on a combined pool 58% of
/// the pair space is cross-set and can never have data at all, because sets are drafted
/// separately. Within one deck the same question is far better conditioned — ~15 distinct
/// cards means ~105 pairs rather than 83 000, every one of them a 4-of that is drawn in most
/// games, so pairs reach hundreds or thousands of games within a few generations.
///
/// **This is why summing here is safe when summing globally was not.** MtgSimulator/CLAUDE.md
/// measured that adding ~27 thin cross-pool pair deltas accumulates noise as sqrt(27) against
/// a small signal and costs win rate. The pairs here are dense, few, and about the deck being
/// scored rather than about the card pool — a different quantity, measured on a different
/// event, at two orders of magnitude more evidence per pair.
///
/// Used for CUTTING only. Selection stays on overall win rate plus curve: a card not yet in
/// the deck has no pair history in it, so this can say nothing about it.
/// </summary>
public sealed class DeckHistory
{
	/// Denser evidence than the global table, so a lighter shrink than its 200.
	public const int PairShrinkK = 50;
	public const int CardShrinkK = 25;

	private readonly double _prior;
	private readonly Dictionary<string, CardStat> _cards;
	private readonly Dictionary<string, List<PairStat>> _pairsByCard;

	public DeckHistory(DraftTrainingData data)
	{
		_prior = data.Prior;
		_cards = data.Cards.ToDictionary(c => c.Name, StringComparer.Ordinal);

		_pairsByCard = new Dictionary<string, List<PairStat>>(StringComparer.Ordinal);
		foreach (var p in data.Pairs)
		{
			if (p.Games < ConstructedValues.MinPairGames)
				continue;
			Add(p.A, p);
			Add(p.B, p);
		}

		void Add(string key, PairStat p)
		{
			if (!_pairsByCard.TryGetValue(key, out var list))
				_pairsByCard[key] = list = [];
			list.Add(p);
		}

		var scored = data
			.Pairs.Where(p => p.Games >= ConstructedValues.MinPairGames)
			.Select(p =>
				100.0 * (DraftTrainingData.Shrink(p.Wins, p.Games, _prior, PairShrinkK) - _prior)
			)
			.ToList();
		MeanPairDelta = scored.Count == 0 ? 0 : scored.Average();
	}

	public int DeckGames => _cards.Count == 0 ? 0 : _cards.Values.Max(c => c.DeckGames);

	/// <summary>
	/// How well this card itself has done in this deck, in percentage points against the
	/// deck's own base rate. The baseline is the DECK's rate, not a global one — the question
	/// is whether the card is pulling its weight in the shell it is actually in.
	/// </summary>
	public double CardDelta(string name) =>
		_cards.TryGetValue(name, out var c)
			? 100.0 * (DraftTrainingData.Shrink(c.Wins, c.Games, _prior, CardShrinkK) - _prior)
			: 0;

	/// <summary>
	/// Summed performance of every well-evidenced pair this card is part of, restricted to
	/// partners still in the deck.
	///
	/// **Summed rather than averaged, deliberately** — a card carrying four good pairs should
	/// be harder to cut than one carrying a single good pair, which is what "bias toward the
	/// card that shows up in more pairs" means. Averaging throws that away and makes a card
	/// with one lucky pair look identical to a linchpin.
	///
	/// Measured against the deck's own base rate rather than an independence baseline: inside
	/// one deck the question is simply "do the games where both were drawn go better than this
	/// deck's average game", and the cards' individual rates are already carried by
	/// <see cref="CardDelta"/>.
	/// </summary>
	public double PairDelta(string name, Decklist deck)
	{
		var measured = 0.0;
		var seen = 0;

		if (_pairsByCard.TryGetValue(name, out var pairs))
		{
			foreach (var p in pairs)
			{
				var other = string.Equals(p.A, name, StringComparison.Ordinal) ? p.B : p.A;
				if (!deck.Spells.ContainsKey(other))
					continue; // partner already cut; its record is not evidence about now
				measured +=
					100.0
					* (DraftTrainingData.Shrink(p.Wins, p.Games, _prior, PairShrinkK) - _prior);
				seen++;
			}
		}

		// **Unmeasured pairs are imputed at the deck's mean, not at zero.** Summing only what
		// has been measured turns this into a TENURE bonus: a card added this generation has
		// no pair clearing MinPairGames, so it scores exactly 0, while an entrenched card in
		// ten measured pairs carries +20 — making every new card the weakest thing in the deck
		// on arrival and cutting it before it can prove anything.
		//
		// That is not hypothetical. It threw away the two best cards in the format: Ancestral
		// Recall (measured +14.91pp in constructed) cut after 812 games and Sol Ring (+20.62pp,
		// the highest in the run) after 2 209, while an +11.04pp card that happened to arrive
		// early accumulated 27 264.
		//
		// Imputing at the deck's own mean pair delta is the honest estimate for a pair with no
		// evidence — unknown, not bad — and it makes a new card comparable to an old one
		// instead of automatically last.
		var unmeasured = Math.Max(0, deck.DistinctSpells - 1 - seen);
		return measured + unmeasured * MeanPairDelta;
	}

	/// <summary>
	/// The typical measured pair in this deck, in percentage points. Cached because
	/// <see cref="PairDelta"/> is called once per card per cut proposal.
	/// </summary>
	public double MeanPairDelta { get; }

	/// <summary>
	/// How much this deck wants to keep this card. Higher is safer from the cut.
	///
	/// A card that loses on its own AND drags down every pair it is in is the first thing to
	/// go; a card that wins in several combinations is protected even if its solo rate is
	/// unremarkable, which is the whole point — that is what a synergy piece looks like.
	/// </summary>
	public double KeepScore(string name, Decklist deck) => CardDelta(name) + PairDelta(name, deck);

	/// Number of well-evidenced pairs this card is part of within the deck — diagnostics.
	public int SupportCount(string name, Decklist deck) =>
		_pairsByCard.TryGetValue(name, out var pairs)
			? pairs.Count(p =>
				deck.Spells.ContainsKey(
					string.Equals(p.A, name, StringComparison.Ordinal) ? p.B : p.A
				)
			)
			: 0;
}
