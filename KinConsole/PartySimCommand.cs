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

		var regions = PartyWorld.Regions;
		for (var region = 0; region < regions.Count; region++)
		{
			var reached = runs.Where(r => r.Region > region || r.Region == region).ToList();
			var died = reached.Where(r => r.Region == region && r.End != RunEnd.Won).ToList();

			Console.WriteLine();
			Console.WriteLine(
				$"  REGION {region + 1} — {regions[region].Name}: {reached.Count} runs reached it"
			);
			if (reached.Count == 0)
				continue;
			Console.WriteLine(
				$"    survived it      {Pct(reached.Count - died.Count, reached.Count)}"
			);
			foreach (var end in new[] { RunEnd.Trail, RunEnd.Deep, RunEnd.Gym, RunEnd.Stalled })
			{
				var here = died.Where(r => r.End == end).ToList();
				if (here.Count > 0)
					Console.WriteLine(
						$"    died: {end, -9} {Pct(here.Count, reached.Count)}"
							+ $"   (your health ran out in {here.Count(r => r.KilledByTrainerHp)} of {here.Count})"
					);
			}

			var gyms = runs.SelectMany(r => r.Gyms).Where(g => g.Region == region).ToList();
			if (gyms.Count > 0)
				Console.WriteLine(
					$"    at the gym: your health {gyms.Average(g => g.TrainerHp):F1}/{PartyRun.TrainerMaxHp}"
						+ $", team HP {gyms.Average(g => g.TeamHpShare):P0}, team size {gyms.Average(g => g.TeamSize):F1}"
						+ $", went deeper {Pct(gyms.Count(g => g.WentDeep), gyms.Count)}"
				);
			if (gyms.Count > 0)
				Console.WriteLine(
					$"    the gym fell to its LEADER's health in {Pct(gyms.Count(g => g.ByLeader), gyms.Count)}"
						+ $", in {gyms.Average(g => g.Turns):F1} turns"
				);
		}

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
