namespace MtgCore;

/// <summary>
/// **Combo Proving Ground (CMB) — a test instrument, not a balanced set.**
///
/// Every other set here exists to be drafted. This one exists to answer one question: **can the
/// deckbuilder DISCOVER a combo?** HLM and CSC are deliberately low-powered and low-synergy, so a
/// run over them cannot distinguish "the builder cannot find combos" from "this pool has none" —
/// the exact pool-limitation-vs-builder-failure ambiguity the handoff names as the most valuable
/// open item. A pool with combos planted in it by hand is that discriminator, as a fixture with a
/// known answer rather than as a metric.
///
/// **It is a SUPPLEMENT, meant to be played inside DES (HLM + CSC + CMB), not alone.** Two reasons,
/// both structural:
///
/// - `DeckCore.MinPoolForBreadth` is 100 and `MaxSlotShare` is calibrated against a real format. On
///   a 60-card pool the breadth gate switches off entirely and every core passes, so a run over CMB
///   alone measures the fixture rather than the builder.
/// - The packages here are payoffs and enablers only. Filler, removal and a curve come from the
///   other two sets, which is also what forces the builder to CHOOSE the combo over good stuff
///   rather than having nothing else to play.
///
/// **Nothing here is costed to a rate.** Cards are pushed well past cube tier on purpose: the
/// measurement is whether an archetype is found and built, not whether it is fair. Win rate is the
/// wrong instrument for this set and a low one is not a defect — read cohesion and assembly.
///
/// **No card here reuses a name from HLM or CSC.** `SetRegistry` resolves duplicate names
/// last-registered-wins, and CMB registers last, so a shared name would silently replace the other
/// set's card in every union — an invisible behaviour swap. Where a package needs a piece those
/// sets already provide (Ponder, Preordain, Viscera Seer, Faithless Looting, Reanimate, Llanowar
/// Elves, Hangarback Walker) it is NOT reprinted here; the union supplies it.
///
/// Each package states what it is testing and what the expected outcome is, so a run can be read
/// against a prediction instead of interpreted after the fact.
/// </summary>
public static class ComboProving
{
	public const string Code = "CMB";
	public const string Name = "Combo Proving Ground";

	/// <summary>
	/// Creature types used as the narrow filters the packages are discoverable BY.
	///
	/// **This is the whole reason the combos are findable and it is not decoration.** `PoolFeatures`
	/// harvests demands from a card's own filters, so a copier reading "target creature you control"
	/// produces a demand answered by 400+ cards, which the breadth gate correctly discards — the
	/// archetype would be invisible. Reading "target Illusionist you control" produces a demand
	/// answered by four, which is a core. A conjunction has to be constructed, and a narrow printed
	/// filter is what there is to construct it from.
	/// </summary>
	public const string Illusionist = "Illusionist";

	public static IReadOnlyList<Card> Cards { get; } =
		[
			.. ComboProvingTwin.Cards,
			.. ComboProvingElves.Cards,
			.. ComboProvingReanimator.Cards,
			.. ComboProvingCounters.Cards,
			.. ComboProvingDrain.Cards,
		];

	public static CardSet Set { get; } = new(Code, Name, Cards);
}
