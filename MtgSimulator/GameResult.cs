namespace MtgSimulator;

public enum GameEndReason
{
	Damage,
	LibraryEmpty,
	TurnLimitReached,
	ActionLimitReached,
	TimeLimitReached,
}

/// <summary>
/// The result of a single simulated game.
/// </summary>
public record GameResult
{
	/// <summary>
	/// The winning player's ID, or -1 for a draw.
	/// </summary>
	public int WinnerPlayerId { get; init; }

	public int Player1Id { get; init; }
	public int Player2Id { get; init; }

	public GameEndReason EndReason { get; init; }
	public int TurnCount { get; init; }

	/// <summary>
	/// Total actions taken across all turns. High values may indicate loop issues.
	/// </summary>
	public int TotalActions { get; init; }

	/// <summary>
	/// Whether any turn exceeded the action warning threshold (50 actions).
	/// </summary>
	public bool HadActionWarning { get; init; }

	/// <summary>
	/// Wall-clock milliseconds the game took from first action to termination.
	/// </summary>
	public long GameDurationMs { get; init; }

	/// <summary>
	/// Path to the saved state file, or null if this game was not saved.
	/// </summary>
	public string? SavedFilePath { get; init; }

	/// <summary>
	/// Cards that were drawn by Player 1 during the game.
	/// Used for per-card win rate tracking.
	/// </summary>
	public IReadOnlyList<string> Player1DrawnCards { get; init; } = Array.Empty<string>();

	/// <summary>
	/// Cards that were drawn by Player 2 during the game.
	/// </summary>
	public IReadOnlyList<string> Player2DrawnCards { get; init; } = Array.Empty<string>();

	public bool IsPlayer1Win => WinnerPlayerId == Player1Id;
	public bool IsPlayer2Win => WinnerPlayerId == Player2Id;
	public bool IsDraw => WinnerPlayerId == -1;
	public bool IsFlagged =>
		EndReason
			is GameEndReason.TurnLimitReached
				or GameEndReason.ActionLimitReached
				or GameEndReason.TimeLimitReached
		|| HadActionWarning;
}
