using System.Diagnostics;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Evolves a field of constructed decks by playing them against each other.
///
/// Each generation, every deck proposes a few mutants; every candidate plays the FROZEN field
/// on an identical seed schedule; the best mutant that beats its parent and stays distinct
/// from the rest of the field is accepted. Decks that cannot clear the viability floor against
/// the field are culled and re-seeded, which is what keeps the metagame turning over.
///
/// Phases mirror DraftTrainer exactly and for the same reasons: proposals are built
/// sequentially (cheap, must stay deterministic), all games run in ONE parallel batch into a
/// pre-allocated array, and results are folded in sequentially so nothing depends on thread
/// scheduling.
/// </summary>
public sealed class MetagameEvolver
{
	private readonly CardSet _set;
	private readonly IReadOnlyList<Card> _spellPool;
	private readonly IReadOnlyDictionary<string, Card> _poolIndex;
	private readonly int _deckCount;
	private readonly int _generations;
	private readonly int _mutantsPerDeck;
	private readonly int _gamesPerMatchup;
	private readonly int _finalGamesPerMatchup;
	private readonly int _seed;
	private readonly int _aiDepth;
	private readonly double _minDifference;
	private readonly double _viabilityFloor;
	private readonly int _graceGenerations;
	private readonly bool _useDraftPrior;
	private readonly bool _cullEnabled;
	private readonly int _preSimDecks;
	private readonly int _preSimOpponents;
	private readonly int _conceptSlots;
	private readonly int _gauntletGames;
	private readonly IReadOnlyList<string> _gauntlet;
	private readonly string? _enginesPath;
	private readonly HashSet<string> _excludedEngines;

	/// <summary>
	/// Every proposal the search considered. A field rather than a parameter because `CullWorst`
	/// writes to it too and that method already takes eleven arguments.
	/// </summary>
	private readonly MutationLog _mutations = new();

	/// <summary>
	/// How many field slots are seeded from the engine report. The rest become curve-profile decks —
	/// Aggro, Midrange, Control — which are good-stuff piles BY DESIGN and are the control the themed
	/// slots are read against.
	///
	/// **Setting this turns the wildcard off**, because the engine slots ARE the exploration. On a
	/// large pool the wildcard never survived anyway: 13 of 48 culls, final rate 17.1%, and every
	/// other deck's best matchup was "vs Wildcard".
	/// </summary>
	private readonly int _engineSlots;

	private readonly bool _useWildcard;

	/// How many blank-first candidates are eligible per slot to be sampled from. Wide enough that
	/// consecutive seeds build different fields, narrow enough to stay inside the measured tier.
	private const int TierBreadth = 3;

	/// Share of one engine's card pool that may overlap an already-chosen one before it is skipped.
	/// <summary>
	/// Share of one engine's card pool that may overlap an already-chosen one before it is skipped.
	///
	/// **0.5 was too strict and cost half the field.** Asked for 6 engine slots on DES it filled 3,
	/// because cores built on creature demands share most of the creature pool and any two of them
	/// tripped the guard. The job here is only to stop two near-duplicates — two Spirit lords —
	/// taking two slots, so it has to sit above the overlap that unrelated creature archetypes have
	/// with each other by construction.
	/// </summary>
	private const double MaxEngineOverlap = 0.8;

	/// <summary>
	/// How much longer a concept slot is left alone before it can be culled.
	///
	/// **A proxy for "the candidate set is exhausted", which is the honest abandon rule and is
	/// not built.** A human drops an archetype once the pool has no more cards that could fix it,
	/// not because it is losing this week; a synergy deck is a coherent thing that gets better as
	/// the wrong copies are pruned out, and culling it on the viability floor at generation 5
	/// judges it before any of that has happened. The wildcard slot failed exactly this way on
	/// the combined pool — 13 of 48 culls, never once survived, so the exploration it existed to
	/// provide never materialised.
	/// </summary>
	public const int ConceptGraceMultiplier = 3;

	/// <param name="minDifference">
	/// Minimum fraction of spells any two decks must differ by. Enforced at seeding AND at
	/// mutation acceptance — a constraint rather than a fitness penalty, because the
	/// requirement is categorical and a penalty would let a strong deck buy past it.
	/// </param>
	/// <param name="viabilityFloor">
	/// Overall win rate below which a deck is a non-viable list and gets replaced. Note that a
	/// closed round-robin averages exactly 50% by construction, so this is a floor on the
	/// WORST deck, never a target for every deck.
	/// </param>
	/// <param name="excludedEngines">
	/// Archetypes a previous run measured as non-viable, named by concept. An excluded engine is
	/// not seeded and its slot falls back to a curve deck.
	///
	/// **This exists because a dead engine does not merely waste its own slot — it inflates every
	/// other number in the run.** Mere-Storm finished at 8.2% and 0.0% across two runs on CMB, and
	/// every other deck's best matchup was "vs Mere-Storm": the field spread, the viable count and
	/// each deck's overall rate are all measured partly against a punching bag. Leaving the slot as
	/// a curve deck is the honest control; seeding an archetype the pool has already been shown not
	/// to support is not.
	///
	/// Matched on <see cref="EngineCandidate.Concept"/>, case-insensitively, with a leading
	/// "Engine-" tolerated so a deck name copied straight out of a previous report works.
	/// </param>
	/// <param name="graceGenerations">
	/// How long a freshly seeded deck is immune from culling. A new seed starts bad by
	/// definition; without a grace period the field culls its own replacements before they can
	/// climb, and never converges.
	/// </param>
	public MetagameEvolver(
		CardSet set,
		int deckCount = 8,
		int generations = 30,
		int mutantsPerDeck = 3,
		int gamesPerMatchup = 6,
		int finalGamesPerMatchup = 20,
		int seed = 0,
		int aiDepth = 2,
		double minDifference = 0.35,
		double viabilityFloor = 0.40,
		int graceGenerations = 5,
		bool useDraftPrior = true,
		bool cullEnabled = true,
		int preSimDecks = 300,
		int preSimOpponents = 12,
		int conceptSlots = 0,
		int gauntletGames = 0,
		string? enginesPath = null,
		int engineSlots = -1,
		IReadOnlyList<string>? excludedEngines = null
	)
	{
		if (deckCount < 2)
			throw new ArgumentOutOfRangeException(nameof(deckCount), "Need at least two decks.");
		if (generations < 1)
			throw new ArgumentOutOfRangeException(
				nameof(generations),
				"Need at least one generation."
			);

		_set = set;
		_spellPool = set.Cards.Where(c => !c.HasSubtype("Land")).ToList();
		_poolIndex = ConstructedGameSetup.PoolIndex(_spellPool);
		_deckCount = deckCount;
		_generations = generations;
		_mutantsPerDeck = mutantsPerDeck;
		_gamesPerMatchup = gamesPerMatchup;
		_finalGamesPerMatchup = finalGamesPerMatchup;
		_seed = seed;
		_aiDepth = aiDepth;
		_minDifference = minDifference;
		_viabilityFloor = viabilityFloor;
		_graceGenerations = graceGenerations;
		_useDraftPrior = useDraftPrior;
		_cullEnabled = cullEnabled;
		_preSimDecks = preSimDecks;
		_preSimOpponents = preSimOpponents;
		_conceptSlots = Math.Clamp(conceptSlots, 0, Math.Max(0, deckCount - 1));
		_gauntletGames = Math.Max(0, gauntletGames);
		_gauntlet = _gauntletGames > 0 ? Gauntlet.For(set.Code) : [];
		_enginesPath = enginesPath;
		_excludedEngines = (excludedEngines ?? [])
			.Select(NormaliseEngineName)
			.Where(n => n.Length > 0)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		// -1 keeps the old behaviour: fill every slot but the wildcard.
		_engineSlots =
			engineSlots < 0 ? Math.Max(0, deckCount - 1) : Math.Clamp(engineSlots, 0, deckCount);
		_useWildcard = engineSlots < 0;
	}

	/// <summary>
	/// A gauntlet opponent is encoded as a NEGATIVE opponent index: -1 - gauntletIndex. Keeps
	/// <see cref="Seed"/> unchanged (negative values still give distinct seeds) and keeps the
	/// paired-evaluation guarantee intact, which is the property everything else depends on.
	/// </summary>
	/// <summary>
	/// A concept name reduced to what an exclusion list matches on. The "Engine-" prefix is
	/// stripped because that is how the name appears in the report a user reads it out of.
	/// </summary>
	private static string NormaliseEngineName(string name)
	{
		var trimmed = (name ?? string.Empty).Trim();
		return trimmed.StartsWith("Engine-", StringComparison.OrdinalIgnoreCase)
			? trimmed["Engine-".Length..]
			: trimmed;
	}

	private static int GauntletOpponent(int gauntletIndex) => -1 - gauntletIndex;

	private static bool IsGauntlet(int opponentIndex) => opponentIndex < 0;

	private static int GauntletIndexOf(int opponentIndex) => -1 - opponentIndex;

	/// <param name="DeckIndex">Which deck slot this candidate belongs to.</param>
	/// <param name="CandidateIndex">0 is the parent; 1..M are its mutants.</param>
	/// <param name="CandidateOnPlay">
	/// Whether the candidate is Player 1. Alternated so the play/draw advantage does not
	/// systematically favour any deck.
	/// </param>
	private readonly record struct ScheduledGame(
		int DeckIndex,
		int CandidateIndex,
		int OpponentIndex,
		int GameSeed,
		bool CandidateOnPlay
	);

	private sealed record Tally
	{
		public int Wins;
		public int Games;

		public double Rate => Games == 0 ? 0 : (double)Wins / Games;
	}

