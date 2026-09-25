using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>What a stop on an area's trail is.</summary>
public enum StopKind
{
	/// <summary>A wild fight — its foes can be caught.</summary>
	Battle,

	/// <summary>Something lying on the path: see <see cref="FindKind"/>.</summary>
	Find,

	/// <summary>**The deeper path**: optional, harder, and the only place the area's rare lives.</summary>
	Deep,
}

public enum FindKind
{
	Snare,
	Gold,

	/// <summary>A quiet spot: every monster heals 30% of its max.</summary>
	Rest,
}

/// <summary>One stop on a trail. A battle or deep stop carries its fight; a find, what is found.</summary>
public record Stop(StopKind Kind, Encounter? Encounter = null, FindKind Find = FindKind.Snare);

/// <summary>
/// **A wild area: its own POOL of creatures** (Shayne, 2026-09-24 — random from a pool, and the pool
/// is the area's). Choosing an area is choosing what you might catch. `Rare` appears only down the
/// deeper path.
/// </summary>
public record Area(string Name, string Description, ImmutableList<Foe> Pool, Foe Rare);

/// <summary>
/// **A region: a town, then one of two wild areas, then its gym** (KinJam.md "THE MAP"). Wild fights
/// field between `MinFoes` and `MaxFoes` creatures from the chosen area's pool. Its areas and gym are
/// already scaled to the region's DIFFICULTY TIER (`PartyWorld.Tiers`).
/// </summary>
public record Region(
	string Name,
	ImmutableList<Area> Areas,
	Encounter Gym,
	int MinFoes,
	int MaxFoes
);

/// <summary>
/// **THE MAP v1 — two regions** (Shayne: "2 regions to get a feel for the gameplay loop"). Every
/// number is a guess; exploring, not tuning.
/// </summary>
public static class PartyWorld
{
	private static readonly ImmutableList<int> Ahead = [0];
	private static readonly ImmutableList<int> ThreeWide = [-1, 0, 1];

	private static Intent Attack(string name, int amount, ImmutableList<int>? offsets = null) =>
		new()
		{
			Name = name,
			Kind = IntentType.Attack,
			Amount = amount,
			Offsets = offsets ?? Ahead,
		};

	private static Intent Guard(string name, int amount) =>
		new()
		{
			Name = name,
			Kind = IntentType.Block,
			Amount = amount,
		};

	private static Foe Creature(string name, int hp, int speed, params Intent[] cycle) =>
		new()
		{
			Name = name,
			Hp = hp,
			MaxHp = hp,
			Speed = speed,
			Pattern = [.. cycle],
		};

	// ===== New creatures. Each asks something different of the board.

	/// <summary>
	/// **Mosshell — slow, armoured, one big blow.** Its Shell Up soaks a whole turn of chip damage,
	/// so it asks for the damage to land on the Slam turn (Rally, Hasten) — or for a Stagger.
	/// </summary>
	public static readonly Foe Mosshell = Creature(
		"Mosshell",
		26,
		1,
		Guard("Shell Up", 8),
		Attack("Slam", 8)
	);

	/// <summary>
	/// **Briar Viper — fast and fragile.** It strikes two columns before almost anything acts, so it
	/// asks you to kill it first or stand out of its way; then it slithers and re-aims.
	/// </summary>
	public static readonly Foe BriarViper = Creature(
		"Briar Viper",
		10,
		3,
		Attack("Strike", 6, [0, 1]),
		Attack("Strike", 6, [0, 1]),
		new Intent
		{
			Name = "Slither",
			Kind = IntentType.Move,
			Amount = -1,
		}
	);

	/// <summary>**Cinder Newt — a wide spitter.** Chip across three columns, then one hot Flare.</summary>
	public static readonly Foe CinderNewt = Creature(
		"Cinder Newt",
		14,
		2,
		Attack("Spit", 3, ThreeWide),
		Attack("Flare", 8)
	);

	/// <summary>
	/// **Bog Toad — hunts the weak.** Its Tongue homes on your lowest-HP monster, so a wounded catch
	/// is a liability; its Belly Flop is three wide.
	/// </summary>
	public static readonly Foe BogToad = Creature(
		"Bog Toad",
		24,
		1,
		new Intent
		{
			Name = "Tongue",
			Kind = IntentType.Attack,
			Amount = 5,
			Homing = true,
		},
		Guard("Swell", 6),
		Attack("Belly Flop", 6, ThreeWide)
	);

