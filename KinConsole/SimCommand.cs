using System.Diagnostics;
using System.Text.Json;
using KinCore;

namespace KinConsole;

/// <summary>
/// `dotnet run --project KinConsole -- sim [count]` — plays N runs with <see cref="KinBot"/> and
/// prints what they say about the balance.
///
/// **Read the tables, never the memory of them.** Every run is written to `doom_sim_results/`; the
/// numbers here go stale the moment content changes, and a quoted win rate from an old build is
/// worse than no number at all.
/// </summary>
public static class SimCommand
{
	public static void Execute(string[] args)
	{
		var count = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 200;

		// `companion=Pike` picks who plays. Taken out BEFORE `Tune`, which reads every key=value
		// as a bot weight and would report "no such weight: companion".
		var companion = CompanionFrom(args);
		var weights = Tune(
			new KinEvalWeights(),
			[.. args.Where(a => !a.StartsWith("companion=", StringComparison.OrdinalIgnoreCase))]
		);

		Console.WriteLine(
			$"  Simulating {count} runs, seeds 1-{count}, {weights.Version}, {companion.Name}..."
		);

		var clock = Stopwatch.StartNew();
		var results = RunSimulator.PlayMany(count, weights, companion);
		clock.Stop();

		Console.WriteLine(
			$"  {clock.Elapsed.TotalSeconds:F1}s  ({clock.ElapsedMilliseconds / (double)count:F0}ms a run)"
		);

		var path = Write(results, weights);
		Themes(results);
		SurvivalCurve(results);
		Pressure(results);
		CardValue(results);

		Console.WriteLine();
		Console.WriteLine($"  Full results: {path}");
		Console.WriteLine();
	}

	/// <summary>
	/// Per theme, since a theme is a different act rather than a different coat of paint. **This is
	/// the table that says whether the three are balanced against each other**, which the aggregate
	/// numbers below will happily hide.
	/// </summary>
	private static void Themes(RunResult[] results)
	{
		Console.WriteLine();
		Console.WriteLine("  THEMES");
		Console.WriteLine("  theme                 runs   completed   mean floor   life/battle");

		foreach (var group in results.GroupBy(r => r.Theme).OrderBy(g => g.Key))
		{
			var runs = group.ToList();
			var floors = runs.SelectMany(r => r.Floors).ToList();
			var complete = runs.Count(r => r.ActComplete);

			Console.WriteLine(
				$"  {group.Key, -18} {runs.Count, 6}   {100.0 * complete / runs.Count, 8:F1}%   "
					+ $"{runs.Average(r => r.FloorReached), 10:F2}   "
					+ $"{floors.Average(f => f.LifeLost), 11:F1}"
			);
		}
	}

	/// <summary>
	/// How far runs get, and what stopped them. **The headline number**: a floor where the reach
	/// column collapses is where the curve is wrong, and that reading survives a mediocre bot in a
	/// way an absolute win rate does not.
	/// </summary>
	private static void SurvivalCurve(RunResult[] results)
	{
		Console.WriteLine();
		Console.WriteLine("  SURVIVAL — how far the bot gets");
		Console.WriteLine("  floor  reached   cleared   died  stalled   avg life on entry");

		// **RunLength, not ActLength.** A run is all three acts now, and this loop quietly kept
		// printing only the first fifteen floors — so the curve past act 1 was invisible while
		// looking like a complete table. Act 2's boss was being tuned blind.
		for (var floor = 1; floor <= Run.RunLength; floor++)
		{
			var attempts = results.SelectMany(r => r.Floors).Where(f => f.Floor == floor).ToList();

			if (attempts.Count == 0)
				continue;

			var cleared = attempts.Count(f => f.Outcome == "Cleared");
			var died = attempts.Count(f => f.Outcome == "Died");
			var stalled = attempts.Count(f => f.Outcome == "Stalled");

			Console.WriteLine(
				$"  {floor, 5}  {attempts.Count, 7}   {cleared, 7}   {died, 4}  {stalled, 7}   {attempts.Average(f => f.LifeBefore), 17:F1}"
			);
		}

		var complete = results.Count(r => r.ActComplete);
		Console.WriteLine();
		Console.WriteLine(
			$"  Act completed: {complete}/{results.Length} ({100.0 * complete / results.Length:F1}%)   "
				+ $"mean floor reached {results.Average(r => r.FloorReached):F2}"
		);

		foreach (var group in results.GroupBy(r => r.EndReason).OrderByDescending(g => g.Count()))
			Console.WriteLine($"    {group.Count(), 5}  {group.Key}");
	}

