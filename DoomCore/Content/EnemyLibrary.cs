using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// Every enemy and Opponent in the game, as data.
///
/// **This file replaces stat formulas.** `EnemiesFor` used to compute `5 + floor * 2` health and
/// invent a name for it, so no enemy had an identity and none could ever do anything. A floor now
/// draws from the roster legal for it — the same mechanism `PlayableOn` uses for scenarios, and the
/// same idea: the roster IS the difficulty curve, written as content.
///
/// **Health here IS measured now** — see `docs/findings/doom-balance.md`, run 2. Attack is not:
/// it is the lever that actually decides how long a run lasts, and it has been left alone on
/// purpose so the next pass changes one thing. Re-run `sim 1000` after touching any of it.
/// </summary>
public static class EnemyLibrary
{
	private static DoomEffect On(
		EffectTrigger trigger,
		DoomTarget target,
		GameAction template,
		string text
	) =>
		new()
		{
			Trigger = trigger,
			Target = target,
			Template = template,
			Text = text,
		};

	// ===== Enemies =====

	public static readonly EnemyDefinition Wretch =
		new()
		{
			Name = "Wretch",
			Description = "It was somebody, before.",
			Health = 14,
			Attack = 2,
			MinFloor = 1,
		};

	public static readonly EnemyDefinition ScavHound =
		new()
		{
			Name = "Scav Hound",
			Description = "Fast, and it does not stop.",
			Health = 10,
			Attack = 4,
			MinFloor = 1,
		};

	public static readonly EnemyDefinition Revenant =
		new()
		{
			Name = "Revenant",
			Description = "Sent up to fill the gap.",
			Health = 18,
			Attack = 4,
			MinFloor = 2,
		};

	/// <summary>The first enemy that does something. Killing it is no longer free.</summary>
	public static readonly EnemyDefinition HeraldOfTheEnd =
		new()
		{
			Name = "Herald of the End",
			Description = "It has been counting down since before you arrived.",
			Health = 26,
			Attack = 4,
			MinFloor = 5,
			Effects =
			[
				On(
					EffectTrigger.OnDeath,
					DoomTarget.Player,
					new DealDamageAction { Amount = 4 },
					"on death: 4 to you"
				),
			],
		};

	/// <summary>Punishes a long lane. Holding it costs you every turn it stands.</summary>
	public static readonly EnemyDefinition Rotbearer =
		new()
		{
			Name = "Rotbearer",
			Description = "Whatever it carries is catching.",
			Health = 32,
			Attack = 4,
			MinFloor = 7,
			Effects =
			[
				On(
					EffectTrigger.OnTurnEnd,
					DoomTarget.Player,
					new DealDamageAction { Amount = 2 },
					"each turn: 2 to you"
				),
			],
		};

	public static readonly EnemyDefinition SiegeHulk =
		new()
		{
			Name = "Siege Hulk",
			Description = "Built to take a building down.",
			Health = 44,
			Attack = 6,
			MinFloor = 10,
		};

	public static readonly EnemyDefinition Husk =
		new()
		{
			Name = "Husk",
			Description = "Stands where it fell.",
			Health = 18,
			Attack = 2,
			MinFloor = 1,
		};

	public static readonly EnemyDefinition AshCrow =
		new()
		{
			Name = "Ash Crow",
			Description = "Comes down fast and does not pull up.",
			Health = 8,
			Attack = 6,
			MinFloor = 2,
		};

	/// <summary>Killing it feeds them. The first enemy you might choose NOT to kill.</summary>
	public static readonly EnemyDefinition Chorister =
		new()
		{
			Name = "Chorister",
			Description = "It is singing something, and it is not for you.",
			Health = 20,
			Attack = 4,
			MinFloor = 3,
			Effects =
			[
				On(
					EffectTrigger.OnDeath,
					DoomTarget.Opponent,
					new DealDamageAction { Amount = -6 },
					"on death: the Opponent heals 6"
				),
			],
		};

	public static readonly EnemyDefinition CinderHound =
		new()
		{
			Name = "Cinder Hound",
			Description = "It has been burning the whole time.",
			Health = 14,
			Attack = 6,
			MinFloor = 4,
		};

	/// <summary>Punishes killing it with a wide board — the answer to filling every lane.</summary>
	public static readonly EnemyDefinition PyreWalker =
		new()
		{
			Name = "Pyre Walker",
			Description = "Whatever it was carrying went up with it.",
			Health = 26,
			Attack = 6,
			MinFloor = 7,
			Effects =
			[
				On(
					EffectTrigger.OnDeath,
					DoomTarget.YourUnits,
					new DealDamageAction { Amount = 4 },
					"on death: 4 to every unit you hold"
				),
			],
		};

	/// <summary>Outlasts a slow lane. Chip damage will never finish it.</summary>
	public static readonly EnemyDefinition Gravecaller =
		new()
		{
			Name = "Gravecaller",
			Description = "It keeps putting itself back together.",
			Health = 30,
			Attack = 4,
			MinFloor = 8,
			Effects =
			[
				On(
					EffectTrigger.OnTurnEnd,
					DoomTarget.Self,
					new DealDamageAction { Amount = -4 },
					"each turn: heals 4"
				),
			],
		};

