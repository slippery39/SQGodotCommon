using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

public enum TargetSelectionMode
{
	/// <summary>Player chooses between MinTargets and MaxTargets from valid targets.</summary>
	UserSelect,

	/// <summary>All valid targets are automatically selected.</summary>
	AllValid,

	/// <summary>One random valid target is selected.</summary>
	Random,

	/// <summary>Automatically targets the casting player. Used for effects like "you gain 3 life".</summary>
	CastingPlayer,

	/// <summary>No target needed.</summary>
	None,
}

/// <summary>
/// Describes how targets are found and selected for a CardEffect.
/// Combines a TargetSpecification (which objects qualify) with a
/// TargetSelectionMode (how they are chosen) and min/max counts.
///
/// Used by the UI to know what to highlight, and by CastSpellAction
/// to validate submitted targets before they reach the stack.
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
	/// Used by the UI to highlight selectable targets, and by AllValid/Random modes at resolution.
	/// </summary>
	public ImmutableList<int> GetValidTargets(TargetingContext context) =>
		context
			.GameState.IdToGameObjectMap.Keys.Where(id => Specification.IsSatisfiedBy(id, context))
			.ToImmutableList();

	/// <summary>
	/// Returns true if the given list of target IDs is a legal selection for this strategy.
	/// Called by CastSpellAction.ValidateAdd for UserSelect effects.
	/// </summary>
	public bool ValidateTargets(ImmutableList<int> targetIds, TargetingContext context)
	{
		if (targetIds.Count < MinTargets || targetIds.Count > MaxTargets)
			return false;

		return targetIds.All(id => Specification.IsSatisfiedBy(id, context));
	}

	// ===== FACTORY METHODS =====

	/// <summary>
	/// Player must choose exactly one valid target.
	/// </summary>
	public static TargetingStrategy SingleTarget(TargetSpecification specification) =>
		new()
		{
			Specification = specification,
			SelectionMode = TargetSelectionMode.UserSelect,
			MinTargets = 1,
			MaxTargets = 1,
		};

	/// <summary>
	/// Player may choose between min and max valid targets.
	/// </summary>
	public static TargetingStrategy MultiTarget(
		TargetSpecification specification,
		int minTargets,
		int maxTargets
	) =>
		new()
		{
			Specification = specification,
			SelectionMode = TargetSelectionMode.UserSelect,
			MinTargets = minTargets,
			MaxTargets = maxTargets,
		};

	/// <summary>
	/// All valid targets are selected automatically at resolution time.
	/// Used for mass effects like Pyroclasm.
	/// </summary>
	public static TargetingStrategy AllValid(TargetSpecification specification) =>
		new()
		{
			Specification = specification,
			SelectionMode = TargetSelectionMode.AllValid,
			MinTargets = 0,
			MaxTargets = int.MaxValue,
		};

	/// <summary>
	/// One random valid target is selected automatically at resolution time.
	/// </summary>
	public static TargetingStrategy RandomTarget(TargetSpecification specification) =>
		new()
		{
			Specification = specification,
			SelectionMode = TargetSelectionMode.Random,
			MinTargets = 1,
			MaxTargets = 1,
		};

	/// <summary>
	/// No target required. Used for effects that don't need a target (e.g. draw a card).
	/// </summary>
	public static TargetingStrategy NoTarget() =>
		new()
		{
			Specification = new IsPlayerSpecification(), // unused
			SelectionMode = TargetSelectionMode.None,
			MinTargets = 0,
			MaxTargets = 0,
		};

	/// <summary>
	/// Automatically targets the casting player.
	/// Used for effects like "you gain 3 life" where the caster is always the recipient.
	/// </summary>
	public static TargetingStrategy Self() =>
		new()
		{
			Specification = new IsPlayerSpecification(), // unused — target is always the caster
			SelectionMode = TargetSelectionMode.CastingPlayer,
			MinTargets = 1,
			MaxTargets = 1,
		};
}
