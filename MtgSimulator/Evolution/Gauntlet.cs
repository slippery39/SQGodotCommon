using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Fixed reference decks a challenger is measured against, in ADDITION to the evolving field.
///
/// **This exists because mode 6 had no absolute reference point and could not tell that its whole
/// field was weak.** Fitness was win rate inside a closed round-robin, which averages exactly 50%
/// by construction — so eight decks converging on the same package report a perfectly healthy
/// metagame (8/8 viable, tight spread, diversity at its floor) while sitting ~15pp below a
/// hand-built deck. Measured against the evolved ALL field, 160 games each: **Zoo 66.2%,
/// Traditional Storm 63.1%**, with Jund/Goblins/Reanimator/Affinity/Dragonstorm at 44-51%.
///
/// Zoo is the important one. Storm alone would have suggested "hill climbing cannot reach combo";
/// Zoo is plain aggro, reachable by one-card steps, and beats the field by MORE. The field was not
/// failing to reach an exotic archetype — it was converging on something worse than an ordinary
/// deck and had no way to notice.
///
/// **The gauntlet is deliberately NOT part of the diversity constraint.** Challengers converging
/// onto a gauntlet deck is a desired outcome, not a failure: it means the field found the good
/// deck. Diversity is measured over the evolving field only, exactly as before.
/// </summary>
public static class Gauntlet
{
	/// <summary>
	/// Which reference decks to use for a pool.
	///
	/// **Per-pool by necessity, not by preference.** A gauntlet deck whose cards are absent from
	/// the evolution pool is a benchmark the field can never converge on. Measured: on ALL every
	/// registry deck is fully buildable (only Plains missing, which the mana base supplies), but
	/// on CSC they are 0-5 of 13 cards each — Zoo 1/13, Traditional Storm 0/12, Affinity 1/13.
	/// A CSC gauntlet has to be built from CSC cards; until it exists, CSC gets none.
	///
	/// **DES had none for exactly that reason and it was the pool everything was being measured
	/// in.** Designed is every set EXCEPT Legacy, and all nine original decks are Legacy lists, so
	/// the one absolute yardstick mode 6 has was silently absent from every recent run.
	/// <see cref="DesignedGauntletDecks"/> fills it.
	/// </summary>
	public static IReadOnlyList<string> For(string setCode)
	{
		static bool Is(string a, string b) =>
			string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

		// **The split is by which POOL each deck's cards come from, not by preference.** The nine
		// original decks are built from Legacy cards and the three CMB ones from Designed cards;
		// only the combined pool holds both. Offering a deck whose cards are absent is a benchmark
		// the field can never converge on — measured on CSC, where every registry deck was 0-5 of
		// 13 cards.
		if (Is(setCode, SetRegistry.CombinedCode))
			return DeckRegistry.All.Select(d => d.Name).ToList();

		if (Is(setCode, SetRegistry.LegacyCode))
			return DeckRegistry
				.All.Select(d => d.Name)
				.Except(DesignedGauntletDecks.Names, StringComparer.Ordinal)
				.ToList();

		if (Is(setCode, SetRegistry.DesignedCode))
			return DesignedGauntletDecks.Names;

		return [];
	}

	/// <summary>
	/// Cards a gauntlet deck names that the pool does not have, so a run can report up front
	/// whether its benchmark is reachable rather than leaving it as an invisible ceiling.
	///
	/// Lands are excluded: the pool deliberately holds no lands and the mana base is supplied by
	/// the deck builder, so "Plains is missing" is noise on every deck.
	/// </summary>
	public static IReadOnlyList<string> MissingFrom(string deckName, IReadOnlyList<Card> pool)
	{
		var have = pool.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
		return DeckRegistry
			.Build(deckName, 1)
			.Where(c => !c.HasSubtype("Land"))
			.Select(c => c.Name)
			.Distinct(StringComparer.Ordinal)
			.Where(n => !have.Contains(n))
			.ToList();
	}

	/// Distinct non-land card names in a gauntlet deck — what the card-value accumulator credits.
	public static IReadOnlyList<string> SpellNames(string deckName) =>
		DeckRegistry
			.Build(deckName, 1)
			.Where(c => !c.HasSubtype("Land"))
			.Select(c => c.Name)
			.Distinct(StringComparer.Ordinal)
			.ToList();
}
