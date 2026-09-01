using System.Text.Json;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// One concept, the deck built to serve it, and whether that deck's engine actually assembles.
/// </summary>
/// <param name="Coverage">
/// Depth as a share of the deck's own enabler copies — "how much of my support was down when the
/// payoff landed". Raw depth alone ranks broad concepts first for free, because a deck with 30
/// enablers deploys more of them than a deck with 12 whether or not either is an archetype. This
/// is the same bias `DeckBuilder.PickConcept`'s inverse weighting exists to correct, one step
/// further down the pipeline.
/// </param>
/// <param name="ControlDepth">
/// The same probe, run against a pile of the format's best cards holding the SAME payoffs.
/// </param>
/// <param name="Lift">
/// <c>MedianDepth - ControlDepth</c>. **This is the only column that distinguishes a synergy from
/// a coincidence**, and without it most of the report is noise: a demand like "a creature entered
/// the battlefield" has 453 suppliers — every creature in the pool — so a deck of creatures plus
/// one ETB payoff scores near the top while being an ordinary midrange pile. It scores just as
/// well in the control, so its lift is ~0 and it falls off the report. Goblins, storm and affinity
/// keep theirs, because their payoffs genuinely do nothing in a good-stuff deck.
///
/// Same instrument that killed the goldfish: hold the payoffs fixed, change the support, and see
/// whether the measurement moves.
/// </param>
/// <param name="Payoffs">
/// **Every pool card that asks this demand — the archetype's payoff pool, not a decklist.**
/// </param>
/// <param name="Enablers">Every pool card that answers it. Together with Payoffs, the pool a
/// deck for this archetype should be built out of.</param>
/// <param name="Deck">
/// One sample from that pool, built by `SeedConcept` and used to take the measurements. It is
/// evidence that the pool supports a deck, not the recommended list.
/// </param>
/// <param name="DeadInDeck">
/// Cards in <paramref name="Deck"/> starving on at least one of their demands.
/// </param>
/// <param name="Core">
/// **What this archetype must CONTAIN, read off the payoff card's own demands.** Replaces the
/// single `DemandIndex` this record used to be keyed on, and that change is the whole point:
/// a demand-keyed candidate probes "spells cast" and "Dragon" as two unrelated concepts, so
/// Dragonstorm — a conjunction of exactly those two — could never be a candidate at all. It is
/// also why tribal decks were found easily and combo decks were not; one demand with a big
/// supplier set is something mutation stumbles into, a conjunction of narrow demands is not.
///
/// This is the contract the evolver wants too. `MetagameEvolver` used to rebuild a core out of
/// `Payoffs`/`Enablers` with invented minimums; now it reads this.
/// </param>
public sealed record EngineCandidate(
	string Name,
	DeckCore Core,
	string Concept,
	string Origin,
	int SuppliersInPool,
	Decklist Deck,
	IReadOnlyList<string> Payoffs,
	IReadOnlyList<string> Enablers,
	IReadOnlyList<string> DeadInDeck,
	double AssemblyRate,
	double MedianDepth,
	double Coverage,
	double ControlDepth,
	double Lift,
	double MedianTurn,
	double MedianSpeed,
	int Wins,
	float Bare = 0f,
	float Supplied = 0f,
	bool LeverageMeasured = false
)
{
	/// How much the payoff gains from having its demands answered.
	public float Leverage => Supplied - Bare;

	/// <summary>
	/// **Rank key: cards that are worth NOTHING on their own come first.**
	///
	/// Leverage alone ranks tribal above combo — measured, and backwards for the problem this mode
	/// exists to solve. A lord reads bare 11.67 and supplied 105.67, so it posts the biggest
	/// leverage in the pool while being a perfectly good card by itself; Dragonstorm reads bare
	/// **0.00**, which is what a combo payoff actually looks like. `Bare` is the column that
	/// separates "this card is a blank until assembled" from "this card scales", and only the first
	/// is the class local search cannot reach on its own.
	///
	/// Clamped at zero so a card that is actively BAD alone sits in the same tier as one worth
	/// nothing, with leverage breaking the tie. Unmeasured candidates sort last rather than
	/// pretending to a bare of zero, which would put every failed measurement at the top.
	/// </summary>
	/// <remarks>
	/// **Zero leverage means it is not a payoff, so it leaves the blank tier.** A card that gained
	/// NOTHING from having its demands answered does not want a deck built around it, whatever its
	/// bare value — that is what leverage means, not a threshold anyone tuned. Without this, Entomb
	/// (0.00 → 0.00, an enabler wearing a payoff's clothes) ranked third, above Flameshadow
	/// Conjuring at 1.50 → 24.50.
	/// </remarks>
	public (int Unmeasured, double Bare, double Leverage) BlankFirstKey =>
		(LeverageMeasured && Leverage > 0f ? 0 : 1, Math.Max(Bare, 0f), -Leverage);
}

