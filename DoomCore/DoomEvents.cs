using ImmutableGameObjects;

namespace DoomCore;

public record BattleStartedEvent : GameEvent
{
	public DoomScenario Scenario { get; init; }
	public int Countdown { get; init; }
}

public record TurnStartedEvent : GameEvent
{
	public int TurnNumber { get; init; }
	public int CountdownRemaining { get; init; }
}

public record CardPlayedEvent : GameEvent
{
	public int CardId { get; init; }
	public string CardName { get; init; } = "";
	public int EnergySpent { get; init; }
}

public record UnitDiedEvent : GameEvent
{
	public int CardId { get; init; }
	public int RunCardId { get; init; }
	public string CardName { get; init; } = "";
}

public record EnemyDiedEvent : GameEvent
{
	public int EnemyId { get; init; }
	public string EnemyName { get; init; } = "";
}

public record PlayerDamagedEvent : GameEvent
{
	public int Amount { get; init; }
	public int LifeRemaining { get; init; }

	/// <summary>Damage absorbed by blockers before the rest came through. For UI, and for tuning.</summary>
	public int Absorbed { get; init; }
}

public record CountdownTickedEvent : GameEvent
{
	public int Remaining { get; init; }
}

/// <summary>
/// The apocalypse landed. Carries what the board looked like at that instant, because that — not
/// the battle log — is what a doom transform reads.
/// </summary>
public record DoomResolvedEvent : GameEvent
{
	public DoomScenario Scenario { get; init; }
}

/// <summary>Life hit 0. This ends the RUN, not just the battle.</summary>
public record PlayerDiedEvent : GameEvent;
