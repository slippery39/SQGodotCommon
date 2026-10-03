using System.Collections.Immutable;

namespace KinCore.Party;

/// <summary>
/// **REGION 1 — THE GREENWOOD: beasts and GOBLINS** (`KinEnemiesPlan.md`, draft 2 for a team of
/// three, approved 2026-10-03). Foes are AUTHORED at the region's strength — there are no levels — and
/// every wild fight is an authored group: the first fights of the region come from `Easy`. Each foe asks
/// one question; numbers are first guesses (exploring).
/// </summary>
public static class PartyGreenwood
{
	private static Intent Attack(string name, int amount, Aim aim = Aim.Front) =>
		new()
		{
			Name = name,
			Kind = IntentType.Attack,
			Amount = amount,
			Target = aim,
		};

	private static Intent Guard(string name, int amount, Aim aim = Aim.Front) =>
		new()
		{
			Name = name,
			Kind = IntentType.Block,
			Amount = amount,
			Target = aim,
		};

	private static Foe Creature(string name, int hp, params Intent[] cycle) =>
		new()
		{
			Name = name,
			Hp = hp,
			MaxHp = hp,
			Pattern = [.. cycle],
		};

	/// <summary>The baseline.</summary>
	public static readonly Foe GoblinGrunt = Creature(
		"Goblin Grunt",
		14,
		Attack("Stab", 5),
		Guard("Shield Up", 5)
	);

	/// <summary>Protect the back line.</summary>
	public static readonly Foe GoblinSlinger = Creature(
		"Goblin Slinger",
		10,
		Attack("Sling", 4, Aim.Back)
	);

	/// <summary>Kill the support first?</summary>
	public static readonly Foe GoblinShaman = Creature(
		"Goblin Shaman",
		12,
		Attack("Spark", 3, Aim.Hunt),
		Guard("Ward", 6, Aim.Ahead)
	);

	/// <summary>
	/// **The clock**: two fuses, then a blast on your whole line — and it falls. Race it, or Block all
	/// three.
	/// </summary>
	public static readonly Foe PowderGoblin = Creature(
		"Powder Goblin",
		8,
		new Intent { Name = "Fuse", Kind = IntentType.WindUp },
		new Intent { Name = "Fuse", Kind = IntentType.WindUp },
		Attack("Blast", 8, Aim.Sweep) with
		{
			SelfDestructs = true,
		}
	) with
	{
		Trait = "BLAST: its third move hits your whole line, and then it falls.",
	};

	/// <summary>Pack pressure on the fragile one.</summary>
	public static readonly Foe GreyWolf = Creature("Grey Wolf", 9, Attack("Bite", 4, Aim.Hunt));

	private static readonly Foe Boar = PartyContent.Boar(0);
	private static readonly Foe Viper = PartyWorld.BriarViper;
	private static readonly Foe Mite = PartyWorld.RockMite;
	private static readonly Foe Magpie = PartyWorld.Magpie;

	/// <summary>A wild fight: these foes, front to back.</summary>
	private static Encounter Wild(params Foe[] foes) =>
		new(
			"Wild " + string.Join(", ", foes.Select(f => f.Name).Distinct()),
			[.. foes.Select((f, i) => f with { Position = i })]
		);

	/// <summary>The region's first fights.</summary>
	public static readonly ImmutableList<Encounter> Easy =
	[
		Wild(GoblinGrunt, GoblinGrunt),
		Wild(Boar),
		Wild(Viper, Mite),
	];

	public static readonly ImmutableList<Encounter> Normal =
	[
		Wild(GoblinGrunt, GoblinSlinger, GoblinSlinger),
		Wild(GoblinGrunt, GoblinGrunt, GoblinShaman),
		Wild(GreyWolf, GreyWolf, GreyWolf),
		Wild(GoblinGrunt, PowderGoblin, GoblinSlinger),
		Wild(Boar, Magpie),
		Wild(Mite, Mite, Viper),
	];
}
