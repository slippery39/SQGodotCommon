using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **GROVE's tokens, cards and monsters — DRAFT 1, as approved** (`KinFamiliesPlan.md`; Shayne,
/// 2026-09-29: "good as a first pass, we will see how it plays out"). Pushed first; every number is a
/// guess to tune after play. The rules are in `PartyGrove` and `PartyFamilies`.
/// </summary>
public static class GroveCards
{
	private static KinCard Card(
		string name,
		int cost,
		Rarity rarity,
		string text,
		params GameAction[] steps
	) => PartyCards.Card(name, cost, text, steps) with { Family = Family.Grove, Rarity = rarity };

	private static KinCard Plus(KinCard card, KinCard better) => PartyCards.Plus(card, better);

	private const Rarity C = Rarity.Common;
	private const Rarity U = Rarity.Uncommon;
	private const Rarity R = Rarity.Rare;

	// ===== Tokens

	private static readonly ImmutableList<Intent> NoMoves =
	[
		new Intent { Name = "Wait", Kind = IntentType.Block },
	];

	private static TokenTemplate Token(
		string name,
		int hp,
		int power,
		int fadesIn = 0,
		params GameComponent[] abilities
	) =>
		new(
			new PartyCompanion(name, hp, power, NoMoves)
			{
				Family = Family.Grove,
				Abilities = [.. abilities],
			},
			fadesIn
		);

	/// <summary>Cheap, and gone at your next turn: a wall for one hit, or fuel.</summary>
	public static readonly TokenTemplate Seedling = Token("Seedling", 4, 0, fadesIn: 1);

	public static readonly TokenTemplate Sprout = Token("Sprout", 6, 1);

	public static readonly TokenTemplate Log = Token("Log", 12, 0, 0, new DrawOnFall { Count = 2 });
	public static readonly TokenTemplate BigLog = Token(
		"Log",
		16,
		0,
		0,
		new DrawOnFall { Count = 2 }
	);

	public static readonly TokenTemplate Treant = Token("Treant", 20, 3);
	public static readonly TokenTemplate BigTreant = Token("Treant", 26, 4);

	// ===== Commons

	public static readonly KinCard Root = Plus(
		Card("Root", 1, C, "Gain 8 Rooted Block.", new RootAction { Amount = 8 }),
		Card("Root", 1, C, "Gain 11 Rooted Block.", new RootAction { Amount = 11 })
	);

	public static readonly KinCard Sow = Plus(
		Card(
			"Sow",
			1,
			C,
			"Summon a Sprout (6 HP, 1 Power). Draw a card.",
			new SummonTokenAction { Token = Sprout },
			new DrawAction()
		),
		Card(
			"Sow",
			0,
			C,
			"Summon a Sprout (6 HP, 1 Power). Draw a card.",
			new SummonTokenAction { Token = Sprout },
			new DrawAction()
		)
	);

	public static readonly KinCard Seedlings = Plus(
		Card(
			"Seedlings",
			0,
			C,
			"Summon 2 Seedlings (4 HP). They wither at your next turn.",
			new SummonTokenAction { Token = Seedling, Count = 2 }
		),
		Card(
			"Seedlings",
			0,
			C,
			"Summon 3 Seedlings (4 HP). They wither at your next turn.",
			new SummonTokenAction { Token = Seedling, Count = 3 }
		)
	);

	public static readonly KinCard Bristle = Plus(
		Card("Bristle", 1, C, "8 Thorns this turn.", new ThornsAction { Amount = 8 }),
		Card("Bristle", 1, C, "12 Thorns this turn.", new ThornsAction { Amount = 12 })
	);

	public static readonly KinCard BarkSlam = Plus(
		Card(
			"Bark Slam",
			1,
			C,
			"It attacks their front for its Block + Power.",
			new StrikeAction { PerBlock = 1 }
		),
		Card(
			"Bark Slam",
			0,
			C,
			"It attacks their front for its Block + Power.",
			new StrikeAction { PerBlock = 1 }
		)
	);

