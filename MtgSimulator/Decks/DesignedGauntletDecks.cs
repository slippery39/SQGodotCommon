using MtgCore;

namespace MtgSimulator;

/// <summary>
/// **Hand-built reference decks for the DESIGNED pool (HLM + CSC + CMB).**
///
/// `Gauntlet` existed but returned nothing for DES: every `DeckRegistry` deck is built from Legacy
/// cards, and DES is defined as every set EXCEPT Legacy. So the one absolute yardstick mode 6 has
/// was silently unavailable on the pool all recent work has been measured in — a field could
/// converge on something ~15pp below an ordinary hand-built deck and every internal metric would
/// still read healthy, which is exactly what was measured on ALL (Zoo 66.2% against the evolved
/// field).
///
/// **Three archetypes, chosen because their answers are known by construction.** CMB plants the
/// combos deliberately, so a hand-built list holding all of a package is a deck that provably works
/// — unlike a hand-built "good stuff" pile, whose quality would itself be a guess and whose loss
/// would tell you nothing.
///
/// ### How the support was chosen, and why it is not hand-waved
///
/// The combo cores are hand-picked (that is the point). The SUPPORT is drawn from cards the evolved
/// field itself converged on across several runs — Nissa, Scavenging Ooze, Vampire Nighthawk, Snuff
/// the Lantern, Barrin. Those are measured-competitive in this pool, so the comparison is "a human
/// assembling a known combo out of cards the search already likes" against "what the search built".
/// Picking support on taste would make a loss unreadable: bad deck, or bad archetype?
///
/// **The elf list deliberately plays Wirewood Conduit and Timberwatch Elder**, the two cards the
/// builder has never once proposed in any measured run. That is the whole reason this deck is worth
/// playing: it is the direct test of whether the omission is correct. Read the elf matchup as the
/// answer to that, not as a general statement about elves.
///
/// **Read the gap, not the rate.** Gauntlet decks overperforming means the builder still has work;
/// even or slightly behind means it is doing its job. These are NOT costed to a rate and were never
/// tuned — treat a big loss as a finding about the field and a big win as a finding about them.
/// </summary>
public static class DesignedGauntletDecks
{
	public const string Twin = "CMB Twin";
	public const string Elves = "CMB Elves";
	public const string Reanimator = "CMB Reanimator";

	public static IReadOnlyList<string> Names => [Twin, Elves, Reanimator];

	/// <summary>
	/// Two copiers, two untappers, and the cantrip-and-burn shell the real deck is built on.
	///
	/// The loop is a copier targeting an Illusionist it can untap; `LoopDetector` finds it in two
	/// actions and reports it lethal, so 16 combo cards is the maximum-redundancy version of a
	/// two-card kill rather than a pile of situational cards.
	///
	/// **Cantrips rather than mana dorks, which is what the archetype actually is.** The first
	/// version ramped with Llanowar Elves and Elvish Mystic to deploy the pair a turn early; real
	/// Splinter Twin does not ramp, it digs — Ponder, Preordain and Opt find the missing half, and
	/// Lightning Bolt is the interaction that also happens to be reach. A ramp shell tests "can the
	/// combo be deployed fast", a cantrip shell tests "can the combo be ASSEMBLED reliably", and the
	/// second is the question a two-card deck lives or dies on.
	/// </summary>
	public static IReadOnlyList<Card> BuildTwin(int ownerId) =>
		Build(
			ownerId,
			lands: 17,
			(4, "Mirevale Deceiver"),
			(4, "Tidebinder Sprite"),
			(4, "Twinflame Artisan"),
			(4, "Kilnmother Vess"),
			(4, "Ponder"),
			(4, "Preordain"),
			(4, "Opt"),
			(4, "Lightning Bolt"),
			(4, "Snuff the Lantern"),
			(4, "Scavenging Ooze"),
			(3, "Barrin, Tolarian Archmage")
		);

	/// <summary>
	/// Critical-mass elves. No loop here by construction — readiness is conserved, never created —
	/// so this is the "does a tribal engine assemble" arm rather than a combo.
	///
	/// Holds the two cards the builder never proposes, on purpose. See the type remarks.
	/// </summary>
	public static IReadOnlyList<Card> BuildElves(int ownerId) =>
		Build(
			ownerId,
			lands: 17,
			(4, "Wirewood Symbiont"),
			(4, "Wirewood Herald"),
			(4, "Llanowar Elves"),
			(4, "Elvish Mystic"),
			(4, "Wirewood Conduit"),
			(4, "Timberwatch Elder"),
			(4, "Dwynen's Elite"),
			(4, "Elvish Visionary"),
			(4, "Elvish Archdruid"),
			(4, "Nissa, Vastwood Seer"),
			(3, "Hoofthunder Colossus")
		);

	/// <summary>
	/// Entomb a fatty, reanimate it for one mana. Eight big creatures against the enablers that find
	/// and bury them — a deck holding only the eight-drop cannot be told apart from one that found
	/// the archetype and drew badly.
	///
	/// **Cantrips and a discard outlet, in place of the self-mill pile the first version played.**
	/// Reanimator wants to see a specific two cards, so Ponder and Preordain do more for it than a
	/// fifth mill spell; Faithless Looting is the classic outlet, and it is the card that makes an
	/// uncastable eight-drop in hand into a resource rather than a dead draw. The remaining
	/// graveyard cards are the ones measured as the pool's real enablers rather than the ones whose
	/// names read best.
	/// </summary>
	public static IReadOnlyList<Card> BuildReanimator(int ownerId) =>
		Build(
			ownerId,
			lands: 17,
			(4, "Consign to Rot"),
			(4, "Raise the Sunken"),
			(4, "Ponder"),
			(4, "Preordain"),
			(4, "Faithless Looting"),
			(4, "Drowned Acolyte"),
			(4, "Drown in the Mere"),
			(4, "Consult the Drowned"),
			(4, "Aurex, the Sevenfold"),
			(4, "Sunken Colossus"),
			(3, "Corpse Harvest")
		);

	/// <summary>
	/// Looked up by NAME from the set rather than through per-card accessors, so a card being
	/// renamed or dropped fails loudly here instead of silently building a 56-card deck.
	/// </summary>
	private static IReadOnlyList<Card> Build(
		int ownerId,
		int lands,
		params (int Copies, string Name)[] spells
	)
	{
		var pool = SetRegistry.Designed.Cards.ToDictionary(
			c => c.Name,
			c => c,
			StringComparer.OrdinalIgnoreCase
		);

		// `CardLibrary.Plains()` rather than a set lookup: basic lands are not part of any set's
		// card list (packs exclude them and the deck builder supplies the mana base), so
		// `GetByName("Plains")` throws. Every sibling factory takes lands from here too.
		var deck = new List<Card>();
		for (var i = 0; i < lands; i++)
			deck.Add(CardLibrary.Plains() with { OwnerId = ownerId, ControllerId = ownerId });

		foreach (var (copies, name) in spells)
		{
			if (!pool.TryGetValue(name, out var card))
				throw new InvalidOperationException(
					$"Gauntlet deck names '{name}', which is not in {SetRegistry.Designed.Code}."
				);
			for (var i = 0; i < copies; i++)
				deck.Add(card with { OwnerId = ownerId, ControllerId = ownerId });
		}

		return deck;
	}
}