	/// <summary>
	/// Engine slots from a mode 7 report, paired with the field slots they occupy.
	///
	/// **Taken in REPORT ORDER, which is blank-first.** This used to re-sort by LIFT and thereby
	/// undo the ranking `EngineDiscovery` had already applied — and blank-first is the better one for
	/// picking slots, because it puts payoffs that do NOTHING without their support at the top, which
	/// is the class hill climbing provably cannot reach. LIFT ranks a tribal lord and a combo payoff
	/// together; `bare` separates them.
	///
	/// The remaining slots are curve-profile decks (Aggro / Midrange / Control) — good-stuff piles by
	/// design, and the control the themed slots are read against.
	/// </summary>
	/// <remarks>
	/// `internal` so <c>EngineExclusionTests</c> can assert which archetypes are seeded without
	/// playing a generation of games. Same reasoning as <c>ActionsMatch</c>: the invariant lives
	/// here, and a test that has to run a whole evolution to see it would be too slow to keep.
	/// </remarks>
	internal IEnumerable<(int Slot, EngineCandidate Engine)> LoadEngines()
	{
		if (string.IsNullOrWhiteSpace(_enginesPath))
			yield break;

		var report = EngineReportStore.Load(_enginesPath);
		if (report is null)
		{
			Console.WriteLine($"  WARNING: no engine report at {_enginesPath} — running without.");
			yield break;
		}

		var usable = Math.Clamp(_engineSlots, 0, _deckCount);

		// **Excluded BEFORE the tier is cut, not after.** Filtering the chosen slots instead would
		// let a dead archetype consume one of `usable * TierBreadth` places and shrink the pool the
		// sampling draws from, so an exclusion list would quietly narrow the field it was meant to
		// widen.
		var offered = report.Engines.ToList();
		if (_excludedEngines.Count > 0)
		{
			var kept = offered
				.Where(e => !_excludedEngines.Contains(NormaliseEngineName(e.Concept)))
				.ToList();

			// Named, not counted. A silent filter is how a run comes back clean for the wrong
			// reason, and a misspelt exclusion is indistinguishable from an effective one.
			var dropped = offered.Select(e => NormaliseEngineName(e.Concept)).ToHashSet();
			var unmatched = _excludedEngines.Where(n => !dropped.Contains(n)).ToList();
			Console.WriteLine(
				$"  Excluded {offered.Count - kept.Count} of {offered.Count} engines: "
					+ string.Join(", ", _excludedEngines.Order(StringComparer.OrdinalIgnoreCase))
			);
			if (unmatched.Count > 0)
				Console.WriteLine(
					"    WARNING: no engine in this report is named "
						+ $"{string.Join(", ", unmatched)} — check the spelling."
				);

			offered = kept;
		}

		// **Sampled from the BLANK TIER, not uniformly over the whole report.**
		//
		// Taking the top N always rebuilds the same field and never tests the report past the cut
		// line, so sampling is right — but sampling UNIFORMLY throws away the measurement. Measured
		// on DES: it drew Captain of the Watch (bare 39.20, a fine card on its own and definitionally
		// not a combo payoff) plus two Spirit lords and two Parish cards, so six slots held about
		// three archetypes, while every bare-0.00 payoff went unpicked — Zombie Apocalypse,
		// Necromantic Summons, Illusory Angel, Echo of the Drowned, Ashen Rite.
		//
		// `bare` is a MEASUREMENT, not a hypothesis: a card worth nothing until its demands are met
		// is the class hill climbing cannot reach, and that is the whole reason these slots exist.
		// Variety comes from sampling WITHIN that tier rather than from ignoring it.
		var eligible = offered
			.OrderBy(e => e.BlankFirstKey)
			.Take(Math.Max(usable * TierBreadth, usable))
			.ToList();

		// Deterministic shuffle over the eligible tier — one RNG, not one per comparison, or the
		// ordering depends on evaluation order rather than on the seed.
		var pick = new Random(_seed + 909_091);
		eligible = [.. eligible.OrderBy(_ => pick.Next())];

		// **A distinctness guard, because two Spirit lords are one archetype in two slots.** Cores
		// are already deduped when IDENTICAL; near-duplicates survive that and still waste a slot.
		var take = new List<EngineCandidate>();
		var claimed = new List<HashSet<string>>();
		foreach (var candidate in eligible)
		{
			if (take.Count >= usable)
				break;

			var pool = candidate
				.Core.Slots.SelectMany(sl => sl.Cards)
				.ToHashSet(StringComparer.Ordinal);

			if (
				claimed.Any(other =>
					pool.Count > 0
					&& other.Count > 0
					&& (double)pool.Intersect(other).Count() / Math.Min(pool.Count, other.Count)
						> MaxEngineOverlap
				)
			)
				continue;

			take.Add(candidate);
			claimed.Add(pool);
		}

		if (take.Count < usable)
			Console.WriteLine(
				$"  WARNING: asked for {usable} engine slots and exclusion plus the distinctness "
					+ $"guard left {take.Count}. The rest of the field falls back to curve profiles."
			);

		Console.WriteLine(
			$"  Engines: {take.Count} of {report.Engines.Count} from {_enginesPath} "
				+ $"(sampled from the top {Math.Max(usable * TierBreadth, usable)} blank-first, "
				+ "distinct archetypes only)"
		);
		for (var i = 0; i < take.Count; i++)
		{
			Console.WriteLine(
				$"    slot {i}: {take[i].Concept} (lift {take[i].Lift:+0.0;-0.0}, "
					+ $"{take[i].Payoffs.Count + take[i].Enablers.Count} cards in pool)"
			);
			yield return (i, take[i]);
		}
	}

