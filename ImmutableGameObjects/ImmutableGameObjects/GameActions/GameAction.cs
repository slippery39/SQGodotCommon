using System.Collections.Immutable;

namespace ImmutableGameObjects;

/// <summary>
/// Base class for all game actions/commands.
/// </summary>
public abstract record GameAction
{
	// Context data passed from previous action in pipeline actions.
	// Pipeline actions are actions where we use the previous actions output as input for the next action.
	public ImmutableDictionary<string, object> InputContext { get; init; } =
		ImmutableDictionary<string, object>.Empty;

	public abstract ActionResult Execute(GameState gameState);

	// Helper to get input from context
	protected T GetInput<T>(string key, T defaultValue = default!)
	{
		return InputContext.TryGetValue(key, out var value) ? (T)value : defaultValue;
	}
}
