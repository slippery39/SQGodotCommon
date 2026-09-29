using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>What a find on a route holds.</summary>
public enum FindKind
{
	Gold,

	/// <summary>A quiet spot: every monster heals 30% of its max (a route's spring).</summary>
	Rest,
}

/// <summary>
/// **A wild area: its own POOL of creatures** (Shayne, 2026-09-24 — random from a pool, and the pool
/// is the area's). Choosing an area is choosing what you might catch. `Rare` appears only down the
/// deeper path.
/// </summary>
public record Area(string Name, string Description, ImmutableList<Foe> Pool, Foe Rare);

/// <summary>
/// **A region: a town, then a wild route across two areas, then its BOSS at the route's end**
/// (`KinFamiliesPlan.md`, round 2). Wild fights field `MinFoes`–`MaxFoes` creatures. **The difficulty
/// is the ELITES and the BOSS** — already scaled here to the region's tier (`PartyWorld.Tiers`); wild
/// fights are light attrition. One of the two `Bosses` is the region's, SHOWN from its town on.
/// </summary>
public record Region(
	string Name,
	ImmutableList<Area> Areas,
	ImmutableList<Encounter> Bosses,
	ImmutableList<Encounter> Elites,
	int MinFoes,
	int MaxFoes,
	int MinLevel = PartyLevels.Base,
	int MaxLevel = PartyLevels.Base,
	int EliteLevel = PartyLevels.Base,
	int BossLevel = PartyLevels.Base
)
{
	/// <summary>The rare's lair and trainers field monsters above the wild range.</summary>
	public int RareLevel => MaxLevel + 2;

	public int TrainerLevel => MaxLevel + 1;
}

/// <summary>
/// **THE MAP v1 — two regions** (Shayne: "2 regions to get a feel for the gameplay loop"). Every
/// number is a guess; exploring, not tuning.
/// </summary>
public static class PartyWorld
{
	private static Intent Attack(string name, int amount, Aim aim = Aim.Front) =>
		new()
		{
			Name = name,
			Kind = IntentType.Attack,
			Amount = amount,
			Target = aim,
		};

	private static Intent Guard(string name, int amount) =>
		new()
		{
			Name = name,
			Kind = IntentType.Block,
			Amount = amount,
		};

	private static Foe Creature(string name, int hp, params Intent[] cycle) =>
		new()
		{
			Name = name,
			Hp = hp,
			MaxHp = hp,
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
		Guard("Shell Up", 8),
		Attack("Slam", 8)
	) with
	{
		Family = Family.Grove,
		CaughtPassive = "MOSSBACK",
		CaughtRule = "All its Block is ROOTED: none of it vanishes at your turn start.",
		CaughtAbilities = [new Mossback()],
	};

	/// <summary>
	/// **Briar Viper — fast and fragile.** It strikes two columns before almost anything acts, so it
	/// asks you to kill it first or stand out of its way; then it slithers and re-aims.
	/// </summary>
	public static readonly Foe BriarViper = Creature(
		"Briar Viper",
		10,
		Attack("Strike", 6, Aim.Pierce),
		Attack("Strike", 6, Aim.Pierce),
		new Intent
		{
			Name = "Slither",
			Kind = IntentType.Move,
			Amount = -1,
		}
	) with
	{
		Family = Family.Mire,
	};

	/// <summary>**Cinder Newt — a wide spitter.** Chip across three columns, then one hot Flare.</summary>
	public static readonly Foe CinderNewt = Creature(
		"Cinder Newt",
		14,
		Attack("Spit", 3, Aim.Sweep),
		Attack("Flare", 8)
	) with
	{
		Family = Family.Ember,
		CaughtPassive = "EMBERSKIN",
		CaughtRule = "At your turn start it gains Block equal to your KINDLE.",
		CaughtAbilities = [new Emberskin()],
	};

	/// <summary>
	/// **Bog Toad — hunts the weak.** Its Tongue homes on your lowest-HP monster, so a wounded catch
	/// is a liability; its Belly Flop is three wide.
	/// </summary>
	public static readonly Foe BogToad = Creature(
		"Bog Toad",
		24,
		new Intent
		{
			Name = "Tongue",
			Kind = IntentType.Attack,
			Amount = 5,
			Target = Aim.Hunt,
		},
		Guard("Swell", 6),
		Attack("Belly Flop", 6, Aim.Sweep)
	) with
	{
		Family = Family.Mire,
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
		Family = Family.Mire,
	};

