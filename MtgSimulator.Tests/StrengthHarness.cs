using System.Diagnostics;
using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// Plays two AI configurations against each other over real drafted decks and reports a win rate
/// with its standard error.
///
/// Every strength claim in this project either comes from here or is a guess. Three changes
/// shipped in one session that altered which action the AI returns, each defensible on reasoning
/// alone, none measured — this exists so that stops being the normal case.
///
/// Extracted from <c>BranchingCapStrengthTests</c>, which pioneered the shape, with three fixes:
///
/// 1. **Each arm gets its own RNG.** The original shares one Random between both strategies, so
///    they consume each other's draws. Deterministic while sequential, but it means the two arms
///    interfere, and it cannot be parallelised at all.
/// 2. **Games run in parallel** into a pre-allocated array and fold sequentially — the DraftTrainer
///    phase ordering, for the same reason: thread completion order must never reach a result.
/// 3. **Arms interleave across the schedule** rather than running in blocks. From the Strategy Card
///    Game AI Competition organisers: different agents use the CPU differently, so concatenated
///    runs let one arm systematically get a busier machine. This project has already lost a
///    measurement to exactly that (the parallel-batch draw disaster).
/// </summary>
public static class StrengthHarness
{
	/// <summary>One side of the comparison. The Random is per-game and per-arm.</summary>
	public sealed record Arm(string Name, Func<MtgGameIds, Random, IAiStrategy> Build);

	public sealed record Outcome(
		string ChallengerName,
		string BaselineName,
		int ChallengerWins,
		int Decided,
		int Games,
		double Seconds,
		long TotalActions
	)
	{
		public double Rate => 100.0 * ChallengerWins / Math.Max(1, Decided);

		/// <summary>Standard error in percentage points: 50/sqrt(n).</summary>
		public double StandardError => 100.0 * Math.Sqrt(0.25 / Math.Max(1, Decided));

		/// <summary>
		/// Reported beside the clock, never without it. A wall-clock number names a symptom, not a
		/// culprit: an 8.8% "regression" in this project was blamed on an evaluator refactor and
		/// "fixed" twice before anyone noticed actions had gone DOWN while time went UP — impossible
		/// for a per-call cost, and the real cause was a win-detection bug two commits earlier.
		/// If time moves and this does not, the cost is per-call; if both move, the search changed
		/// how much work it does and the two are not comparable.
		/// </summary>
		public double AvgActions => (double)TotalActions / Math.Max(1, Games);

		public override string ToString() =>
			$"{ChallengerName} vs {BaselineName}: {ChallengerWins}/{Decided} decided "
			+ $"({Rate:F1}%, 1 SE = {StandardError:F1}pp) over {Games} games in {Seconds:F0}s, "
			+ $"{AvgActions:F1} actions/game";
	}

	/// <summary>
	/// 40 drafts is 1120 games, which resolves a 3.0pp effect at two standard errors. The 8 drafts
	/// the original used gives 224 games and 6.7pp — enough to answer "did we break it", not
	/// "is it better".
	/// </summary>
	public const int DefaultDrafts = 40;

	private const int Seats = 8;
	private const int AiDepth = 3;

	private static IReadOnlyList<IReadOnlyList<Card>> DraftPools(int seed)
	{
		var pickers = Enumerable
			.Range(0, Seats)
			.Select(i =>
				i % 2 == 0
					? (DraftPicker)DraftPickers.Curve
					: DraftPickers.Random(new Random(seed + i))
			)
			.ToList();

		var final = Draft.RunToCompletion(
			Draft.Create(DraftFormat.Booster, CoresetCube.Set.Cards, seed, Seats),
			pickers
		);
		return final.Seats.Select(s => (IReadOnlyList<Card>)s.Pool).ToList();
	}

	private sealed record Pairing(
		IReadOnlyList<Card> Pool1,
		IReadOnlyList<Card> Pool2,
		int GameSeed,
		bool ChallengerIsPlayer1
	);

	public static Outcome Measure(
		Arm challenger,
		Arm baseline,
		int drafts = DefaultDrafts,
		int poolSeed = 4242,
		int gameSeed = 900_000
	)
	{
		// Schedule built sequentially so the pools — and therefore the whole comparison — are
		// reproducible regardless of how the games are later distributed across cores.
		var schedule = new List<Pairing>();
		for (var d = 0; d < drafts; d++)
		{
			var pools = DraftPools(poolSeed + d * 1000);
			for (var a = 0; a < Seats; a++)
			for (var b = a + 1; b < Seats; b++)
				schedule.Add(
					new Pairing(
						pools[a],
						pools[b],
						gameSeed + schedule.Count * 5,
						// Alternating means the play/draw advantage cannot be read as an arm
						// effect, and it interleaves the two arms across the run so neither
						// systematically gets a busier machine.
						schedule.Count % 2
							== 0
					)
				);
		}

		var results = new (bool Decided, bool ChallengerWon, int Actions)[schedule.Count];
		var sw = Stopwatch.StartNew();

		Parallel.For(
			0,
			schedule.Count,
			i =>
			{
				var p = schedule[i];
				var (state, ids, cardNames) = DraftGameSetup.Build(p.Pool1, p.Pool2);

				// Each arm builds from its OWN Random. Sharing one instance makes the two
				// strategies consume each other's draws, which is both a confound and the reason
				// the original harness could not be parallelised.
				var challengerAi = challenger.Build(ids, new Random(p.GameSeed + 4));
				var baselineAi = baseline.Build(ids, new Random(p.GameSeed + 4));

				var runner = p.ChallengerIsPlayer1
					? new GameRunner(challengerAi, baselineAi)
					: new GameRunner(baselineAi, challengerAi);

				var (result, _) = runner.Run(
					state,
					ids,
					cardNames,
					shuffleSeed: p.GameSeed + 2,
					gameRngSeed: p.GameSeed + 3
				);

				if (result.IsDraw)
				{
					results[i] = (false, false, result.TotalActions);
					return;
				}

				var challengerWon = p.ChallengerIsPlayer1
					? result.IsPlayer1Win
					: result.IsPlayer2Win;
				results[i] = (true, challengerWon, result.TotalActions);
			}
		);

		sw.Stop();

		// Folded sequentially — a sum does not care what order it is added in, but reading the
		// array in index order keeps the reported number independent of thread scheduling.
		var decided = 0;
		var wins = 0;
		var actions = 0L;
		foreach (var (isDecided, won, gameActions) in results)
		{
			// Actions are summed over EVERY game, drawn or not — it is a cost metric, not a
			// strength one, and a game that drew still did the work.
			actions += gameActions;
			if (!isDecided)
				continue;
			decided++;
			if (won)
				wins++;
		}

		return new Outcome(
			challenger.Name,
			baseline.Name,
			wins,
			decided,
			schedule.Count,
			sw.Elapsed.TotalSeconds,
			actions
		);
	}

