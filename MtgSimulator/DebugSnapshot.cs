namespace MtgSimulator;

/// <summary>
/// Rich debug snapshot for UI export — human-readable, not a round-trippable format.
/// Contains the full history of state summaries and AI decisions for a game session.
/// </summary>
public record DebugSnapshot
{
	public string ExportedAt { get; init; } = "";
	public int TurnNumber { get; init; }
	public string ActivePlayerName { get; init; } = "";
	public PlayerSnapshot CurrentPlayer1 { get; init; } = new();
	public PlayerSnapshot CurrentPlayer2 { get; init; } = new();
	public IReadOnlyList<DebugHistoryEntry> StateHistory { get; init; } = [];
	public IReadOnlyList<AiDecision> AiDecisions { get; init; } = [];
}

public record DebugHistoryEntry(
	int Index,
	string ActionDescription,
	PlayerSnapshot Player1,
	PlayerSnapshot Player2
);
