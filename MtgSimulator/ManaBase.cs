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
	/// The land count <see cref="SourcesNeeded"/> was measured at. Requirements are expressed in
	/// these units, which is what makes the feasibility test below independent of deck size: a
	/// 40-card limited deck runs 17 lands in 40 (42.5%) against 24 in 60 (40%), so the DENSITY a
	/// colour needs is very nearly the same and only the absolute count differs.
	/// </summary>
	public const int ReferenceLands = 24;

	/// <summary>
	/// What a whole deck needs from each colour, in sources per <see cref="ReferenceLands"/>.
	///
	/// **The MAXIMUM over the cards, not the sum.** A WW two-drop (18 sources) beside a W five-drop
	/// (9) needs eighteen white sources, not twenty-seven — the most demanding card sets the bar and
	/// every cheaper demand is met on the way. <see cref="Build"/> deliberately sums instead, because
	/// it is choosing PROPORTIONS and a colour asked for by more cards should get more lands; that is
	/// a weight, and this is a requirement. Do not confuse the two.
	///
	/// Summing ACROSS colours is right, because a basic produces exactly one colour, so the lands
	/// serving each demand are disjoint. Dual lands break that and this is the function they will
	/// break — see the manabase item in HANDOFF-Colours.md §6.
	/// </summary>
	public static ManaPool Requirements(IEnumerable<Card> spells)
	{
		var required = ManaPool.Empty;
		foreach (var card in spells)
		{
			var demand = DemandOf(card);
			foreach (var color in ManaPool.Colors)
				if (demand[color] > required[color])
					required = required.With(color, demand[color]);
		}
		return required;
	}

	/// <summary>
	/// How many sources short of casting all of <paramref name="spells"/> on curve a manabase is,
	/// in <see cref="ReferenceLands"/> units. Zero means the colours fit; positive means they do not
	/// and no allocation of basics can rescue it.
	///
	/// This is the arithmetic behind the headline of HANDOFF-Colours.md §2. WW plus UU is 18 + 18 =
	/// 36 against 24 — six lands' worth of impossible — while W plus U is 12 + 12 = 24 and fits
	/// exactly. Both pairs pass <see cref="ColorIdentity.Allows"/> identically, which is why legality
	/// was never enough on its own.
	///
	/// **Returned as a NUMBER rather than a bool on purpose.** Requiring every card on curve 90% of
	/// the time is stricter than any real deck is built to — decks cast their greediest card late and
	/// accept it. So the tolerance belongs to the caller: a detector asking whether an archetype can
	/// exist should allow a few sources of slack, a draft picker wants the gradient rather than a
	/// cliff, and neither wants this function to have decided for them.
	/// </summary>
	public static int Shortfall(IEnumerable<Card> spells) =>
		Math.Max(0, Requirements(spells).Total - ReferenceLands);

	/// <summary>
	/// How much of a card survives the manabase it is going into: 1 when every colour it needs has
	/// the sources <see cref="Requirements"/> asks for, falling toward 0 as they thin out. The WORST
	/// colour decides — a gold card is only as castable as its scarcer half.
	///
	/// This is the graded answer <see cref="Shortfall"/> deliberately does not give. Shortfall says
	/// "these colours do not all fit in 24 lands", which is true of nearly every real two-colour deck
	/// and so cannot be used as a gate; this says HOW BADLY, which can.
	///
	/// ponytail: linear in the ratio of sources HAD to sources NEEDED. The truthful curve is the
	/// hypergeometric one measured in <see cref="SourcesNeeded"/>, and it is an S rather than a line,
	/// so this over-rates a card sitting at half its sources. Upgrade path is to interpolate that
	/// table instead of the ratio.
	/// </summary>
	public static double Castability(Card card, ManaPool sources, int landCount)
	{
		if (card.ColorPips.IsEmpty || landCount <= 0)
			return 1.0;

		var needed = Requirements([card]);
		var worst = 1.0;

		foreach (var color in ManaPool.Colors)
		{
			if (needed[color] <= 0)
				continue;

			// Requirements are per ReferenceLands, so scale them to this deck's land count.
			var want = (double)needed[color] * landCount / ReferenceLands;
			worst = Math.Min(worst, Math.Min(1.0, sources[color] / want));
		}

		return worst;
	}

	/// <summary>The colour sources a manabase of this size would hold for these spells.</summary>
	public static ManaPool SourcesFor(IEnumerable<Card> spells, int landCount) =>
		Build(spells, landCount, ownerId: 0)
			.Aggregate(
				ManaPool.Empty,
				(sum, land) => sum.Add(land.GetComponent<LandColorComponent>()!.Produces)
			);

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
	/// Splits <paramref name="landCount"/> across the colours in proportion to their SUMMED demand.
	///
	/// **Threshold-first allocation was built here, measured, and removed.** The intuition — that a
	/// splashed card needs its nine sources rather than its share of the card count — is correct,
	/// and the pathology is real: measured over 400 mono-plus-two-card-splash decks on CSC, the
	/// splash colour gets **2.1 of 17 lands** for a card the table says needs nine or ten. It is a
	/// dead draw, exactly as HANDOFF-Colours.md §6 predicted.
	///
	/// What the measurement killed was fixing it HERE. A deck's <see cref="Requirements"/> exceed
	/// <see cref="ReferenceLands"/> in every realistic shape — 400 of 400 splash decks and 400 of
	/// 400 two-colour decks, because any 21-card mono core already contains a double pip worth 17-18
	/// sources on its own. So there is no allocation that serves both colours; someone is starved
	/// whatever this function does, and CHOOSING WHO is a deckbuilding decision. Handing the splash
	/// its threshold means taking two lands off a main colour full of double pips, and which of
	/// those decks wins is an empirical question this function cannot answer.
	///
	/// The decision therefore belongs to whoever assembles the deck, priced with
	/// <see cref="Shortfall"/> — a splash that cannot be paid for should not be drafted or built in
	/// the first place, which is also what run B's "decks collapse to mono inside a two-colour
	/// identity" was telling us. Do not rebuild the threshold gate here without a head-to-head
	/// showing the trade wins; a gate that never fires passed every test in the suite.
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