	/// <summary>Makes the apocalypse cost life directly, on a board that was already dodging it.</summary>
	public static readonly EnemyDefinition Tollman =
		new()
		{
			Name = "Tollman",
			Description = "It counts what the sky takes.",
			Health = 34,
			Attack = 6,
			MinFloor = 9,
			Effects =
			[
				On(
					EffectTrigger.OnDoomFires,
					DoomTarget.Player,
					new DealDamageAction { Amount = 4 },
					"when the doom fires: 4 to you"
				),
			],
		};

	public static readonly EnemyDefinition Rampart =
		new()
		{
			Name = "Rampart",
			Description = "It was a wall. It is still a wall.",
			Health = 56,
			Attack = 4,
			MinFloor = 11,
		};

	public static readonly EnemyDefinition TheTally =
		new()
		{
			Name = "The Tally",
			Description = "It has your number and it is not finished reading.",
			Health = 40,
			Attack = 8,
			MinFloor = 14,
			Effects =
			[
				On(
					EffectTrigger.OnTurnEnd,
					DoomTarget.Player,
					new DealDamageAction { Amount = 2 },
					"each turn: 2 to you"
				),
			],
		};

	/// <summary>Kill it last. Killing it first hands the whole line back its health.</summary>
	public static readonly EnemyDefinition LastChorus =
		new()
		{
			Name = "Last Chorus",
			Description = "When one stops, the others get louder.",
			Health = 48,
			Attack = 8,
			MinFloor = 15,
			Effects =
			[
				On(
					EffectTrigger.OnDeath,
					DoomTarget.AllEnemies,
					new DealDamageAction { Amount = -6 },
					"on death: every enemy still standing heals 6"
				),
			],
		};

	public static readonly EnemyDefinition Doomsayer =
		new()
		{
			Name = "Doomsayer",
			Description = "It told you. It is still telling you.",
			Health = 38,
			Attack = 10,
			MinFloor = 17,
			Effects =
			[
				On(
					EffectTrigger.OnDoomFires,
					DoomTarget.Player,
					new DealDamageAction { Amount = 5 },
					"when the doom fires: 5 to you"
				),
			],
		};

	public static readonly ImmutableArray<EnemyDefinition> All =
	[
		Wretch,
		ScavHound,
		Husk,
		AshCrow,
		Chorister,
		CinderHound,
		HeraldOfTheEnd,
		Revenant,
		PyreWalker,
		Rotbearer,
		Gravecaller,
		Tollman,
		SiegeHulk,
		Rampart,
		TheTally,
		LastChorus,
		Doomsayer,
	];

	/// <summary>The roster a floor may draw from. Empty is impossible — Wretch has MinFloor 1.</summary>
	public static ImmutableArray<EnemyDefinition> PlayableOn(int floor) =>
		[.. All.Where(e => e.MinFloor <= floor)];

	// ===== Opponents =====

	public static readonly OpponentDefinition TheOpponent =
		new()
		{
			Name = "The Opponent",
			Description = "It has not moved since you walked in.",
			Health = 64,
			SummonInterval = 3,
			Reinforcement = Revenant,
			MinFloor = 1,
		};

	public static readonly OpponentDefinition TheChoir =
		new()
		{
			Name = "The Choir",
			Description = "It is not one thing, and it is not finished.",
			Health = 96,
			SummonInterval = 3,
			Reinforcement = HeraldOfTheEnd,
			MinFloor = 5,
			Effects =
			[
				On(
					EffectTrigger.OnTurnStart,
					DoomTarget.Opponent,
					new DealDamageAction { Amount = -2 },
					"each turn: heals 2"
				),
			],
		};

	public static readonly OpponentDefinition TheLastWarden =
		new()
		{
			Name = "The Last Warden",
			Description = "Still holding a door that is no longer there.",
			Health = 140,
			SummonInterval = 3,
			Reinforcement = SiegeHulk,
			MinFloor = 10,
		};

	/// <summary>
	/// The last thing in the act. Reachable only on the final floor, and it is the only Opponent
	/// that makes the apocalypse itself the weapon rather than the weather.
	/// </summary>
	public static readonly OpponentDefinition TheLastMorning =
		new()
		{
			Name = "The Last Morning",
			Description = "It has been waiting at the end of every one of these.",
			Health = 200,
			SummonInterval = 2,
			Reinforcement = Doomsayer,
			MinFloor = Run.ActLength,
			Effects =
			[
				On(
					EffectTrigger.OnDoomFires,
					DoomTarget.Player,
					new DealDamageAction { Amount = 10 },
					"when the doom fires: 10 to you"
				),
				On(
					EffectTrigger.OnTurnStart,
					DoomTarget.Opponent,
					new DealDamageAction { Amount = -4 },
					"each turn: heals 4"
				),
			],
		};

