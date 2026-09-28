using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **A monster between battles**: who it is (its BASE stats), the HP it carries, its LEVEL and the XP
/// toward the next (`PartyLevels`).
/// </summary>
public record RunCompanion(
	PartyCompanion Companion,
	int Hp,
	int Level = PartyLevels.Base,
	int Xp = 0
)
{
	/// <summary>Its stats at its level — what fights.</summary>
	public PartyCompanion Stats => PartyLevels.Scale(Companion, Level);

	public int MaxHp => Stats.Hp;
}

/// <summary>What happened in a battle just won, for the screen to tell.</summary>
public record RunReport(
	ImmutableList<string> Revived,
	ImmutableList<string> Caught,
	ImmutableList<string> ToBench,
	int Gold
)
{
	/// <summary>XP each team member earned.</summary>
	public int Xp { get; init; }

	/// <summary>"Pike grew to Lv 6!" — one per monster that levelled.</summary>
	public ImmutableList<string> LevelUps { get; init; } = [];
}

/// <summary>Where a run is. The screens are chosen from this and nothing else.</summary>
public enum RunPhase
{
	/// <summary>In a region's town: a map of buildings — hospital, shop, pen; from the second town on, a
	/// LEADER must be beaten before you can leave.</summary>
	Town,

	/// <summary>Fighting the town's leader (the region's `Gym` encounter).</summary>
	Gym,

	/// <summary>
	/// **On a wild ROUTE** (`KinMapPlan.md`): walking a branching map toward the next town, at
	/// <see cref="PartyRun.Here"/>.
	/// </summary>
	Route,

	Won,
	Lost,
}

/// <summary>
/// **THE RUN** (`KinMapPlan.md`): TOWN → wild ROUTE → next town → … Each town has a hospital (healing
/// for gold) and a shop; from the second town on its LEADER must be beaten before you leave (the last town's leader
/// wins the run). A route is a branching map of fights, finds and springs (`PartyRun.Route.cs`).
/// You start with one monster and catch the rest. HP carries between fights; a knocked-out monster
/// revives at a quarter of its max; gold from every win buys Snares and cards.
///
/// **Lives OUTSIDE GameState**, like the lane game's `Run`: a battle is built from it, played, and
/// read back into it. Plain records only — the Serialization Rule holds here too.
/// </summary>
public partial record PartyRun
{
	/// <summary>How many fight. The rest wait on the bench.</summary>
	public const int TeamSize = 3;

	public const int StartingSnares = 3;

	public const int StartingGold = 60;

	public const int WildGold = 20;
	public const int DeepGold = 35;
	public const int GymGold = 50;
	public const int FoundGold = 40;

	public const int SnarePrice = 30;
	public const int CardPrice = 50;
	public const int RemovePrice = 40;

	/// <summary>A Rest find heals this much of each monster's max.</summary>
	public const double RestHeal = 0.3;

	public ImmutableList<RunCompanion> Team { get; init; } = [];

	/// <summary>**The bench**: caught monsters beyond the three that fight.</summary>
	public ImmutableList<RunCompanion> Bench { get; init; } = [];

	/// <summary>**The trainer's deck** — one for the run, whoever is on the team.</summary>
	public ImmutableList<KinCard> Deck { get; init; } = [];

	/// <summary>The cards a win or a shop can offer.</summary>
	public ImmutableList<KinCard> Rewards { get; init; } = [];

	public ImmutableList<Region> Regions { get; init; } = [];

	public int RegionIndex { get; init; }
	public RunPhase Phase { get; init; }

	public int Snares { get; init; }
	public int Gold { get; init; }

	/// <summary>Shop cards bought in THIS town, by offer index — each can be bought once.</summary>
	public ImmutableList<int> Sold { get; init; } = [];

	public int Seed { get; init; }

	/// <summary>The route being walked (`PartyRun.Route.cs`), or null off a route.</summary>
	public RouteMap? Route { get; init; }

	/// <summary>Where on the route you stand.</summary>
	public int NodeId { get; init; }

	/// <summary>Route places whose business is done: a find taken, a fight won.</summary>
	public ImmutableList<int> Cleared { get; init; } = [];

	public bool IsWon => Phase == RunPhase.Won;
	public bool IsOver => Phase is RunPhase.Won or RunPhase.Lost;
	public Region Region => Regions[RegionIndex];

	/// <summary>From the second town on, a leader stands between you and the road out.</summary>
	public bool HasLeader => RegionIndex >= 1;

	/// <summary>Whether this town's leader has been beaten.</summary>
	public bool LeaderBeaten { get; init; }

	/// <summary>A new run: one starter, in the first region's town.</summary>
	public static PartyRun Start(
		PartyCompanion starter,
		int seed,
		ImmutableList<Region>? regions = null,
		ImmutableList<KinCard>? deck = null,
		ImmutableList<KinCard>? rewards = null
	) =>
		new()
		{
			Team = [new RunCompanion(starter, starter.Hp)],
			Regions = regions ?? PartyWorld.Regions,
			Deck = deck ?? PartyContent.StarterDeck,
			Rewards = rewards ?? PartyContent.Rewards,
			Snares = StartingSnares,
			Gold = StartingGold,
			Phase = RunPhase.Town,
			Seed = seed,
		};

