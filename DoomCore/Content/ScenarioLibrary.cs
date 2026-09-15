using System.Collections.Immutable;

namespace DoomCore;

/// <summary>
/// An apocalypse as CONTENT — everything about it that is not code.
///
/// **Only the BATTLE-scope fallout can be data**, and that asymmetry is a real finding rather than
/// an oversight. A battle scenario changes this `GameState`, so it is a list of
/// <see cref="DoomEffect"/> like anything else. A PERMANENT one rewrites the run deck, and the run
/// deliberately lives outside `GameState` — so it cannot be a `GameAction`, and it stays in
/// `DoomTransforms` as a `(run, firing) -> run` function. See DoomJam.md's engine findings.
/// </summary>
public record ScenarioDefinition
{
	public DoomScenario Scenario { get; init; } = DoomScenario.None;

	/// <summary>Flavour only, never mechanics. Shown on the banner every turn.</summary>
	public string Description { get; init; } = "";

	/// <summary>
	/// Turns between firings. Varying it per scenario is free texture: each apocalypse feels
	/// different before the player has read a word of its text.
	/// </summary>
	public int Countdown { get; init; } = 3;

	public DoomScope Scope { get; init; } = DoomScope.Permanent;

	/// <summary>
	/// Earliest floor this may roll. **Scope is the difficulty curve**: early floors draw from
	/// battle-only apocalypses you merely navigate, later ones from those that leave marks.
	/// </summary>
	public int MinFloor { get; init; } = 1;

	/// <summary>
	/// What it does to the battle when it fires. Empty for a permanent scenario, whose fallout is
	/// recorded as a firing and replayed by the run instead.
	/// </summary>
	public ImmutableList<DoomEffect> BattleEffects { get; init; } = ImmutableList<DoomEffect>.Empty;

	/// <summary>False when nothing implements it yet — see Rapture.</summary>
	public bool Implemented { get; init; } = true;
}

/// <summary>
/// Every apocalypse, as data. Adding a BATTLE-scope one is now a single entry here and nothing
/// else — no enum case in a hook, no `ScopeOf` row, no `CountdownFor` row.
/// </summary>
public static class ScenarioLibrary
{
	public static readonly ScenarioDefinition Flood =
		new()
		{
			Scenario = DoomScenario.Flood,
			Description = "The water takes whatever is still standing in it.",
			Countdown = 5,
			Scope = DoomScope.Battle,
			MinFloor = 1,
			BattleEffects =
			[
				new DoomEffect
				{
					Target = DoomTarget.YourUnits,
					Template = new SweepFieldAction(),
					Text = "everything standing is washed to Discard",
				},
			],
		};

	public static readonly ScenarioDefinition Zombie =
		new()
		{
			Scenario = DoomScenario.Zombie,
			Description = "The dead do not stay where you leave them.",
			Countdown = 3,
			Scope = DoomScope.Permanent,
			MinFloor = 3,
		};

	public static readonly ScenarioDefinition Nuclear =
		new()
		{
			Scenario = DoomScenario.Nuclear,
			Description = "What stands in the open will be changed by it.",
			Countdown = 2,
			Scope = DoomScope.Permanent,
			MinFloor = 3,
		};

	/// <summary>
	/// Needs a sacrifice mechanic that does not exist. Flagged unimplemented rather than quietly
	/// left out, because an apocalypse that silently did nothing would look exactly like one that
	/// worked — the same reason `DoomTransforms` throws for it.
	/// </summary>
	public static readonly ScenarioDefinition Rapture =
		new()
		{
			Scenario = DoomScenario.Rapture,
			Description = "What you give up is not lost.",
			Countdown = 3,
			Scope = DoomScope.Permanent,
			MinFloor = 99,
			Implemented = false,
		};

	/// <summary>
	/// A pure obstacle, which BATTLE scope is allowed to be: nothing carries forward, so it owes the
	/// player no bargain. It chips the board instead of clearing it, so holding a lane through one
	/// is possible but expensive — a different question from Flood's "how much do I commit?".
	/// </summary>
	public static readonly ScenarioDefinition Ashfall =
		new()
		{
			Scenario = DoomScenario.Ashfall,
			Description = "It falls on everything, and it is still warm.",
			Countdown = 4,
			Scope = DoomScope.Battle,
			MinFloor = 2,
			BattleEffects =
			[
				new DoomEffect
				{
					Target = DoomTarget.YourUnits,
					Template = new DealDamageAction { Amount = 2 },
					Text = "2 to every unit you hold",
				},
				new DoomEffect
				{
					Target = DoomTarget.Player,
					Template = new DealDamageAction { Amount = 3 },
					Text = "3 to you",
				},
			],
		};

	public static readonly ImmutableArray<ScenarioDefinition> All =
	[
		Flood,
		Ashfall,
		Zombie,
		Nuclear,
		Rapture,
	];

	public static ScenarioDefinition Of(DoomScenario scenario) =>
		All.FirstOrDefault(d => d.Scenario == scenario)
		?? throw new ArgumentOutOfRangeException(
			nameof(scenario),
			$"{scenario} has no definition. A scenario the library does not know would fire with "
				+ "no countdown, no scope and no effect, and look exactly like one that worked."
		);

	/// <summary>Which apocalypses a floor may roll. Unimplemented ones are never offered.</summary>
	public static ImmutableArray<ScenarioDefinition> PlayableOn(int floor) =>
		[.. All.Where(d => d.Implemented && d.MinFloor <= floor)];
}
