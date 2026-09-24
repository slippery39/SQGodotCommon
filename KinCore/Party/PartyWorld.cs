using System.Collections.Immutable;

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
/// field between `MinFoes` and `MaxFoes` creatures from the chosen area's pool.
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

	private static Foe At(Foe foe, int space) => foe with { Space = space };

	/// <summary>**The leader's health in a gym** — your swings into empty columns hit them.</summary>
	public const int LeaderHp = 35;

	/// <summary>
	/// **A gym: a leader and their creatures.** None of them can be caught — they are the leader's,
	/// not wild — and the leader has health of their own.
	/// </summary>
	private static Encounter Gym(string name, params Foe[] foes) =>
		new(name, [.. foes.Select(f => f with { Catchable = false })], LeaderHp);

	public static readonly ImmutableList<Region> Regions =
	[
		new(
			"The Greenwood",
			[
				new(
					"Mossy Hollow",
					"Damp and green. Boars root here; wisps drift between the trees.",
					[PartyContent.Boar(0), PartyContent.Wisp(0), Mosshell],
					BriarViper
				),
				new(
					"Stony Ridge",
					"Bare rock and wind. Stonebeaks nest on the crags.",
					[PartyContent.Stonebeak(0), PartyContent.Boar(0), CinderNewt],
					Mosshell
				),
			],
			Gym("The Old Tusker", PartyContent.OldTusker(2), At(PartyContent.Wisp(0), 4)),
			MinFoes: 1,
			MaxFoes: 2
		),
		new(
			"The Mirelands",
			[
				new(
					"Misty Marsh",
					"Fog over black water. Toads, wisps, and things that bite.",
					[BogToad, PartyContent.Wisp(0), BriarViper],
					CinderNewt
				),
				new(
					"Ember Crags",
					"Hot stone and ash. Newts in every crack.",
					[CinderNewt, PartyContent.Stonebeak(0), Mosshell],
					BogToad
				),
			],
			Gym("The Old Mire", At(BogToad, 0), At(OldMire, 2), At(BriarViper, 4)),
			MinFoes: 2,
			MaxFoes: 3
		),
	];

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
			new(StopKind.Deep, Fight(Math.Min(3, region.MaxFoes + 1), area.Rare)),
		];
	}
}