public sealed record EngineReport(
	string SetCode,
	int Seed,
	int GamesPerEngine,
	IReadOnlyList<EngineCandidate> Engines
);

/// <summary>
/// **Phase one: what engines does this pool support, and do they assemble?**
///
/// No battles, no win rate, no evolution. Every viable concept in the pool gets a deck built for
/// it and plays solitaire games; <see cref="EngineProbe"/> reports whether the payoff went off with
/// its support deployed. The output is a report you READ and a file the evolver can seed from.
///
/// **The split exists because win rate cannot judge an engine that is not built yet.** A
/// half-assembled storm deck loses every game, so hill climbing on win rate walks away from it
/// before it is finished — which is what every run of mode 6 has done, correctly, for the wrong
/// reason. Asking "does it assemble" first, and "is it competitive" second, is the only ordering
/// where the first question has an answer.
///
/// **Every viable concept is probed rather than a ranked top slice.** Taking the N most
/// distinctive gives N demands with exactly <see cref="DeckBuilder.MinConceptSuppliers"/>
/// suppliers, which is the narrow tail rather than a survey, and the question this mode exists to
/// answer is what the pool supports — a cap would hide the answer. Cost is linear in concepts;
/// turn `gamesPerEngine` down if a pool ever makes it hurt.
/// </summary>
public static class EngineDiscovery
{
	/// <summary>
	/// Probes every concept in the pool and returns them ranked by whether the engine works.
	///
	/// Deterministic: each concept gets its own seed derived from <paramref name="seed"/>, games
	/// run into a pre-allocated array, and the sort has an explicit name tiebreak. Same seed in,
	/// same report out.
	/// </summary>
	public static EngineReport Run(
		CardSet set,
		int gamesPerEngine = 10,
		int seed = 0,
		int aiDepth = 2,
		TextWriter? log = null
	)
	{
		var writer = log ?? Console.Out;
		// Spells only, matching `MetagameEvolver` exactly — a mana base is a scalar on `Decklist`
		// and `Materialize` builds it from `CardLibrary.Plains()` rather than looking it up here.
		var spells = set.Cards.Where(c => !c.HasSubtype("Land")).ToList();
		var pool = ConstructedGameSetup.PoolIndex(spells);
		var values = ConstructedValuesStore.Load(set.Code);

		writer.WriteLine($"Reading pool features for {set.Code} ({spells.Count} spells)...");
		var features = PoolFeatures.Build(spells);

		// A failure here is an engine bug, not a quirk of the extractor — a spec must return "not
		// a match" for an id it cannot make sense of.
		foreach (var failure in features.Failures)
			writer.WriteLine($"  WARNING  {failure}");

		// **Candidates are payoff CARDS now, not demand indices.** Every card that asks something
		// answerable gets the core its own demands describe; the majority of any pool asks nothing
		// and drops out here.
		var cores = spells
			.Select(c => DeckCore.For(features, c.Name))
			.OfType<DeckCore>()
			// **Deduped on the payoff slot, which IS the archetype's identity.** Cards asking the
			// same things produce the byte-identical core: on ALL, ~64 payoffs that merely target a
			// creature collapse to one entry, and probing each separately would be 64 runs of the
			// same experiment charged to the same budget.
			.GroupBy(
				c => string.Join("|", c.Slots[0].Cards.Order(StringComparer.Ordinal)),
				StringComparer.Ordinal
			)
			.Select(g => g.OrderBy(c => c.Name, StringComparer.Ordinal).First())
			.OrderBy(c => c.Name, StringComparer.Ordinal)
			.ToList();

		writer.WriteLine(
			$"{features.Demands.Count} demands, {cores.Count} distinct cores "
				+ $"({cores.Count(c => c.Slots.Count > 2)} with a conjunction), "
				+ $"{gamesPerEngine} solitaire games each ({cores.Count * gamesPerEngine * 2} games)."
		);
		writer.WriteLine();

		// Sequential and before the parallel batch: each measurement runs its own rollouts, and the
		// whole 783-card pool takes ~11s, so ~50 cores is seconds. Cheap enough not to bother
		// parallelising, and keeping it out of the batch keeps the batch's determinism argument
		// simple.
		writer.WriteLine($"Measuring leverage for {cores.Count} payoffs...");
		var leverage = CardValueSandbox
			.MeasureLeverage(cores.Select(c => c.Name).ToList(), features, pool)
			.ToDictionary(r => r.Name, r => r, StringComparer.Ordinal);

		var probed = new EngineCandidate?[cores.Count];
		var done = 0;

		Parallel.For(
			0,
			cores.Count,
			i =>
			{
				probed[i] = Probe(
					cores[i],
					i,
					features,
					spells,
					pool,
					values,
					gamesPerEngine,
					seed,
					aiDepth,
					leverage.GetValueOrDefault(cores[i].Name)
				);
				var n = Interlocked.Increment(ref done);
				if (n % 25 == 0)
					writer.WriteLine($"  ...{n}/{cores.Count}");
			}
		);

		// **Ranked BLANK-FIRST — lowest bare value, then most leverage.** Two earlier orderings were
		// tried and both are wrong for this mode's purpose:
		//
		//   assembly rate  — puts "a creature entered" on top, since 453 of 783 cards answer it and
		//                    any creature deck satisfies it by accident.
		//   LIFT           — right about synergy-vs-coincidence, and kept as a column, but it ranks
		//                    a tribal lord and a combo payoff together.
		//   leverage       — measured, and it ranks TRIBAL ABOVE COMBO: Drogskol Captain posts +94
		//                    while being a fine card alone, Dragonstorm posts +32 from bare 0.00.
		//
		// A card that does nothing until its demands are met is the class local search provably
		// cannot reach; a card that merely gets better is one the existing search already finds.
		// `bare` is what tells them apart.
		var engines = probed
			.OfType<EngineCandidate>()
			.OrderBy(e => e.BlankFirstKey)
			.ThenByDescending(e => e.Lift)
			.ThenBy(e => e.Concept, StringComparer.Ordinal)
			.ToList();

		// **Self-check on the control, because this exact measurement has been vacuous twice.**
		// Both times the control was structurally unable to deploy an enabler — once because the
		// filler excluded them, once because the enabler set was defined over the concept deck —
		// so every control scored ~0 and lift was just depth wearing a hat. A control that never
		// scores is not a baseline, and the only symptom is a plausible-looking column.
		var bestControl = engines.Count == 0 ? 0 : engines.Max(e => e.ControlDepth);
		if (bestControl < 2.0)
			writer.WriteLine(
				$"\n  WARNING  no control deck scored above {bestControl:F1}. LIFT is measuring"
					+ " nothing — the control cannot deploy enablers. Do not rank on it."
			);

		// **Named, not silently dropped.** A concept that cannot be built is evidence about the
		// pool, and a report that just shows fewer rows than it probed is indistinguishable from
		// one where the builder is broken — which is how the missing-payoff defect above survived
		// its first run.
		var unbuildable = cores.Where((_, i) => probed[i] is null).ToList();
		if (unbuildable.Count > 0)
		{
			writer.WriteLine();
			writer.WriteLine($"{unbuildable.Count} cores produced no buildable deck:");
			foreach (var c in unbuildable)
				writer.WriteLine(
					$"  {c.Name}   ({string.Join(" + ", c.Slots.Select(s => s.Role))})"
				);
		}

		return new EngineReport(set.Code, seed, gamesPerEngine, engines);
	}