	public MetagameResult Run()
	{
		var values = ConstructedValuesStore.Load(_set.Code, _useDraftPrior);
		var rng = new Random(_seed);

		PrintHeader(values);

		// Measure the pool with random decks before any selection pressure exists, so a card's
		// starting value does not depend on whether it happened to be picked up early.
		var presim = DraftTrainingData.Empty;
		if (_preSimDecks > 0)
		{
			presim = PreSimulation.Run(
				_spellPool,
				_preSimDecks,
				_preSimOpponents,
				_seed + 3_000_000,
				_aiDepth
			);
			values = new ConstructedValues(
				DraftTrainingData.Merge(values.Data, presim),
				ConstructedValuesStore.LoadDraftPrior(_set.Code, _useDraftPrior)
			);

			// Persisted so the next run inherits it. Presim accumulates safely — random decks
			// carry no selection pressure, so this is only ever more evidence about the same
			// quantity. Evolved counts must never land here; see ConstructedValuesStore.
			ConstructedValuesStore.SavePresim(presim, _set.Code);
			Console.WriteLine();
		}

		// Structural features are pool-derived and deterministic, so this is built once.
		//
		// **Gated on concept slots ALONE this was null for every engine run, and the consequence was
		// silent.** `SupportScore` returns 0 without features, so `DeadCardPenalty` never fires and
		// nothing penalises a card whose demands the deck answers with nothing — a real 8-deck run
		// finished holding a lone Cranial Plating in a list with two artifacts. Engines need the same
		// scoring the concept slots do; ~20s once against a 26-minute run.
		PoolFeatures? features = null;
		if (_conceptSlots > 0 || !string.IsNullOrWhiteSpace(_enginesPath))
		{
			var featureTimer = Stopwatch.StartNew();
			features = PoolFeatures.Build(_spellPool);
			featureTimer.Stop();
			Console.WriteLine(
				$"  Features: {features.Demands.Count} demands over {_spellPool.Count} spells "
					+ $"({featureTimer.ElapsedMilliseconds} ms), {_conceptSlots} concept slots"
			);
			foreach (var f in features.Failures)
				Console.WriteLine($"    WARNING: {f}");
			Console.WriteLine();
		}

		var field = DeckBuilder
			.SeedField(
				_deckCount,
				_spellPool,
				values,
				rng,
				_minDifference,
				includeWildcard: _useWildcard,
				features: features,
				conceptSlots: _conceptSlots
			)
			.ToList();

		// SeedDistinct relaxes rather than hanging when a pool cannot supply N genuinely
		// distinct decks, so a threshold the pool cannot meet would otherwise show up only as
		// a final diversity number below the one that was asked for — which reads as the
		// constraint having failed rather than as the pool being too small.
		var seededDiversity = Enumerable
			.Range(0, _deckCount)
			.Select(i =>
				DeckBuilder.MinDifference(field[i], field.Where((_, j) => j != i).ToList())
			)
			.Min();
		if (seededDiversity < _minDifference)
			Console.WriteLine(
				$"  WARNING: seeded at {seededDiversity:P0} diversity against a {_minDifference:P0} "
					+ $"target — {_spellPool.Count} spells cannot supply {_deckCount} decks that "
					+ "distinct. Lower the target, use fewer decks, or use a bigger pool."
			);

		// --- Engine slots: a discovered archetype, seeded from mode 7 and held to its card pool ---
		//
		// **The POOL is locked, not the decklist.** Freezing specific cards reproduces the
		// max-density failure (jammed goblins 24.4% against the AI's half-built one at 55.0%);
		// freezing nothing lets the deck dissolve into the midrange pile every unconstrained run
		// converges on. A quota over the archetype's card pool keeps the identity while leaving
		// ~40% of the spells free for removal and metagame answers.
		var identities = new DeckCore?[_deckCount];
		foreach (var (slot, engine) in LoadEngines())
		{
			var starting = engine.Deck with { Name = $"Engine-{engine.Concept}" };
			var invalid = starting.Validate();
			if (invalid is not null)
			{
				// Almost always the land floor: mode 7 is run at MTG_MIN_LANDS=12 because Storm
				// and Affinity are illegal above it, and an evolution run at the 20 default cannot
				// hold the deck that produced the engine.
				Console.WriteLine($"  WARNING: engine '{engine.Concept}' unusable — {invalid}");
				continue;
			}

			field[slot] = starting;
			// **The core comes from the report, it is no longer rebuilt here.** This used to invent
			// two slots — "Payoff" and "Enabler" — with minimums set to a third of whatever the
			// sample deck happened to play, which made the constraint a function of one seeding
			// draw. `DeckCore.For` derives it from the payoff card's own demands instead, so a
			// Dragonstorm slot carries a Dragon floor and a storm floor as separate facts rather
			// than one merged "Enabler" bucket that either could satisfy alone.
			var identity = engine.Core;
			identities[slot] = identity;

			// **Reported, because the constraint failing is otherwise invisible.** The first
			// version of this allowed 40% drift and the storm slot spent all of it on Steppe Lynx
			// and Gravecrawler while reporting nothing. `SeedConcept` tops up from the whole pool
			// when a concept cannot fill 36 slots, so a starting deck below 100% is expected and
			// self-heals — but it must never FALL, and a number here is what makes that checkable.
			var missing = identity.Missing(starting);
			if (missing.Count > 0)
				Console.WriteLine(
					$"    slot {slot} starts BELOW its core: {string.Join(", ", missing)}"
				);
		}

		// --- Context value: what each core card is worth IN ITS OWN DECK ---
		//
		// **`CardDelta` measures a card in a RANDOM deck and that is the wrong question for a core
		// slot.** Measured on DES: Wirewood Conduit reads 51.6% in random decks and is never once
		// proposed in twelve generations, while the hand-built elf list holding it beats the
		// builder's own elf deck 65-35 from the same 24-card pool at the same land count. A synergy
		// card is weak alone by definition, so more isolation data measures the wrong quantity more
		// precisely.
		//
		// Computed ONCE per engine slot, before any generation: it depends on the core's pool and
		// the shell, neither of which moves during a run.
		var contextValues = new Dictionary<string, double>?[_deckCount];
		for (var i = 0; i < _deckCount; i++)
		{
			if (identities[i] is not { } core)
				continue;
			try
			{
				contextValues[i] = ContextValuesFor(core, field[i]);
			}
			catch (Exception ex)
			{
				// A slot that cannot be measured falls back to isolation value rather than taking
				// the run down — and says so, because a silently absent term is indistinguishable
				// from one that is present and doing nothing.
				Console.WriteLine(
					$"  WARNING: context values for {field[i].Name} threw {ex.GetType().Name} — "
						+ "that slot falls back to isolation value."
				);
			}
		}

		// What each slot recently accepted, so the climb cannot spend its budget undoing itself.
		var recentAccepts = Enumerable
			.Range(0, _deckCount)
			.Select(_ => new List<(int Gen, HashSet<string> Added, HashSet<string> Removed)>())
			.ToArray();

		var ages = new int[_deckCount];
		var isWildcard = Enumerable
			.Range(0, _deckCount)
			.Select(i => _useWildcard && i == _deckCount - 1)
			.ToArray();
		var isConcept = Enumerable
			.Range(0, _deckCount)
			.Select(i => features is not null && i < _conceptSlots && !isWildcard[i])
			.ToArray();

		// **The band a slot must stay inside, kept for the whole run rather than used once at
		// seeding.** Engine slots take `Any` because their core IS the constraint, and the wildcard
		// takes `Any` because unconstrained drift is its entire job — stated explicitly rather than
		// happening to every slot by omission.
		var profiles = Enumerable
			.Range(0, _deckCount)
			.Select(i =>
				identities[i] is not null
					? DeckBuilder.DeckProfile.Any
					: DeckBuilder.ProfileForSlot(i, _conceptSlots, isWildcard[i])
			)
			.ToArray();

		var accumulator = new CardStatAccumulator();

		// Per-slot history, which is what makes cutting synergy-aware. Reset when a slot is
		// re-seeded: the new deck shares almost nothing with the old one, so carrying its pair
		// record over would judge new cards on a shell they were never in.
		var slotHistory = Enumerable
			.Range(0, _deckCount)
			.Select(_ => new CardStatAccumulator())
			.ToArray();

		// Previous generation's win rate per slot, which drives the adaptive mutation budget.
		// Starts at 0 so generation 1 mutates fully for everyone.
		var lastRate = new double[_deckCount];

		var totalGames = 0;
		var totalExcluded = 0;
		var timer = Stopwatch.StartNew();

		for (var gen = 1; gen <= _generations; gen++)
		{
			// --- Phase 1: proposals (sequential, deterministic) ---
			var candidates = new List<Decklist?>[_deckCount];
			for (var i = 0; i < _deckCount; i++)
			{
				// Own RNG stream per deck per generation, so the number of proposals one deck
				// happens to reject cannot shift what another deck is offered.
				var mutRng = new Random(_seed + gen * 100_003 + i * 1_009);
				var history = new DeckHistory(slotHistory[i].ToData());
				var list = new List<Decklist?> { field[i] };
				for (var m = 0; m < MutantsFor(lastRate[i]); m++)
					list.Add(
						TryMutate(
							field[i],
							values,
							mutRng,
							history,
							features,
							identities[i],
							profiles[i],
							contextValues[i]
						)
					);
				candidates[i] = list;
			}

			var schedule = BuildSchedule(candidates, gen);

			// --- Phase 2: games (parallel into a pre-allocated array) ---
			var results = PlayBatch(schedule, candidates, field, gen);

			// --- Phase 3: fold in (sequential — independent of thread order) ---
			//
			// **Two tallies, and the split is deliberate.** `tallies` is field + gauntlet and
			// drives ACCEPTANCE: that is the whole point, since a mutant that beats the field but
			// loses to the gauntlet is not an improvement. `fieldTallies` is field-only and drives
			// CULLING and the viability floor, because the floor means "competitive in this field".
			// Judging viability on the combined rate would put every deck under 40% on day one
			// against a gauntlet that beats the field 66-34, and `CullWorst` would replace a deck
			// every generation forever — resetting its DeckHistory each time, which this document
			// records as the thing culled slots never recover from.
			var tallies = new Tally[_deckCount][];
			var fieldTallies = new Tally[_deckCount][];
			for (var i = 0; i < _deckCount; i++)
			{
				tallies[i] = Enumerable
					.Range(0, candidates[i].Count)
					.Select(_ => new Tally())
					.ToArray();
				fieldTallies[i] = Enumerable
					.Range(0, candidates[i].Count)
					.Select(_ => new Tally())
					.ToArray();
			}

			var excluded = 0;
			for (var s = 0; s < schedule.Count; s++)
			{
				var g = schedule[s];
				var result = results[s];

				// Keep what the rules decided; drop what the machine decided. Same rule as
				// DraftTrainer: a turn- or action-limit draw is real evidence, a wall-clock
				// timeout or a crash says nothing about the decks.
				if (
					result.EndReason
					is GameEndReason.TimeLimitReached
						or GameEndReason.UnhandledException
				)
				{
					excluded++;
					continue;
				}

				var candidateWon = g.CandidateOnPlay ? result.IsPlayer1Win : result.IsPlayer2Win;
				var tally = tallies[g.DeckIndex][g.CandidateIndex];
				tally.Games++;
				if (candidateWon)
					tally.Wins++;

				if (!IsGauntlet(g.OpponentIndex))
				{
					var fieldTally = fieldTallies[g.DeckIndex][g.CandidateIndex];
					fieldTally.Games++;
					if (candidateWon)
						fieldTally.Wins++;
				}

				FoldIntoValues(accumulator, g, result, candidates, field);

				// The candidate's own side into its slot's history. Only the candidate's side:
				// the opponent's cards belong to the OPPONENT's shell, and crediting them here
				// would mix two decks' pair records together.
				var candidateDeck = candidates[g.DeckIndex][g.CandidateIndex]!;
				slotHistory[g.DeckIndex]
					.Add(
						g.CandidateOnPlay ? result.Player1DrawnCards : result.Player2DrawnCards,
						candidateDeck.Spells.Keys.ToList(),
						candidateWon
					);
			}

			totalGames += schedule.Count;
			totalExcluded += excluded;

			// --- Accept the best improving mutant per deck ---
			var accepted = 0;
			for (var i = 0; i < _deckCount; i++)
			{
				var parent = tallies[i][0];
				var bestIndex = -1;
				var bestRate = parent.Rate;

				// Why each proposal ended where it did, decided in the same pass that decides it.
				// Recorded here rather than reconstructed afterwards: the loop overwrites `bestRate`
				// as it goes, so "was this rejected on fitness or on diversity" is knowable at this
				// point and nowhere else.
				var outcomes = new string[candidates[i].Count];

				// Computed here rather than in the logging pass below, because the reversal guard
				// needs the diff to make its DECISION and `field[i]` is replaced afterwards.
				var diffs = new (string Added, string Removed)?[candidates[i].Count];
				for (var c = 1; c < candidates[i].Count; c++)
					if (candidates[i][c] is { } proposal)
						diffs[c] = MutationLog.Diff(field[i], proposal);

				for (var c = 1; c < candidates[i].Count; c++)
				{
					var mutant = candidates[i][c];
					if (mutant is null || tallies[i][c].Games == 0)
						continue;
					if (tallies[i][c].Rate <= bestRate)
					{
						outcomes[c] = MutationLog.Rejected;
						continue;
					}

					// **Refuse to undo what this slot accepted a few generations ago.** Measured on
					// one elf slot: four of its accepted mutations were the same pair swapped back
					// and forth, each direction scoring +3 to +6pp, which is inside the noise of a
					// paired 6-game evaluation. The climb read two coin flips as two gains and ended
					// where it started, burning ~15% of the run's accepted mutations.
					if (Reverses(recentAccepts[i], diffs[c], gen))
					{
						outcomes[c] = MutationLog.Reversal;
						continue;
					}

					// The diversity constraint applies to the field as it will be, so it is
					// checked against every OTHER deck's current list.
					var others = field.Where((_, j) => j != i).ToList();
					if (DeckBuilder.MinDifference(mutant, others) < _minDifference)
					{
						outcomes[c] = MutationLog.TooSimilar;
						continue;
					}

					// A proposal that leads now can still be beaten by a later one in this same
					// generation, so the winner is stamped after the loop and everything else here
					// is provisionally a reject.
					outcomes[c] = MutationLog.Rejected;
					bestIndex = c;
					bestRate = tallies[i][c].Rate;
				}

				if (bestIndex >= 0)
				{
					outcomes[bestIndex] = MutationLog.Accepted;
					recentAccepts[i]
						.Add(
							(
								gen,
								[.. MutationLog.NamesIn(diffs[bestIndex]!.Value.Added)],
								[.. MutationLog.NamesIn(diffs[bestIndex]!.Value.Removed)]
							)
						);
				}

				for (var c = 1; c < candidates[i].Count; c++)
				{
					// **A budget spent producing nothing is the single most diagnostic row here.**
					// `Mutate` draws only from a pool-locked slot's core cards, so a core with a
					// handful of cards in pool has almost no legal distinct edit and the slot sits
					// frozen at its seed — while the matrix shows it losing and the report calls it
					// non-viable. Left unlogged, that is indistinguishable from a deck that was
					// offered improvements and refused them.
					if (candidates[i][c] is not { } proposal)
					{
						_mutations.Add(
							new MutationRow(
								gen,
								i,
								field[i].Name,
								"",
								"",
								parent.Rate,
								parent.Rate,
								0,
								MutationLog.NoProposal
							)
						);
						continue;
					}

					if (outcomes[c] is null)
						continue;

					var (added, removed) = diffs[c]!.Value;
					_mutations.Add(
						new MutationRow(
							gen,
							i,
							field[i].Name,
							added,
							removed,
							parent.Rate,
							tallies[i][c].Rate,
							tallies[i][c].Games,
							outcomes[c]
						)
					);
				}

				if (bestIndex >= 0)
				{
					field[i] = candidates[i][bestIndex]! with { Name = field[i].Name };
					accepted++;
				}
				ages[i]++;
			}

			for (var i = 0; i < _deckCount; i++)
				lastRate[i] = tallies[i][0].Games > 0 ? tallies[i][0].Rate : lastRate[i];

			// --- Cull: at most one per generation, past its grace period ---
			var culled = CullWorst(
				field,
				fieldTallies,
				ages,
				isWildcard,
				isConcept,
				identities,
				values,
				features,
				rng,
				gen,
				slotHistory
			);

			PrintGeneration(gen, field, tallies, accepted, culled, excluded, timer);
		}

		timer.Stop();

		// The reported matrix comes from a dedicated high-N round-robin rather than from the
		// last generation's mutation tests: those measured each deck against the field as it
		// stood BEFORE that generation's accepted mutations, which is not the field being
		// reported.
		Console.WriteLine();
		Console.WriteLine($"  Final round-robin: {_finalGamesPerMatchup} games per matchup...");
		var (matrix, overall) = FinalRoundRobin(field, accumulator);

		var result2 = new MetagameResult(
			_set.Code,
			_generations,
			_gamesPerMatchup,
			field,
			matrix,
			overall
		);

		PrintReport(result2, values, accumulator, totalGames, totalExcluded, timer.Elapsed, ages);
		Save(result2, accumulator, presim);

		return result2;
	}