	public static readonly KinCard ThornLash = Plus(
		Card(
			"Thorn Lash",
			1,
			C,
			"It attacks their front for 4 + its Thorns + Power.",
			new StrikeAction { Amount = 4, PerThorns = 1 }
		),
		Card(
			"Thorn Lash",
			1,
			C,
			"It attacks their front for 6 + its Thorns + Power.",
			new StrikeAction { Amount = 6, PerThorns = 1 }
		)
	);

	public static readonly KinCard Overgrow = Plus(
		Card("Overgrow", 1, C, "It grows 3.", new GrowAction { Amount = 3 }),
		Card("Overgrow", 1, C, "It grows 4.", new GrowAction { Amount = 4 })
	);

	public static readonly KinCard Thicket = Plus(
		Card(
			"Thicket",
			1,
			C,
			"Every creature on your line gains 5 Block.",
			new GroveBlockAction { Amount = 5, AllLine = true }
		),
		Card(
			"Thicket",
			1,
			C,
			"Every creature on your line gains 7 Block.",
			new GroveBlockAction { Amount = 7, AllLine = true }
		)
	);

	public static readonly KinCard Hardwood = Plus(
		Card(
			"Hardwood",
			1,
			C,
			"Gain 7 Block. If it already had Block, 7 more.",
			new GroveBlockAction { Amount = 7, IfHadBlock = 7 }
		),
		Card(
			"Hardwood",
			1,
			C,
			"Gain 9 Block. If it already had Block, 9 more.",
			new GroveBlockAction { Amount = 9, IfHadBlock = 9 }
		)
	);

	public static readonly KinCard Compost = Plus(
		Card(
			"Compost",
			0,
			C,
			"Sacrifice a token: draw 2 and gain 1 energy.",
			new SacrificeTokenAction(),
			new DrawAction { Count = 2 },
			new GainEnergyAction()
		),
		Card(
			"Compost",
			0,
			C,
			"Sacrifice a token: draw 3 and gain 1 energy.",
			new SacrificeTokenAction(),
			new DrawAction { Count = 3 },
			new GainEnergyAction()
		)
	);

	public static readonly KinCard Barkskin = Plus(
		Card(
			"Barkskin",
			1,
			C,
			"Gain Block equal to three times its Power.",
			new GroveBlockAction { PerPower = 3 }
		),
		Card(
			"Barkskin",
			1,
			C,
			"Gain Block equal to four times its Power.",
			new GroveBlockAction { PerPower = 4 }
		)
	);

	// ===== Uncommons

	public static readonly KinCard DeepRoots = Plus(
		Card("Deep Roots", 1, U, "Its Rooted Block doubles.", new DeepRootsAction()),
		Card("Deep Roots", 0, U, "Its Rooted Block doubles.", new DeepRootsAction())
	);

	public static readonly KinCard Ironbark = Plus(
		Card("Ironbark", 2, U, "Gain 16 Rooted Block.", new RootAction { Amount = 16 }),
		Card("Ironbark", 2, U, "Gain 22 Rooted Block.", new RootAction { Amount = 22 })
	);

	public static readonly KinCard BraceRoots = Plus(
		Card(
			"Brace Roots",
			1,
			U,
			"Gain 4 Block. All its Block becomes Rooted.",
			new GroveBlockAction { Amount = 4, RootAll = true }
		),
		Card(
			"Brace Roots",
			1,
			U,
			"Gain 7 Block. All its Block becomes Rooted.",
			new GroveBlockAction { Amount = 7, RootAll = true }
		)
	);

	public static readonly KinCard BriarPatch = Plus(
		Card(
			"Briar Patch",
			1,
			U,
			"Every creature on your line gains 5 Thorns this turn.",
			new ThornsAction { Amount = 5, AllLine = true }
		),
		Card(
			"Briar Patch",
			1,
			U,
			"Every creature on your line gains 7 Thorns this turn.",
			new ThornsAction { Amount = 7, AllLine = true }
		)
	);

	public static readonly KinCard Needles = Plus(
		Card(
			"Needles",
			1,
			U,
			"+3 Thorns for the rest of the fight.",
			new ThornsAction { Amount = 3, ForFight = true }
		),
		Card(
			"Needles",
			1,
			U,
			"+4 Thorns for the rest of the fight.",
			new ThornsAction { Amount = 4, ForFight = true }
		)
	);

