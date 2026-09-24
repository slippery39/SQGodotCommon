using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>A monster between battles: who it is and the HP it carries.</summary>
public record RunCompanion(PartyCompanion Companion, int Hp);

/// <summary>What happened in a battle just won, for the screen to tell.</summary>
public record RunReport(
	ImmutableList<string> Revived,
	ImmutableList<string> Caught,
	ImmutableList<string> ToBench,
	int Gold
);

/// <summary>Where a run is. The screens are chosen from this and nothing else.</summary>
public enum RunPhase
{
	/// <summary>In a region's town: healed on arrival; the shop is open.</summary>
	Town,

	/// <summary>Choosing one of the region's two wild areas.</summary>
	ChooseArea,

	/// <summary>On the chosen area's trail, at <see cref="PartyRun.CurrentStop"/>.</summary>
	Trail,

	/// <summary>At the region's gym.</summary>
	Gym,

	Won,
	Lost,
}

/// <summary>
/// **THE RUN** (KinJam.md "THE MAP", "CATCHING"): two regions, each a TOWN (full heal, a shop), then
/// ONE of two wild AREAS — its trail of wild fights, a find and an optional deeper path — then the
/// region's GYM. You start with one monster and catch the rest. HP carries between fights; a
/// knocked-out monster revives at a quarter of its max; gold from every win buys Snares and cards.
///
/// **Lives OUTSIDE GameState**, like the lane game's `Run`: a battle is built from it, played, and
/// read back into it. Plain records only — the Serialization Rule holds here too.
/// </summary>
public record PartyRun
{
	/// <summary>How many fight. The rest wait on the bench.</summary>
	public const int TeamSize = 3;

	public const int StartingSnares = 3;

	/// <summary>**Your health across the run.** Only a town heals it.</summary>
	public const int TrainerMaxHp = PartyScenario.DefaultTrainerHp;

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

	/// <summary>The chosen area's trail, and where on it you are. Empty outside an area.</summary>
	public ImmutableList<Stop> Trail { get; init; } = [];

	public int StopIndex { get; init; }

	/// <summary>The area chosen in this region, or null before choosing.</summary>
	public Area? Area { get; init; }

	public int Snares { get; init; }
	public int Gold { get; init; }
	public int TrainerHp { get; init; }

	/// <summary>Shop cards bought in THIS town, by offer index — each can be bought once.</summary>
	public ImmutableList<int> Sold { get; init; } = [];

	public int Seed { get; init; }