	/// <summary>
	/// **Common random numbers — the single most important detail in this class.**
	///
	/// The game seed is derived from (generation, deck, opponent, repeat) and deliberately NOT
	/// from the candidate index, so a parent and all of its mutants play the identical
	/// opponents on the identical shuffles with the identical AI RNG. Shuffle and search
	/// variance is then shared between the arms and cancels in the comparison.
	///
	/// Without this the accept/reject decision is a coin flip: a mutation is worth ~1-3pp and
	/// an unpaired 42-game sample has a standard error near 7pp, so a whole run would be a
	/// random walk that looks like evolution.
	/// </summary>
	private List<ScheduledGame> BuildSchedule(List<Decklist?>[] candidates, int gen)
	{
		var schedule = new List<ScheduledGame>();
		for (var i = 0; i < _deckCount; i++)
		{
			for (var c = 0; c < candidates[i].Count; c++)
			{
				if (candidates[i][c] is null)
					continue;
				for (var j = 0; j < _deckCount; j++)
				{
					if (j == i)
						continue;
					for (var k = 0; k < _gamesPerMatchup; k++)
					{
						var gameSeed = Seed(gen, i, j, k);
						schedule.Add(new ScheduledGame(i, c, j, gameSeed, k % 2 == 0));
					}
				}

				// Gauntlet games use the SAME seeding rule — independent of candidate index — so a
				// parent and its mutants face identical reference games on identical shuffles.
				// Without that the extra games are unpaired noise (~7pp) swamping the 1-3pp effect
				// the comparison exists to detect.
				for (var q = 0; q < _gauntlet.Count; q++)
				{
					var opponent = GauntletOpponent(q);
					for (var k = 0; k < _gauntletGames; k++)
					{
						var gameSeed = Seed(gen, i, opponent, k);
						schedule.Add(new ScheduledGame(i, c, opponent, gameSeed, k % 2 == 0));
					}
				}
			}
		}
		return schedule;
	}

	/// <summary>
	/// How many mutants a deck proposes, as a function of how it is doing. A deck at or above
	/// <see cref="StableRate"/> proposes NONE and is carried unchanged; a deck at or below
	/// <see cref="StrugglingRate"/> proposes the full budget.
	///
	/// **Constant mutation is a random walk dressed as evolution.** Acceptance is "any mutant
	/// that beats the parent", and at ~42 paired games a genuinely neutral change is close to a
	/// coin flip to look better — so a deck that has already found something good keeps being
	/// asked to change it, and keeps accepting noise. Measured across four runs, field spread
	/// never converged (39-46pp at generation 100, same as generation 1) while acceptance stayed
	/// at 4-6 of 8 every generation to the end.
	///
	/// Rate is the deck's own win rate from the PREVIOUS generation, so generation 1 (no data
	/// yet) proposes the full budget for everyone.
	/// </summary>
	/// <summary>
	/// How many times a single mutation slot may re-roll before it is given up as dry.
	///
	/// **The budget means "mutants to EVALUATE", and it was implemented as "times to call a function
	/// that often fails".** Those are different quantities and the gap is invisible without the
	/// mutation log. `Mutate` rolls ONE operator and returns null whenever that operator cannot
	/// produce a legal, distinct, core-holding, profile-respecting list — so a slot whose null rate
	/// is high spent its whole budget on nothing and was then reported as a deck that refused its
	/// improvements.
	///
	/// **Measured null rates per single call** (DES, `MTG_MIN_LANDS=12`, 600 mutations of each of a
	/// real run's decks):
	///
	/// | deck | null rate |
	/// |---|---|
	/// | no core, profile `Any` | 3–9% |
	/// | `Control` profile (deck below its band) | **34%** |
	/// | engine core, 508 cards in pool | 18% |
	/// | engine core, **5** cards in pool | **95%** |
	///
	/// At 95% a budget of 3 yields 0.15 proposals per generation — which is what put two engine
	/// slots at 2 and 3 real proposals across twelve generations and then reported both NON-VIABLE.
	/// With 20 re-rolls the same slot yields ~1.9.
	///
	/// **This does NOT make a narrow core fully searchable and must not be read as a fix for that.**
	/// A five-card pool genuinely has few distinct legal lists; re-rolling stops the budget being
	/// wasted, it does not create options that do not exist. The `dry` column still reports what is
	/// left.
	///
	/// 20 is chosen to make the common cases certain rather than tuned: at a 34% null rate the odds
	/// of 20 consecutive failures are ~1 in 40 000. Mutation is pure CPU with no games attached, so
	/// the cost is noise against a generation's game batch.
	/// </summary>
	private const int MutationRetries = 20;

	/// <summary>
	/// Re-rolls <see cref="DeckBuilder.Mutate"/> until it yields a proposal or the retries run out.
	///
	/// Consumes <paramref name="rng"/> in order, so the sequence stays deterministic — a re-roll is
	/// simply more draws from the same stream, exactly as an extra mutant would have been.
	/// </summary>
	private Decklist? TryMutate(
		Decklist deck,
		ConstructedValues values,
		Random rng,
		DeckHistory history,
		PoolFeatures? features,
		DeckCore? core,
		DeckBuilder.DeckProfile profile,
		IReadOnlyDictionary<string, double>? contextValue
	)
	{
		for (var attempt = 0; attempt < MutationRetries; attempt++)
		{
			var mutant = DeckBuilder.Mutate(
				deck,
				_spellPool,
				values,
				rng,
				history,
				features,
				core,
				profile,
				contextValue
			);
			if (mutant is not null)
				return mutant;
		}
		return null;
	}

	/// <summary>
	/// Points of `CardDelta`-equivalent that one standard deviation of measured output is worth.
	///
	/// **A scale choice, not a tuned value, and it must stay small.** `OutputProbe` is a PROPOSAL
	/// generator — it says which cards are worth trying in a slot, and the win rate still judges
	/// whether the resulting deck is good. Sized against `SupportBonus` (4.0) and `DeadCardPenalty`
	/// (5.0) so it can lift a card the isolation table rates as merely average, and cannot rescue
	/// one it rates as genuinely bad. Raising it until the metric decides the deck is how a proxy
	/// becomes a fitness function, which this file records as the failure that produced a
	/// maximum-density goblin deck at 24.4%.
	/// </summary>
	private const double ContextValueWeight = 6.0;

