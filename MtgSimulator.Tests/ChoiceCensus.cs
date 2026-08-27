using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// How much of the AI's time goes to resolving CHOICES rather than selecting actions, and what
/// shape those choices are.
///
/// This exists because the cost is invisible in every existing report: `GameRunner.ProcessChoice`
/// does not increment `TotalActions`, so a game that spent five minutes on choice resolution with
/// three permanents on the board reads as "13 actions, 327 seconds". Any claim about what pruning
/// the option list would save is a guess until this has been run — and a wall-clock number has
/// accused the wrong code three times in this project already.
///
/// Instrumentation is a decorator over `IAiStrategy`, so **nothing in production changes**.
/// `StrengthHarness.Arm` is already a factory returning the interface, which makes a mirror match
/// of two instrumented defaults a census over real drafted-deck games with no new game-running
/// code.
///
/// The timing is THREAD time, not wall time: games run in parallel, so a wall-clock share would be
/// divided by however many cores happened to be free. Shares are reported against
/// `select + choice`, which is the work the AI itself did.
/// </summary>
[TestFixture]
public class ChoiceCensus
{
	/// <summary>
	/// 8 drafts = 224 games. This is a population profile, not a strength measurement — it needs to
	/// be representative, not to resolve 3pp — so it deliberately does not pay for
	/// `StrengthHarness.DefaultDrafts`.
	/// </summary>
	private const int Drafts = 8;

	private readonly record struct ChoiceRow(
		string Prompt,
		int Options,
		int MinChoices,
		long Ticks
	);

	private sealed class CensusStrategy(IAiStrategy inner) : IAiStrategy
	{
		public static long SelectCalls;
		public static long SelectTicks;
		public static readonly ConcurrentBag<ChoiceRow> Choices = [];

		public static void Reset()
		{
			SelectCalls = 0;
			SelectTicks = 0;
			Choices.Clear();
		}

		public GameAction SelectAction(GameState state, MtgGameIds ids, int playerId)
		{
			var start = Stopwatch.GetTimestamp();
			var action = inner.SelectAction(state, ids, playerId);
			Interlocked.Add(ref SelectTicks, Stopwatch.GetTimestamp() - start);
			Interlocked.Increment(ref SelectCalls);
			return action;
		}

		public ImmutableList<int> ResolveChoice(GameState state, ChoiceAction choice, int playerId)
		{
			// Captured BEFORE the call: resolution mutates nothing here, but reading the option list
			// off the returned state is how a census ends up describing a different choice.
			var options = choice.Options.Count;
			var min = choice.MinChoices;
			var prompt = string.IsNullOrWhiteSpace(choice.Prompt) ? "(no prompt)" : choice.Prompt;

			var start = Stopwatch.GetTimestamp();
			var result = inner.ResolveChoice(state, choice, playerId);
			Choices.Add(new ChoiceRow(prompt, options, min, Stopwatch.GetTimestamp() - start));
			return result;
		}
	}

	/// <summary>
	/// Resolves every choice TWICE — once with card values, once without — and counts how often the
	/// two disagree.
	///
	/// This is what separates the two readings of a 50% strength result. "Fires often and does not
	/// help" and "never fires" are the same number and need opposite responses, and the sweep at
	/// 0.1 and 0.5 returned byte-identical results (560/1120, 62.5 actions), which is exactly what
	/// an inert feature looks like.
	///
	/// Both strategies get their own RNG seeded identically, so a disagreement is the card values
	/// and not the tiebreak.
	/// </summary>
	private sealed class AbStrategy(IAiStrategy withValues, IAiStrategy without) : IAiStrategy
	{
		public static long Choices;
		public static long Disagreements;
		public static long FlatRollouts;

		public static void Reset()
		{
			Choices = 0;
			Disagreements = 0;
			FlatRollouts = 0;
		}

		public GameAction SelectAction(GameState state, MtgGameIds ids, int playerId) =>
			withValues.SelectAction(state, ids, playerId);

