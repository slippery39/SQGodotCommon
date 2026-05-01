namespace MtgSimulator;

/// <summary>
/// Human-readable snapshot of game state at the moment a game was flagged.
/// Intended for manual inspection — not a round-trippable replay format.
/// </summary>
public record GameStateSnapshot
{
	public string FlagReason { get; init; } = "";
	public long DurationMs { get; init; }
	public int TurnNumber { get; init; }
	public string ActivePlayerName { get; init; } = "";
	public int TotalActions { get; init; }
	public IReadOnlyList<string> StackCards { get; init; } = [];
	public PlayerSnapshot Player1 { get; init; } = new();
	public PlayerSnapshot Player2 { get; init; } = new();
	public IReadOnlyList<TurnLog> TurnLogs { get; init; } = [];
	public string? ExceptionMessage { get; init; }
	public string? ExceptionStackTrace { get; init; }
}

public record PlayerSnapshot
{
	public string Name { get; init; } = "";
	public int Life { get; init; }
	public int CurrentMana { get; init; }
	public int MaxMana { get; init; }
	public IReadOnlyList<string> HandCards { get; init; } = [];
	public int LibraryCount { get; init; }
	public IReadOnlyList<string> GraveyardCards { get; init; } = [];
	public IReadOnlyList<CreatureSnapshot> Battlefield { get; init; } = [];
}

public record CreatureSnapshot
{
	public string Name { get; init; } = "";
	public int Power { get; init; }
	public int Toughness { get; init; }
	public int Damage { get; init; }
	public bool HasSummoningSickness { get; init; }
	public bool HasAttacked { get; init; }
}

public record TurnLog
{
	public int TurnNumber { get; init; }
	public string PlayerName { get; init; } = "";
	public IReadOnlyList<string> Events { get; init; } = [];
}
