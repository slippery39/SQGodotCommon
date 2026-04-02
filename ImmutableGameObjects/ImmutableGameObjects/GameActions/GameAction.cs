using System.Collections.Immutable;

namespace ImmutableGameObjects;

/// <summary>
/// Base record for all game actions.
/// </summary>
public abstract record GameAction
{
	/// <summary>
	/// Context data passed from previous action in a pipeline.
	/// </summary>
	public ImmutableDictionary<string, object> InputContext { get; init; } =
		ImmutableDictionary<string, object>.Empty;

	/// <summary>
	/// When true, this action is treated as a post-action processor and will not
	/// itself trigger the PostActionProcessor after it resolves. This prevents
	/// infinite recursion when the processor runs.
	///
	/// Override to true in any action that is registered as GameState.PostActionProcessor.
	/// </summary>
	public virtual bool IsPostProcessor => false;

	/// <summary>
	/// Validates whether this action can be added to the action stack.
	/// Called by GameState.AddAction before pushing to the stack.
	/// Override to enforce preconditions such as valid targets existing,
	/// correct inputs being set, or game state requirements being met.
	/// Default is always valid.
	/// </summary>
	public virtual ValidationResult ValidateAdd(GameState gameState) => ValidationResult.Valid;

	/// <summary>
	/// Validates whether this action can still resolve from the stack.
	/// Called by GameState.ProcessNextAction before executing.
	/// Override to handle cases where conditions have changed since the action
	/// was added — e.g. a target was destroyed in response.
	/// If invalid, the action is removed from the stack and an
	/// ActionValidationFailedEvent is emitted. What happens next
	/// (graveyard, exile, nothing) is the game layer's responsibility.
	/// Default is always valid.
	/// </summary>
	public virtual ValidationResult ValidateResolve(GameState gameState) => ValidationResult.Valid;

	public abstract ActionResult Execute(GameState gameState);

	protected T GetInput<T>(string key, T defaultValue = default!)
	{
		return InputContext.TryGetValue(key, out var value) ? (T)value : defaultValue;
	}
}