	/// <summary>
	/// **The Old Mire — the second gym, an exam of the whole row.** Deluge covers all five columns, so
	/// nobody steps out of it — Block, or kill it first. Swallow is huge and one wide.
	/// </summary>
	public static readonly Foe OldMire = Creature(
		"Old Mire",
		64,
		1,
		Attack("Deluge", 5, [-2, -1, 0, 1, 2]),
		Attack("Swallow", 15),
		Guard("Wallow", 12)
	) with
	{
		Catchable = false,
	};

	// ===== DISCARD + DRAW (round one, docs/paper/round-one-synergies.md). Each is a question when
	// wild and an engine when caught; the tester, caught, becomes a bridge for what it punished.

	/// <summary>
	/// **Magpie — wild: a THIEF** (its Snatch takes the top card of your draw pile until it is
	/// beaten). **Caught: the Discard engine** — every discard throws 2 at a random foe.
	/// </summary>
	public static readonly Foe Magpie = Creature(
		"Magpie",
		14,
		3,
		Attack("Snatch", 3) with
		{
			Steals = true,
		},
		new Intent
		{
			Name = "Hop",
			Kind = IntentType.Move,
			Amount = 1,
		}
	) with
	{
		CaughtPassive = "SCAVENGER",
		CaughtRule = "When you discard a card, 2 damage to a random foe.",
		CaughtAbilities =
		[
			new Trigger
			{
				Name = "Scavenger",
				When = new OnCardDiscarded(),
				Effects = [new DamageRandomFoeAction { Amount = 2 }],
			},
		],
		CaughtCards = [PartyCards.Sift, PartyCards.Rummage],
	};

	/// <summary>
	/// **Inkling — caught: the Draw engine, pairing Draw with Surge.** The first draw each turn is
	/// an energy; Sift turns it on for nothing. Wild it is only a wide splash and an inky shell.
	/// </summary>
	public static readonly Foe Inkling = Creature(
		"Inkling",
		16,
		2,
		Attack("Splash", 2, ThreeWide),
		Guard("Ink", 4)
	) with
	{
		CaughtPassive = "INKWELL",
		CaughtRule = "The first time a card draws each turn, +1 energy.",
		CaughtAbilities =
		[
			new Trigger
			{
				Name = "Inkwell",
				When = new OnCardsDrawn(),
				Effects = [new GainEnergyAction()],
				MaxPerTurn = 1,
			},
		],
		CaughtCards = [PartyCards.Sift, PartyCards.Jot],
	};

	/// <summary>
	/// **Hoard Drake — the Discard + Draw TESTER.** Wild, every draw or discard during your turn
	/// thickens its hoard, so it becomes the foe to kill first. Caught, discarding shields it — a
	/// bridge from Discard to Block.
	/// </summary>
	public static readonly Foe HoardDrake = Creature(
		"Hoard Drake",
		26,
		1,
		Guard("Hoard", 6),
		Attack("Tail", 7)
	) with
	{
		Trait = "HOARD: gains 2 Block whenever you draw or discard during your turn.",
		Components =
		[
			new Trigger
			{
				Name = "Hoard",
				When = new OnDrawOrDiscard(),
				Effects = [new GainBlockAction { Amount = 2 }],
			},
		],
		CaughtPassive = "HOARDER",
		CaughtRule = "When you discard a card, it gains 3 Block.",
		CaughtAbilities =
		[
			new Trigger
			{
				Name = "Hoarder",
				When = new OnCardDiscarded(),
				Effects = [new GainBlockAction { Amount = 3 }],
			},
		],
		CaughtCards = [PartyCards.Ration, PartyCards.Sift],
	};

	// ===== SPELLCRAFT (round one)

	/// <summary>
	/// **Emberling — caught: the Spellcraft engine.** Your spells deal 2 more while it stands, on a
	/// body of 12 HP: the strategy lives in a column you have to protect. Wild, a weak chip creature.
	/// </summary>
	public static readonly Foe Emberling = Creature(
		"Emberling",
		12,
		2,
		Attack("Ember", 2),
		Attack("Ember", 2)
	) with
	{
		CaughtPassive = "KINDLING +2",
		CaughtRule = "Your spells deal 2 more while it stands.",
		CaughtAbilities = [new SpellPower { Amount = 2 }],
		CaughtCards = [PartyCards.Zap, PartyCards.SparkScroll],
	};

