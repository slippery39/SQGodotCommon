using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>Where a run ended, and how.</summary>
public enum RunEnd
{
	Won,

	/// <summary>Lost in a fight on a route (named for the trail the route replaced).</summary>
	Trail,

	/// <summary>Lost at a route's rare lair (the old deeper path).</summary>
	Deep,

	/// <summary>Lost to an ELITE on a route.</summary>
	Elite,

	/// <summary>Lost to a region's boss.</summary>
	Boss,

	/// <summary>A battle that ran past <see cref="PartySim.TurnLimit"/> turns — the bot could not finish it.</summary>
	Stalled,
}

/// <summary>A team arriving at a gym: what it brought.</summary>
public record GymArrival(int Region, double TeamHpShare, int TeamSize, bool WentDeep)
{
	/// <summary>How many turns the gym took.</summary>
	public int Turns { get; init; }

	/// <summary>The team's average level on arrival, against the leader's (`PartyLevels`).</summary>
	public double TeamLevel { get; init; }
}

/// <summary>One simulated run.</summary>
public record SimRun(
	string Starter,
	int Seed,
	RunEnd End,
	int Region,
	ImmutableList<GymArrival> Gyms,
	int Caught,
	int Battles,
	int Turns
)
{
	/// <summary>Elites fought, and won.</summary>
	public int Elites { get; init; }

	public int ElitesWon { get; init; }

	/// <summary>Each WILD fight's cost: the share of the team's HP it took, by region.</summary>
	public ImmutableList<WildChip> Chips { get; init; } = [];
}

/// <summary>What one wild fight took out of the team: the share of its HP, from the line that fought.</summary>
public record WildChip(int Region, double Lost);

/// <summary>
/// **`party-sim`: whole runs played by <see cref="PartyBot"/>** under simple map rules — Snares when
/// low, a card when there is gold, an area at random, the deeper path only when healthy. Seeded,
/// deterministic, parallel. For a quick read on where runs die; the bot plays one move ahead, so
/// its numbers are a floor.
/// </summary>
public static class PartySim
{
	public const int TurnLimit = 40;

	/// <summary>
	/// **THE CURVE (Shayne, 2026-09-24), for the BOT as a baseline**: the share of runs that should get
	/// THROUGH each region — 90% through region 3, 75% through region 5, 25% win all ten. Between those
	/// points the per-region survival is even (geometric). Players are compared to the bot later.
	/// </summary>
	public static readonly ImmutableList<double> Target = Curve(
		[(0, 1.0), (3, 0.90), (5, 0.75), (10, 0.25)]
	);

	private static ImmutableList<double> Curve((int Region, double Through)[] anchors)
	{
		var curve = ImmutableList.CreateBuilder<double>();
		for (var a = 1; a < anchors.Length; a++)
		{
			var (from, start) = anchors[a - 1];
			var (to, end) = anchors[a];
			var step = Math.Pow(end / start, 1.0 / (to - from));
			for (var r = from + 1; r <= to; r++)
				curve.Add(start * Math.Pow(step, r - from));
		}
		return curve.ToImmutable();
	}

	/// <summary>Deeper only with this much of your health and the team's HP left.</summary>
	public const double DeepIfHealthy = 0.6;

	/// <summary>
	/// **And only with this many monsters** — the first sim's bot took the deeper path with Pike alone,
	/// against three foes, and your health drained 13, 9, 5, 3. A person would not.
	/// </summary>
	public const int DeepWithAtLeast = 2;

	public static SimRun[] PlayMany(int count, ImmutableList<PartyCompanion>? starters = null)
	{
		var roster = starters ?? PartyContent.Roster;
		var results = new SimRun[count];
		Parallel.For(0, count, i => results[i] = PlayRun(roster[i % roster.Count], seed: i + 1));
		return results;
	}

