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

		// `party-sim cards 300` — WHICH cards and monsters carry a family: among runs that reached
		// region 2, the win rate of those holding each (at region 2's start) against those without.
		// The bot's picks are the first offered, so what a run holds is near-random.
		if (args.Length > 1 && args[1] == "cards")
		{
			var total = args.Length > 2 && int.TryParse(args[2], out var c) ? c : 300;
			var played = PartySim.PlayMany(total);
			foreach (var family in played.GroupBy(r => r.Starter))
			{
				var reached = family.Where(r => r.Region2Deck is not null).ToList();
				double Rate(IEnumerable<SimRun> rs) =>
					rs.Any() ? rs.Count(r => r.End == RunEnd.Won) / (double)rs.Count() : 0;
				Console.WriteLine();
				Console.WriteLine(
					$"  {family.Key}: {reached.Count} of {family.Count()} reached region 2, "
						+ $"{Rate(reached):P0} of them won"
				);
				var names = reached
					.SelectMany(r => r.Region2Deck!.Distinct().Select(n => ("card", n)))
					.Concat(
						reached.SelectMany(r => r.Region2Team!.Skip(1).Select(n => ("monster", n)))
					)
					.Distinct();
				foreach (
					var (kind, name, with, without) in names
						.Select(k =>
						{
							var has = reached
								.Where(r =>
									(k.Item1 == "card" ? r.Region2Deck! : r.Region2Team!).Contains(
										k.Item2
									)
								)
								.ToList();
							return (k.Item1, k.Item2, has, reached.Except(has).ToList());
						})
						.Where(x => x.has.Count >= 8 && x.Item4.Count >= 8)
						.OrderByDescending(x => Rate(x.has) - Rate(x.Item4))
				)
					Console.WriteLine(
						$"    {kind, -7} {name, -18} {Rate(with) - Rate(without), 6:+0%;-0%}   "
							+ $"with {Rate(with):P0} (n={with.Count, 3})   without {Rate(without):P0}"
					);
			}
			return;
		}

		// `party-sim variants 150` — is it the STARTER or its FAMILY? Each starter, then with one thing
		// swapped: its HP, its family (deck, rewards, boss picks), its passive. Same seeds for every row.
		if (args.Length > 1 && args[1] == "variants")
		{
			var each = args.Length > 2 && int.TryParse(args[2], out var v) ? v : 150;
			var pike = PartyContent.Pike;
			var bramble = PartyContent.Bramble;
			// Starting decks swapped: is it the family's first two cards, or everything after?
			Func<PartyRun, PartyRun> Starts(Family family) =>
				run => run with { Deck = PartyContent.StartingDeck(family) };
			foreach (
				var (label, starter, setup) in new (
					string,
					PartyCompanion,
					Func<PartyRun, PartyRun>?
				)[]
				{
					("Pike, basics only", pike, Starts(Family.None)),
					("Pike, starts Root+Sow", pike, Starts(Family.Grove)),
					("Bramble, basics only", bramble, Starts(Family.None)),
					("Bramble, starts Zap+Kindle", bramble, Starts(Family.Ember)),
				}
			)
			{
				var played = PartySim.PlayMany(each, [starter], setup);
				Console.WriteLine(
					$"  {label, -26} won {Pct(played.Count(r => r.End == RunEnd.Won), each)}"
						+ $"   died in region 1: {played.Count(r => r.Region == 0 && r.End != RunEnd.Won), 3}"
				);
			}
			foreach (
				var (label, starter) in new (string, PartyCompanion)[]
				{
					("Pike", pike),
					($"Pike, {bramble.Hp} HP", pike with { Hp = bramble.Hp }),
					("Pike, GROVE family", pike with { Family = Family.Grove }),
					("Bramble", bramble),
					("Bramble, EMBER family", bramble with { Family = Family.Ember }),
					("Bramble, no passive", bramble with { Thorns = 0, Abilities = [] }),
					($"Bramble, {pike.Hp} HP", bramble with { Hp = pike.Hp }),
				}
			)
			{
				var played = PartySim.PlayMany(each, [starter]);
				var region1 = played.Count(r => r.Region == 0 && r.End != RunEnd.Won);
				Console.WriteLine(
					$"  {label, -24} won {Pct(played.Count(r => r.End == RunEnd.Won), each)}"
						+ $"   died in region 1: {region1, 3}"
				);
			}
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

		// One row a region: how many got THROUGH it against the curve, how they died, and the boss.
		var regions = PartyWorld.Regions;
		Console.WriteLine();
		Console.WriteLine(
			"  REGION               THROUGH (target)   survived (target)  died trail/elite/boss   "
				+ "at boss: team HP  size  turns"
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
					+ $"{Died(RunEnd.Trail), 4}/{Died(RunEnd.Elite)}/{Died(RunEnd.Boss), -6}"
					+ (
						gyms.Count == 0
							? ""
							: $"{"", 12}{gyms.Average(g => g.TeamHpShare), 4:P0}  "
								+ $"{gyms.Average(g => g.TeamSize), 4:F1}  {gyms.Average(g => g.Turns), 5:F1}"
					)
			);
			previous = target;
		}

		// Where each STARTER's runs die, region by region — one starter's wall hides in the total.
		Console.WriteLine();
		foreach (var starter in runs.GroupBy(r => r.Starter))
			Console.WriteLine(
				$"  {starter.Key, -8} died in region: "
					+ string.Join(
						"  ",
						Enumerable
							.Range(0, regions.Count)
							.Select(region =>
							{
								var died = starter
									.Where(r => r.Region == region && r.End != RunEnd.Won)
									.ToList();
								return $"{region + 1}: {died.Count, 2} "
									+ $"(elite {died.Count(r => r.End == RunEnd.Elite)}, boss {died.Count(r => r.End == RunEnd.Boss)})";
							})
					)
			);

		// What a WILD fight costs, per region — the design wants a little chip (5–15% of the team's HP).
		Console.WriteLine(
			"  wild fight chip:  "
				+ string.Join(
					"  ",
					Enumerable
						.Range(0, regions.Count)
						.Select(region =>
						{
							var chips = runs.SelectMany(r => r.Chips)
								.Where(c => c.Region == region)
								.ToList();
							return $"{region + 1}: {(chips.Count == 0 ? 0 : chips.Average(c => c.Lost)), 4:P0}";
						})
				)
		);

		var stalled = runs.Count(r => r.End == RunEnd.Stalled);
		if (stalled > 0)
			Console.WriteLine(
				$"  {stalled} runs STALLED — a battle ran past {PartySim.TurnLimit} turns."
			);

		Console.WriteLine();
		Console.WriteLine(
			$"  Per run: {runs.Average(r => r.Battles):F1} battles, "
				+ $"{runs.Sum(r => r.Turns) / (double)Math.Max(1, runs.Sum(r => r.Battles)):F1} turns a battle, "
				+ $"{runs.Average(r => r.Elites):F1} elites fought ({Pct(runs.Sum(r => r.ElitesWon), runs.Sum(r => r.Elites))} won)"
		);
		Console.WriteLine(
			"  The bot searches a turn's plays and looks one turn on; a person plans further. Read it for WHERE runs die."
		);
	}

	private static string Pct(int part, int whole) =>
		whole == 0 ? "—" : $"{part * 100.0 / whole, 5:F1}%  ({part}/{whole})";
}