	/// <summary>
	/// **Echo Owl — caught: pushed.** Speed 1, so it acts after your whole hand: its Echo repeats the
	/// last spell you cast, and the decision is which spell to cast LAST. Wild, the Echo is empty.
	/// </summary>
	public static readonly Foe EchoOwl = Creature(
		"Echo Owl",
		16,
		1,
		new Intent { Name = "Echo", Kind = IntentType.Echo },
		Attack("Peck", 3)
	) with
	{
		// No passive line: its power IS its move, and the move already says it (the cell showed "ECHO" twice).
		CaughtCards = [PartyCards.Zap, PartyCards.Arc],
	};

	/// <summary>
	/// **Warden — the Spellcraft TESTER.** Wild, spells deal half to it: can your monsters kill
	/// without your deck? Caught, every spell you play shields it — a bridge from Spellcraft to Block.
	/// </summary>
	public static readonly Foe Warden = Creature(
		"Warden",
		22,
		1,
		Guard("Shell", 6),
		Attack("Slam", 8)
	) with
	{
		Trait = "WARD: spells deal half to it.",
		Components = [new SpellWard()],
		CaughtPassive = "SPELLGUARD",
		CaughtRule = "When you play a spell, it gains 3 Block.",
		CaughtAbilities =
		[
			new Trigger
			{
				Name = "Spellguard",
				When = new OnSpellPlayed(),
				Effects = [new GainBlockAction { Amount = 3 }],
			},
		],
		CaughtCards = [PartyCards.Zap, PartyCards.Jot],
	};

	// ===== SURGE (round one)

	/// <summary>
	/// **Glowmoth — caught: energy for keeping it safe.** Unhit last turn, +1 energy: protecting it
	/// IS the energy, so position pays for your hand. Wild, fast, fragile and flitting.
	/// </summary>
	public static readonly Foe Glowmoth = Creature(
		"Glowmoth",
		10,
		3,
		Attack("Dust", 1, ThreeWide),
		new Intent
		{
			Name = "Flutter",
			Kind = IntentType.Move,
			Amount = -1,
		}
	) with
	{
		CaughtPassive = "GLOW +1",
		CaughtRule = "Unhit last turn: +1 energy at the start of your turn.",
		CaughtAbilities = [new EnergyIfUnhit { Amount = 1 }],
		CaughtCards = [PartyCards.Surge, PartyCards.Quicken],
	};

	/// <summary>
	/// **Stormbuck — caught: a kill made with your hand is energy.** Only DURING your turn — a kill at
	/// the end of the turn would bring energy with nothing to spend it on — so it bridges Surge to
	/// Hasten, Strike and spells.
	/// </summary>
	public static readonly Foe Stormbuck = Creature(
		"Stormbuck",
		20,
		2,
		Attack("Antler", 5),
		Guard("Rear", 4)
	) with
	{
		CaughtPassive = "STORM",
		CaughtRule = "When a foe dies during your turn, +1 energy.",
		CaughtAbilities =
		[
			new Trigger
			{
				Name = "Storm",
				When = new OnFoeDefeatedDuringYourTurn(),
				Effects = [new GainEnergyAction()],
			},
		],
		CaughtCards = [PartyCards.BattleCry, PartyCards.Zap],
	};

	/// <summary>
	/// **Hushcap — the Surge TESTER.** Wild, your first card each turn costs 1 more: kill it first,
	/// it is fragile. Caught, the same spores work for you: your first card costs 1 less.
	/// </summary>
	public static readonly Foe Hushcap = Creature(
		"Hushcap",
		14,
		2,
		new Intent
		{
			Name = "Spores",
			Kind = IntentType.Attack,
			Amount = 3,
			Homing = true,
		},
		Guard("Cap", 4)
	) with
	{
		Trait = "HUSH: your first card each turn costs 1 more.",
		Components = [new FirstCardCost { Amount = 1 }],
		CaughtPassive = "HUSH",
		CaughtRule = "Your first card each turn costs 1 less.",
		CaughtAbilities = [new FirstCardCost { Amount = -1 }],
		CaughtCards = [PartyCards.Quicken, PartyCards.Surge],
	};

	// ===== SUMMON (round one)