	public static readonly KinCard Graft = Plus(
		Card(
			"Graft",
			1,
			U,
			"Sacrifice your front token: it grows by the token's Power and gains its HP as Block.",
			new GraftAction()
		),
		Card(
			"Graft",
			1,
			U,
			"Sacrifice your front token: it grows by the token's Power and gains its HP as Block. Draw a card.",
			new GraftAction(),
			new DrawAction()
		)
	);

	public static readonly KinCard Harvest = Plus(
		Card(
			"Harvest",
			2,
			U,
			"Drop on a foe: sacrifice all your tokens. It takes their total HP.",
			new HarvestAction()
		),
		Card(
			"Harvest",
			1,
			U,
			"Drop on a foe: sacrifice all your tokens. It takes their total HP.",
			new HarvestAction()
		)
	);

	public static readonly KinCard PackCharge = Plus(
		Card(
			"Pack Charge",
			1,
			U,
			"Each of your tokens attacks their front for its Power.",
			new TokensAttackAction()
		),
		Card(
			"Pack Charge",
			0,
			U,
			"Each of your tokens attacks their front for its Power.",
			new TokensAttackAction()
		)
	);

	public static readonly KinCard WildGrowth = Plus(
		Card(
			"Wild Growth",
			1,
			U,
			"Every creature on your line grows 1.",
			new GrowAction { Amount = 1, AllLine = true }
		),
		Card(
			"Wild Growth",
			1,
			U,
			"Every creature on your line grows 2.",
			new GrowAction { Amount = 2, AllLine = true }
		)
	);

	public static readonly KinCard NurseLog = Plus(
		Card(
			"Nurse Log",
			1,
			U,
			"Summon a Log (12 HP). When it falls, draw 2.",
			new SummonTokenAction { Token = Log }
		),
		Card(
			"Nurse Log",
			1,
			U,
			"Summon a Log (16 HP). When it falls, draw 2.",
			new SummonTokenAction { Token = BigLog }
		)
	);

	// ===== Rares

	public static readonly KinCard CrushingWeight = Plus(
		Card(
			"Crushing Weight",
			2,
			R,
			"It attacks their front for twice its Block + Power.",
			new StrikeAction { PerBlock = 2 }
		),
		Card(
			"Crushing Weight",
			2,
			R,
			"It attacks their front for three times its Block + Power.",
			new StrikeAction { PerBlock = 3 }
		)
	);

	public static readonly KinCard Heartwood = Plus(
		Card(
			"Heartwood",
			1,
			R,
			"Drop on a foe: deal the Rooted Block on your whole line. It stays.",
			new SpellDamageAction { PerRootedOnLine = 1 }
		),
		Card(
			"Heartwood",
			0,
			R,
			"Drop on a foe: deal the Rooted Block on your whole line. It stays.",
			new SpellDamageAction { PerRootedOnLine = 1 }
		)
	);

	public static readonly KinCard AncientBark = Plus(
		Card(
			"Ancient Bark",
			2,
			R,
			"AURA: all your Block is Rooted.",
			new AuraAction { Aura = new AncientBarkAura() }
		),
		Card(
			"Ancient Bark",
			1,
			R,
			"AURA: all your Block is Rooted.",
			new AuraAction { Aura = new AncientBarkAura() }
		)
	);

	public static readonly KinCard Thornmail = Plus(
		Card(
			"Thornmail",
			1,
			R,
			"AURA: a foe that hits any creature on your line takes 3.",
			new AuraAction { Aura = new ThornmailAura { Damage = 3 } }
		),
		Card(
			"Thornmail",
			1,
			R,
			"AURA: a foe that hits any creature on your line takes 5.",
			new AuraAction { Aura = new ThornmailAura { Damage = 5 } }
		)
	);

	public static readonly KinCard WildHeart = Plus(
		Card(
			"Wild Heart",
			2,
			R,
			"AURA: at the start of each of your turns, each of your monsters grows 1.",
			new AuraAction { Aura = new WildHeartAura() }
		),
		Card(
			"Wild Heart",
			1,
			R,
			"AURA: at the start of each of your turns, each of your monsters grows 1.",
			new AuraAction { Aura = new WildHeartAura() }
		)
	);

