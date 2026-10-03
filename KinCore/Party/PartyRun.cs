using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **A monster between battles**: who it is and the HP it carries. No levels (round 4): a monster
/// grows through cards, relics and upgrades.
/// </summary>
public record RunCompanion(PartyCompanion Companion, int Hp)
{
	public int MaxHp => Companion.Hp;
}

/// <summary>What happened in a battle just won, for the screen to tell.</summary>
public record RunReport(ImmutableList<string> Revived, int Gold)
{
	/// <summary>The RELIC an elite paid, if it did (none are left once you hold them all).</summary>
	public Relic? Relic { get; init; }
}

/// <summary>Where a run is. The screens are chosen from this and nothing else.</summary>
public enum RunPhase
{
	/// <summary>In a region's town: a map of buildings — the hospital, the shop, and the gate out.</summary>
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
/// **You start with ONE monster and choose another after each of the first two bosses** (round 4:
/// no catching, no bench, no levels). HP carries between fights; a knocked-out monster is back at
/// 1 HP after the fight; gold buys cards and healing.
///
/// **Lives OUTSIDE GameState**, like the lane game's `Run`: a battle is built from it, played, and
/// read back into it. Plain records only — the Serialization Rule holds here too.
/// </summary>
public partial record PartyRun
{
	/// <summary>The most monsters a team holds: the starter and two bosses' picks.</summary>
	public const int TeamSize = 3;

	public const int StartingGold = 60;

	public const int WildGold = 20;
	public const int BossGold = 100;
	public const int EliteGold = 60;
	public const int FoundGold = 40;

	public const int CardPrice = 50;
	public const int RemovePrice = 40;

	/// <summary>A Rest find heals this much of each monster's max.</summary>
	public const double RestHeal = 0.3;

	/// <summary>
	/// **A boss beaten heals HALF** (Shayne, 2026-10-02) — not in full: a full heal before the town
	/// left its hospital nothing to heal. A guess (exploring).
	/// </summary>
	public const double BossHeal = 0.5;

	public ImmutableList<RunCompanion> Team { get; init; } = [];

	/// <summary>
	/// **The run's FAMILY — its starter's** (`KinFamiliesPlan.md`, round 2). Rewards, the shop and a
	/// boss's monsters offer only it and colourless.
	/// </summary>
	public Family Family { get; init; }

	/// <summary>**The trainer's deck** — one for the run, whoever is on the team.</summary>
	public ImmutableList<KinCard> Deck { get; init; } = [];

	/// <summary>The cards a win or a shop can offer.</summary>
	public ImmutableList<KinCard> Rewards { get; init; } = [];

	public ImmutableList<Region> Regions { get; init; } = [];

	public int RegionIndex { get; init; }
	public RunPhase Phase { get; init; }

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

	/// <summary>
	/// **A boss beaten: EVOLVE one of your monsters** (`KinFamiliesPlan.md`, round 5 — no new monsters
	/// any more). Set as you reach the next town; spent by <see cref="Evolve"/>.
	/// </summary>
	public bool EvolutionDue { get; init; }

	/// <summary>The team places whose monster can evolve now — none unless an evolution is due.</summary>
	public ImmutableList<int> Evolvable =>
		EvolutionDue
			?
			[
				.. Team.Select((m, i) => (m, i))
					.Where(p => p.m.Companion.EvolvesInto is not null)
					.Select(p => p.i),
			]
			: [];

	/// <summary>
	/// **That monster becomes its evolved form** — it gains the extra HP and keeps its damage (no hidden
	/// heal, 2026-10-03). Refused changes nothing.
	/// </summary>
	public PartyRun Evolve(int index)
	{
		if (!Evolvable.Contains(index))
			return this;
		var member = Team[index];
		var form = member.Companion.EvolvesInto!;
		return this with
		{
			Team = Team.SetItem(
				index,
				new RunCompanion(form, member.Hp + form.Hp - member.Companion.Hp)
			),
			EvolutionDue = false,
		};
	}

