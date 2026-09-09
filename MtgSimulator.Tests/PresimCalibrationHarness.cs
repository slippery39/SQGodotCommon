using MtgCore;
using MtgSimulator;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// Runs an identity-scoped presimulation and reports the games-per-(card, identity) distribution.
///
/// This is the calibration input for <see cref="IdentityValues.IdentityShrinkK"/>. Explicit and
/// slow; the size knobs are environment variables so a run can be scaled without an edit.
/// </summary>
[TestFixture]
public class PresimCalibrationHarness
{
	private static int Env(string name, int fallback) =>
		int.TryParse(Environment.GetEnvironmentVariable(name), out var v) ? v : fallback;

	[Test, Explicit("Calibration run — minutes to hours depending on size.")]
	public void MeasureGamesPerIdentityCell()
	{
		var set = SetRegistry.Get(Environment.GetEnvironmentVariable("MTG_SET") ?? "CSC");
		var decks = Env("MTG_PRESIM_DECKS", 300);
		var opponents = Env("MTG_PRESIM_OPPONENTS", 12);

		var pool = set.Cards.Where(c => !c.HasSubtype("Land")).ToList();
		var result = PreSimulation.Run(
			pool,
			decks,
			opponents,
			seed: 20260909,
			aiDepth: 2,
			label: $"Calibration ({set.Code})",
			identities: ColorIdentity.Standard
		);

		var lines = new List<string> { $"set {set.Code}, {decks} decks, {opponents} opponents" };

		var cells = result
			.ByIdentity.SelectMany(kv => kv.Value.Cards.Select(c => (kv.Key, c.Name, c.Games)))
			.ToList();
		var games = cells.Select(c => c.Games).OrderBy(g => g).ToList();

		double Pct(double p) => games.Count == 0 ? 0 : games[(int)(games.Count * p)];

		lines.Add(
			$"POOLED: {result.Overall.Cards.Count} cards, median "
				+ $"{result.Overall.Cards.Select(c => c.Games).OrderBy(g => g).ElementAt(result.Overall.Cards.Count / 2)} games/card"
		);
		lines.Add(
			$"CELLS: {cells.Count} (card,identity) cells across {result.ByIdentity.Count} identities"
		);
		lines.Add(
			$"  p10 {Pct(0.10):F0}  p25 {Pct(0.25):F0}  median {Pct(0.50):F0}  "
				+ $"p75 {Pct(0.75):F0}  p90 {Pct(0.90):F0}  max {games.LastOrDefault()}"
		);

		// Exposure by card kind — the gold-card problem, measured rather than predicted.
		var byName = pool.ToDictionary(c => c.Name, StringComparer.Ordinal);
		string Kind(string name)
		{
			var pips = byName[name].ColorPips;
			if (pips.IsEmpty)
				return "colourless";
			return ManaPool.Colors.Count(c => pips[c] > 0) > 1 ? "gold" : "mono";
		}

		foreach (
			var group in cells
				.Where(c => byName.ContainsKey(c.Name))
				.GroupBy(c => Kind(c.Name))
				.OrderBy(g => g.Key)
		)
		{
			var total = group
				.GroupBy(g => g.Name)
				.Select(g => g.Sum(x => x.Games))
				.OrderBy(g => g)
				.ToList();
			lines.Add(
				$"  {group.Key, -11}: {total.Count} cards, {group.Count()} cells, "
					+ $"median {total[total.Count / 2]} total games/card, "
					+ $"median {group.Select(g => g.Games).OrderBy(g => g).ElementAt(group.Count() / 2)} per cell"
			);
		}

		foreach (var line in lines)
			TestContext.Out.WriteLine(line);
		File.WriteAllLines(
			"C:/Users/shayn/AppData/Local/Temp/claude/c--SQGodotHelperApps-SQGodotCommon/408328b0-5433-46a7-bd46-741f65818bcb/scratchpad/calib.txt",
			lines
		);
	}
}