	/// <summary>
	/// **Broodvine — its Brood summons a Grub beside it.** Caught, the grubs are yours: bodies for
	/// Swarm and Offering. Wild, they fill the FOE row — a fight that grows, and asks for Arc.
	/// </summary>
	public static readonly Foe Broodvine = Creature(
		"Broodvine",
		24,
		1,
		new Intent
		{
			Name = "Brood",
			Kind = IntentType.Summon,
			Summons = PartyCards.Grub,
		},
		Attack("Lash", 4)
	) with
	{
		CaughtPassive = "BROOD",
		CaughtRule = "Its Brood summons a Grub (2 HP) beside it, for 2 turns.",
		CaughtCards = [PartyCards.Sow, PartyCards.CallSparks],
	};

	/// <summary>**Howler — caught: the Summon engine.** Every token you summon arrives stronger.</summary>
	public static readonly Foe Howler = Creature(
		"Howler",
		20,
		2,
		Attack("Bite", 4),
		Guard("Snarl", 4)
	) with
	{
		CaughtPassive = "PACK +2/+1",
		CaughtRule = "Your tokens arrive with +2 HP and +1 Power.",
		CaughtAbilities = [new TokenBoost { Hp = 2, Power = 1 }],
		CaughtCards = [PartyCards.CallSparks, PartyCards.DecoyCard],
	};

	/// <summary>
	/// **Ironhorn — the Summon TESTER.** Wild, TRAMPLE: what fells a monster and more comes through
	/// to you, so a wall of 3-HP Sprouts is paper. Caught, its trample carries into another foe.
	/// </summary>
	public static readonly Foe Ironhorn = Creature(
		"Ironhorn",
		24,
		1,
		Attack("Charge", 8),
		Attack("Stomp", 4, ThreeWide)
	) with
	{
		Trait = "TRAMPLE: damage beyond what fells a monster hits YOU.",
		Components = [new Trample()],
		CaughtPassive = "TRAMPLE",
		CaughtRule = "Damage beyond what fells a foe hits a random other foe.",
		CaughtAbilities = [new Trample()],
		CaughtCards = [PartyCards.Sow, PartyCards.Jot],
	};

	private static Foe At(Foe foe, int space) => foe with { Space = space };

	/// <summary>
	/// **The leader's health in a gym, before the tier.** Was 35: the first good-bot sim won nine gyms
	/// in ten by racing it (docs/findings/companion-balance.md). Scales with the tier's HP.
	/// </summary>
	public const int LeaderHp = 90;

	// ===== The areas and gyms the regions are built from. Unscaled — a region scales its copy.

	private static readonly Area MossyHollow =
		new(
			"Mossy Hollow",
			"Damp and green. Boars root here; wisps drift between the trees.",
			[PartyContent.Boar(0), PartyContent.Wisp(0), Mosshell, Hushcap, Broodvine],
			Howler
		);

	private static readonly Area StonyRidge =
		new(
			"Stony Ridge",
			"Bare rock and wind. Stonebeaks nest on the crags; a warden keeps the pass.",
			[PartyContent.Stonebeak(0), PartyContent.Boar(0), CinderNewt, Warden, Stormbuck],
			Glowmoth
		);

	private static readonly Area MistyMarsh =
		new(
			"Misty Marsh",
			"Fog over black water. Toads, thieving magpies, and a drake on its hoard.",
			[BogToad, PartyContent.Wisp(0), BriarViper, Magpie, HoardDrake],
			Inkling
		);

	private static readonly Area EmberCrags =
		new(
			"Ember Crags",
			"Hot stone and ash. Newts in every crack, embers that bite, and an owl that answers back.",
			[CinderNewt, PartyContent.Stonebeak(0), Mosshell, Emberling, Ironhorn],
			EchoOwl
		);

	private static readonly Encounter TuskerGym =
		new("The Old Tusker", [PartyContent.OldTusker(2), At(PartyContent.Wisp(0), 4)]);

	private static readonly Encounter MireGym =
		new("The Old Mire", [At(BogToad, 0), At(OldMire, 2), At(BriarViper, 4)]);

	private static readonly Encounter LastGym =
		new("The Last Stand", [At(PartyContent.OldTusker(0), 1), At(OldMire, 3)]);

	/// <summary>
	/// **A region's difficulty: foes per wild fight, and how much foes' HP and damage are multiplied.**
	/// THE TUNING TABLE — the curve (90% through region 3, 75% through region 5, 25% win, Shayne,
	/// 2026-09-24) is hit by changing these, measured with `party-sim` against `PartySim.Target`.
	/// </summary>
	public record Tier(int MinFoes, int MaxFoes, double Hp, double Damage);