	/// <summary>
	/// **The BOSS RELICS offered — pick one** (`ChooseRelic`), or leave them. Set when a boss falls;
	/// cleared by the choice, or on leaving the town.
	/// </summary>
	public ImmutableList<Relic> RelicChoice { get; init; } = [];

	/// <summary>Three boss relics you do not hold, seeded by the region.</summary>
	private ImmutableList<Relic> BossRelicOffer()
	{
		var rng = new Random(Seed * 43 + RegionIndex);
		return [.. PartyRelics.Boss.Where(r => !Has(r)).OrderBy(_ => rng.Next()).Take(3)];
	}

	/// <summary>Takes one of the offered boss relics; the others are gone.</summary>
	public PartyRun ChooseRelic(Relic relic) =>
		RelicChoice.Contains(relic) ? Gain(relic) with { RelicChoice = [] } : this;

	/// <summary>A relic joins the run.</summary>
	public PartyRun Gain(Relic relic) =>
		Has(relic) ? this : this with { Relics = Relics.Add(relic) };

	/// <summary>Whether you stand before the BOSS — the route's last place.</summary>
	public bool AtBoss => Phase == RunPhase.Route && Route is not null && Here.Kind == NodeKind.End;

	/// <summary>A new run: one starter, in the first region's town.</summary>
	/// <summary>
	/// **A run of a FAMILY: three of its pool, rolled** (`KinJam.md`, top: the run's new shape,
	/// 2026-10-02). Seeded, so a seed is always the same trio; <see cref="Reroll"/> rolls once more.
	/// </summary>
	public static PartyRun Start(Family family, int seed) =>
		Start(Roll(family, seed, attempt: 0), family, seed);

	/// <summary>A run with exactly this one monster — tests and the console, where the team is the point.</summary>
	public static PartyRun Start(
		PartyCompanion starter,
		int seed,
		ImmutableList<Region>? regions = null,
		ImmutableList<KinCard>? deck = null,
		ImmutableList<KinCard>? rewards = null
	) => Start([starter], starter.Family, seed, regions, deck, rewards);

	private static PartyRun Start(
		ImmutableList<PartyCompanion> team,
		Family family,
		int seed,
		ImmutableList<Region>? regions = null,
		ImmutableList<KinCard>? deck = null,
		ImmutableList<KinCard>? rewards = null
	) =>
		new()
		{
			Team = [.. team.Select(m => new RunCompanion(m, m.Hp))],
			Family = family,
			Regions = regions ?? PartyWorld.Regions,
			Deck = deck ?? PartyContent.StartingDeck(family),
			Rewards = rewards ?? PartyContent.Rewards,
			Gold = StartingGold,
			Phase = RunPhase.Town,
			Seed = seed,
		};

	/// <summary>Three of the family's pool, in a seeded order — the front first.</summary>
	public static ImmutableList<PartyCompanion> Roll(Family family, int seed, int attempt)
	{
		var rng = new Random(seed * 7919 + attempt);
		return [.. PartyContent.PoolOf(family).OrderBy(_ => rng.Next()).Take(TeamSize)];
	}

	/// <summary>The one REROLL has been spent.</summary>
	public bool Rerolled { get; init; }

	/// <summary>Why the trio cannot be rerolled — or null if it can: once, before the run sets out.</summary>
	public string? CannotReroll =>
		Rerolled ? "The reroll is spent"
		: Phase != RunPhase.Town || RegionIndex != 0 || Route is not null
			? "Only before the run sets out"
		: null;