	/// <summary>
	/// **A caught foe as a monster of yours: exactly what it had.** Its cycle and its max HP.
	/// Power 0, because a foe's move amounts are already its whole damage. **Its deck ability wakes
	/// now**: the passive, triggers and monster deck it carried dormant. A wild trait (Thief) stays wild.
	/// **Its stats are the species' BASE** (`PartyWorld.Species`); it joins at the level it was caught.
	/// </summary>
	public static PartyCompanion FromFoe(Foe foe)
	{
		var basis = PartyWorld.Species(foe.Name) ?? foe;
		return new(
			foe.Name,
			basis.MaxHp,
			Power: 0,
			[.. basis.Pattern.Select(i => i with { Steals = false })],
			Passive: foe.CaughtPassive,
			PassiveRule: foe.CaughtRule
		)
		{
			Cards = foe.CaughtCards,
			Abilities = foe.CaughtAbilities,
			Family = foe.Family,
		};
	}

	// ===== Leaving town

	/// <summary>Why you cannot leave the town yet — or null if you can.</summary>
	public string? CannotLeaveTown =>
		Phase != RunPhase.Town ? "Not in a town"
		: HasLeader && !LeaderBeaten ? "Beat the leader first"
		: null;

	/// <summary>Into the leader's hall — the town's leader fight. Refused with no leader, or twice.</summary>
	public PartyRun FightLeader() =>
		Phase == RunPhase.Town && HasLeader && !LeaderBeaten
			? this with
			{
				Phase = RunPhase.Gym,
			}
			: this;

	// ===== Battles

	/// <summary>The fight at this point of the run: the current stop's, or the gym's.</summary>
	public Encounter NextFight =>
		Phase switch
		{
			RunPhase.Gym => Region.Gym,
			RunPhase.Route => Here.Encounter,
			_ => null,
		} ?? throw new InvalidOperationException("No fight here");

	public GameState StartBattle()
	{
		var scenario = new PartyScenario(
			NextFight.Name,
			Phase switch
			{
				RunPhase.Gym => $"{Region.Name} — the leader",
				_ => $"{Region.Name} — the route",
			},
			// **The team's order IS the line** (front first), then the bench off it (-1) — in that
			// order, so slots line up. The battle opens DEPLOYING (R2): the order can change before FIGHT.
			[
				.. Team.Select((m, i) => new PlacedCompanion(m.Stats, i, m.Hp)),
				.. Bench.Select(m => new PlacedCompanion(m.Stats, -1, m.Hp)),
			],
			NextFight.Foes,
			Deck,
			[],
			Snares,
			Deploy: true
		);
		return PartyBattleFactory.Create(scenario, Seed + RegionIndex * 1009 + NodeId * 37);
	}

	/// <summary>
	/// **Reads a finished battle back into the run.** A loss ends it. A win carries HP over — a
	/// knocked-out monster revives at a quarter of its max — keeps the Snares left, pays gold, and
	/// every caught foe joins: the team if there is room, else the bench. On a route you stay on the
	/// place you fought; after a leader you are back in its town with the road open.
	/// </summary>
	public (PartyRun Run, RunReport Report) AfterBattle(GameState finished)
	{
		var party = finished.GetParty();
		if (!party.IsOver)
			throw new InvalidOperationException("The battle is not over");

		if (!party.Won)
			return (this with { Phase = RunPhase.Lost }, new RunReport([], [], [], 0));

		// Team and bench both come back by slot: a benched monster may have stepped in and fought.
		var revived = ImmutableList<string>.Empty;
		RunCompanion After(RunCompanion member, int slot)
		{
			var ally = finished.Allies().Single(a => a.Slot == slot);
			if (!ally.IsKnockedOut)
				return member with { Hp = ally.Hp };

			revived = revived.Add(ally.Name);
			return member with { Hp = (int)Math.Ceiling(ally.MaxHp / 4.0) };
		}
		var team = Team.Select(After).ToImmutableList();

		// **The order you deployed is kept for the next fight** (R2) — not where the line ended up.
		var deployed = party.DeployedOrder.Where(slot => slot < team.Count).ToList();
		team =
		[
			.. deployed.Select(slot => team[slot]),
			.. team.Where((_, slot) => !deployed.Contains(slot)),
		];
		var bench = Bench.Select((m, i) => After(m, Team.Count + i)).ToImmutableList();

		var gold = Phase == RunPhase.Gym ? GymGold : RouteGold(Here.Kind);
		// **XP to every monster on the team** — the line that fought (`PartyLevels.XpFor`).
		var xp = PartyLevels.XpFor(NextFight.Foes, leader: Phase == RunPhase.Gym);
		var levelUps = ImmutableList<string>.Empty;
		team =
		[
			.. team.Select(m =>
			{
				var grown = PartyLevels.Gain(m, xp);
				if (grown.Level > m.Level)
					levelUps = levelUps.Add($"{m.Companion.Name} grew to Lv {grown.Level}!");
				return grown;
			}),
		];

		var run = this with
		{
			Team = team,
			Bench = bench,
			Snares = party.Snares,
			Gold = Gold + gold,
		};

		var caught = ImmutableList<string>.Empty;
		var toBench = ImmutableList<string>.Empty;
		foreach (var foe in finished.CaughtFoes())
		{
			var joining = new RunCompanion(FromFoe(foe), foe.Hp, foe.Level);
			caught = caught.Add(foe.Name);
			if (run.Team.Count < TeamSize)
				run = run with { Team = run.Team.Add(joining) };
			else
			{
				run = run with { Bench = run.Bench.Add(joining) };
				toBench = toBench.Add(foe.Name);
			}
		}

		run =
			// On a route you stay where you fought — the place is cleared, and the map is yours again.
			Phase == RunPhase.Route
				? run with
				{
					Cleared = run.Cleared.Add(NodeId),
				}
			// The leader beaten: back in its town, the road out open — or, the last one, the run won.
			: RegionIndex + 1 >= Regions.Count ? run with { Phase = RunPhase.Won }
			: run with { Phase = RunPhase.Town, LeaderBeaten = true };

		return (
			run,
			new RunReport(revived, caught, toBench, gold) { Xp = xp, LevelUps = levelUps }
		);
	}