	/// <summary>
	/// **Inkling — caught: the Draw engine, pairing Draw with Surge.** The first draw each turn is
	/// an energy; Sift turns it on for nothing. Wild it is only a wide splash and an inky shell.
	/// </summary>
	public static readonly Foe Inkling = Creature(
		"Inkling",
		16,
		Attack("Splash", 2, Aim.Sweep),
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
		Family = Family.Mire,
	};

	/// <summary>
	/// **Hoard Drake — the Discard + Draw TESTER.** Wild, every draw or discard during your turn
	/// thickens its hoard, so it becomes the foe to kill first. Caught, discarding shields it — a
	/// bridge from Discard to Block.
	/// </summary>
	public static readonly Foe HoardDrake = Creature(
		"Hoard Drake",
		26,
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
		Family = Family.Mire,
	};

	// ===== SPELLCRAFT (round one)

	/// <summary>
	/// **Emberling — caught: the Spellcraft engine.** Your spells deal 2 more while it stands, on a
	/// body of 12 HP: the strategy lives in a column you have to protect. Wild, a weak chip creature.
	/// </summary>
	public static readonly Foe Emberling = Creature(
		"Emberling",
		12,
		Attack("Ember", 2),
		Attack("Ember", 2)
	) with
	{
		Family = Family.Ember,
		CaughtPassive = "STOKER",
		CaughtRule = "Every spell you play adds 2 KINDLE, not 1.",
		CaughtAbilities = [new Stoker()],
	};

	/// <summary>
	/// **Echo Owl — caught: pushed.** Speed 1, so it acts after your whole hand: its Echo repeats the
	/// last spell you cast, and the decision is which spell to cast LAST. Wild, the Echo is empty.
	/// </summary>
	public static readonly Foe EchoOwl = Creature(
		"Echo Owl",
		16,
		new Intent { Name = "Echo", Kind = IntentType.Echo },
		Attack("Peck", 3)
	) with
	{
		Family = Family.Ember,
		CaughtPassive = "ECHO",
		CaughtRule = "The first spell you play each turn is cast twice.",
		CaughtAbilities = [new EchoFirstSpell()],
	};

	/// <summary>
	/// **Warden — the Spellcraft TESTER.** Wild, spells deal half to it: can your monsters kill
	/// without your deck? Caught, every spell you play shields it — a bridge from Spellcraft to Block.
	/// </summary>
	public static readonly Foe Warden = Creature(
		"Warden",
		22,
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
		Family = Family.Storm,
	};

	// ===== SURGE (round one)

	/// <summary>
	/// **Glowmoth — caught: energy for keeping it safe.** Unhit last turn, +1 energy: protecting it
	/// IS the energy, so position pays for your hand. Wild, fast, fragile and flitting.
	/// </summary>
	public static readonly Foe Glowmoth = Creature(
		"Glowmoth",
		10,
		Attack("Dust", 1, Aim.Sweep),
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
		Family = Family.Storm,
	};

	/// <summary>
	/// **Stormbuck — caught: a kill made with your hand is energy.** Only DURING your turn — a kill at
	/// the end of the turn would bring energy with nothing to spend it on — so it bridges Surge to
	/// Hasten, Strike and spells.
	/// </summary>
	public static readonly Foe Stormbuck = Creature(
		"Stormbuck",
		20,
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
		Family = Family.Storm,
	};

	/// <summary>
	/// **Hushcap — the Surge TESTER.** Wild, your first card each turn costs 1 more: kill it first,
	/// it is fragile. Caught, the same spores work for you: your first card costs 1 less.
	/// </summary>
	public static readonly Foe Hushcap = Creature(
		"Hushcap",
		14,
		new Intent
		{
			Name = "Spores",
			Kind = IntentType.Attack,
			Amount = 3,
			Target = Aim.Hunt,
		},
		Guard("Cap", 4)
	) with
	{
		Trait = "HUSH: your first card each turn costs 1 more.",
		Components = [new FirstCardCost { Amount = 1 }],
		Family = Family.Grove,
		CaughtPassive = "SPORES",
		CaughtRule = "When a token of yours falls: draw a card and gain 1 energy.",
		CaughtAbilities = [new Spores()],
	};

	// ===== SUMMON (round one)

	/// <summary>
	/// **Broodvine — its Brood summons a Grub beside it.** Caught, the grubs are yours: bodies for
	/// Swarm and Offering. Wild, they fill the FOE row — a fight that grows, and asks for Arc.
	/// </summary>
	public static readonly Foe Broodvine = Creature(
		"Broodvine",
		24,
		new Intent
		{
			Name = "Brood",
			Kind = IntentType.Summon,
			Summons = PartyCards.Grub,
		},
		Attack("Lash", 4)
	) with
	{
		Family = Family.Grove,
		CaughtPassive = "NURSERY",
		CaughtRule = "Tokens you summon while it stands have GROW (+1 Power, +2 HP each turn).",
		CaughtAbilities = [new Nursery()],
	};