	/// <summary>
	/// Output value for every card in a core's pool, normalised to `CardDelta` units.
	///
	/// Standardised by the population's own spread rather than by a fixed divisor, so the term keeps
	/// the same influence whether a slot's candidates differ by ten output points or a hundred —
	/// the same reason the metric reports against a median rather than an absolute.
	/// </summary>
	private Dictionary<string, double> ContextValuesFor(DeckCore core, Decklist shell)
	{
		var candidates = core
			.Slots.SelectMany(s => s.Cards)
			.Distinct(StringComparer.Ordinal)
			.Where(_poolIndex.ContainsKey)
			.Order(StringComparer.Ordinal)
			.ToList();

		// `Compare` sizes the shell itself — it removes the candidate and trims the rest to leave
		// exactly room for it, so every candidate is measured in a deck of the same size. Doing that
		// here as well is how the counts got out of step in the first place.
		var measured = OutputProbe.Compare(
			shell.Spells,
			shell.Lands,
			candidates,
			_poolIndex,
			copies: 4,
			seeds: ContextValueSeeds,
			aiDepth: _aiDepth
		);

		var values = measured.Select(m => m.OverAverage).ToList();
		var sd = Math.Sqrt(values.Sum(v => v * v) / Math.Max(1, values.Count));
		if (sd <= double.Epsilon)
			return [];

		return measured.ToDictionary(
			m => m.Name,
			m => ContextValueWeight * m.OverAverage / sd,
			StringComparer.Ordinal
		);
	}

	/// Shuffles per candidate. Three is enough to rank; it is not enough to quote a number from.
	private const int ContextValueSeeds = 3;

	/// <summary>
	/// Generations an accepted mutation is protected from being undone.
	///
	/// **A window, never a permanent ban.** A swap that was wrong at generation 2 can be right at
	/// generation 8 once the shell around it has changed, so this forbids the immediate flip-flop and
	/// nothing more. Four is long enough to cover the measured oscillation (gen 5→6 and gen 11→12)
	/// and short enough that the search can still change its mind about a card as the deck evolves.
	/// </summary>
	private const int ReversalMemory = 4;

	/// <summary>
	/// Whether a proposal undoes something this slot accepted inside <see cref="ReversalMemory"/>.
	///
	/// **Matched on NAMES, not counts.** The measured oscillation left at three copies and came back
	/// at one; a count-sensitive comparison would have called that a different mutation and let it
	/// through. Overlap rather than set equality for the same reason — a reversal bundled with an
	/// unrelated edit is still a reversal.
	/// </summary>
	internal static bool Reverses(
		List<(int Gen, HashSet<string> Added, HashSet<string> Removed)> history,
		(string Added, string Removed)? diff,
		int gen
	)
	{
		if (diff is not { } d)
			return false;

		var added = MutationLog.NamesIn(d.Added).ToHashSet(StringComparer.Ordinal);
		var removed = MutationLog.NamesIn(d.Removed).ToHashSet(StringComparer.Ordinal);
		if (added.Count == 0 && removed.Count == 0)
			return false;

		return history.Any(h =>
			gen - h.Gen <= ReversalMemory && added.Overlaps(h.Removed) && removed.Overlaps(h.Added)
		);
	}

	private int MutantsFor(double rate)
	{
		if (rate >= StableRate)
			return 0;
		if (rate <= StrugglingRate)
			return _mutantsPerDeck;

		var t = (StableRate - rate) / (StableRate - StrugglingRate);
		return Math.Max(1, (int)Math.Round(_mutantsPerDeck * t));
	}

	/// At or above this a deck is left alone entirely.
	private const double StableRate = 0.60;

	/// At or below this a deck gets the full mutation budget.
	private const double StrugglingRate = 0.45;

	/// Independent of the candidate index — see BuildSchedule.
	private int Seed(int gen, int deck, int opponent, int repeat) =>
		_seed + 1_000_000 + gen * 1_000_003 + deck * 10_007 + opponent * 101 + repeat * 5;

	private GameResult[] PlayBatch(
		IReadOnlyList<ScheduledGame> schedule,
		List<Decklist?>[] candidates,
		IReadOnlyList<Decklist> field,
		int gen
	)
	{
		var results = new GameResult[schedule.Count];
		var completed = 0;

		Parallel.For(
			0,
			schedule.Count,
			s =>
			{
				var g = schedule[s];
				var candidate = candidates[g.DeckIndex][g.CandidateIndex]!;

				// A gauntlet deck is a card LIST, a field deck is a Decklist — FromDecks takes
				// builders precisely so either can be supplied per game with the right owner id.
				var gauntletName = IsGauntlet(g.OpponentIndex)
					? _gauntlet[GauntletIndexOf(g.OpponentIndex)]
					: null;

				Func<int, IReadOnlyList<Card>> BuildOpponent() =>
					gauntletName is not null
						? owner => DeckRegistry.Build(gauntletName, owner)
						: owner => field[g.OpponentIndex].Materialize(owner, _poolIndex);

				var buildCandidate = (int owner) => candidate.Materialize(owner, _poolIndex);
				var buildOpponent = BuildOpponent();

				var (state, ids, cardNames) = GameSetup.FromDecks(
					g.CandidateOnPlay ? buildCandidate : buildOpponent,
					g.CandidateOnPlay ? buildOpponent : buildCandidate
				);
				var aiRng = new Random(g.GameSeed + 4);
				var runner = new GameRunner(
					new MultiTurnBeamSearchAiStrategy(
						ids,
						_aiDepth,
						rng: aiRng,
						cardValues: AiCardValues.Current
					),
					new MultiTurnBeamSearchAiStrategy(
						ids,
						_aiDepth,
						rng: aiRng,
						cardValues: AiCardValues.Current
					)
				);

				var (result, _) = runner.Run(
					state,
					ids,
					cardNames,
					shuffleSeed: g.GameSeed + 2,
					gameRngSeed: g.GameSeed + 3
				);

				// Drop the event log. Nothing here reads it, and holding one per game for the
				// whole batch is what once made a 28 000-game run's median game take 7.5s
				// against 2.8s — the same games, just slower.
				results[s] = result with
				{
					AllEvents = [],
				};

				var done = Interlocked.Increment(ref completed);
				if (done % 500 == 0)
					Console.WriteLine($"    gen {gen}: {done} / {schedule.Count} games...");
			}
		);

		return results;
	}

	/// <summary>
	/// Records BOTH sides of a game into the constructed values table. Every game is evidence
	/// about both decks in it, and the opponent's side is free — it is already played.
	/// </summary>
	private void FoldIntoValues(
		CardStatAccumulator accumulator,
		ScheduledGame g,
		GameResult result,
		List<Decklist?>[] candidates,
		IReadOnlyList<Decklist> field
	)
	{
		var candidate = candidates[g.DeckIndex][g.CandidateIndex]!;

		// Gauntlet cards ARE credited, deliberately. Their values rise, which makes challengers
		// likelier to try them — and challengers converging onto a gauntlet deck is the desired
		// outcome here, not a failure. Read constructed_values_*.json accordingly.
		var opponentSpells = IsGauntlet(g.OpponentIndex)
			? Gauntlet.SpellNames(_gauntlet[GauntletIndexOf(g.OpponentIndex)])
			: field[g.OpponentIndex].Spells.Keys.ToList();

		var candidateWon = g.CandidateOnPlay ? result.IsPlayer1Win : result.IsPlayer2Win;
		var opponentWon = g.CandidateOnPlay ? result.IsPlayer2Win : result.IsPlayer1Win;

		var candidateDrawn = g.CandidateOnPlay
			? result.Player1DrawnCards
			: result.Player2DrawnCards;
		var opponentDrawn = g.CandidateOnPlay ? result.Player2DrawnCards : result.Player1DrawnCards;

		accumulator.Add(candidateDrawn, candidate.Spells.Keys.ToList(), candidateWon);
		accumulator.Add(opponentDrawn, opponentSpells, opponentWon);
	}