		public ImmutableList<int> ResolveChoice(GameState state, ChoiceAction choice, int playerId)
		{
			var a = withValues.ResolveChoice(state, choice, playerId);
			var b = without.ResolveChoice(state, choice, playerId);

			Interlocked.Increment(ref Choices);
			if (!a.SequenceEqual(b))
				Interlocked.Increment(ref Disagreements);

			// How often the rollout cannot separate the options at all — the region card values
			// exist to decide. Measured by whether the unvalued strategy had anything to go on.
			if (choice.Options.Count > 1)
			{
				var scores = choice
					.Options.Select(o => state.ResolveChoice(ImmutableList.Create(o.Id)))
					.Select(r => StateEvaluator.Evaluate(r.State, MtgGameIds(state), playerId))
					.ToList();
				if (scores.Max() - scores.Min() < 0.0001f)
					Interlocked.Increment(ref FlatRollouts);
			}

			return a;
		}

		private static MtgGameIds MtgGameIds(GameState state) =>
			new(
				state.GetWellKnownId(MtgObjectKeys.Game),
				state.GetWellKnownId(MtgObjectKeys.Stack),
				state.GetWellKnownId(MtgObjectKeys.Player1),
				state.GetWellKnownId(MtgObjectKeys.Player1Hand),
				state.GetWellKnownId(MtgObjectKeys.Player1Library),
				state.GetWellKnownId(MtgObjectKeys.Player1Graveyard),
				state.GetWellKnownId(MtgObjectKeys.Player1Battlefield),
				state.GetWellKnownId(MtgObjectKeys.Player1Exile),
				state.GetWellKnownId(MtgObjectKeys.Player2),
				state.GetWellKnownId(MtgObjectKeys.Player2Hand),
				state.GetWellKnownId(MtgObjectKeys.Player2Library),
				state.GetWellKnownId(MtgObjectKeys.Player2Graveyard),
				state.GetWellKnownId(MtgObjectKeys.Player2Battlefield),
				state.GetWellKnownId(MtgObjectKeys.Player2Exile)
			);
	}

	[Test]
	[Explicit("Plays 224 real games")]
	public void HowOftenDoCardValuesChangeAChoice()
	{
		AbStrategy.Reset();

		var table = CardValueTable.TryLoad(CoresetCube.Set.Code, weight: 0.5f);
		Assert.That(
			table,
			Is.Not.Null,
			"no sandbox file — run CardValueSweep in this configuration"
		);

		var arm = new StrengthHarness.Arm(
			"ab",
			(ids, rng) =>
				new AbStrategy(
					new MultiTurnBeamSearchAiStrategy(
						ids,
						3,
						rng: new Random(12345),
						cardValues: table
					),
					new MultiTurnBeamSearchAiStrategy(ids, 3, rng: new Random(12345))
				)
		);

		var outcome = StrengthHarness.Measure(arm, arm, drafts: Drafts);

		TestContext.Out.WriteLine($"=== {outcome.Games} games ===");
		TestContext.Out.WriteLine($"  choices resolved     {AbStrategy.Choices}");
		TestContext.Out.WriteLine(
			$"  card values changed  {AbStrategy.Disagreements} "
				+ $"({100.0 * AbStrategy.Disagreements / Math.Max(1, AbStrategy.Choices):F1}%)"
		);
		TestContext.Out.WriteLine(
			$"  flat rollouts        {AbStrategy.FlatRollouts} "
				+ $"({100.0 * AbStrategy.FlatRollouts / Math.Max(1, AbStrategy.Choices):F1}%) "
				+ "— the region card values exist to decide"
		);

		Assert.That(
			AbStrategy.Choices,
			Is.GreaterThan(0),
			"no choices at all means this measured nothing"
		);
	}

