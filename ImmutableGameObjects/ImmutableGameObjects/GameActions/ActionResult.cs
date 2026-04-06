using System.Collections.Immutable;

namespace ImmutableGameObjects;

/// <summary>
/// Result of executing an action. Returns the new game state and any events
/// emitted during execution.
///
/// Actions that need to queue follow-up work should call state.SpawnAction()
/// or state.SpawnActions() directly within Execute() rather than returning
/// spawned actions here. This keeps the action's intent explicit and removes
/// the need for a separate reconciliation step in the executor.
/// </summary>
public record ActionResult
{
	public GameState GameState { get; init; }

	// Data that can be passed to the next action in a pipeline
	public ImmutableDictionary<string, object> OutputData { get; init; } =
		ImmutableDictionary<string, object>.Empty;

	/// <summary>
	/// Events emitted by this action. Collected and returned to the caller
	/// by ProcessNextAction / ProcessAllActions / ResolveChoice.
	/// Events are never stored in GameState — they are transient and exist
	/// only for the duration of the transition that produced them.
	/// </summary>
	public ImmutableList<GameEvent> Events { get; init; } = ImmutableList<GameEvent>.Empty;

	public ActionResult(GameState gameState)
	{
		GameState = gameState;
	}

	public ActionResult WithOutput(string key, object value)
	{
		return this with { OutputData = OutputData.Add(key, value) };
	}

	public ActionResult WithEvent(GameEvent gameEvent)
	{
		return this with { Events = Events.Add(gameEvent) };
	}

	public ActionResult WithEvents(IEnumerable<GameEvent> events)
	{
		return this with { Events = Events.AddRange(events) };
	}
}