	public bool IsWon => Phase == RunPhase.Won;
	public bool IsOver => Phase is RunPhase.Won or RunPhase.Lost;
	public Region Region => Regions[RegionIndex];
	public Stop CurrentStop => Trail[StopIndex];

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
			TrainerHp = TrainerMaxHp,
			Phase = RunPhase.Town,
			Seed = seed,
		};

	/// <summary>
	/// **Where a team of N stands**: one in the middle, two either side of it, three spread out — so
	/// every size has a space to step into.
	/// </summary>
	public static ImmutableList<int> Formation(int size) =>
		size switch
		{
			1 => [2],
			2 => [1, 3],
			3 => [0, 2, 4],
			4 => [0, 1, 3, 4],
			_ => [0, 1, 2, 3, 4],
		};

	/// <summary>
	/// **A caught foe as a monster of yours: exactly what it had.** Its cycle, its Speed, its max HP.
	/// Power 0, because a foe's move amounts are already its whole damage.
	/// </summary>
	public static PartyCompanion FromFoe(Foe foe) =>
		new(foe.Name, foe.MaxHp, Power: 0, foe.Speed, foe.Pattern);

	// ===== Moving along

	public PartyRun LeaveTown() => this with { Phase = RunPhase.ChooseArea, Sold = [] };

	/// <summary>Into one of the region's areas: its trail is drawn now, from its own pool.</summary>
	public PartyRun ChooseArea(int index)
	{
		var area = Region.Areas[index];
		return this with
		{
			Area = area,
			Trail = PartyWorld.Trail(Region, area, new Random(Seed * 17 + RegionIndex * 7 + index)),
			StopIndex = 0,
			Phase = RunPhase.Trail,
		};
	}

	/// <summary>Picks up what a find stop holds, and walks on.</summary>
	public PartyRun TakeFind() =>
		(
			CurrentStop.Find switch
			{
				FindKind.Snare => this with { Snares = Snares + 1 },
				FindKind.Gold => this with { Gold = Gold + FoundGold },
				_ => this with { Team = Heal(Team, RestHeal), Bench = Heal(Bench, RestHeal) },
			}
		).Walk();

	/// <summary>Turns back from the deeper path, straight to the gym.</summary>
	public PartyRun SkipDeep() => this with { Phase = RunPhase.Gym };

	/// <summary>The next stop, or the gym when the trail is done.</summary>
	private PartyRun Walk() =>
		StopIndex + 1 < Trail.Count
			? this with
			{
				StopIndex = StopIndex + 1,
			}
			: this with
			{
				Phase = RunPhase.Gym,
			};

	// ===== Battles

	/// <summary>The fight at this point of the run: the current stop's, or the gym's.</summary>
	public Encounter NextFight =>
		Phase == RunPhase.Gym
			? Region.Gym
			: CurrentStop.Encounter ?? throw new InvalidOperationException("No fight here");

	public GameState StartBattle()
	{
		var spaces = Formation(Team.Count);
		var scenario = new PartyScenario(
			NextFight.Name,
			Phase == RunPhase.Gym
				? $"{Region.Name} — the gym"
				: $"{Region.Name} — {Area!.Name}, stop {StopIndex + 1} of {Trail.Count}",
			// The team on the board, then the bench off it (space -1) — in that order, so slots line up.
			[
				.. Team.Select((m, i) => new PlacedCompanion(m.Companion, spaces[i], m.Hp)),
				.. Bench.Select(m => new PlacedCompanion(m.Companion, -1, m.Hp)),
			],
			NextFight.Foes,
			Deck,
			[],
			Snares,
			TrainerHp,
			NextFight.LeaderHp
		);
		return PartyBattleFactory.Create(scenario, Seed + RegionIndex * 1009 + StopIndex * 101);
	}

	/// <summary>
	/// **Reads a finished battle back into the run.** A loss ends it. A win carries HP over — a
	/// knocked-out monster revives at a quarter of its max — keeps the Snares left, pays gold, and
	/// every caught foe joins: the team if there is room, else the bench. Then the run walks on: the
	/// next stop, the gym, or — after a gym — the next region's town, where everyone is healed.
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
		var bench = Bench.Select((m, i) => After(m, Team.Count + i)).ToImmutableList();

		var gold =
			Phase == RunPhase.Gym ? GymGold
			: CurrentStop.Kind == StopKind.Deep ? DeepGold
			: WildGold;
		var run = this with
		{
			Team = team,
			Bench = bench,
			Snares = party.Snares,
			Gold = Gold + gold,
			TrainerHp = party.TrainerHp,
		};

		var caught = ImmutableList<string>.Empty;
		var toBench = ImmutableList<string>.Empty;
		foreach (var foe in finished.CaughtFoes())
		{
			var joining = new RunCompanion(FromFoe(foe), foe.Hp);
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
			Phase != RunPhase.Gym ? run.Walk()
			: RegionIndex + 1 >= Regions.Count ? run with { Phase = RunPhase.Won }
			: run.EnterTown(RegionIndex + 1);

		return (run, new RunReport(revived, caught, toBench, gold));
	}

	/// <summary>**A town heals everyone to full** — the bench and you too — once per region, so no stall.</summary>
	private PartyRun EnterTown(int region) =>
		this with
		{
			RegionIndex = region,
			Phase = RunPhase.Town,
			Area = null,
			Trail = [],
			StopIndex = 0,
			Sold = [],
			Team = Heal(Team, 1),
			Bench = Heal(Bench, 1),
			TrainerHp = TrainerMaxHp,
		};

	private static ImmutableList<RunCompanion> Heal(
		ImmutableList<RunCompanion> monsters,
		double share
	) =>
		[
			.. monsters.Select(m =>
				m with
				{
					Hp = Math.Min(m.Companion.Hp, m.Hp + (int)Math.Ceiling(m.Companion.Hp * share)),
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
		var rng = new Random(Seed * 31 + RegionIndex * 13 + StopIndex);
		return [.. Rewards.OrderBy(_ => rng.Next()).Take(3)];
	}

	/// <summary>The chosen card joins the deck for the rest of the run.</summary>
	public PartyRun Take(KinCard reward) => this with { Deck = Deck.Add(reward) };

	/// <summary>The town's three cards for sale — the same all visit.</summary>
	public ImmutableList<KinCard> ShopCards()
	{
		var rng = new Random(Seed * 53 + RegionIndex);
		return [.. Rewards.OrderBy(_ => rng.Next()).Take(3)];
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