	/// <summary>
	/// One core, the deck it describes, and whether that deck's engine actually assembles.
	///
	/// **The deck is the core plus good stuff, and that is deliberate rather than lazy.** The core
	/// slots are the archetype; everything else is a flex slot, which is the one part of
	/// deckbuilding the existing machinery is already good at. It also makes LIFT a cleaner
	/// measurement than it was under `SeedConcept`: the control is the same payoffs plus the same
	/// good stuff, so the ONLY difference between the two arms is the core's support slots.
	/// </summary>
	private static EngineCandidate? Probe(
		DeckCore core,
		int slot,
		PoolFeatures features,
		IReadOnlyList<Card> spells,
		IReadOnlyDictionary<string, Card> pool,
		ConstructedValues values,
		int games,
		int seed,
		int aiDepth,
		CardLeverage? leverage
	)
	{
		// Derived from the core's identity rather than the loop index, so a candidate's seed does
		// not move when an unrelated card is added to the pool and the dedupe reorders.
		var stamp = Math.Abs(StringComparer.Ordinal.GetHashCode(core.Name)) % 100_003;
		var rng = new Random(seed + stamp * 1_009 + 17);

		var coreCards = core.Slots.SelectMany(s => s.Cards).ToHashSet(StringComparer.Ordinal);
		var lands = DeckBuilder.LandsForConcept(spells.Where(c => coreCards.Contains(c.Name)), rng);

		var deck = core.Satisfy(Decklist.Empty($"Engine-{slot}") with { Lands = lands }, values);
		deck = FillWithBestCards(deck, spells, values, _ => false);
		if (!deck.IsValid || !core.Holds(deck))
			return null;

		var probe = EngineProbe.FromCore(core, deck);
		if (!probe.IsUsable)
			return null;

		var (speed, _, wins, readings) = Goldfish.Measure(
			deck,
			pool,
			games,
			seed + 400_000 + stamp * 101,
			aiDepth,
			probe
		);

		var (depth, turn, rate) = EngineProbe.Summarise(readings);
		var enablerCopies = deck
			.Spells.Where(kv => probe.Enablers.Contains(kv.Key))
			.Sum(kv => kv.Value);

		// The control: same payoffs, same land count, support replaced by the format's best cards.
		// One variable changed, so the difference is attributable to the concept and nothing else.
		var control = GoodStuffControl(deck, probe, spells, values);
		var controlDepth = 0.0;
		if (control is not null)
		{
			var (_, _, _, controlReadings) = Goldfish.Measure(
				control,
				pool,
				games,
				// Same seeds as the concept arm: common random numbers, exactly as
				// `MetagameEvolver.Seed` does it. The two decks see identical shuffles, so the
				// shared variance cancels in the difference instead of drowning it.
				seed + 400_000 + stamp * 101,
				aiDepth,
				probe
			);
			(controlDepth, _, _) = EngineProbe.Summarise(controlReadings);
		}

		var support = core
			.Slots.Skip(1)
			.SelectMany(s => s.Cards)
			.Distinct(StringComparer.Ordinal)
			.Order(StringComparer.Ordinal)
			.ToList();

		return new EngineCandidate(
			deck.Name,
			core,
			core.Name,
			string.Join(" + ", core.Slots.Skip(1).Select(s => s.Role)),
			support.Count,
			deck,
			// Pool-wide, NOT the probe's sets. The probe scopes payoffs to the deck because the
			// control comparison needs "the same payoffs"; the report is answering a different
			// question — what cards belong to this archetype at all.
			core.Slots[0].Cards.Order(StringComparer.Ordinal).ToList(),
			support,
			features.DeadCards(deck),
			rate,
			depth,
			enablerCopies == 0 ? 0 : depth / enablerCopies,
			controlDepth,
			depth - controlDepth,
			turn,
			speed,
			wins,
			leverage?.Bare ?? 0f,
			leverage?.Supplied ?? 0f,
			leverage?.WasMeasured ?? false
		);
	}

