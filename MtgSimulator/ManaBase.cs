using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Builds a deck's basic lands to match the colours its spells actually need.
///
/// Every deck builder in the project used to pad with Plains, which was correct while the engine
/// had no colours and silently catastrophic the moment it did: a drafted blue-black deck padded
/// with 17 Plains cannot cast a single card in it. All three padding sites — Draft.BuildDeck,
/// CardPool.BuildRandomDeck and Decklist.Materialize — route through here so that failure cannot
/// come back in one of them.
///
/// Allocation is by MEASURED DEMAND, not by pip count. What a colour needs from a manabase is
/// "enough sources to cast this card ON THE TURN IT COSTS", and that number depends on both how
/// deep the pip is and how early the card wants to be cast — a turn-two double pip is a far
/// heavier demand than a turn-six single one. <see cref="SourcesNeeded"/> holds the measured
/// numbers; summing it over the deck's cards is the demand.
///
/// **The demands are measured in THIS engine and paper Magic's tables do not transfer**, because
/// the opening hand here is guaranteed to contain exactly three lands
/// (<c>SetupGameAction.OpeningHandLandCount</c>) drawn uniformly from the manabase. That makes
/// early colour access markedly more reliable than a real seven-card draw, so a table lifted from
/// paper systematically over-builds.
/// </summary>
public static class ManaBase
{
	/// <summary>
	/// <paramref name="landCount"/> basics matching <paramref name="spells"/>' colour demand.
	///
	/// A deck with no coloured cards at all gets Plains, preserving the old behaviour exactly for
	/// the colourless preconstructed decks.
	///
	/// Every colour the deck actually uses is guaranteed at least one source when there are
	/// enough lands to go round: a single splashed card is otherwise liable to round to zero and
	/// become a dead draw, which is a strictly worse outcome than one fewer source of the main
	/// colour. Allocation is by largest remainder over ManaPool.Colors' fixed order, so it is
	/// deterministic — the simulator's determinism tests depend on that.
	/// </summary>
	public static List<Card> Build(IEnumerable<Card> spells, int landCount, int ownerId)
	{
		var lands = new List<Card>(Math.Max(0, landCount));
		if (landCount <= 0)
			return lands;

		var demand = spells.Aggregate(ManaPool.Empty, (sum, card) => sum.Add(DemandOf(card)));

		foreach (var (color, count) in Allocate(demand, landCount))
			for (var i = 0; i < count; i++)
				lands.Add(
					CardLibrary.BasicLand(color) with
					{
						OwnerId = ownerId,
						ControllerId = ownerId,
					}
				);

		return lands;
	}

	/// <summary>
	/// What one card asks of each colour, in units of "sources needed to cast it on curve".
	///
	/// The card's curve position is <c>max(ManaCost, total pips)</c>, not ManaCost alone. Generic
	/// and coloured mana come from separate tracks here, but every land grants exactly one of each,
	/// so a card with two White pips needs two lands to pay them however little generic it costs —
	/// a one-mana WW card is a turn-TWO play, and asking the table for its turn-one demand would
	/// understate it.
	/// </summary>
	private static ManaPool DemandOf(Card card)
	{
		var pips = card.ColorPips;
		if (pips.IsEmpty)
			return ManaPool.Empty;

		var turn = Math.Max(card.ManaCost, pips.Total);
		var demand = ManaPool.Empty;

		foreach (var color in ManaPool.Colors)
			if (pips[color] > 0)
				demand = demand.Add(color, SourcesNeeded(pips[color], turn));

		return demand;
	}

	/// <summary>
	/// Sources of one colour needed to cast a card with <paramref name="pips"/> pips of it on the
	/// turn it costs, about 90% of the time, in a 60-card deck.
	///
	/// **Measured, not borrowed.** Monte Carlo over this engine's real rules — 24 lands in 60, an
	/// opening hand of exactly three lands sampled uniformly from the manabase, one land per turn,
	/// target colour played first. The 22-land table differs by at most one source, so a single
	/// table covers the legal land range.
	///
	/// The shape of it is the important part, and it is what makes colour a real constraint:
	///
	/// | pips | cost 1 | 2  | 3  | 4  | 5  | 6  |
	/// |------|--------|----|----|----|----|----|
	/// | 1    | 13     | 12 | 11 | 10 | 9  | 9  |
	/// | 2    | -      | 18 | 17 | 17 | 15 | 15 |
	/// | 3    | -      | -  | 22 | 22 | 21 | 20 |
	///
	/// **A single pip is nearly free and a double pip is nearly mono-colour.** A 24-land two-colour
	/// deck split 12/12 casts a single pip 89% of the time on turn one and 94% by turn three, but a
	/// double pip only 65% by turn three — because 17 of its 24 lands would have to be one colour.
	/// So the colour constraint in this format lives almost entirely in double pips, and a WW card
	/// is in practice a mono-white card. Triple pips need 20-22 and are mono-only even then.
	/// </summary>
	private static int SourcesNeeded(int pips, int turn)
	{
		var t = Math.Clamp(turn, 1, 6) - 1;
		return Math.Clamp(pips, 1, 3) switch
		{
			1 => new[] { 13, 12, 11, 10, 9, 9 }[t],
			2 => new[] { 18, 18, 17, 17, 15, 15 }[t],
			_ => new[] { 22, 22, 22, 22, 21, 20 }[t],
		};
	}

	/// <summary>
	/// Splits <paramref name="landCount"/> across the colours in proportion to their demand.
	///
	/// ponytail: proportional, where the real question is a threshold one — a splashed card needs
	/// its 9 sources or it is a dead draw, and does not care that it is only two cards of the deck.
	/// Proportional allocation under-serves a small splash and over-serves a large one. Upgrade
	/// path is to satisfy demands in priority order and only fall back to proportional when they
	/// cannot all be met; do it when a measured run shows splashes failing, not before.
	/// </summary>
	private static List<(ManaColor Color, int Count)> Allocate(ManaPool demand, int landCount)
	{
		// No coloured cards — the deck is colourless, so any basic will do.
		if (demand.IsEmpty)
			return [(ManaColor.White, landCount)];

		var used = ManaPool.Colors.Where(c => demand[c] > 0).ToList();
		var counts = new Dictionary<ManaColor, int>();

		// One guaranteed source per colour used, while there are lands to spare. Beyond that the
		// split is proportional, so a heavy main colour still gets the bulk.
		var guaranteed = Math.Min(used.Count, landCount);
		foreach (var color in used)
			counts[color] = used.IndexOf(color) < guaranteed ? 1 : 0;

		var remaining = landCount - guaranteed;
		var total = demand.Total;

		var exact = used.ToDictionary(c => c, c => (double)remaining * demand[c] / total);
		foreach (var color in used)
			counts[color] += (int)Math.Floor(exact[color]);

		// Largest remainder, ties broken by ManaPool.Colors order so the result is deterministic.
		var leftover = landCount - counts.Values.Sum();
		foreach (
			var color in used.OrderByDescending(c => exact[c] - Math.Floor(exact[c]))
				.ThenBy(ManaPool.Colors.IndexOf)
				.Take(Math.Max(0, leftover))
		)
			counts[color]++;

		return used.Select(c => (c, counts[c])).Where(x => x.Item2 > 0).ToList();
	}
}