	/// <summary>Turn length and the rate life actually drains — the pressure the handoff flagged.</summary>
	private static void Pressure(RunResult[] results)
	{
		var floors = results.SelectMany(r => r.Floors).ToList();

		Console.WriteLine();
		Console.WriteLine("  PRESSURE");
		Console.WriteLine($"    turns per battle      {floors.Average(f => f.Turns):F1}");
		Console.WriteLine($"    life lost per battle  {floors.Average(f => f.LifeLost):F1}");
		Console.WriteLine(
			$"    deck size at the end  {results.Average(r => r.FinalDeck.Count):F1}"
		);
	}

	/// <summary>
	/// Mean floor reached by runs that took a card, against runs that did not.
	///
	/// **Unbiased only because the picker is random** — see <see cref="RunSimulator"/>. Starter
	/// cards never appear here: every run holds them, so there is no "without" group to compare to.
	/// Treat a delta with a small n as noise; this is the table that needs the most runs.
	/// </summary>
	private static void CardValue(RunResult[] results)
	{
		Console.WriteLine();
		Console.WriteLine("  CARD VALUE — mean floor reached, took it vs did not");
		Console.WriteLine("  card                taken    with   without    delta");

		var names = results.SelectMany(r => r.TakenRewards).Distinct().OrderBy(n => n);

		foreach (var name in names)
		{
			var with = results.Where(r => r.TakenRewards.Contains(name)).ToList();
			var without = results.Where(r => !r.TakenRewards.Contains(name)).ToList();

			if (with.Count == 0 || without.Count == 0)
				continue;

			var delta = with.Average(r => r.FloorReached) - without.Average(r => r.FloorReached);

			Console.WriteLine(
				$"  {name, -18} {with.Count, 5}  {with.Average(r => r.FloorReached), 6:F2}   "
					+ $"{without.Average(r => r.FloorReached), 6:F2}   {delta, +6:F2}"
			);
		}
	}

	/// <summary>
	/// `sim 200 Life=4 OpponentHealth=2` — overrides any eval weight by name.
	///
	/// Sweeping a weight is how you find out whether the bot is anywhere near its own ceiling, and
	/// that question comes back every time the eval or the content changes. Reflection rather than
	/// a flag per weight, so a new weight is sweepable the moment it exists.
	/// </summary>
	/// <summary>
	/// The companion named by `companion=`, or the starter. **An unknown name is an error, not a
	/// silent default** — a typo that quietly measured Ash would be read as a Pike number.
	/// </summary>
	private static Companion CompanionFrom(string[] args)
	{
		var named = args.FirstOrDefault(a =>
			a.StartsWith("companion=", StringComparison.OrdinalIgnoreCase)
		);
		if (named is null)
			return StarterContent.StarterCompanion;

		var name = named["companion=".Length..];
		return StarterContent.Roster.FirstOrDefault(c =>
				c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
			)
			?? throw new ArgumentException(
				$"No companion called '{name}'. The roster is: "
					+ string.Join(", ", StarterContent.Roster.Select(c => c.Name))
			);
	}

	private static KinEvalWeights Tune(KinEvalWeights weights, string[] args)
	{
		foreach (var arg in args.Where(a => a.Contains('=')))
		{
			var parts = arg.Split('=', 2);
			var property = typeof(KinEvalWeights).GetProperty(
				parts[0],
				System.Reflection.BindingFlags.Public
					| System.Reflection.BindingFlags.Instance
					| System.Reflection.BindingFlags.IgnoreCase
			);

			if (property is null)
			{
				Console.WriteLine($"  no such weight: {parts[0]}");
				continue;
			}

			property.SetValue(weights, Convert.ChangeType(parts[1], property.PropertyType));

			// The version stamp travels with the numbers, so a swept file can never be mistaken for
			// a default-weights one when it is read back months later.
			weights = weights with
			{
				Version = $"{weights.Version} {arg}",
			};
		}

		return weights;
	}

	private static string Write(RunResult[] results, KinEvalWeights weights)
	{
		var dir = Path.Combine(
			AppContext.BaseDirectory,
			"..",
			"..",
			"..",
			"..",
			"doom_sim_results"
		);
		Directory.CreateDirectory(dir);

		var path = Path.GetFullPath(Path.Combine(dir, $"sim-{DateTime.Now:yyyyMMdd-HHmmss}.json"));

		File.WriteAllText(
			path,
			JsonSerializer.Serialize(
				new
				{
					Recorded = DateTime.Now,
					Weights = weights,
					Runs = results,
				},
				new JsonSerializerOptions { WriteIndented = true }
			)
		);

		return path;
	}
}
