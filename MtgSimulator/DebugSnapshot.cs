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

	/// <summary>
	/// Where the snapshot came from and, for a crash dump, the exception. Null for a manual
	/// export. First field to read when diagnosing: it says whether this state is a crime scene
	/// or just a scene.
	/// </summary>
	public string? Error { get; init; }
}

public record DebugHistoryEntry(
	int Index,
	string ActionDescription,
	PlayerSnapshot Player1,
	PlayerSnapshot Player2
);