	public static readonly KinCard TreantCard = Plus(
		Card(
			"Treant",
			2,
			R,
			"Summon a Treant (20 HP, 3 Power).",
			new SummonTokenAction { Token = Treant }
		),
		Card(
			"Treant",
			2,
			R,
			"Summon a Treant (26 HP, 4 Power).",
			new SummonTokenAction { Token = BigTreant }
		)
	);

	public static readonly KinCard RampantGrowth = Plus(
		Card(
			"Rampant Growth",
			1,
			R,
			"It grows by its own Power.",
			new GrowAction { ByOwnPower = true }
		),
		Card(
			"Rampant Growth",
			0,
			R,
			"It grows by its own Power.",
			new GrowAction { ByOwnPower = true }
		)
	);

	public static readonly KinCard LifeCycle = Plus(
		Card(
			"Life Cycle",
			1,
			R,
			"AURA: when a token of yours falls, draw a card and your front gains 4 Rooted Block.",
			new AuraAction { Aura = new LifeCycleAura() }
		),
		Card(
			"Life Cycle",
			0,
			R,
			"AURA: when a token of yours falls, draw a card and your front gains 4 Rooted Block.",
			new AuraAction { Aura = new LifeCycleAura() }
		)
	);

	/// <summary>**Every Grove card a reward or a shop can offer.**</summary>
	public static readonly ImmutableList<KinCard> Pool =
	[
		Root,
		Sow,
		Seedlings,
		Bristle,
		BarkSlam,
		ThornLash,
		Overgrow,
		Thicket,
		Hardwood,
		Compost,
		Barkskin,
		DeepRoots,
		Ironbark,
		BraceRoots,
		BriarPatch,
		Needles,
		Graft,
		Harvest,
		PackCharge,
		WildGrowth,
		NurseLog,
		CrushingWeight,
		Heartwood,
		AncientBark,
		Thornmail,
		WildHeart,
		TreantCard,
		RampantGrowth,
		LifeCycle,
	];

	/// <summary>The two Grove cards in the starting deck.</summary>
	public static readonly ImmutableList<KinCard> Starting = [Root, Sow];

	// ===== The monsters (Bramble, the starter, is in `PartyContent`)

	private static PartyCompanion Monster(
		string name,
		int hp,
		int power,
		string passive,
		string rule,
		params GameComponent[] abilities
	) =>
		new(name, hp, power, NoMoves, Passive: passive, PassiveRule: rule)
		{
			Family = Family.Grove,
			Abilities = [.. abilities],
		};

	/// <summary>**ROOTED** — a wall that keeps everything and grows from being hit.</summary>
	public static readonly PartyCompanion Mosshell = Monster(
		"Mosshell",
		34,
		1,
		"MOSSBACK",
		"Its Block is Rooted. When its Block stops a hit, it grows 1.",
		new Mossback(),
		new FirstAttack { Block = 6 }
	);

	/// <summary>**THORNS** — every Thorns card, bigger.</summary>
	public static readonly PartyCompanion Hushcap = Monster(
		"Hushcap",
		20,
		2,
		"SPORECAP",
		"A card that gives Thorns gives 3 more.",
		new Sporecap(),
		new FirstAttack { Thorns = 6 }
	);

	/// <summary>**TOKENS** — they arrive bigger, and its own attacks sow them.</summary>
	public static readonly PartyCompanion Broodvine = Monster(
		"Broodvine",
		22,
		1,
		"NURSERY",
		"Your tokens arrive with +3 HP and +1 Power.",
		new TokenBoost { Hp = 3, Power = 1 },
		new FirstAttack { Summons = Sprout }
	);

	/// <summary>**GROWTH** — every token lost makes the pack stronger.</summary>
	public static readonly PartyCompanion Howler = Monster(
		"Howler",
		24,
		4,
		"PACK LEADER",
		"When a token of yours falls, each of your monsters grows 1.",
		new PackLeader(),
		new FirstAttack { Grow = 1 }
	);

	/// <summary>The four a boss can offer a Grove run.</summary>
	public static readonly ImmutableList<PartyCompanion> Monsters =
	[
		Mosshell,
		Hushcap,
		Broodvine,
		Howler,
	];
}