	/// <summary>
	/// The concept's payoffs, at the same copy counts, in a pile of the format's best cards.
	///
	/// **The support is the only thing that changes.** If a payoff executes just as readily
	/// surrounded by the highest-win-rate cards in the format as it does surrounded by its own
	/// concept, then the concept is contributing nothing and what the report found is a
	/// coincidence of vocabulary rather than a synergy.
	///
	/// **Enablers are NOT excluded from the filler, and excluding them made this measure nothing.**
	/// The first version filtered them out to stop the control "rebuilding the concept". That
	/// guarantees the control cannot deploy an enabler, so its depth is 0 by construction and lift
	/// is just depth under another name — measured, 18 of 43 controls scored exactly 0.0 and the
	/// column ranked identically to the one it was meant to correct.
	///
	/// The exclusion was also wrong on its own terms. If the format's best cards genuinely ARE
	/// artifacts, then an artifact deck in that format is not a synergy deck, it is the good cards
	/// — and the control saying so is the correct answer, not a failure mode. A good-stuff pile is
	/// whatever the good stuff is.
	/// </summary>
	private static Decklist? GoodStuffControl(
		Decklist concept,
		EngineProbe probe,
		IReadOnlyList<Card> spells,
		ConstructedValues values
	)
	{
		var control = Decklist.Empty($"{concept.Name}-control") with { Lands = concept.Lands };

		foreach (var name in probe.Payoffs)
			if (concept.CopiesOf(name) > 0)
				control = control.WithCopies(name, concept.CopiesOf(name));

		if (control.SpellCount == 0)
			return null;

		// Payoffs are skipped so the control keeps the concept's exact copy counts — "the same
		// payoffs" is the one thing held fixed between the arms, and topping them up to four would
		// change it.
		control = FillWithBestCards(control, spells, values, c => probe.Payoffs.Contains(c.Name));

		return control.IsValid ? control : null;
	}