	/// <summary>**Howler — caught: the Summon engine.** Every token you summon arrives stronger.</summary>
	public static readonly Foe Howler = Creature(
		"Howler",
		20,
		Attack("Bite", 4),
		Guard("Snarl", 4)
	) with
	{
		Family = Family.Grove,
		CaughtPassive = "ALPHA",
		CaughtRule = "Your tokens arrive with +2 HP and +1 Power, and attack the front each round.",
		CaughtAbilities = [new TokenBoost { Hp = 2, Power = 1 }, new Alpha()],
	};

	/// <summary>
	/// **Ironhorn — the Summon TESTER.** Wild, TRAMPLE: what fells a monster and more comes through
	/// to you, so a wall of 3-HP Sprouts is paper. Caught, its trample carries into another foe.
	/// </summary>
	public static readonly Foe Ironhorn = Creature(
		"Ironhorn",
		24,
		Attack("Charge", 8),
		Attack("Stomp", 4, Aim.Sweep)
	) with
	{
		Trait = "TRAMPLE: damage beyond what fells a monster hits YOU.",
		Components = [new Trample()],
		Family = Family.Ember,
		CaughtPassive = "TRAMPLE",
		CaughtRule = "Damage beyond what fells a foe hits the one behind it.",
		CaughtAbilities = [new Trample()],
	};

	/// <summary>A foe's place in its line; the factory closes the line up from these in order.</summary>
	private static Foe At(Foe foe, int position) => foe with { Position = position };

	/// <summary>
	/// **A wild fight from an area's pool**: `count` foes, the `rare` first if there is one, placed
	/// front to back. Shared by the trail and the route (`PartyRoutes`).
	/// </summary>
	internal static Encounter WildFight(
		Area area,
		int count,
		Foe? rare,
		Random rng,
		int minLevel = PartyLevels.Base,
		int maxLevel = PartyLevels.Base,
		int rareLevel = PartyLevels.Base
	)
	{
		// **A FAMILY first, evenly, then a species of it** (Shayne, 2026-09-28: "uniformly mixed") —
		// so Mire's six species do not crowd out Storm's three.
		var families = area.Pool.GroupBy(f => f.Family).Select(g => g.ToList()).ToList();
		Foe Roll()
		{
			var family = families[rng.Next(families.Count)];
			return family[rng.Next(family.Count)];
		}

		var foes = Enumerable
			.Range(0, count)
			.Select(i =>
				i == 0 && rare is not null
					? PartyLevels.Scale(rare, rareLevel)
					: PartyLevels.Scale(Roll(), rng.Next(minLevel, maxLevel + 1))
			)
			.ToList();

		return new(
			"Wild " + string.Join(", ", foes.Select(f => f.Name)),
			[.. foes.Select((f, i) => At(f, i))]
		);
	}

	// ===== The areas and gyms the regions are built from. Unscaled — a region scales its copy.

	/// <summary>
	/// **Every wild species, in every area** (`KinFamiliesPlan.md`, round 2: wild foes are mixed
	/// uniformly, so a run of any family meets its own kind everywhere). An area keeps its name, its
	/// look and its RARE; the rares stay lair-only.
	/// </summary>
	private static readonly ImmutableList<Foe> Wilds =
	[
		PartyContent.Boar(0),
		Mosshell,
		Hushcap,
		Broodvine,
		CinderNewt,
		Emberling,
		Ironhorn,
		PartyContent.Stonebeak(0),
		Warden,
		Stormbuck,
		PartyContent.Wisp(0),
		BogToad,
		BriarViper,
		Magpie,
		HoardDrake,
	];

	private static readonly Area MossyHollow =
		new("Mossy Hollow", "Damp and green, moss over everything.", Wilds, Howler);

	private static readonly Area StonyRidge =
		new("Stony Ridge", "Bare rock and wind, and a long way down.", Wilds, Glowmoth);

	private static readonly Area MistyMarsh =
		new("Misty Marsh", "Fog over black water.", Wilds, Inkling);

	private static readonly Area EmberCrags =
		new("Ember Crags", "Hot stone and ash.", Wilds, EchoOwl);

