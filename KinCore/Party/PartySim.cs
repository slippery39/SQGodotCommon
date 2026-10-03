using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>Where a run ended, and how.</summary>
public enum RunEnd
{
	Won,

	/// <summary>Lost in a fight on a route (named for the trail the route replaced).</summary>
	Trail,

	/// <summary>Lost to an ELITE on a route.</summary>
	Elite,

	/// <summary>Lost to a region's boss.</summary>
	Boss,

	/// <summary>A battle that ran past <see cref="PartySim.TurnLimit"/> turns — the bot could not finish it.</summary>
	Stalled,
}

/// <summary>A team arriving at a boss: what it brought.</summary>
public record GymArrival(int Region, double TeamHpShare, int TeamSize)
{
	/// <summary>How many turns the boss took.</summary>
	public int Turns { get; init; }
}

/// <summary>One simulated run.</summary>
public record SimRun(
	string Starter,
	int Seed,
	RunEnd End,
	int Region,
	ImmutableList<GymArrival> Gyms,
	int Battles,
	int Turns
)
{
	/// <summary>
	/// **The deck and team as region 2 began** — null if the run fell in region 1. The bot takes the
	/// first card, relic and monster offered, so what a run holds is near-random: comparing the runs
	/// with a card to those without measures the card (`party-sim cards`).
	/// </summary>
	public ImmutableList<string>? Region2Deck { get; init; }

	public ImmutableList<string>? Region2Team { get; init; }

	/// <summary>Elites fought, and won.</summary>
	public int Elites { get; init; }

	public int ElitesWon { get; init; }

	/// <summary>Each WILD fight's cost: the share of the team's HP it took, by region.</summary>
	public ImmutableList<WildChip> Chips { get; init; } = [];
}

/// <summary>What one wild fight took out of the team: the share of its HP, from the line that fought.</summary>
public record WildChip(int Region, double Lost);

/// <summary>
/// **`party-sim`: whole runs played by <see cref="PartyBot"/>** under simple map rules — a card when
/// there is gold, the hospital when hurt, an elite only when healthy, a boss's first relic and first
/// monster. Seeded, deterministic, parallel. For a quick read on where runs die; the bot plays one
/// move ahead, so its numbers are a floor.
/// </summary>
public static class PartySim
{
	public const int TurnLimit = 40;

	/// <summary>
	/// **THE CURVE for the BOT** (Shayne, 2026-09-30: the bot at ~50% was "way too easy" for a person,
	/// who plans further than it does — the bot should win ~35%): the share of runs that should get
	/// THROUGH each region, losing evenly — about 81% of those who reach a region get through it.
	/// </summary>
	public const double BotWins = 0.35;

	public static readonly ImmutableList<double> Target =
	[
		.. Enumerable.Range(1, 5).Select(r => Math.Pow(BotWins, r / 5.0)),
	];

	/// <summary>An elite, and the hospital skipped, only with this share of the team's HP left.</summary>
	public const double HealthyAt = 0.6;

	/// <param name="setup">A variant's change to the run as it starts (a different starting deck) — for
	/// `party-sim variants` only. The sim is outside the game state, so a delegate is fine here.</param>
	public static SimRun[] PlayMany(
		int count,
		ImmutableList<Family>? families = null,
		Func<PartyRun, PartyRun>? setup = null
	)
	{
		var roster = families ?? PartyContent.Families;
		var results = new SimRun[count];
		Parallel.For(
			0,
			count,
			i => results[i] = PlayRun(roster[i % roster.Count], seed: i + 1, setup: setup)
		);
		return results;
	}