	/// <summary>
	/// **Into a town.** Nobody is healed for free any more: the hospital sells it (`PartyRun.Town.cs`).
	/// </summary>
	internal PartyRun EnterTown(int region) =>
		this with
		{
			RegionIndex = region,
			Phase = RunPhase.Town,
			LeaderBeaten = false,
			Route = null,
			NodeId = 0,
			Cleared = [],
			Sold = [],
		};

	internal static ImmutableList<RunCompanion> Heal(
		ImmutableList<RunCompanion> monsters,
		double share
	) =>
		[
			.. monsters.Select(m =>
				m with
				{
					Hp = Math.Min(m.MaxHp, m.Hp + (int)Math.Ceiling(m.MaxHp * share)),
				}
			),
		];

	/// <summary>**A benched monster takes a team member's place**, and the team member sits down.</summary>
	public PartyRun Swap(int teamIndex, int benchIndex) =>
		this with
		{
			Team = Team.SetItem(teamIndex, Bench[benchIndex]),
			Bench = Bench.SetItem(benchIndex, Team[teamIndex]),
		};

	// ===== Cards: a win's reward, and the town's shop

	/// <summary>**Three cards to choose from after a win** — seeded, so a run replays.</summary>
	public ImmutableList<KinCard> RewardOffer()
	{
		var rng = new Random(Seed * 31 + RegionIndex * 13 + NodeId * 3 + (LeaderBeaten ? 1 : 0));
		return Leaning(rng);
	}

	/// <summary>
	/// **Three cards, leaning to your team's FAMILIES** (`KinFamiliesPlan.md`): a card of a family on
	/// your team or bench is three times as likely to be offered. Neutral cards stay in the draw.
	/// </summary>
	private ImmutableList<KinCard> Leaning(Random rng)
	{
		var families = Team.Concat(Bench).Select(m => m.Companion.Family).ToHashSet();
		families.Remove(Family.None);
		return
		[
			.. Rewards
				.Select(c =>
					(Card: c, Key: rng.NextDouble() / (families.Contains(c.Family) ? 3.0 : 1.0))
				)
				.OrderBy(p => p.Key)
				.Take(3)
				.Select(p => p.Card),
		];
	}

	/// <summary>The chosen card joins the deck for the rest of the run.</summary>
	public PartyRun Take(KinCard reward) => this with { Deck = Deck.Add(reward) };

	/// <summary>The town's three cards for sale — the same all visit.</summary>
	public ImmutableList<KinCard> ShopCards()
	{
		var rng = new Random(Seed * 53 + RegionIndex);
		return Leaning(rng);
	}

	public bool CanBuySnare => Gold >= SnarePrice;

	public bool CanBuyCard(int offer) => Gold >= CardPrice && !Sold.Contains(offer);

	public bool CanRemove => Gold >= RemovePrice && Deck.Count > 1;

	public PartyRun BuySnare() =>
		CanBuySnare ? this with { Gold = Gold - SnarePrice, Snares = Snares + 1 } : this;

	public PartyRun BuyCard(int offer) =>
		CanBuyCard(offer)
			? this with
			{
				Gold = Gold - CardPrice,
				Deck = Deck.Add(ShopCards()[offer]),
				Sold = Sold.Add(offer),
			}
			: this;

	/// <summary>A card out of the deck for good — a thinner deck draws its best cards more.</summary>
	public PartyRun Remove(int deckIndex) =>
		CanRemove ? this with { Gold = Gold - RemovePrice, Deck = Deck.RemoveAt(deckIndex) } : this;
}
