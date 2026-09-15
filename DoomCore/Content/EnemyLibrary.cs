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
/// Balance here is authored, not measured. Every number is meant to be changed.
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
			Health = 7,
			Attack = 2,
			MinFloor = 1,
		};

	public static readonly EnemyDefinition ScavHound =
		new()
		{
			Name = "Scav Hound",
			Description = "Fast, and it does not stop.",
			Health = 5,
			Attack = 3,
			MinFloor = 1,
		};

	public static readonly EnemyDefinition Revenant =
		new()
		{
			Name = "Revenant",
			Description = "Sent up to fill the gap.",
			Health = 9,
			Attack = 3,
			MinFloor = 2,
		};

	/// <summary>The first enemy that does something. Killing it is no longer free.</summary>
	public static readonly EnemyDefinition HeraldOfTheEnd =
		new()
		{
			Name = "Herald of the End",
			Description = "It has been counting down since before you arrived.",
			Health = 11,
			Attack = 3,
			MinFloor = 3,
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
			Health = 13,
			Attack = 2,
			MinFloor = 4,
			Effects =
			[
				On(
					EffectTrigger.OnTurnEnd,
					DoomTarget.Player,
					new DealDamageAction { Amount = 1 },
					"each turn: 1 to you"
				),
			],
		};

	public static readonly EnemyDefinition SiegeHulk =
		new()
		{
			Name = "Siege Hulk",
			Description = "Built to take a building down.",
			Health = 18,
			Attack = 5,
			MinFloor = 6,
		};

	public static readonly ImmutableArray<EnemyDefinition> All =
	[
		Wretch,
		ScavHound,
		Revenant,
		HeraldOfTheEnd,
		Rotbearer,
		SiegeHulk,
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
			Health = 26,
			SummonInterval = 3,
			Reinforcement = Revenant,
			MinFloor = 1,
		};

	public static readonly OpponentDefinition TheChoir =
		new()
		{
			Name = "The Choir",
			Description = "It is not one thing, and it is not finished.",
			Health = 40,
			SummonInterval = 3,
			Reinforcement = HeraldOfTheEnd,
			MinFloor = 4,
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
			Health = 58,
			SummonInterval = 2,
			Reinforcement = SiegeHulk,
			MinFloor = 9,
		};

	public static readonly ImmutableArray<OpponentDefinition> AllOpponents =
	[
		TheOpponent,
		TheChoir,
		TheLastWarden,
	];

	/// <summary>The hardest Opponent this floor allows — the curve, chosen from content.</summary>
	public static OpponentDefinition ForFloor(int floor) =>
		AllOpponents.Where(o => o.MinFloor <= floor).MaxBy(o => o.MinFloor) ?? TheOpponent;
}
