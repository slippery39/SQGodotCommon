using MtgCore;

namespace MtgSimulator.Scenarios;

/// <summary>
/// Console mode 5 — the standalone scenario viewer.
///
/// The in-game overlay shows one strategy deciding live; this shows several deciding the SAME
/// saved position. That is the only way to tell whether a change helped, rather than whether the
/// board happened to differ, and it is the half the overlay cannot do.
///
/// Lives in the library rather than in Program.cs so a Godot front end can call the same code.
/// </summary>
public static class ScenarioConsole
{
	/// <summary>
	/// The strategies offered for comparison.
	///
	/// This list is the seam <c>EvaluatorStrengthTests</c> needs too — "construct these AIs by
	/// name and run them" is the same requirement for a one-position diff and for a 1120-game
	/// head-to-head. Keep it the single place strategies are named.
	/// </summary>
	public static IReadOnlyList<(
		string Name,
		Func<MtgGameIds, Random, IAiStrategy> Build
	)> Strategies { get; } =
		[
			(
				"MultiTurnBeam",
				(ids, rng) =>
					new MultiTurnBeamSearchAiStrategy(
						ids,
						rng: rng,
						captureDecisions: true,
						cardValues: AiCardValues.Current
					)
			),
			("Beam", (ids, rng) => new BeamSearchAiStrategy(ids, rng: rng, captureDecisions: true)),
			("DepthLimited", (ids, rng) => new DepthLimitedAiStrategy(ids)),
			("Random", (ids, rng) => new RandomAiStrategy(rng)),
		];

	public static void Run()
	{
		var files = ScenarioStore.List();
		if (files.Count == 0)
		{
			Console.WriteLine(
				$"No scenarios found in {ScenarioStore.DefaultDirectory}/.\n"
					+ "Save one with F7 in the Godot game, or point the working directory at the\n"
					+ "repo root — the folder is relative to the shell's cwd, not the project's."
			);
			return;
		}

		Console.WriteLine("Scenarios:");
		for (var i = 0; i < files.Count; i++)
			Console.WriteLine($"  {i + 1} - {Path.GetFileNameWithoutExtension(files[i])}");
		Console.Write($"Which? (default 1): ");
		var pick =
			int.TryParse(Console.ReadLine()?.Trim(), out var p) && p >= 1 && p <= files.Count
				? p - 1
				: 0;

		Scenario scenario;
		try
		{
			scenario = ScenarioStore.Load(files[pick]);
		}
		catch (Exception ex)
		{
			// A scenario saved by a build with a mechanic this one lacks fails here, and the
			// message from StateJson names the missing type. Worth surfacing rather than a stack.
			Console.WriteLine($"Could not load {files[pick]}:\n  {ex.Message}");
			return;
		}

		Console.WriteLine();
		Console.WriteLine("Strategies (blank = all):");
		for (var i = 0; i < Strategies.Count; i++)
			Console.WriteLine($"  {i + 1} - {Strategies[i].Name}");
		Console.Write("Comma-separated: ");
		var chosen = ParseSelection(Console.ReadLine(), Strategies.Count);

		var ids = ScenarioComparer.MtgGameIdsFor(scenario.State);
		var selected = chosen.Select(i => Strategies[i]).ToList();

		Console.WriteLine();
		Console.WriteLine(
			ScenarioComparer.Format(scenario, ScenarioComparer.Compare(scenario, selected, ids))
		);
	}

	private static IReadOnlyList<int> ParseSelection(string? input, int count)
	{
		if (string.IsNullOrWhiteSpace(input))
			return Enumerable.Range(0, count).ToList();

		var picked = input
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(s => int.TryParse(s, out var n) ? n - 1 : -1)
			.Where(i => i >= 0 && i < count)
			.Distinct()
			.ToList();

		return picked.Count > 0 ? picked : Enumerable.Range(0, count).ToList();
	}
}
