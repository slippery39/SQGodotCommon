using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// **The core hook for PERMANENT scenarios: read what a firing saw, rewrite the run deck.**
///
/// Every apocalypse is one function of (run, firing) → run, folded over every firing the battle
/// recorded. There is deliberately no second mechanism — a new permanent scenario is a new case
/// here plus a value on <see cref="DoomScenario"/> and a `ScopeOf` row.
///
/// Its sibling is <see cref="DoomBattleEffects"/>, which owns BATTLE-scope scenarios. The split is
/// by what each may touch, and each throws when handed the other's kind: a scenario in the wrong
/// hook does nothing at all and looks exactly like one that worked.
///
/// **Every scenario must be a BARGAIN, never a pure tax.** It converts one resource into another,
/// and a greedy line must exist. A deck that only ever gets worse is a misery engine players quit,
/// and it makes escalating enemies unbalanceable — the apocalypses ARE the power curve, so there
/// is no separate progression system to lean on.
/// </summary>
public static class DoomTransforms
{
	public const string ZombieTag = "Zombie";
	public const string IrradiatedTag = "Irradiated";

	/// <summary>
	/// What Irradiated adds, in both directions. **Named because tests were restating it**: a
	/// literal in six assertions turned a balance pass into six false failures.
	/// </summary>
	public const int IrradiatedBuff = 4;

	/// <summary>Life an Irradiated card costs when it is DRAWN. Named for the same reason.</summary>
	public const int IrradiatedDrawCost = 2;

	/// <summary>
	/// Replays every firing of the battle against the run, in order.
	///
	/// **A fold, because the doom recurs.** Each firing carries its own snapshot of what it read
	/// (<see cref="DoomFiring"/>), so a Nuclear that fired twice irradiates whatever was standing
	/// on each occasion rather than the final board twice.
	/// </summary>
	public static Run Apply(Run run, GameState finishedBattle) =>
		finishedBattle
			.GetBattle()
			.Firings
			// Battle-scope firings already happened, inside the battle. Replaying them here would
			// apply them twice — and against a deck they were never meant to touch.
			.Where(f => StarterContent.ScopeOf(f.Scenario) == DoomScope.Permanent)
			.Aggregate(run, ApplyFiring);

	/// <summary>
	/// The body Zombie mints. Named so the scenario entry can point at it and the test can read it
	/// rather than restating 2/2 in a third place.
	/// </summary>
	public static readonly RunCard ZombieBody =
		new()
		{
			Name = "Zombie",
			Description = "Shambles back. 2/2.",
			Cost = 0,
			IsUnit = true,
			Power = 2,
			Toughness = 2,
			Tags = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, ZombieTag),
		};

	/// <summary>
	/// Replays ONE firing onto the run by running the scenario's transforms in order.
	///
	/// **There is no per-scenario code here any more.** Zombie and Nuclear were hand-written
	/// methods and a themed act wants a dozen more; both are now entries in `ScenarioLibrary` made
	/// of a read and a verb. Adding a permanent apocalypse is content, exactly as adding a
	/// battle-scope one has been since Ashfall.
	/// </summary>
	public static Run ApplyFiring(Run run, DoomFiring firing)
	{
		var definition = ScenarioLibrary.Of(firing.Scenario);

		if (definition.Scope != DoomScope.Permanent)
			throw new InvalidOperationException(
				$"{firing.Scenario} is a Battle scenario and belongs in DoomBattleEffects, not "
					+ "here. A scenario in the wrong hook does nothing at all and looks exactly "
					+ "like one that worked."
			);

		// Rapture is the case this guards: it needs a sacrifice mechanic that does not exist, and
		// an apocalypse that quietly rewrote nothing would look exactly like one that worked.
		if (!definition.Implemented)
			throw new NotSupportedException(
				$"{firing.Scenario} is flagged unimplemented. Do not offer it as a floor yet."
			);

		if (definition.Transforms.IsEmpty)
			throw new InvalidOperationException(
				$"{firing.Scenario} is Permanent scope and declares no transforms, so firing it "
					+ "would rewrite nothing. Give it transforms or make it Battle scope."
			);

		return definition.Transforms.Aggregate(run, (current, t) => t.Apply(current, firing));
	}
}
