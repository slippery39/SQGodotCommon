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
		string? enginesPath = null
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
	}

	/// <summary>
	/// A gauntlet opponent is encoded as a NEGATIVE opponent index: -1 - gauntletIndex. Keeps
	/// <see cref="Seed"/> unchanged (negative values still give distinct seeds) and keeps the
	/// paired-evaluation guarantee intact, which is the property everything else depends on.
	/// </summary>
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
	/// Highest LIFT first — that column is the only one distinguishing a synergy from a
	/// coincidence, so taking the top of it is taking the archetypes most likely to be real. The
	/// last slot is left alone: it is the permanent wildcard, and replacing the exploration arm
	/// with a fixed archetype removes the only slot that can find something nobody discovered.
	/// </summary>
	private IEnumerable<(int Slot, EngineCandidate Engine)> LoadEngines()
	{
		if (string.IsNullOrWhiteSpace(_enginesPath))
			yield break;

		var report = EngineReportStore.Load(_enginesPath);
		if (report is null)
		{
			Console.WriteLine($"  WARNING: no engine report at {_enginesPath} — running without.");
			yield break;
		}

		var usable = Math.Max(0, _deckCount - 1);
		var take = report.Engines.OrderByDescending(e => e.Lift).Take(usable).ToList();

		Console.WriteLine($"  Engines: {take.Count} of {report.Engines.Count} from {_enginesPath}");
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
			Console.WriteLine();
		}

		// Structural features are pool-derived and deterministic, so this is built once. Null
		// when no concept slot was asked for, which leaves every scoring path exactly as it was.
		PoolFeatures? features = null;
		if (_conceptSlots > 0)
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

		var ages = new int[_deckCount];
		var isWildcard = Enumerable.Range(0, _deckCount).Select(i => i == _deckCount - 1).ToArray();
		var isConcept = Enumerable
			.Range(0, _deckCount)
			.Select(i => features is not null && i < _conceptSlots && !isWildcard[i])
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
						DeckBuilder.Mutate(
							field[i],
							_spellPool,
							values,
							mutRng,
							history,
							features,
							identities[i]
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

				for (var c = 1; c < candidates[i].Count; c++)
				{
					var mutant = candidates[i][c];
					if (mutant is null || tallies[i][c].Games == 0)
						continue;
					if (tallies[i][c].Rate <= bestRate)
						continue;

					// The diversity constraint applies to the field as it will be, so it is
					// checked against every OTHER deck's current list.
					var others = field.Where((_, j) => j != i).ToList();
					if (DeckBuilder.MinDifference(mutant, others) < _minDifference)
						continue;

					bestIndex = c;
					bestRate = tallies[i][c].Rate;
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

		Console.WriteLine(
			$"    culled {field[worst].Name} at {worstRate:P1} (gen {gen}) — re-seeded"
		);
		return 1;
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
				? $"  Seeding prior: draft model. Constructed data so far: {values.ConstructedDeckGames} deck-games."
				: "  Seeding prior: NONE (quality-blind seeding, table builds from scratch)."
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
		ConstructedValuesStore.SaveMerged(
			DraftTrainingData.Merge(accumulator.ToData(), presim),
			_set.Code
		);

		Console.WriteLine();
		Console.WriteLine($"  Decklists -> {path}");
		Console.WriteLine(
			$"  Constructed values -> {ConstructedValuesStore.PathFor(_set.Code)} (merged)"
		);
		Console.WriteLine(
			"  Both are relative to the SHELL's working directory — run from the repo root."
		);
	}

	private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

	private static string Abbrev(string s) => Truncate(s, 7);
}