	public static SimRun PlayRun(PartyCompanion starter, int seed, Action<string>? log = null)
	{
		var rng = new Random(seed);
		var run = PartyRun.Start(starter, seed);
		var gyms = ImmutableList<GymArrival>.Empty;
		var caught = 0;
		var battles = 0;
		var turns = 0;
		var wentDeep = false;
		var elites = 0;
		var elitesWon = 0;
		var chips = ImmutableList<WildChip>.Empty;

		while (!run.IsOver)
		{
			switch (run.Phase)
			{
				case RunPhase.Town:
					// Heal only when it matters: under the deeper-path threshold, and it can pay.
					if (!Healthy(run) && run.CannotHeal is null)
						run = run.HealAtHospital();
					while (run.Snares < 2 && run.CanBuySnare)
						run = run.BuySnare();
					if (run.CanBuyCard(0))
						run = run.BuyCard(0);
					run = run.EnterRoute();
					wentDeep = false;
					break;

				case RunPhase.Route when run.HereIsCleared:
					run = run.MoveTo(Walk(run, rng));
					break;

				default:
					if (run.AtBoss)
						gyms = gyms.Add(
							new GymArrival(
								run.RegionIndex,
								TeamShare(run),
								run.Team.Count,
								wentDeep
							)
							{
								TeamLevel = run.Team.Average(m => m.Level),
							}
						);
					else if (run.Here.Kind == NodeKind.Rare)
						wentDeep = true;

					var where =
						run.AtBoss ? RunEnd.Boss
						: run.Here.Kind == NodeKind.Elite ? RunEnd.Elite
						: run.Here.Kind == NodeKind.Rare ? RunEnd.Deep
						: RunEnd.Trail;
					if (where == RunEnd.Elite)
						elites++;

					var battle = run.StartBattle();
					var hpBefore = battle.Allies().Where(a => a.FadesIn == 0).Sum(a => a.Hp);
					var hpMax = battle.Allies().Where(a => a.FadesIn == 0).Sum(a => a.MaxHp);
					var wild = run.Here.Kind is NodeKind.Wild or NodeKind.Grass;
					log?.Invoke(
						$"  BATTLE {battles + 1} ({where}): {run.NextFight.Name} — team "
							+ string.Join(", ", run.Team.Select(m => $"{m.Companion.Name} {m.Hp}"))
					);
					var t = 0;
					while (!battle.GetParty().IsOver && t < TurnLimit)
					{
						battle = PartyBot.PlayTurn(battle, log);
						log?.Invoke(
							$"   turn {t + 1} ends: "
								+ string.Join(", ", battle.Allies().Select(a => $"{a.Name} {a.Hp}"))
								+ $" | foes "
								+ string.Join(
									", ",
									battle.LivingFoes().Select(f => $"{f.Name} {f.Hp}")
								)
						);
						t++;
					}
					battles++;
					turns += t;

					if (!battle.GetParty().IsOver)
						return new(
							starter.Name,
							seed,
							RunEnd.Stalled,
							run.RegionIndex,
							gyms,
							caught,
							battles,
							turns
						);

					var party = battle.GetParty();
					caught += battle.CaughtFoes().Count();
					if (where == RunEnd.Boss)
						gyms = gyms.SetItem(gyms.Count - 1, gyms[^1] with { Turns = t });
					var region = run.RegionIndex;
					(run, _) = run.AfterBattle(battle);
					log?.Invoke(
						$"   => {(party.Won ? "WON" : "LOST")}, caught {battle.CaughtFoes().Count()}; next: {run.Phase}"
					);

					if (wild && run.Phase != RunPhase.Lost)
						chips = chips.Add(
							new WildChip(
								region,
								(
									hpBefore
									- battle
										.Allies()
										.Where(a => a.FadesIn == 0)
										.Sum(a => Math.Max(0, a.Hp))
								) / (double)Math.Max(1, hpMax)
							)
						);
					if (run.Phase == RunPhase.Lost)
						return new(starter.Name, seed, where, region, gyms, caught, battles, turns)
						{
							Elites = elites,
							ElitesWon = elitesWon,
							Chips = chips,
						};
					if (where == RunEnd.Elite)
						elitesWon++;

					if (!run.IsOver && run.RewardOffer() is [var card, ..])
						run = run.Take(card);
					break;
			}
		}

		return new(starter.Name, seed, RunEnd.Won, run.RegionIndex, gyms, caught, battles, turns)
		{
			Elites = elites,
			ElitesWon = elitesWon,
			Chips = chips,
		};
	}

	/// <summary>
	/// **Where the bot walks next on a route**: hurt, a spring or a find; healthy, the rare's lair
	/// first, then any fight. Ties broken by the seed.
	/// </summary>
	private static int Walk(PartyRun run, Random rng)
	{
		var healthy = Healthy(run);
		int Want(NodeKind kind) =>
			kind switch
			{
				NodeKind.Rest => healthy ? 1 : 6,
				NodeKind.Find => healthy ? 2 : 5,
				NodeKind.Rare => healthy ? 6 : 0,
				NodeKind.Wild => healthy ? 5 : 3,
				NodeKind.Grass => healthy ? 4 : 2,
				NodeKind.Trainer => healthy ? 3 : 1,
				// An elite only when healthy — and then before a wild fight: it pays a relic.
				NodeKind.Elite => healthy ? 5 : 0,
				_ => 4,
			};
		return run.Route!.Next(run.NodeId)
			.OrderByDescending(n => Want(n.Kind))
			.ThenBy(_ => rng.Next())
			.First()
			.Id;
	}

	private static bool Healthy(PartyRun run) =>
		run.Team.Count >= DeepWithAtLeast && TeamShare(run) >= DeepIfHealthy;

	private static double TeamShare(PartyRun run) =>
		run.Team.Sum(m => m.Hp) / (double)run.Team.Sum(m => m.MaxHp);
}