	public static SimRun PlayRun(
		Family family,
		int seed,
		Action<string>? log = null,
		Func<PartyRun, PartyRun>? setup = null
	)
	{
		var rng = new Random(seed);
		var run = PartyRun.Start(family, seed);
		if (setup is not null)
			run = setup(run);
		var gyms = ImmutableList<GymArrival>.Empty;
		var battles = 0;
		var turns = 0;
		var elites = 0;
		var elitesWon = 0;
		var chips = ImmutableList<WildChip>.Empty;
		ImmutableList<string>? deck2 = null,
			team2 = null;

		SimRun Ended(RunEnd end, int region) =>
			new(family.ToString(), seed, end, region, gyms, battles, turns)
			{
				Region2Deck = deck2,
				Region2Team = team2,
				Elites = elites,
				ElitesWon = elitesWon,
				Chips = chips,
			};

		while (!run.IsOver)
		{
			switch (run.Phase)
			{
				case RunPhase.Town:
					// A boss's prizes: the first relic and the first monster offered.
					if (run.RelicChoice is [var relic, ..])
						run = run.ChooseRelic(relic);
					if (run.MonsterChoice is [var monster, ..])
						run = run.ChooseMonster(monster);
					if (run.RegionIndex == 1 && deck2 is null)
					{
						deck2 = [.. run.Deck.Select(c => c.Name)];
						team2 = [.. run.Team.Select(m => m.Companion.Name)];
					}
					if (!Healthy(run) && run.CannotHeal is null)
						run = run.HealAtHospital();
					if (run.CanBuyCard(0))
						run = run.BuyCard(0);
					run = run.EnterRoute();
					break;

				case RunPhase.Route when run.HereIsCleared:
					run = run.MoveTo(Walk(run, rng));
					break;

				// A spring: heal when hurt, else upgrade the first card that has a +.
				case RunPhase.Route when run.AtSpring:
					run =
						!Healthy(run) || !run.Upgradable.Any()
							? run.HealAtSpring()
							: run.UpgradeAtSpring(run.Upgradable.First());
					break;

				default:
					if (run.AtBoss)
						gyms = gyms.Add(
							new GymArrival(run.RegionIndex, TeamShare(run), run.Team.Count)
						);

					var where =
						run.AtBoss ? RunEnd.Boss
						: run.Here.Kind == NodeKind.Elite ? RunEnd.Elite
						: RunEnd.Trail;
					if (where == RunEnd.Elite)
						elites++;

					var battle = run.StartBattle();
					var hpBefore = battle.Allies().Where(a => !a.IsToken).Sum(a => a.Hp);
					var hpMax = battle.Allies().Where(a => !a.IsToken).Sum(a => a.MaxHp);
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
								+ " | foes "
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
						return Ended(RunEnd.Stalled, run.RegionIndex);

					var party = battle.GetParty();
					if (where == RunEnd.Boss)
						gyms = gyms.SetItem(gyms.Count - 1, gyms[^1] with { Turns = t });
					var region = run.RegionIndex;
					(run, _) = run.AfterBattle(battle);
					log?.Invoke($"   => {(party.Won ? "WON" : "LOST")}; next: {run.Phase}");

					if (wild && run.Phase != RunPhase.Lost)
						chips = chips.Add(
							new WildChip(
								region,
								(
									hpBefore
									- battle
										.Allies()
										.Where(a => !a.IsToken)
										.Sum(a => Math.Max(0, a.Hp))
								) / (double)Math.Max(1, hpMax)
							)
						);
					if (run.Phase == RunPhase.Lost)
						return Ended(where, region);
					if (where == RunEnd.Elite)
						elitesWon++;

					if (!run.IsOver && run.RewardOffer() is [var card, ..])
						run = run.Take(card);
					break;
			}
		}

		return Ended(RunEnd.Won, run.RegionIndex);
	}

	/// <summary>
	/// **Where the bot walks next on a route**: hurt, a spring or a find; healthy, an elite first,
	/// then any fight. Ties broken by the seed.
	/// </summary>
	private static int Walk(PartyRun run, Random rng)
	{
		var healthy = Healthy(run);
		int Want(NodeKind kind) =>
			kind switch
			{
				NodeKind.Rest => healthy ? 1 : 6,
				NodeKind.Find => healthy ? 2 : 5,
				NodeKind.Wild => healthy ? 5 : 3,
				NodeKind.Grass => healthy ? 4 : 2,
				NodeKind.Trainer => healthy ? 3 : 1,
				// An elite only when healthy — and then before a wild fight: it pays a relic.
				NodeKind.Elite => healthy ? 6 : 0,
				_ => 4,
			};
		return run.Route!.Next(run.NodeId)
			.OrderByDescending(n => Want(n.Kind))
			.ThenBy(_ => rng.Next())
			.First()
			.Id;
	}

	private static bool Healthy(PartyRun run) => TeamShare(run) >= HealthyAt;

	private static double TeamShare(PartyRun run) =>
		run.Team.Sum(m => m.Hp) / (double)run.Team.Sum(m => m.MaxHp);
}
