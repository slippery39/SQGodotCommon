namespace MtgCore;

/// <summary>
/// The current phase of the active player's turn.
/// Kept minimal for now — expands as needed.
/// </summary>
public enum TurnPhase
{
	/// <summary>
	/// The active player may play cards and attack.
	/// </summary>
	Main,

	/// <summary>
	/// The turn is over. EndTurnAction transitions to the next player's turn.
	/// </summary>
	End,
}
