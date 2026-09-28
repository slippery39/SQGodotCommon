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

	/// <summary>The team indices (after the battle's reorder) of the monsters that levelled.</summary>
	public ImmutableList<int> LevelUps { get; init; } = [];

	/// <summary>The RELIC an elite paid, if it did (none are left once you hold them all).</summary>
	public Relic? Relic { get; init; }
}

/// <summary>Where a run is. The screens are chosen from this and nothing else.</summary>
public enum RunPhase
{
	/// <summary>In a region's town: a map of buildings — hospital, shop, pen, and the gate out.</summary>
	Town,

	/// <summary>
	/// **On a wild ROUTE** (`KinMapPlan.md`): walking a branching map toward the next town, at
	/// <see cref="PartyRun.Here"/>.
	/// </summary>
	Route,

	Won,
	Lost,
}

/// <summary>
/// **THE RUN** (`KinMapPlan.md`; `KinFamiliesPlan.md`, round 2): TOWN → wild ROUTE → its BOSS → next
/// town → … five regions; the fifth boss wins the run. Each town has a hospital (healing for gold) and
/// a shop. A route is a branching map of fights, finds and springs (`PartyRun.Route.cs`).
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
	public const int BossGold = 50;
	public const int EliteGold = 60;
	public const int FoundGold = 40;

	public const int SnarePrice = 30;
	public const int CardPrice = 50;
	public const int RemovePrice = 40;

	/// <summary>A Rest find heals this much of each monster's max.</summary>
	public const double RestHeal = 0.3;

	public ImmutableList<RunCompanion> Team { get; init; } = [];

	/// <summary>**The bench**: caught monsters beyond the three that fight.</summary>
	public ImmutableList<RunCompanion> Bench { get; init; } = [];

	/// <summary>
	/// **The run's FAMILY — its starter's** (`KinFamiliesPlan.md`, round 2). Rewards, the shop and
	/// catching offer only it and colourless.
	/// </summary>
	public Family Family { get; init; }

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

	/// <summary>
	/// **This region's BOSS — one of its two, fixed by the seed**, so it is known from the town on and
	/// the route becomes preparing for it (Shayne, 2026-09-28: shown, like STS).
	/// </summary>
	public Encounter Boss =>
		Region.Bosses[new Random(Seed * 29 + RegionIndex).Next(Region.Bosses.Count)];

	/// <summary>**The RELICS** held (`PartyRelics`) — an elite's prize, kept for the whole run.</summary>
	public ImmutableList<Relic> Relics { get; init; } = [];

	public bool Has(Relic relic) => Relics.Contains(relic);

	/// <summary>A relic joins the run — and Snare Pouch pays its Snares at once.</summary>
	public PartyRun Gain(Relic relic) =>
		Has(relic)
			? this
			: this with
			{
				Relics = Relics.Add(relic),
				Snares = Snares + (relic == Relic.SnarePouch ? PartyRelics.SnarePouchNow : 0),
			};

	/// <summary>Whether you stand before the BOSS — the route's last place.</summary>
	public bool AtBoss => Phase == RunPhase.Route && Route is not null && Here.Kind == NodeKind.End;

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
			Family = starter.Family,
			Regions = regions ?? PartyWorld.Regions,
			Deck = deck ?? PartyContent.StartingDeck(starter.Family),
			Rewards = rewards ?? PartyContent.Rewards,
			Snares = StartingSnares,
			Gold = StartingGold,
			Phase = RunPhase.Town,
			Seed = seed,
		};

	/// <summary>
	/// **A caught foe as a monster of yours: exactly what it had.** Its cycle and its max HP.
	/// Power 0, because a foe's move amounts are already its whole damage. **Its passive wakes now**:
	/// the passive and triggers it carried dormant. A wild trait (Thief) stays wild.
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
			Abilities = foe.CaughtAbilities,
			Family = foe.Family,
		};
	}

	// ===== Leaving town

	/// <summary>Why you cannot leave the town yet — or null if you can.</summary>
	public string? CannotLeaveTown => Phase != RunPhase.Town ? "Not in a town" : null;

	// ===== Battles

	/// <summary>The fight at this point of the run: the place you stand on.</summary>
	public Encounter NextFight =>
		(Phase == RunPhase.Route ? Here.Encounter : null)
		?? throw new InvalidOperationException("No fight here");

	public GameState StartBattle()
	{
		var scenario = new PartyScenario(
			NextFight.Name,
			Here.Kind switch
			{
				NodeKind.End => $"{Region.Name} — the boss",
				NodeKind.Elite => $"{Region.Name} — an elite",
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
			Deploy: true,
			Family,
			Relics
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

		var kind = Here.Kind;
		var gold = (int)(RouteGold(kind) * (Has(Relic.LuckyCoin) ? PartyRelics.LuckyCoinGold : 1));
		// **XP to every monster on the team** — the line that fought (`PartyLevels.XpFor`). An elite
		// and a boss pay double.
		var xp = PartyLevels.XpFor(NextFight.Foes, leader: kind is NodeKind.End or NodeKind.Elite);
		var levelUps = ImmutableList<int>.Empty;
		team =
		[
			.. team.Select(
				(m, i) =>
				{
					var grown = PartyLevels.Gain(m, xp);
					if (grown.Level > m.Level)
						levelUps = levelUps.Add(i);
					return grown;
				}
			),
		];

		var run = this with
		{
			Team = team,
			Bench = bench,
			Snares = party.Snares,
			Gold = Gold + gold,
		};

		// FIELD KIT: every monster that is still up heals a little — a revived one too.
		if (Has(Relic.FieldKit))
			run = run with
			{
				Team = HealBy(run.Team, PartyRelics.FieldKitHeal),
				Bench = HealBy(run.Bench, PartyRelics.FieldKitHeal),
			};

		// **An ELITE pays a RELIC** — one you do not hold, seeded; none once you hold them all.
		Relic? relic = null;
		if (kind == NodeKind.Elite)
		{
			var left = PartyRelics.All.Where(r => !Has(r)).ToList();
			if (left.Count > 0)
			{
				relic = left[new Random(Seed * 41 + RegionIndex * 7 + NodeId).Next(left.Count)];
				run = run.Gain(relic.Value);
			}
		}

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
			kind != NodeKind.End
				? run with
				{
					Cleared = run.Cleared.Add(NodeId),
				}
			// The BOSS beaten: on to the next town — or, the last one, the run won.
			: RegionIndex + 1 >= Regions.Count ? run with { Phase = RunPhase.Won }
			: run.EnterTown(RegionIndex + 1);

		return (
			run,
			new RunReport(revived, caught, toBench, gold)
			{
				Xp = xp,
				LevelUps = levelUps,
				Relic = relic,
			}
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
			Snares = Snares + (Has(Relic.SnarePouch) ? 1 : 0),
			Route = null,
			NodeId = 0,
			Cleared = [],
			Sold = [],
		};

	/// <summary>Everyone heals this many HP, up to their max.</summary>
	internal static ImmutableList<RunCompanion> HealBy(
		ImmutableList<RunCompanion> monsters,
		int hp
	) => [.. monsters.Select(m => m with { Hp = Math.Min(m.MaxHp, m.Hp + hp) })];

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

	/// <summary>How often a reward offers each rarity, relative to the others (STS's shape).</summary>
	public static double Weight(Rarity rarity) =>
		rarity switch
		{
			Rarity.Rare => 10,
			Rarity.Uncommon => 30,
			_ => 60,
		};

	/// <summary>
	/// **Three cards to choose from after a win** — seeded, so a run replays. **An elite's or a boss's
	/// win guarantees a RARE** (Shayne, 2026-09-28). Trainer's Eye offers four.
	/// </summary>
	public ImmutableList<KinCard> RewardOffer()
	{
		// After a boss the run has moved on to the next town (or won); after an elite it stands there.
		var bossBeaten = Phase == RunPhase.Town || IsWon;
		var eliteBeaten = Phase == RunPhase.Route && Here.Kind == NodeKind.Elite;
		var rng = new Random(Seed * 31 + RegionIndex * 13 + NodeId * 3 + (bossBeaten ? 1 : 0));
		return Offer(
			rng,
			rareFirst: bossBeaten || eliteBeaten,
			count: Has(Relic.TrainersEye) ? PartyRelics.TrainersEyeOffer : 3
		);
	}

	/// <summary>The cards this run may be offered: its FAMILY's and the colourless ones.</summary>
	public IEnumerable<KinCard> Pool =>
		Rewards.Where(c => c.Family is Family.None || c.Family == Family);

	/// <summary>
	/// **Three different cards from the pool, weighted by rarity** — a weighted draw without
	/// replacement (each card's key is u^(1/weight), the highest three win).
	/// </summary>
	private ImmutableList<KinCard> Offer(Random rng, bool rareFirst = false, int count = 3)
	{
		var keyed = Pool.Select(c =>
				(Card: c, Key: Math.Pow(rng.NextDouble(), 1 / Weight(c.Rarity)))
			)
			.OrderByDescending(p => p.Key)
			.Select(p => p.Card)
			.ToList();
		if (rareFirst && keyed.FirstOrDefault(c => c.Rarity == Rarity.Rare) is { } rare)
		{
			keyed.Remove(rare);
			keyed.Insert(0, rare);
		}
		return [.. keyed.Take(count)];
	}

	/// <summary>The chosen card joins the deck for the rest of the run.</summary>
	public PartyRun Take(KinCard reward) => this with { Deck = Deck.Add(reward) };

	/// <summary>The town's three cards for sale — the same all visit.</summary>
	public ImmutableList<KinCard> ShopCards()
	{
		var rng = new Random(Seed * 53 + RegionIndex);
		return Offer(rng);
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
