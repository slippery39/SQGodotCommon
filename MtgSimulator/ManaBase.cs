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
/// Deliberately proportional to PIP COUNT rather than card count, because pips are what a land
/// has to pay. A deck with four double-black cards and eight single-red ones needs its base split
/// 8:8, not 4:8 — and under the depleting model that difference is the whole ball game, since
/// colour is spent per turn rather than being a one-off threshold.
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

		var demand = spells.Aggregate(ManaPool.Empty, (sum, card) => sum.Add(card.ColorPips));

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
