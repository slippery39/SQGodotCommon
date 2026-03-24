using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Pairs a TargetingStrategy with a GameAction template.
/// When a spell resolves, the engine reads each CardEffect, resolves the targets,
/// injects the target IDs into the action template, and spawns the action.
///
/// The ActionTemplate holds all fixed data (e.g. Amount = 3).
/// Target IDs are injected at resolution time via the pipeline context.
/// </summary>
public record CardEffect
{
	public TargetingStrategy TargetingStrategy { get; init; } = null!;

	/// <summary>
	/// The action to spawn when this effect resolves.
	/// Must implement ITargetedAction so the engine can inject resolved target IDs.
	/// </summary>
	public GameAction ActionTemplate { get; init; } = null!;
}