	/// <summary>
	/// Replaces the worst deck if it is below the viability floor and out of its grace period.
	///
	/// At most one per generation: the field has to be re-measured after any replacement, and
	/// culling several at once churns faster than the measurement can follow.
	/// </summary>
	private int CullWorst(
		List<Decklist> field,
		Tally[][] tallies,
		int[] ages,
		bool[] isWildcard,
		bool[] isConcept,
		DeckCore?[] identities,
		ConstructedValues values,
		PoolFeatures? features,
		Random rng,
		int gen,
		CardStatAccumulator[] slotHistory
	)
	{
		// **Settling period.** Culling stops for the last graceGenerations, so every deck in
		// the reported field has had at least its full grace period to climb.
		//
		// Without this the run reports decks mid-climb and calls them non-viable: the first
		// real run culled at generation 28 of 30, and that deck was then reported NON-VIABLE at
		// 35.7% having had two generations to improve — a verdict on the cull, not on the deck.
		// A replacement the report cannot evaluate is worse than no replacement.
		if (!_cullEnabled || gen > _generations - _graceGenerations)
			return 0;

		var worst = -1;
		var worstRate = _viabilityFloor;

		for (var i = 0; i < _deckCount; i++)
		{
			// **An engine slot is never culled, and that is the whole premise of the two-phase
			// split.** Its archetype was already judged in mode 7 by whether it ASSEMBLES; the
			// win rate is here to tune it against the field, not to decide whether it deserves
			// to exist. A half-built combo deck loses every game, so a viability floor would
			// delete exactly the decks this feature was built to keep — which is what every
			// unconstrained run has done. Its rate is still reported.
			if (identities[i] is not null)
				continue;

			var grace = isConcept[i]
				? _graceGenerations * ConceptGraceMultiplier
				: _graceGenerations;
			if (ages[i] < grace || tallies[i][0].Games == 0)
				continue;
			if (tallies[i][0].Rate < worstRate)
				(worst, worstRate) = (i, tallies[i][0].Rate);
		}

		if (worst < 0)
			return 0;

		var others = field.Where((_, j) => j != worst).ToList();
		var name = field[worst].Name;
		var before = field[worst];

		// A culled concept slot re-seeds on a CONCEPT, not on an anchor-and-kernel pile. The slot
		// exists to explore archetypes; replacing it with a midrange deck silently retires the
		// exploration arm, which is how the wildcard slot stopped contributing anything.
		field[worst] =
			(
				isConcept[worst] && features is not null
					? DeckBuilder.SeedConcept(name, _spellPool, values, features, rng)
					: null
			)
			?? DeckBuilder.SeedDistinct(
				name,
				_spellPool,
				values,
				rng,
				others,
				_minDifference,
				isWildcard[worst],
				features: features
			);
		ages[worst] = 0;
		// The replacement shares almost nothing with what it replaced, so its predecessor's
		// pair record is not evidence about it.
		slotHistory[worst] = new CardStatAccumulator();

		// A cull is the largest single edit the search makes, and leaving it out of the log would
		// make a card look like it was never tried when in fact its whole deck was replaced under it.
		var (gained, lost) = MutationLog.Diff(before, field[worst]);
		_mutations.Add(
			new MutationRow(
				gen,
				worst,
				name,
				gained,
				lost,
				worstRate,
				worstRate,
				tallies[worst][0].Games,
				MutationLog.Reseeded
			)
		);

		Console.WriteLine(
			$"    culled {field[worst].Name} at {worstRate:P1} (gen {gen}) — re-seeded"
		);
		return 1;
	}

	/// <summary>
	/// **The field against the hand-built references, at final volume — the number a gauntlet
	/// exists to produce, and which nothing printed.**
	///
	/// The gauntlet was already counted in fitness during evolution, so it shaped which mutants were
	/// accepted; but `FinalRoundRobin` plays field-versus-field only, so the REPORT still showed a
	/// closed round-robin that averages exactly 50% by construction. Adding a reference and then not
	/// printing the gap reproduces the original defect one level up: the mode still could not tell
	/// "my decks are good" from "my decks are equally mediocre".
	///
	/// Read the gap, not the rates. References overperforming means the builder still has work; even
	/// or slightly behind means it is doing its job. These decks are not costed to a rate and have
	/// never been tuned, so a large number in either direction is a finding about which side, and
	/// the per-deck rows are what say which.
	/// </summary>
	private double[,] FinalGauntlet(IReadOnlyList<Decklist> field)
	{
		var schedule = new List<(int Deck, int Ref, int Seed, bool DeckOnPlay)>();
		for (var i = 0; i < _deckCount; i++)
		for (var r = 0; r < _gauntlet.Count; r++)
		for (var k = 0; k < _finalGamesPerMatchup; k++)
			schedule.Add((i, r, _seed + 8_000_000 + (i * 1009 + r) * 10_007 + k * 5, k % 2 == 0));

		var results = new GameResult[schedule.Count];
		Parallel.For(
			0,
			schedule.Count,
			s =>
			{
				var (i, r, gameSeed, deckOnPlay) = schedule[s];
				var name = _gauntlet[r];
				var buildDeck = (int owner) => field[i].Materialize(owner, _poolIndex);
				var buildRef = (int owner) => DeckRegistry.Build(name, owner);

				var (state, ids, cardNames) = GameSetup.FromDecks(
					deckOnPlay ? buildDeck : buildRef,
					deckOnPlay ? buildRef : buildDeck
				);
				var aiRng = new Random(gameSeed + 4);
				var runner = new GameRunner(
					new MultiTurnBeamSearchAiStrategy(
						ids,
						_aiDepth,
						rng: aiRng,
						cardValues: AiCardValues.Current
					),
					new MultiTurnBeamSearchAiStrategy(
						ids,
						_aiDepth,
						rng: aiRng,
						cardValues: AiCardValues.Current
					)
				);
				var (result, _) = runner.Run(
					state,
					ids,
					cardNames,
					shuffleSeed: gameSeed + 2,
					gameRngSeed: gameSeed + 3
				);
				results[s] = result with { AllEvents = [] };
			}
		);

		var wins = new int[_deckCount, _gauntlet.Count];
		var games = new int[_deckCount, _gauntlet.Count];
		for (var s = 0; s < schedule.Count; s++)
		{
			var (i, r, _, deckOnPlay) = schedule[s];
			var result = results[s];
			if (
				result.EndReason
				is GameEndReason.TimeLimitReached
					or GameEndReason.UnhandledException
			)
				continue;

			games[i, r]++;
			if (deckOnPlay ? result.IsPlayer1Win : result.IsPlayer2Win)
				wins[i, r]++;
		}

		var rates = new double[_deckCount, _gauntlet.Count];
		for (var i = 0; i < _deckCount; i++)
		for (var r = 0; r < _gauntlet.Count; r++)
			rates[i, r] = games[i, r] == 0 ? -1 : (double)wins[i, r] / games[i, r];
		return rates;
	}

	private void PrintGauntlet(IReadOnlyList<Decklist> field, double[,] rates)
	{
		Console.WriteLine();
		Console.WriteLine(
			$"  === The field against {_gauntlet.Count} hand-built references "
				+ $"({_finalGamesPerMatchup} games each) ==="
		);
		Console.Write($"  {"", -14}");
		foreach (var name in _gauntlet)
			Console.Write($"{Abbrev(name), 14}");
		Console.WriteLine($"{"vs refs", 10}");

		var all = new List<double>();
		for (var i = 0; i < field.Count; i++)
		{
			Console.Write($"  {Truncate(field[i].Name, 14), -14}");
			var row = new List<double>();
			for (var r = 0; r < _gauntlet.Count; r++)
			{
				var cell = rates[i, r];
				Console.Write(cell < 0 ? $"{"--", 14}" : $"{cell, 14:P0}");
				if (cell >= 0)
					row.Add(cell);
			}
			Console.WriteLine(row.Count == 0 ? $"{"--", 10}" : $"{row.Average(), 10:P1}");
			all.AddRange(row);
		}

		if (all.Count == 0)
			return;

		// **The headline, and the only number here that is about the FIELD rather than a deck.**
		// Below 50% the evolved metagame loses to lists nobody tuned, which is the finding that
		// invalidates reading any internal spread as health.
		var mean = all.Average();
		Console.WriteLine();
		Console.WriteLine(
			$"  Field vs references: {mean:P1}"
				+ (
					mean < 0.45 ? "  <- the field LOSES to untuned hand-built decks"
					: mean > 0.55 ? "  <- the field beats them; the references are the weak side"
					: "  <- even, which is what a healthy field looks like"
				)
		);
	}

	/// <summary>
	/// **What the search actually tried, which no report used to say.**
	///
	/// A final decklist shows what SURVIVED. "Wirewood Conduit is not in the elf deck" has at least
	/// two causes with opposite fixes — never proposed (a selection-heuristic problem) or proposed,
	/// played and cut (a survivability problem) — and both were argued from the fill rule rather than
	/// measured, because nothing distinguished them.
	///
	/// Three views, cheapest question first: how the operators are doing overall, which cards the
	/// search keeps reaching for, and the whole log as CSV for anything else. The CSV is the real
	/// deliverable — "when did card X enter and leave slot 3" is a sort, not a report, and building a
	/// fixed view per question is how a report grows to a page nobody reads.
	/// </summary>
	private void PrintMutations()
	{
		var rows = _mutations.Rows;
		if (rows.Count == 0)
			return;

		Console.WriteLine();
		Console.WriteLine("  Mutations");

		// **Per slot, because the interesting failure is not visible in the total.** A slot that
		// spends its whole budget producing nothing legal is frozen at its seed, and it reads in the
		// matrix exactly like a deck that was offered improvements and refused them — opposite
		// diagnoses.
		//
		// Measured on a 10-deck DES run: engine slots with core pools of 129 and 54 cards produced
		// 32 and 33 real proposals on a full budget, while pools of 2 and 5 produced 3 and 2 — and
		// both of the latter finished NON-VIABLE. **Core pool size is not the only cause**, though:
		// a non-engine slot with no pool lock at all was also measured at 0 real against 6 dry, so
		// something else in `Mutate` returns null too and has not been identified. Read this table
		// as the observable, not as a diagnosis.
		var dry = rows.Count(r => r.Outcome == MutationLog.NoProposal);
		if (dry > 0)
		{
			Console.WriteLine(
				$"    {dry} mutation attempts produced NO legal proposal — that slot was frozen at "
					+ "its list, not offered changes and refusing them:"
			);
			Console.WriteLine($"    {"slot", -30}{"real", 7}{"dry", 6}{"kept", 6}");
			foreach (
				var g in rows.Where(r => r.Outcome != MutationLog.Reseeded)
					.GroupBy(r => (r.Slot, r.Deck))
					.OrderBy(g => g.Key.Slot)
			)
				Console.WriteLine(
					$"    {Truncate(g.Key.Deck, 30), -30}"
						+ $"{g.Count(r => r.Outcome != MutationLog.NoProposal), 7}"
						+ $"{g.Count(r => r.Outcome == MutationLog.NoProposal), 6}"
						+ $"{g.Count(r => r.Outcome == MutationLog.Accepted), 6}"
				);
			Console.WriteLine();
		}

		var proposals = rows.Where(r =>
				r.Outcome != MutationLog.Reseeded && r.Outcome != MutationLog.NoProposal
			)
			.ToList();
		var acceptedRows = proposals.Where(r => r.Outcome == MutationLog.Accepted).ToList();
		var tooSimilar = proposals.Count(r => r.Outcome == MutationLog.TooSimilar);

		// **`too-similar` is broken out because it is a different failure from losing on fitness.**
		// A field pinned by its diversity floor is rejecting proposals it agreed were improvements,
		// and this document already records a run whose diversity sat exactly on the constraint for
		// all twelve generations — that reads as healthy in every other number.
		Console.WriteLine(
			$"    {proposals.Count} proposals, {acceptedRows.Count} accepted "
				+ $"({(proposals.Count == 0 ? 0 : 100.0 * acceptedRows.Count / proposals.Count):F0}%), "
				+ $"{tooSimilar} rejected on diversity, "
				+ $"{rows.Count(r => r.Outcome == MutationLog.Reseeded)} culls"
		);
		if (acceptedRows.Count > 0)
			Console.WriteLine(
				$"    accepted mutations gained {acceptedRows.Average(r => r.Delta) * 100:F1}pp on "
					+ $"average; all proposals averaged {proposals.Average(r => r.Delta) * 100:F1}pp"
			);

		// Most-reached-for first. `mean` is over PROPOSALS — averaging the accepted ones alone would
		// report every card as positive by construction, since acceptance is conditioned on beating
		// the parent.
		var byCard = _mutations.ByCard();
		Console.WriteLine();
		Console.WriteLine($"    {"card", -28}{"tried", 7}{"kept", 6}{"mean", 9}{"gens", 10}");
		foreach (var c in byCard.Take(20))
			Console.WriteLine(
				$"    {Truncate(c.Card, 28), -28}{c.Proposed, 7}{c.Accepted, 6}"
					+ $"{c.MeanDelta * 100, 8:+0.0;-0.0}{$"{c.FirstGen}-{c.LastGen}", 10}"
			);

		if (byCard.Count > 20)
			Console.WriteLine($"    … {byCard.Count - 20} more cards in the CSV");

		try
		{
			Console.WriteLine();
			Console.WriteLine($"    Full log: {_mutations.Save(_set.Code)}");
		}
		catch (IOException ex)
		{
			// A report that cannot write its file must not lose the run's results with it.
			Console.WriteLine($"    WARNING: could not write the mutation log — {ex.Message}");
		}
	}

