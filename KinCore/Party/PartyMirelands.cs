using System.Collections.Immutable;

namespace KinCore.Party;

/// <summary>
/// **REGION 2 — THE MIRELANDS: bog beasts and HAGS** (`KinEnemiesPlan.md`, step 3 — the first of its
/// real roster). Authored at region 2's strength (~×1.6 of the Greenwood). These four bring JUNK
/// (`PartyJunk`): they fight your DECK, not only your HP. The restless dead and the bog's beasts carry
/// the DEBUFFS (`PartyDebuffs`) — the region's question is your deck and your monsters' condition.
/// </summary>
public static class PartyMirelands
{
	/// <summary>A wild fight: these foes, front to back.</summary>
	private static Encounter Wild(params Foe[] foes) =>
		new(
			"Wild " + string.Join(", ", foes.Select(f => f.Name).Distinct()),
			[.. foes.Select((f, i) => f with { Position = i })]
		);

	private static Intent Attack(string name, int amount, Aim aim = Aim.Front) =>
		new()
		{
			Name = name,
			Kind = IntentType.Attack,
			Amount = amount,
			Target = aim,
		};

	/// <summary>A move that only adds junk.</summary>
	private static Intent Curse(string name, Junk junk, int count) =>
		new()
		{
			Name = name,
			Kind = IntentType.Curse,
			AddsJunk = junk,
			JunkCount = count,
		};

	private static Intent Guard(string name, int amount) =>
		new()
		{
			Name = name,
			Kind = IntentType.Block,
			Amount = amount,
		};

	private static readonly Intent Drift =
		new()
		{
			Name = "Drift",
			Kind = IntentType.Move,
			Amount = 1,
		};

	private static Foe Creature(string name, int hp, string trait, params Intent[] cycle) =>
		new()
		{
			Name = name,
			Hp = hp,
			MaxHp = hp,
			Pattern = [.. cycle],
			Trait = trait,
		};

	/// <summary>Clogs the hand: MIRE ×2 into your draw pile, then a claw.</summary>
	public static readonly Foe BogHag = Creature(
		"Bog Hag",
		26,
		"Her CURSE shuffles 2 Mire into your draw pile — unplayable.",
		Curse("Curse", Junk.Mire, 2),
		Attack("Claw", 8)
	);

	/// <summary>Every turn, a DOUBT in your hand: play it, or lose an energy.</summary>
	public static readonly Foe Witch = Creature(
		"Witch",
		20,
		"Each WHISPER puts a Doubt in your hand.",
		Attack("Whisper", 5, Aim.Hunt) with
		{
			AddsJunk = Junk.Doubt,
			JunkCount = 1,
		}
	);

	/// <summary>Fragile, but its bite leaves ROT in your discard pile.</summary>
	public static readonly Foe PlagueRat = Creature(
		"Plague Rat",
		12,
		"Its BITE puts a Rot in your discard pile.",
		Attack("Bite", 6) with
		{
			AddsJunk = Junk.Rot,
			JunkCount = 1,
		}
	);

	// ===== THE RESTLESS DEAD, and the bog's own beasts (approved 2026-10-04)

	/// <summary>Kill it twice: it REASSEMBLES once, at half its HP.</summary>
	public static readonly Foe Skeleton = Creature(
		"Skeleton",
		22,
		"REASSEMBLES: the first time it falls, it stands back up at half its HP.",
		Attack("Rusty Blade", 8)
	) with
	{
		Components = [new Reassembles()],
	};

	/// <summary>A wail on your whole line, then a shriek that SHAKES your front; then it drifts back.</summary>
	public static readonly Foe Banshee = Creature(
		"Banshee",
		18,
		"Its SHRIEK shakes your front: no attack cards on it next turn.",
		Attack("Wail", 4, Aim.Sweep),
		Attack("Shriek", 3) with
		{
			Inflicts = Debuff.Shaken,
			InflictTurns = 1,
		},
		Drift
	);

	/// <summary>Its gnaw leaves your monster WEAK.</summary>
	public static readonly Foe Ghoul = Creature(
		"Ghoul",
		26,
		"Its first GNAW leaves its victim Weak.",
		Attack("Gnaw", 9) with
		{
			Inflicts = Debuff.Weak,
			InflictTurns = 1,
		},
		Attack("Gnaw", 9)
	);

	/// <summary>Latches on your weakest and leaves it VULNERABLE.</summary>
	public static readonly Foe BogLeech = Creature(
		"Bog Leech",
		14,
		"Its LATCH leaves your weakest Vulnerable.",
		Attack("Latch", 6, Aim.Hunt) with
		{
			Inflicts = Debuff.Vulnerable,
			InflictTurns = 2,
		}
	);

	/// <summary>A race: it swells, then flops on your whole line — harder every round.</summary>
	public static readonly Foe BogToad = Creature(
		"Bog Toad",
		38,
		"SWELLS: its attacks deal 1 more every round.",
		Guard("Swell", 10),
		Attack("Belly Flop", 10, Aim.Sweep)
	) with
	{
		Components = [new Enrage { PerRound = 1 }],
	};

	/// <summary>Fragile, but it picks on your weakest.</summary>
	public static readonly Foe Wisp = Creature(
		"Wisp",
		16,
		"",
		Attack("Zap", 6, Aim.Hunt),
		Attack("Zap", 6, Aim.Hunt),
		Drift
	);

	/// <summary>Holds your line still: a WEB in your hand, then a heavy bite.</summary>
	public static readonly Foe GiantSpider = Creature(
		"Giant Spider",
		32,
		"Its WEB in your hand stops your line cards until you pay to clear it.",
		Curse("Spin Web", Junk.Web, 1),
		Attack("Bite", 11)
	);

	/// <summary>The region's first fights.</summary>
	public static readonly ImmutableList<Encounter> Easy =
	[
		Wild(PlagueRat, PlagueRat),
		Wild(Skeleton),
		Wild(Wisp, BogLeech),
	];

	public static readonly ImmutableList<Encounter> Normal =
	[
		Wild(BogToad, BogHag),
		Wild(GiantSpider, PlagueRat),
		Wild(Witch, Wisp, Wisp),
		Wild(Skeleton, Skeleton, Banshee),
		Wild(Ghoul, BogLeech, PlagueRat),
		Wild(GiantSpider, BogHag),
	];
}
