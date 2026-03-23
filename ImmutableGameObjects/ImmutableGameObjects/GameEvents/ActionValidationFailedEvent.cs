namespace ImmutableGameObjects;

/// <summary>
/// Emitted when an action fails ValidateResolve and is removed from the stack.
/// The game layer listens for this to decide what happens next —
/// e.g. move a spell to the graveyard, trigger "whenever a spell is countered" abilities, etc.
/// </summary>
public record ActionValidationFailedEvent : GameEvent
{
	public GameAction Action { get; init; } = null!;
	public string Reason { get; init; } = "";
}