	public static readonly ImmutableList<Tier> Tiers =
	[
		new(1, 2, 1.00, 1.00),
		new(2, 3, 1.00, 1.15),
		new(3, 4, 1.30, 1.60),
		new(4, 4, 1.50, 1.80),
		new(4, 5, 1.65, 2.00),
		new(4, 5, 1.80, 2.30),
		new(4, 5, 2.00, 2.60),
		new(4, 5, 2.15, 2.80),
		new(4, 5, 2.25, 2.75),
		new(4, 5, 2.50, 3.30),
	];

	/// <summary>
	/// **THE MAP — ten regions.** Regions 3-10 reuse the four areas and two gyms, scaled by their tier;
	/// new creatures come once the curve is settled. The last gym fields both old bosses.
	/// </summary>
	public static readonly ImmutableList<Region> Regions =
	[
		Build(0, "The Greenwood", MossyHollow, StonyRidge, TuskerGym),
		Build(1, "The Mirelands", MistyMarsh, EmberCrags, MireGym),
		Build(2, "The Stonefells", StonyRidge, EmberCrags, TuskerGym),
		Build(3, "The Deepwood", MossyHollow, MistyMarsh, MireGym),
		Build(4, "The Emberwastes", EmberCrags, StonyRidge, TuskerGym),
		Build(5, "The Sunken Vale", MistyMarsh, MossyHollow, MireGym),
		Build(6, "The Thornmarch", MossyHollow, StonyRidge, TuskerGym),
		Build(7, "The Ashen Steppe", EmberCrags, MistyMarsh, MireGym),
		Build(8, "The High Crag", StonyRidge, EmberCrags, TuskerGym),
		Build(9, "The Wyrm's Rest", MistyMarsh, MossyHollow, LastGym),
	];

	private static Region Build(int tier, string name, Area a, Area b, Encounter gym)
	{
		var t = Tiers[tier];
		Area Scaled(Area area) =>
			area with
			{
				Pool = [.. area.Pool.Select(f => Scale(f, t))],
				Rare = Scale(area.Rare, t),
			};
		return new(
			name,
			[Scaled(a), Scaled(b)],
			new(
				gym.Name,
				[.. gym.Foes.Select(f => Scale(f, t) with { Catchable = false })],
				(int)Math.Round(LeaderHp * t.Hp)
			),
			t.MinFoes,
			t.MaxFoes
		);
	}

	/// <summary>
	/// **A creature at a tier**: HP and Block by the tier's HP, attacks by its damage. A creature caught
	/// here keeps it — a region-6 Boar is a stronger catch than a region-1 one.
	/// </summary>
	public static Foe Scale(Foe foe, Tier tier)
	{
		var hp = (int)Math.Round(foe.MaxHp * tier.Hp);
		return foe with
		{
			Hp = hp,
			MaxHp = hp,
			Pattern =
			[
				.. foe.Pattern.Select(i =>
					i.Kind switch
					{
						IntentType.Attack => i with
						{
							Amount = Math.Max(1, (int)Math.Round(i.Amount * tier.Damage)),
						},
						IntentType.Block => i with { Amount = (int)Math.Round(i.Amount * tier.Hp) },
						_ => i,
					}
				),
			],
		};
	}

	/// <summary>
	/// **An area's trail**: two wild fights from its pool, a find, then the optional deeper path —
	/// one more foe than a wild fight, and the area's rare among them. Seeded, so a run replays.
	/// </summary>
	public static ImmutableList<Stop> Trail(Region region, Area area, Random rng)
	{
		Encounter Fight(int count, Foe? rare)
		{
			var foes = Enumerable
				.Range(0, count)
				.Select(i =>
					i == 0 && rare is not null ? rare : area.Pool[rng.Next(area.Pool.Count)]
				)
				.ToList();
			var spaces = PartyRun.Formation(count);
			return new(
				"Wild " + string.Join(", ", foes.Select(f => f.Name)),
				[.. foes.Select((f, i) => At(f, spaces[i]))]
			);
		}

		var wild = () => Fight(rng.Next(region.MinFoes, region.MaxFoes + 1), null);
		return
		[
			new(StopKind.Battle, wild()),
			new(StopKind.Battle, wild()),
			new(StopKind.Find, Find: (FindKind)rng.Next(3)),
			new(StopKind.Deep, Fight(Math.Min(PartyBattle.Spaces, region.MaxFoes + 1), area.Rare)),
		];
	}
}