	/// <summary>
	/// Every deck against every other, at higher volume than the mutation tests. This is what
	/// the reported matrix is measured on.
	/// </summary>
	private (
		IReadOnlyList<IReadOnlyList<double>> Matrix,
		IReadOnlyList<double> Overall
	) FinalRoundRobin(IReadOnlyList<Decklist> field, CardStatAccumulator accumulator)
	{
		var schedule = new List<(int A, int B, int Seed, bool AOnPlay)>();
		for (var i = 0; i < _deckCount; i++)
		for (var j = i + 1; j < _deckCount; j++)
		for (var k = 0; k < _finalGamesPerMatchup; k++)
			schedule.Add((i, j, _seed + 7_000_000 + (i * 1009 + j) * 10_007 + k * 5, k % 2 == 0));

		var results = new GameResult[schedule.Count];
		Parallel.For(
			0,
			schedule.Count,
			s =>
			{
				var (a, b, gameSeed, aOnPlay) = schedule[s];
				var (deck1, deck2) = aOnPlay ? (field[a], field[b]) : (field[b], field[a]);
				var (state, ids, cardNames) = ConstructedGameSetup.Build(deck1, deck2, _poolIndex);
				var aiRng = new Random(gameSeed + 4);
				var runner = new GameRunner(
					new MultiTurnBeamSearchAiStrategy(
						ids,
						_aiDepth,
						rng: aiRng,
						cardValues: AiCardValues.Current
					),
					new MultiTurnBeamSearchAiStrategy(
						ids,
						_aiDepth,
						rng: aiRng,
						cardValues: AiCardValues.Current
					)
				);
				var (result, _) = runner.Run(
					state,
					ids,
					cardNames,
					shuffleSeed: gameSeed + 2,
					gameRngSeed: gameSeed + 3
				);
				results[s] = result with { AllEvents = [] };
			}
		);

		var wins = new int[_deckCount, _deckCount];
		var games = new int[_deckCount, _deckCount];

		for (var s = 0; s < schedule.Count; s++)
		{
			var (a, b, _, aOnPlay) = schedule[s];
			var result = results[s];
			if (
				result.EndReason
				is GameEndReason.TimeLimitReached
					or GameEndReason.UnhandledException
			)
				continue;

			var aWon = aOnPlay ? result.IsPlayer1Win : result.IsPlayer2Win;
			var bWon = aOnPlay ? result.IsPlayer2Win : result.IsPlayer1Win;

			games[a, b]++;
			games[b, a]++;
			if (aWon)
				wins[a, b]++;
			if (bWon)
				wins[b, a]++;

			var aDrawn = aOnPlay ? result.Player1DrawnCards : result.Player2DrawnCards;
			var bDrawn = aOnPlay ? result.Player2DrawnCards : result.Player1DrawnCards;
			accumulator.Add(aDrawn, field[a].Spells.Keys.ToList(), aWon);
			accumulator.Add(bDrawn, field[b].Spells.Keys.ToList(), bWon);
		}

		var matrix = new List<IReadOnlyList<double>>();
		var overall = new List<double>();
		for (var i = 0; i < _deckCount; i++)
		{
			var row = new List<double>();
			var totalWins = 0;
			var totalGames = 0;
			for (var j = 0; j < _deckCount; j++)
			{
				if (i == j)
				{
					row.Add(-1);
					continue;
				}
				row.Add(games[i, j] == 0 ? -1 : (double)wins[i, j] / games[i, j]);
				totalWins += wins[i, j];
				totalGames += games[i, j];
			}
			matrix.Add(row);
			overall.Add(totalGames == 0 ? 0 : (double)totalWins / totalGames);
		}

		return (matrix, overall);
	}

	private void PrintHeader(ConstructedValues values)
	{
		Console.WriteLine(
			$"Evolving {_deckCount} decks on {_set.Name} ({_set.Cards.Count} cards, "
				+ $"{_spellPool.Count} spells): {_generations} generations x {_mutantsPerDeck} mutants, "
				+ $"{_gamesPerMatchup} games/matchup, AI depth {_aiDepth}, seed {_seed}"
		);
		Console.WriteLine($"  {AiCardValues.Describe()}");
		// A run log has to record its own configuration. "culled 0" on every line is
		// indistinguishable from a field that simply never fell below the floor, and the
		// difference decides whether the run is comparable to another one.
		Console.WriteLine(
			$"  Diversity floor {_minDifference:P0}, viability floor {_viabilityFloor:P0}, "
				+ $"culling {(_cullEnabled ? $"ON (grace {_graceGenerations}, settling {_graceGenerations})" : "OFF")}"
		);
		Console.WriteLine(
			$"  Pre-simulation {(_preSimDecks > 0 ? $"{_preSimDecks} decks x {_preSimOpponents} opponents" : "OFF")}; "
				+ $"adaptive mutation: 0 mutants at >={StableRate:P0}, {_mutantsPerDeck} at <={StrugglingRate:P0}"
		);
		Console.WriteLine(
			values.HasDraftPrior
				? $"  Seeding prior: draft model. Isolation data so far: {values.ConstructedDeckGames} deck-games."
				: "  Seeding prior: NONE (quality-blind seeding, table builds from scratch)."
		);

		// **A card value now means "in a random deck", and an empty table is expected the first
		// time.** Said out loud because the alternative is a run that silently seeds every card at
		// exactly the prior and looks like it is working.
		if (values.ConstructedDeckGames == 0 && _preSimDecks == 0)
			Console.WriteLine(
				"  WARNING: no isolation data and presim is OFF — every card scores at the prior. "
					+ "Run once with presim > 0 to build "
					+ ConstructedValuesStore.PresimPathFor(_set.Code)
			);

		if (File.Exists(ConstructedValuesStore.LegacyPathFor(_set.Code)))
			Console.WriteLine(
				$"  NOTE: {ConstructedValuesStore.LegacyPathFor(_set.Code)} is the pre-split table "
					+ "and is no longer read — it mixed random and evolved games with no provenance."
			);

		if (string.Equals(_set.Code, SetRegistry.CombinedCode, StringComparison.OrdinalIgnoreCase))
		{
			var replaced = SetRegistry.CombinedReplacements;
			Console.WriteLine(
				$"  Combined pool: {replaced.Count} duplicate name(s) resolved to the last printing."
			);
			foreach (var (name, set) in replaced)
				Console.WriteLine($"    {name} -> {set}");
		}

		// **Always print the gauntlet by name.** It is now part of fitness, so a run whose
		// benchmark is unstated is a run whose result cannot be interpreted later.
		if (_gauntletGames > 0)
		{
			if (_gauntlet.Count == 0)
				Console.WriteLine(
					$"  WARNING: gauntlet requested but none is defined for {_set.Code} — "
						+ "running without one. See Gauntlet.For."
				);
			else
			{
				Console.WriteLine(
					$"  Gauntlet ({_gauntlet.Count} decks x {_gauntletGames} games, counted in "
						+ "fitness, NOT in diversity):"
				);
				foreach (var name in _gauntlet)
				{
					var missing = Gauntlet.MissingFrom(name, _spellPool);
					Console.WriteLine(
						$"    {name}"
							+ (
								missing.Count == 0
									? "  (fully buildable from this pool)"
									: $"  WARNING: {missing.Count} card(s) not in pool — challengers "
										+ $"cannot copy it: {string.Join(", ", missing.Take(4))}"
							)
					);
				}
			}
		}

		var perGen =
			_deckCount
			* (1 + _mutantsPerDeck)
			* ((_deckCount - 1) * _gamesPerMatchup + _gauntlet.Count * _gauntletGames);
		Console.WriteLine(
			$"  ~{perGen} games/generation, ~{perGen * _generations} total "
				+ $"(+{_deckCount * (_deckCount - 1) / 2 * _finalGamesPerMatchup} final)."
		);
		Console.WriteLine();
	}