	/// <summary>
	/// Fills the free slots with the format's best cards — the FLEX half of a deck.
	///
	/// Shared by the concept arm and the control so the two cannot drift: if the filler differed
	/// between them, LIFT would be measuring the filler.
	/// </summary>
	private static Decklist FillWithBestCards(
		Decklist deck,
		IReadOnlyList<Card> spells,
		ConstructedValues values,
		Func<Card, bool> skip
	)
	{
		foreach (
			var card in spells
				.Where(c => !skip(c))
				.OrderByDescending(c => values.CardDelta(c.Name))
				.ThenBy(c => c.Name, StringComparer.Ordinal)
		)
		{
			var need = Decklist.DeckSize - deck.Lands - deck.SpellCount;
			if (need <= 0)
				break;
			var have = deck.CopiesOf(card.Name);
			if (have >= Decklist.MaxCopies)
				continue;
			deck = deck.WithCopies(card.Name, Math.Min(Decklist.MaxCopies, have + need));
		}

		return deck;
	}

	/// <summary>
	/// The ranked table. Read the top of it and ask whether those are decks you recognise.
	/// </summary>
	public static void Print(EngineReport report, int keep, TextWriter? log = null)
	{
		var writer = log ?? Console.Out;

		writer.WriteLine();
		writer.WriteLine($"=== Engine discovery: {report.SetCode} ===");
		writer.WriteLine(
			"  assem = share of games where a payoff resolved with support already deployed"
		);
		writer.WriteLine("  cover = that support as a share of the deck's own enabler copies");
		writer.WriteLine("  ctrl  = the same payoffs in a pile of the format's best cards");
		writer.WriteLine("  LIFT  = depth - ctrl. Near zero means the concept is doing nothing;");
		writer.WriteLine(
			"          the deck is a good-stuff pile that happens to share a keyword."
		);
		writer.WriteLine("  kill  = median goldfish turns (99 = never) — DESCRIPTIVE ONLY");
		writer.WriteLine("  bare  = what the payoff is worth cast into an EMPTY board");
		writer.WriteLine("  supp'd= the same card with its demands answered");
		writer.WriteLine("  ROWS ARE SORTED BLANK-FIRST: lowest bare, then most gained.");
		writer.WriteLine(
			"          bare 0.00 is a card that does nothing until the deck assembles —"
		);
		writer.WriteLine(
			"          the class hill climbing cannot reach. A high bare with high gain"
		);
		writer.WriteLine("          is a tribal lord, which the ordinary search already finds.");
		writer.WriteLine();
		writer.WriteLine(
			$"{"", -4}{"concept", -40}{"supp", 6}{"pay", 5}{"enab", 6}"
				+ $"{"assem", 8}{"depth", 7}{"LIFT", 7}{"cover", 7}{"kill", 6}{"bare", 9}{"supp'd", 9}"
		);

		var rank = 0;
		foreach (var e in report.Engines)
		{
			rank++;
			var mark = rank <= keep ? "*" : " ";
			var concept = e.Concept.Length > 38 ? e.Concept[..38] : e.Concept;
			writer.WriteLine(
				$"{mark, -4}{concept, -40}{e.SuppliersInPool, 6}{e.Payoffs.Count, 5}{e.Enablers.Count, 6}"
					+ $"{e.AssemblyRate, 8:P0}{e.MedianDepth, 7:F1}"
					+ $"{e.Lift, 7:+0.0;-0.0; 0.0}{e.Coverage, 7:P0}{e.MedianSpeed, 6:F1}"
					+ (
						e.LeverageMeasured
							? $"{e.Bare, 9:F2}{e.Supplied, 9:F2}"
							: $"{"—", 9}{"—", 9}"
					)
			);
		}

		writer.WriteLine();
		foreach (var e in report.Engines.Take(keep))
		{
			writer.WriteLine($"--- {e.Concept}   ({e.Origin})");
			writer.WriteLine(
				$"    CORE — what a deck must contain. Everything else is a flex slot."
			);
			foreach (var s in e.Core.Slots)
				writer.WriteLine(
					$"      {s.MinCopies, 3}x  {s.Role}  [{s.Cards.Count}] "
						+ string.Join(", ", s.Cards.Order(StringComparer.Ordinal).Take(10))
				);
			writer.WriteLine(
				$"    sample deck ({e.Deck.Lands} lands, {e.Deck.DistinctSpells} spells): "
					+ string.Join(", ", e.Deck.Spells.Select(kv => $"{kv.Value}x {kv.Key}"))
			);
			if (e.DeadInDeck.Count > 0)
				writer.WriteLine($"    DEAD in sample: {string.Join(", ", e.DeadInDeck)}");
			writer.WriteLine();
		}
	}
}

public static class EngineReportStore
{
	private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

	public static string PathFor(string setCode) =>
		Path.Combine(
			"sim_results",
			$"engines_{setCode.ToLowerInvariant()}_{DateTime.Now:yyyyMMdd_HHmmss}.json"
		);

	public static void Save(EngineReport report, string path)
	{
		var dir = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(dir))
			Directory.CreateDirectory(dir);
		File.WriteAllText(path, JsonSerializer.Serialize(report, Options));
	}

	public static EngineReport? Load(string path) =>
		File.Exists(path)
			? JsonSerializer.Deserialize<EngineReport>(File.ReadAllText(path), Options)
			: null;
}
