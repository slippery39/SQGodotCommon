namespace ImmutableGameObjects;

/// <summary>
/// Base class for all game events that occur during gameplay.
/// Events are transient - they last for one frame, then get cleared.
/// </summary>
public abstract record GameEvent
{
	public float Timestamp { get; init; }
}