	[Test]
	[Explicit("Plays 224 real games")]
	public void HowExpensiveIsChoiceResolution()
	{
		CensusStrategy.Reset();

		var arm = new StrengthHarness.Arm(
			"census",
			(ids, rng) => new CensusStrategy(new MultiTurnBeamSearchAiStrategy(ids, 3, rng: rng))
		);

		// Mirror match: both seats instrumented, so the census covers every decision in the run.
		// The win rate is meaningless here by construction and is not reported.
		var outcome = StrengthHarness.Measure(arm, arm, drafts: Drafts);

		var rows = CensusStrategy.Choices.ToList();
		var choiceTicks = rows.Sum(r => r.Ticks);
		var selectTicks = CensusStrategy.SelectTicks;
		var totalTicks = Math.Max(1, choiceTicks + selectTicks);
		double Seconds(long ticks) => (double)ticks / Stopwatch.Frequency;

		TestContext.Out.WriteLine(
			$"=== {outcome.Games} games, {outcome.Seconds:F0}s wall, "
				+ $"{outcome.AvgActions:F1} actions/game ==="
		);
		TestContext.Out.WriteLine(
			$"  SelectAction   {CensusStrategy.SelectCalls, 7} calls  "
				+ $"{Seconds(selectTicks), 8:F1}s thread  {100.0 * selectTicks / totalTicks, 5:F1}%"
		);
		TestContext.Out.WriteLine(
			$"  ResolveChoice  {rows.Count, 7} calls  "
				+ $"{Seconds(choiceTicks), 8:F1}s thread  {100.0 * choiceTicks / totalTicks, 5:F1}%"
		);

		if (rows.Count == 0)
		{
			TestContext.Out.WriteLine(
				"\nNo choices were raised at all. That is the answer: pruning the option list "
					+ "would save nothing on this card pool."
			);
			return;
		}

		TestContext.Out.WriteLine(
			$"\n  choices per game   {(double)rows.Count / outcome.Games:F2}"
		);
		TestContext.Out.WriteLine(
			$"  ms per choice      mean {1000 * Seconds(choiceTicks) / rows.Count:F1}, "
				+ $"max {1000 * Seconds(rows.Max(r => r.Ticks)):F1}"
		);

		// The option count is what a prune actually cuts, so its TAIL is the prize — the mean is
		// not, because cost is per option and the expensive calls are the wide ones.
		var opts = rows.Select(r => r.Options).OrderBy(n => n).ToList();
		int P(double q) => opts[Math.Min(opts.Count - 1, (int)(q * opts.Count))];
		TestContext.Out.WriteLine(
			$"  options per choice p50 {P(0.5)}, p90 {P(0.9)}, p99 {P(0.99)}, max {opts[^1]}"
		);

		// MinChoices > 1 pays a rollout per COMBINATION — C(n,k), not n — so this population is
		// where a prune compounds rather than merely helping.
		var combo = rows.Where(r => r.MinChoices > 1).ToList();
		TestContext.Out.WriteLine(
			$"  MinChoices > 1     {combo.Count} calls ({100.0 * combo.Count / rows.Count:F1}%), "
				+ $"{Seconds(combo.Sum(r => r.Ticks)):F1}s thread"
		);

		TestContext.Out.WriteLine("\n--- most expensive prompts (total thread time) ---");
		foreach (
			var g in rows.GroupBy(r => r.Prompt)
				.Select(g => new
				{
					Prompt = g.Key,
					Calls = g.Count(),
					Ticks = g.Sum(r => r.Ticks),
					MaxOptions = g.Max(r => r.Options),
				})
				.OrderByDescending(g => g.Ticks)
				.Take(15)
		)
			TestContext.Out.WriteLine(
				$"  {Seconds(g.Ticks), 7:F1}s  {g.Calls, 5} calls  "
					+ $"max {g.MaxOptions, 3} opts   {g.Prompt}"
			);

		// No assertion on the share. This is a measurement, and a threshold here would either be
		// unfalsifiable or would fail the day the card pool changes — the report IS the result.
		Assert.That(rows, Is.Not.Empty);
	}
}
