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
/// **A region: a town, a wild route, then its BOSS** (`KinEnemiesPlan.md`, 2026-10-03). Its foes are
/// AUTHORED at its strength — no levels — and its wild fights are AUTHORED groups: the route's first
/// fights draw from `Easy`, the rest from `Normal`, so each encounter can be judged on its own.
/// </summary>
public record Region(
	string Name,
	ImmutableList<Encounter> Bosses,
	ImmutableList<Encounter> Elites,
	ImmutableList<Encounter> Easy,
	ImmutableList<Encounter> Normal
);

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
		Trait = "SWELLS: its attacks deal 1 more every round.",
		Components = [new Enrage { PerRound = 1 }],
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
	/// <summary>
	/// **Rock Mite — CRUSH done right** (playtest 2026-10-02): FRAGILE, and a SMALL crush every turn.
	/// Kill it first, or ignore it and take a little guaranteed damage — never a run-ender.
	/// </summary>
	public static readonly Foe RockMite = Creature(
		"Rock Mite",
		10,
		Attack("Bore", 3) with
		{
			Crushes = true,
		}
	) with
	{
		Trait = "CRUSH: its Bore ignores Block.",
		Family = Family.Mire,
	};

	public static readonly Foe HoardDrake = Creature(
		"Hoard Drake",
		26,
		Guard("Hoard", 6),
		Attack("Tail", 7)
	) with
	{
		// No CRUSH on 26 HP (2026-10-02): CRUSH is for fragile foes only.
		Trait = "HOARD: gains 2 Block when you draw or discard in your turn.",
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
		Trait = "RUT: its attacks deal 1 more every round.",
		Components = [new Enrage { PerRound = 1 }],
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

	// ===== THE REGIONS (`KinEnemiesPlan.md`, 2026-10-03) — three now, five later

	/// <summary>A wild fight: these foes, front to back.</summary>
	private static Encounter Wild(params Foe[] foes) =>
		new(
			"Wild " + string.Join(", ", foes.Select(f => f.Name).Distinct()),
			[.. foes.Select((f, i) => At(f, i))]
		);

	/// <summary>
	/// **PLACEHOLDER strength for regions 2–3**: the old species and region 2's exams, every HP, attack
	/// and Block times `by` (the agreed curve: region 2 ×1.6, region 3 ×2.5 of region 1).
	/// ponytail: a multiplier, not authored numbers — delete once regions 2–3 have their own rosters.
	/// </summary>
	private static Foe Stronger(Foe foe, double by)
	{
		int Times(int amount) => amount <= 0 ? amount : (int)Math.Round(amount * by);
		ImmutableList<Intent> Harder(ImmutableList<Intent> moves) =>
			[
				.. moves.Select(i =>
					i.Kind is IntentType.Attack or IntentType.Block
						? i with
						{
							Amount = Times(i.Amount),
						}
						: i
				),
			];
		return foe with
		{
			Hp = Times(foe.MaxHp),
			MaxHp = Times(foe.MaxHp),
			Pattern = Harder(foe.Pattern),
			Components =
			[
				.. foe.Components.Select(c =>
					c is Phase phase
						? phase with
						{
							Pattern = Harder(phase.Pattern),
							Block = Times(phase.Block),
						}
						: c
				),
			],
		};
	}

	private static Encounter Stronger(Encounter fight, double by) =>
		new(fight.Name, [.. fight.Foes.Select(f => Stronger(f, by))]);

	private const double MirelandsStrength = 1.6,
		StonefellsStrength = 2.5;

	private static Encounter Stone(params Foe[] foes) => Stronger(Wild(foes), StonefellsStrength);

	private static readonly Foe Stonebeak = PartyContent.Stonebeak(0);

	private static readonly ImmutableList<Encounter> Region2Bosses =
	[
		PartyExams.OldMire,
		PartyExams.BlackKnight,
	];
	private static readonly ImmutableList<Encounter> Region2Elites =
	[
		PartyExams.HexerAndGolem,
		PartyExams.HarpyFlock,
	];

	/// <summary>
	/// **THE MAP — three regions, a boss each.** The Greenwood is authored (`PartyGreenwood`); the
	/// Mirelands keep their exams, and both later regions' wild fights are PLACEHOLDERS from the old
	/// species until their factions exist (hags and the dead, giants and kobolds — junk cards and
	/// debuffs first). Region 3's exams are region 2's, made stronger, until its own are designed.
	/// </summary>
	public static readonly ImmutableList<Region> Regions =
	[
		new(
			"The Greenwood",
			[PartyExams.OldTusker, PartyExams.GoblinChief],
			[PartyExams.IronSentinel, PartyExams.GoblinRaiders],
			PartyGreenwood.Easy,
			PartyGreenwood.Normal
		),
		new(
			"The Mirelands",
			Region2Bosses,
			Region2Elites,
			PartyMirelands.Easy,
			PartyMirelands.Normal
		),
		new(
			"The Stonefells",
			[.. Region2Bosses.Select(b => Stronger(b, StonefellsStrength / MirelandsStrength))],
			[.. Region2Elites.Select(e => Stronger(e, StonefellsStrength / MirelandsStrength))],
			[Stone(Stonebeak), Stone(Stormbuck), Stone(Warden)],
			[
				Stone(Stonebeak, Stormbuck),
				Stone(Warden, Stonebeak),
				Stone(Warden, Stormbuck, Stonebeak),
				Stone(HoardDrake, Stonebeak),
				Stone(Stonebeak, Stonebeak, Warden),
				Stone(Stormbuck, Warden),
			]
		),
	];
}
