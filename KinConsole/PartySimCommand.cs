using System.Diagnostics;
using KinCore.Party;

namespace KinConsole;

/// <summary>
/// **`party-sim N` — THE COMPANION GAME, played by `PartyBot`.** Reported PER REGION, never one
/// number across both: an average across regions hides a wall in one of them
/// (docs/findings/doom-balance.md).
/// </summary>
public static class PartySimCommand
{
	public static void Execute(string[] args)
	{
		// `party-sim trace 7` — one run, seed 7, every choice and every turn printed, single-threaded.
		if (args.Length > 1 && args[1] == "trace")
		{
			var seed = args.Length > 2 && int.TryParse(args[2], out var sd) ? sd : 1;
			var starter = PartyContent.Roster[(seed - 1) % PartyContent.Roster.Count];
			var clock1 = Stopwatch.StartNew();
			var one = PartySim.PlayRun(starter, seed, Console.WriteLine);
			Console.WriteLine(
				$"  {one.End} in region {one.Region + 1}, {one.Battles} battles, {one.Turns} turns — "
					+ $"{PartyBot.Simulations} engine passes in {clock1.ElapsedMilliseconds}ms on one thread"
			);
			return;
		}

		var count = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 60;
		Console.WriteLine(
			$"  Simulating {count} companion runs, seeds 1-{count}, starters in turn..."
		);

		var clock = Stopwatch.StartNew();
		var runs = PartySim.PlayMany(count);
		clock.Stop();
		Console.WriteLine(
			$"  {clock.Elapsed.TotalSeconds:F1}s  ({clock.ElapsedMilliseconds / (double)count:F0}ms a run, "
				+ $"{PartyBot.Simulations / count} engine passes a run, {Environment.ProcessorCount} threads)"
		);

		Console.WriteLine();
		Console.WriteLine($"  RUNS WON: {Pct(runs.Count(r => r.End == RunEnd.Won), count)}");
		foreach (var starter in runs.GroupBy(r => r.Starter))
			Console.WriteLine(
				$"    {starter.Key, -8} {Pct(starter.Count(r => r.End == RunEnd.Won), starter.Count())}"
			);

		// One row a region: how many got THROUGH it against the curve, how it went there, and the gym.
		// One row a region: how many got THROUGH it against the curve, how it went there, and the gym.
		var regions = PartyWorld.Regions;
		Console.WriteLine();
		Console.WriteLine(
			"  REGION               THROUGH (target)   survived (target)  died trail/deep/gym  "
				+ "at gym: team  size  deeper  turns  lv (leader)"
		);
		var previous = 1.0;
		for (var region = 0; region < regions.Count; region++)
		{
			var reached = runs.Where(r => r.Region >= region).ToList();
			var died = reached.Where(r => r.Region == region && r.End != RunEnd.Won).ToList();
			var through = (reached.Count - died.Count) / (double)count;
			var target = PartySim.Target[region];
			var survived =
				reached.Count == 0 ? 0 : (reached.Count - died.Count) / (double)reached.Count;
			var gyms = runs.SelectMany(r => r.Gyms).Where(g => g.Region == region).ToList();

			string Died(RunEnd end) => died.Count(r => r.End == end).ToString();
			Console.WriteLine(
				$"  {region + 1, 2} {regions[region].Name, -17} "
					+ $"{through, 6:P0} ({target, 4:P0})   {survived, 6:P0} ({target / previous, 4:P0})   "
					+ $"{Died(RunEnd.Trail), 4}/{Died(RunEnd.Deep), -3}/{Died(RunEnd.Gym), -3}"
					+ (
						gyms.Count == 0
							? ""
							: $"{"", 8}{gyms.Average(g => g.TeamHpShare), 4:P0}  "
								+ $"{gyms.Average(g => g.TeamSize), 4:F1}  {gyms.Count(g => g.WentDeep) / (double)gyms.Count, 5:P0}  "
								+ $"{gyms.Average(g => g.Turns), 5:F1}"
								+ $"  {gyms.Average(g => g.TeamLevel), 4:F1} ({regions[region].LeaderLevel})"
					)
			);
			previous = target;
		}
		var stalled = runs.Count(r => r.End == RunEnd.Stalled);
		if (stalled > 0)
			Console.WriteLine(
				$"  {stalled} runs STALLED — a battle ran past {PartySim.TurnLimit} turns."
			);

		Console.WriteLine();
		Console.WriteLine(
			$"  Per run: {runs.Average(r => r.Caught):F1} caught, {runs.Average(r => r.Battles):F1} battles, "
				+ $"{runs.Sum(r => r.Turns) / (double)Math.Max(1, runs.Sum(r => r.Battles)):F1} turns a battle"
		);
		Console.WriteLine(
			"  The bot searches a turn's plays and looks one turn on; a person plans further. Read it for WHERE runs die."
		);
	}

	private static string Pct(int part, int whole) =>
		whole == 0 ? "—" : $"{part * 100.0 / whole, 5:F1}%  ({part}/{whole})";
}