	/// <summary>
	/// **A region's difficulty** (THE TUNING TABLE): foes per wild fight, the wild LEVELS (light
	/// attrition — at or under the team), and the elites' and the boss's levels, which carry the threat.
	/// Every number is a guess (exploring). The first boss is set against a starter near Lv 7.
	/// </summary>
	public record Tier(int MinFoes, int MaxFoes, int MinLevel, int MaxLevel, int Elite, int Boss);

	/// <summary>
	/// **How much tougher than its level an exam is** — HP and hits (`PartyLevels.Toughen`). Shayne,
	/// 2026-09-28: "way too easy"; the bot should win about half its runs, losing mostly to these.
	/// **HITS stay at their level** (×1): a telegraphed blow must stay ANSWERABLE by that region's
	/// cards — ×1.3 made the region-1 Tusker's Gore 29 against a 19-HP Pike, and no Guard answered it
	/// (party-sim trace, 2026-09-28). The toughness is in HP.
	/// </summary>
	public const double BossHp = 2.5,
		BossHit = 1.0,
		EliteHp = 2.4,
		EliteHit = 1.0;

	public static readonly ImmutableList<Tier> Tiers =
	[
		new(1, 2, 3, 5, 7, 8),
		new(2, 2, 7, 9, 11, 13),
		new(2, 3, 11, 13, 16, 18),
		new(2, 3, 15, 17, 21, 24),
		new(3, 3, 19, 21, 26, 29),
	];

	private static readonly ImmutableList<Encounter> Region1 =
	[
		PartyExams.OldTusker,
		PartyExams.GoblinChief,
	];
	private static readonly ImmutableList<Encounter> Region1Elites =
	[
		PartyExams.IronSentinel,
		PartyExams.GoblinRaiders,
	];
	private static readonly ImmutableList<Encounter> Region2 =
	[
		PartyExams.OldMire,
		PartyExams.BlackKnight,
	];
	private static readonly ImmutableList<Encounter> Region2Elites =
	[
		PartyExams.HexerAndGolem,
		PartyExams.HarpyFlock,
	];
	private static readonly ImmutableList<Encounter> AllElites =
	[
		.. Region1Elites,
		.. Region2Elites,
	];

	/// <summary>
	/// **THE MAP — five regions, a boss each** (Shayne, 2026-09-28). Two bosses per region, one of them
	/// the region's; the elites are drawn from the region's pool.
	/// </summary>
	public static readonly ImmutableList<Region> Regions =
	[
		Build(0, "The Greenwood", MossyHollow, StonyRidge, Region1, Region1Elites),
		Build(1, "The Mirelands", MistyMarsh, EmberCrags, Region2, Region2Elites),
		// Regions 3–5 are PLACEHOLDERS until their exams are designed: region 2's, at their levels.
		Build(2, "The Stonefells", StonyRidge, EmberCrags, Region2, AllElites),
		Build(3, "The Deepwood", MossyHollow, MistyMarsh, Region2, AllElites),
		Build(4, "The Wyrm's Rest", EmberCrags, MistyMarsh, Region2, AllElites),
	];

	/// <summary>
	/// **A region**: its areas' pools stay at their BASE — a route scales each foe to the level it
	/// rolls (`PartyRoutes`) — and its bosses and elites are scaled here. None can be caught.
	/// </summary>
	private static Region Build(
		int tier,
		string name,
		Area a,
		Area b,
		ImmutableList<Encounter> bosses,
		ImmutableList<Encounter> elites
	)
	{
		var t = Tiers[tier];
		ImmutableList<Encounter> Scaled(
			IEnumerable<Encounter> lines,
			int level,
			double hp,
			double hit
		) =>
			[
				.. lines.Select(line => new Encounter(
					line.Name,
					[
						.. line.Foes.Select(f =>
							PartyLevels.Toughen(PartyLevels.Scale(f, level), hp, hit)
						),
					]
				)),
			];
		return new Region(
			name,
			[a, b],
			Scaled(bosses, t.Boss, BossHp, BossHit),
			Scaled(elites, t.Elite, EliteHp, EliteHit),
			t.MinFoes,
			t.MaxFoes,
			t.MinLevel,
			t.MaxLevel,
			t.Elite,
			t.Boss
		);
	}

	/// <summary>
	/// **A creature's BASE, by name** — what a caught one's stats grow from. Null for a name no area
	/// knows (a test's inline foe), which is then its own base.
	/// </summary>
	public static Foe? Species(string name) =>
		new[] { MossyHollow, StonyRidge, MistyMarsh, EmberCrags }
			.SelectMany(a => a.Pool.Append(a.Rare))
			.FirstOrDefault(f => f.Name == name);
}