	// ===== ARMS =====

	public static Arm Default(string name = "default") =>
		new(name, (ids, rng) => new MultiTurnBeamSearchAiStrategy(ids, AiDepth, rng: rng));

	/// <summary>
	/// The harness self-check. Branching 1 is a genuinely crippled search and must lose clearly;
	/// if it scores near even, the harness is measuring nothing and every number it produces is
	/// noise. Never trust a result from this file without running this first.
	/// </summary>
	public static Arm Crippled() =>
		new(
			"branching-1",
			(ids, rng) =>
				new MultiTurnBeamSearchAiStrategy(
					ids,
					AiDepth,
					rng: rng,
					maxBranching: 1,
					expandBranching: 1
				)
		);

	/// <summary>Terminal decay disabled — lambda 1.0 restores the pre-discount behaviour.</summary>
	public static Arm NoTerminalDiscount() =>
		new(
			"no-discount",
			(ids, rng) =>
				new MultiTurnBeamSearchAiStrategy(ids, AiDepth, rng: rng, terminalDiscount: 1.0f)
		);

	/// <summary>
	/// FindWinner restored to FirstOrDefault — takes the first winning node in beam order rather
	/// than the fastest win. Correct before terminals became rankable; measured against the fix.
	/// </summary>
	public static Arm FirstWinNotBestWin() =>
		new(
			"first-win",
			(ids, rng) =>
				new MultiTurnBeamSearchAiStrategy(ids, AiDepth, rng: rng, preferFastestWin: false)
		);

	/// <summary>
	/// The default evaluator with a toughness term switched on. See
	/// <c>WeightedStateEvaluator.ToughnessWeight</c> — the evaluator has never scored toughness at
	/// all, so a 5/1 and a 5/5 are currently the same creature to it.
	/// </summary>
	public static Arm Toughness(float weight) =>
		new(
			$"toughness-{weight:0.##}",
			(ids, rng) =>
				new MultiTurnBeamSearchAiStrategy(
					ids,
					AiDepth,
					rng: rng,
					evaluator: WeightedStateEvaluator.Default with
					{
						ToughnessWeight = weight,
					}
				)
		);

	/// <summary>
	/// The keyword term at an explicit weight. <c>Keywords(0f)</c> is the pre-keyword evaluator:
	/// the term is appended LAST in <c>Explain</c>'s sum, so at zero every other term adds to
	/// exactly the float it did before the term existed, and <c>ScanBattlefield</c> skips the
	/// <c>GetEffectiveStats</c> allocation entirely.
	///
	/// That equivalence is why this arm replaces the worktree-at-the-previous-commit the plan
	/// called for. Same binary, one flag — and no opportunity for a stale <c>bin/</c> to measure
	/// code that was never compiled in, which has cost this project two full training runs.
	/// </summary>
	public static Arm Keywords(float weight) =>
		new(
			$"keywords-{weight:0.###}",
			(ids, rng) =>
				new MultiTurnBeamSearchAiStrategy(
					ids,
					AiDepth,
					rng: rng,
					evaluator: WeightedStateEvaluator.Default with
					{
						KeywordWeight = weight,
					}
				)
		);

	/// <summary>
	/// Card-value scoring inside ResolveChoice, at an explicit weight. A null table is the
	/// behaviour before the feature existed, so <c>CardValues(0f)</c> style comparisons are done by
	/// passing the Default arm as the baseline.
	///
	/// Returns an inert arm if the sandbox file is missing, which shows up as a dead-even mirror
	/// match rather than as a crash — check the file before reading a 50% result as "no effect".
	/// </summary>
	public static Arm CardValues(float weight) =>
		new(
			$"card-values-{weight:0.###}",
			(ids, rng) =>
				new MultiTurnBeamSearchAiStrategy(
					ids,
					AiDepth,
					rng: rng,
					cardValues: CardValueTable.TryLoad(CoresetCube.Set.Code, weight)
				)
		);

	/// <summary>
	/// The half-applied-action bug restored: ExecuteAction leaves a raised choice unresolved, so
	/// the rollout can end the turn twice and fire end-of-turn triggers twice. See
	/// HalfAppliedActionTests.
	/// </summary>
	public static Arm UnresolvedChoices() =>
		new(
			"half-applied",
			(ids, rng) =>
				new MultiTurnBeamSearchAiStrategy(
					ids,
					AiDepth,
					rng: rng,
					resolveChoicesOnExecute: false
				)
		);
}
