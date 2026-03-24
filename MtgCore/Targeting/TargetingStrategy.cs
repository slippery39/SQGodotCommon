using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

public enum TargetSelectionMode
{
	/// <summary>Player chooses from valid targets.</summary>
	UserSelect,

	/// <summary>All valid targets are automatically selected.</summary>
	AllValid,

	/// <summary>One random valid target is selected.</summary>
	Random,

	/// <summary>No target needed (e.g. affects the casting player automatically).</summary>
	None,
}

/// <summary>
/// Describes how targets are found and selected for an Effect.
/// Combines a TargetSpecification (which objects qualify) with a
/// TargetSelectionMode (how they are chosen) and min/max counts.
///
/// Used by the UI to know what to highlight, and by CastSpellAction
/// to validate submitted targets.
/// </summary>
public record TargetingStrategy
{
	public TargetSpecification Specification { get; init; } = null!;
	public TargetSelectionMode SelectionMode { get; init; } = TargetSelectionMode.UserSelect;
	public int MinTargets { get; init; } = 1;
	public int MaxTargets { get; init; } = 1;

	public bool RequiresUserSelection => SelectionMode == TargetSelectionMode.UserSelect;

	/// <summary>
	/// Returns all valid target IDs from the current game state.
	/// Used by the UI to highlight targets, and by auto-resolution modes.
	/// </summary>
	public ImmutableList<int> GetValidTargets(TargetingContext context)
	{
		return context
			.GameState.IdToGameObjectMap.Keys.Where(id => Specification.IsSatisfiedBy(id, context))
			.ToImmutableList();
	}

	/// <summary>
	/// Returns true if the given list of target IDs is a legal selection
	/// for this strategy in the current game state.
	/// </summary>
	public bool ValidateTargets(ImmutableList<int> targetIds, TargetingContext context)
	{
		if (targetIds.Count < MinTargets || targetIds.Count > MaxTargets)
			return false;

		return targetIds.All(id => Specification.IsSatisfiedBy(id, context));
	}

	// ===== COMMON FACTORY METHODS =====

	public static TargetingStrategy SingleTarget(TargetSpecification specification) =>
		new()
		{
			Specification = specification,
			SelectionMode = TargetSelectionMode.UserSelect,
			MinTargets = 1,
			MaxTargets = 1,
		};

	public static TargetingStrategy AllValid(TargetSpecification specification) =>
		new()
		{
			Specification = specification,
			SelectionMode = TargetSelectionMode.AllValid,
			MinTargets = 0,
			MaxTargets = int.MaxValue,
		};

	public static TargetingStrategy NoTarget() =>
		new()
		{
			Specification = new IsPlayerSpecification(), // unused
			SelectionMode = TargetSelectionMode.None,
			MinTargets = 0,
			MaxTargets = 0,
		};
}