	public static readonly ImmutableArray<OpponentDefinition> AllOpponents =
	[
		TheOpponent,
		TheChoir,
		TheLastWarden,
		TheLastMorning,
	];

	/// <summary>The hardest Opponent this floor allows — the curve, chosen from content.</summary>
	public static OpponentDefinition ForFloor(int floor) =>
		AllOpponents.Where(o => o.MinFloor <= floor).MaxBy(o => o.MinFloor) ?? TheOpponent;

	// ===== Opponent traits =====

	/// <summary>
	/// A modifier laid over an Opponent, so a roster of four covers sixteen battles without one
	/// fight repeating. **Content crossed with content** — the same trick the apocalypses use.
	///
	/// Writing sixteen Opponents would have been sixteen names, sixteen reinforcements and sixteen
	/// places on the curve. Four Opponents and seven traits is eleven pieces of content and
	/// twenty-eight distinct fights.
	///
	/// **There must be at least as many traits as the longest span any one Opponent holds.** The
	/// Last Warden covers seven battles; at six traits it wrapped and fought the same fight twice.
	/// `NoOpponentIsFoughtTwiceWearingTheSameTrait` is what catches that, and it caught exactly
	/// this — widen the curve and it will catch it again.
	/// </summary>
	public static readonly ImmutableArray<OpponentTrait> Traits =
	[
		// The plain fight. **Deliberately first and deliberately empty**: if every Opponent has a
		// gimmick then none of them read as one, and the curve has nowhere quiet to stand.
		new() { Name = "", Text = "" },
		new()
		{
			Name = "Zealous",
			Text = "heals 4 each turn",
			Effects =
			[
				On(
					EffectTrigger.OnTurnStart,
					DoomTarget.Opponent,
					new DealDamageAction { Amount = -4 },
					"each turn: heals 4"
				),
			],
		},
		new()
		{
			Name = "Relentless",
			Text = "reinforces a turn sooner",
			SummonIntervalDelta = -1,
		},
		new()
		{
			Name = "Cruel",
			Text = "4 to you whenever the doom fires",
			Effects =
			[
				On(
					EffectTrigger.OnDoomFires,
					DoomTarget.Player,
					new DealDamageAction { Amount = 4 },
					"when the doom fires: 4 to you"
				),
			],
		},
		new()
		{
			Name = "Blighted",
			Text = "2 to every unit you hold, each turn",
			Effects =
			[
				On(
					EffectTrigger.OnTurnEnd,
					DoomTarget.YourUnits,
					new DealDamageAction { Amount = 2 },
					"each turn: 2 to every unit you hold"
				),
			],
		},
		new()
		{
			Name = "Shepherd",
			Text = "its line heals 4 each turn",
			Effects =
			[
				On(
					EffectTrigger.OnTurnStart,
					DoomTarget.AllEnemies,
					new DealDamageAction { Amount = -4 },
					"each turn: every enemy heals 4"
				),
			],
		},
		new()
		{
			Name = "Leeching",
			Text = "2 to you each turn",
			Effects =
			[
				On(
					EffectTrigger.OnTurnEnd,
					DoomTarget.Player,
					new DealDamageAction { Amount = 2 },
					"each turn: 2 to you"
				),
			],
		},
	];

	/// <summary>
	/// The traits in a run's own order. Deterministic from the seed, so a run replays exactly.
	/// </summary>
	public static ImmutableArray<OpponentTrait> TraitsFor(int seed)
	{
		var pool = Traits.ToList();
		var rng = new Random(seed * 6271 + 13);

		for (var i = pool.Count - 1; i > 0; i--)
		{
			var j = rng.Next(i + 1);
			(pool[i], pool[j]) = (pool[j], pool[i]);
		}

		return [.. pool];
	}
}

/// <summary>
/// One modifier an Opponent can wear. Effects are the same <see cref="DoomEffect"/> everything else
/// carries; the interval delta is here because reinforcement rate is a FIELD and no effect can
/// reach it.
/// </summary>
public record OpponentTrait
{
	/// <summary>Empty for the plain fight, which is a trait like any other.</summary>
	public string Name { get; init; } = "";

	public string Text { get; init; } = "";

	public ImmutableList<DoomEffect> Effects { get; init; } = ImmutableList<DoomEffect>.Empty;

	public int SummonIntervalDelta { get; init; }

	/// <summary>
	/// Lays the trait over an Opponent. **Adds to its effects rather than replacing them**, so The
	/// Choir stays The Choir and the trait is something on top of it.
	/// </summary>
	public OpponentDefinition ApplyTo(OpponentDefinition opponent) =>
		Name.Length == 0
			? opponent
			: opponent with
			{
				Name = $"{opponent.Name}, {Name}",
				Effects = opponent.Effects.AddRange(Effects),
				SummonInterval = Math.Max(1, opponent.SummonInterval + SummonIntervalDelta),
			};
}