	/// <summary>
	/// **The one reroll: a different trio** (2026-10-03 — it saves a run dealt three that clash). The
	/// next seeded roll that is not the same three. Refused changes nothing.
	/// </summary>
	public PartyRun Reroll()
	{
		if (CannotReroll is not null)
			return this;
		var now = Team.Select(m => m.Companion.Name).ToHashSet();
		var attempt = 1;
		ImmutableList<PartyCompanion> trio;
		do trio = Roll(Family, Seed, attempt++);
		while (trio.All(m => now.Contains(m.Name)) && attempt < 50);
		return this with
		{
			Team = [.. trio.Select(m => new RunCompanion(m, m.Hp))],
			Rerolled = true,
		};
	}

	/// <summary>
	/// **A species as a monster of yours** — its HP and its passive (the kit it carries dormant as a
	/// wild foe). PLACEHOLDER until the families are drafted (round 4): the boss picks are built from
	/// today's species this way.
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
			// **The team's order IS the line** (front first) — set in town (`MoveToFront`), not before a
			// fight (deploy cut, playtest 2026-10-02).
			[.. Team.Select((m, i) => new PlacedCompanion(m.Companion, i, m.Hp))],
			NextFight.Foes,
			Deck,
			[],
			Relics: Relics
		);
		return PartyBattleFactory.Create(scenario, Seed + RegionIndex * 1009 + NodeId * 37);
	}

	/// <summary>
	/// **Reads a finished battle back into the run.** A loss ends it. A win carries HP over — a
	/// knocked-out monster is back at 1 HP (round 4) — and pays gold. On a route you stay on the place
	/// you fought; after a boss you are in the next town, healed, with its prizes to choose.
	/// </summary>
	public (PartyRun Run, RunReport Report) AfterBattle(GameState finished)
	{
		var party = finished.GetParty();
		if (!party.IsOver)
			throw new InvalidOperationException("The battle is not over");

		if (!party.Won)
			return (this with { Phase = RunPhase.Lost }, new RunReport([], 0));

		var revived = ImmutableList<string>.Empty;
		RunCompanion After(RunCompanion member, int slot)
		{
			var ally = finished.Allies().Single(a => a.Slot == slot);
			if (!ally.IsKnockedOut)
				return member with { Hp = ally.Hp };

			revived = revived.Add(ally.Name);
			return member with { Hp = 1 };
		}
		var team = Team.Select(After).ToImmutableList();

		var kind = Here.Kind;
		var gold = (int)(RouteGold(kind) * (Has(Relic.LuckyCoin) ? PartyRelics.LuckyCoinGold : 1));

		var run = this with { Team = team, Gold = Gold + gold };

		// FIELD KIT: every monster heals a little — a revived one too.
		if (Has(Relic.FieldKit))
			run = run with { Team = HealBy(run.Team, PartyRelics.FieldKitHeal) };

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

		run =
			// On a route you stay where you fought — the place is cleared, and the map is yours again.
			kind != NodeKind.End
				? run with
				{
					Cleared = run.Cleared.Add(NodeId),
				}
			// The BOSS beaten: on to the next town — healed by half (`BossHeal`), with three boss relics and (the first
			// two bosses) three monsters to choose from (Shayne, 2026-09-28) — or, the last one, the run won.
			: RegionIndex + 1 >= Regions.Count ? run with { Phase = RunPhase.Won }
			: run.EnterTown(RegionIndex + 1) with
			{
				Team = Heal(run.Team, BossHeal),
				RelicChoice = BossRelicOffer(),
				EvolutionDue = run.Team.Any(m => m.Companion.EvolvesInto is not null),
			};

		return (run, new RunReport(revived, gold) { Relic = relic });
	}

	/// <summary>
	/// **Into a town.** Nobody is healed for free any more: the hospital sells it (`PartyRun.Town.cs`).
	/// </summary>
	internal PartyRun EnterTown(int region) =>
		this with
		{
			RegionIndex = region,
			Phase = RunPhase.Town,
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

	public bool CanBuyCard(int offer) => Gold >= CardPrice && !Sold.Contains(offer);

	public bool CanRemove => Gold >= RemovePrice && Deck.Count > 1;

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
