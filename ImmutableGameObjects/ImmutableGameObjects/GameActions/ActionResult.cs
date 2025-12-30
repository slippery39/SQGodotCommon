using System.Collections.Immutable;

namespace ImmutableGameObjects;

/// <summary>
/// Result of executing an action. Can return new state and/or spawn new actions.
/// </summary>
public record ActionResult
{
	public GameState GameState { get; init; }
	public ImmutableList<GameAction> SpawnedActions { get; init; } =
		ImmutableList<GameAction>.Empty;

	// Data that can be passed to next action in the pipeline
	public ImmutableDictionary<string, object> OutputData { get; init; } =
		ImmutableDictionary<string, object>.Empty;

	public ActionResult(GameState gameState)
	{
		GameState = gameState;
	}

	// Helper to add output data
	public ActionResult WithOutput(string key, object value)
	{
		return this with { OutputData = OutputData.Add(key, value) };
	}
}