	private void PrintGeneration(
		int gen,
		IReadOnlyList<Decklist> field,
		Tally[][] tallies,
		int accepted,
		int culled,
		int excluded,
		Stopwatch timer
	)
	{
		var rates = Enumerable.Range(0, _deckCount).Select(i => tallies[i][0].Rate).ToList();
		var spread = rates.Max() - rates.Min();
		var minDiff = Enumerable
			.Range(0, _deckCount)
			.Select(i =>
				DeckBuilder.MinDifference(field[i], field.Where((_, j) => j != i).ToList())
			)
			.Min();

		Console.WriteLine(
			$"  gen {gen, 3}: spread {rates.Min():P1}-{rates.Max():P1} ({spread * 100:F1}pp), "
				+ $"diversity {minDiff:P0}, accepted {accepted}/{_deckCount}, culled {culled}, "
				+ $"excluded {excluded}, {timer.Elapsed.TotalMinutes:F1}m"
		);
	}

	private void PrintReport(
		MetagameResult result,
		ConstructedValues values,
		CardStatAccumulator accumulator,
		int totalGames,
		int totalExcluded,
		TimeSpan elapsed,
		int[] ages
	)
	{
		Console.WriteLine();
		Console.WriteLine("  === Metagame ===");
		Console.WriteLine();

		var overall = result.OverallWinRates;
		var spread = overall.Max() - overall.Min();
		var viable = overall.Count(r => r >= _viabilityFloor);
		var minDiff = Enumerable
			.Range(0, result.Decks.Count)
			.Select(i =>
				DeckBuilder.MinDifference(
					result.Decks[i],
					result.Decks.Where((_, j) => j != i).ToList()
				)
			)
			.Min();

		Console.WriteLine(
			$"  Field spread: {overall.Min():P1} .. {overall.Max():P1} ({spread * 100:F1}pp)     "
				+ $"Min diversity: {minDiff:P0}     Viable: {viable}/{result.Decks.Count}"
		);
		Console.WriteLine();

		// Matchup matrix. `age` is generations since this slot was last seeded — a low number
		// next to a low win rate means the deck is still climbing, not that it is bad.
		Console.Write($"  {"", -12}");
		for (var j = 0; j < result.Decks.Count; j++)
			Console.Write($"{Abbrev(result.Decks[j].Name), 8}");
		Console.WriteLine($"{"overall", 10}{"age", 6}");

		for (var i = 0; i < result.Decks.Count; i++)
		{
			Console.Write($"  {Truncate(result.Decks[i].Name, 12), -12}");
			for (var j = 0; j < result.Decks.Count; j++)
			{
				var cell = result.Matrix[i][j];
				Console.Write(cell < 0 ? $"{"--", 8}" : $"{cell, 8:P0}");
			}
			var flag = overall[i] < _viabilityFloor ? "  NON-VIABLE" : "";
			Console.WriteLine($"{overall[i], 10:P1}{ages[i], 6}{flag}");
		}

		// Best matchup per deck — a deck under the floor that still counters something is a
		// real archetype; one that beats nothing is not.
		Console.WriteLine();
		for (var i = 0; i < result.Decks.Count; i++)
		{
			var best = -1;
			var bestRate = -1.0;
			for (var j = 0; j < result.Decks.Count; j++)
			{
				if (i != j && result.Matrix[i][j] > bestRate)
					(best, bestRate) = (j, result.Matrix[i][j]);
			}
			if (best >= 0)
				Console.WriteLine(
					$"  {Truncate(result.Decks[i].Name, 12), -12} best matchup: "
						+ $"{bestRate:P0} vs {result.Decks[best].Name}"
				);
		}

		// The external yardstick. Printed before the mutation log because it is the number that
		// decides whether anything else in this report is worth reading — an internal spread says
		// nothing if the whole field loses to lists nobody tuned.
		if (_gauntlet.Count > 0)
			PrintGauntlet(result.Decks, FinalGauntlet(result.Decks));

		PrintMutations();

		// **The exclusion list for the NEXT run, printed rather than transcribed.** An engine slot
		// under the floor is the punching-bag case: it is never culled (mode 7 already judged the
		// archetype on assembly), so it stays in the field for the whole run inflating every other
		// deck's rate. Deciding it is dead is still the reader's call — this only removes the step
		// where that decision is retyped from a matchup matrix by hand.
		var dead = Enumerable
			.Range(0, result.Decks.Count)
			.Where(i =>
				overall[i] < _viabilityFloor
				&& result.Decks[i].Name.StartsWith("Engine-", StringComparison.Ordinal)
			)
			.Select(i => NormaliseEngineName(result.Decks[i].Name))
			.ToList();
		if (dead.Count > 0)
		{
			Console.WriteLine();
			Console.WriteLine(
				$"  {dead.Count} engine slot(s) finished below the {_viabilityFloor:P0} floor. To "
					+ "leave them as curve decks next run, paste at the exclusion prompt:"
			);
			Console.WriteLine($"    {string.Join(", ", dead)}");
		}

		// Decklists
		Console.WriteLine();
		foreach (var deck in result.Decks)
		{
			Console.WriteLine();
			Console.WriteLine(deck.Format(_poolIndex));
			Console.WriteLine(
				$"  (avg cost {deck.AverageCost(_poolIndex):F2}, {deck.DistinctSpells} distinct)"
			);
		}

		PrintMovers(values, accumulator);

		Console.WriteLine();
		Console.WriteLine(
			$"  {totalGames} evolution games, {totalExcluded} excluded (broken), "
				+ $"{elapsed.TotalMinutes:F1} minutes."
		);
	}

	/// <summary>
	/// The acceptance test for the whole limited-vs-constructed concern. A Spearman near 1.0
	/// with an empty mover list means the constructed data is NOT displacing the draft prior,
	/// and the mode is just building consistent draft decks.
	/// </summary>
	private void PrintMovers(ConstructedValues values, CardStatAccumulator accumulator)
	{
		var updated = new ConstructedValues(
			DraftTrainingData.Merge(values.Data, accumulator.ToData()),
			ConstructedValuesStore.LoadDraftPrior(_set.Code)
		);

		var movers = updated.Movers();
		if (movers.Count == 0)
		{
			Console.WriteLine();
			Console.WriteLine(
				"  Constructed vs limited: no card has enough constructed games yet. Run more "
					+ "generations before reading anything into the valuations."
			);
			return;
		}

		var spearman = updated.SpearmanAgainstDraft();
		Console.WriteLine();
		Console.WriteLine(
			$"  === Constructed vs limited ({movers.Count} cards, Spearman {spearman:F3}) ==="
		);
		Console.WriteLine(
			$"  Movement is centred on the median ({updated.FormatOffset():+0.00;-0.00}pp), which is "
				+ "the games-in-hand offset between formats, not a finding about any card."
		);
		if (spearman > 0.95)
			Console.WriteLine(
				"  WARNING: ranking is nearly unchanged from the draft model. Either there is "
					+ "not enough constructed data yet, or the blend is not being applied."
			);

		// Split rather than Take(10)/TakeLast(10): with fewer than 20 movers those overlap and
		// the same card is printed as both a riser and a faller, which reads as a bug in the
		// measurement rather than in the display.
		var half = Math.Min(10, movers.Count / 2);
		if (half == 0)
		{
			Console.WriteLine();
			Console.WriteLine(
				"  Too few cards with enough games to split into risers and fallers."
			);
			return;
		}

		Console.WriteLine();
		Console.WriteLine("  Better in constructed than limited:");
		foreach (var m in movers.Take(half))
			Console.WriteLine(
				$"    {m.Name, -32} draft {m.DraftPP, +6:F2}pp -> constructed {m.ConstructedPP, +6:F2}pp  ({m.Move, +6:F2})"
			);

		Console.WriteLine();
		Console.WriteLine("  Worse in constructed than limited:");
		foreach (var m in movers.TakeLast(half).Reverse())
			Console.WriteLine(
				$"    {m.Name, -32} draft {m.DraftPP, +6:F2}pp -> constructed {m.ConstructedPP, +6:F2}pp  ({m.Move, +6:F2})"
			);
	}

	private void Save(
		MetagameResult result,
		CardStatAccumulator accumulator,
		DraftTrainingData presim
	)
	{
		var path = DecklistStore.PathFor(_set.Code);
		DecklistStore.Save(result, path);

		// **Evolved counts do NOT go into the value table**, and `presim` is deliberately not
		// merged in here either — it was already persisted at the top of the run, where it was
		// still separable. Mixing them is what put Goblin Chieftain 9.6pp above its draft rate
		// and Thoughtcast 8.1pp below its own.
		ConstructedValuesStore.SaveEvolved(accumulator.ToData(), _set.Code);

		Console.WriteLine();
		Console.WriteLine($"  Decklists -> {path}");
		Console.WriteLine(
			$"  Card values (isolation) -> {ConstructedValuesStore.PresimPathFor(_set.Code)}"
		);
		Console.WriteLine(
			$"  Evolved-game counts (diagnostics only) -> "
				+ ConstructedValuesStore.EvolvedPathFor(_set.Code)
		);
		Console.WriteLine(
			"  Both are relative to the SHELL's working directory — run from the repo root."
		);
	}

	private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

	private static string Abbrev(string s) => Truncate(s, 7);
}
