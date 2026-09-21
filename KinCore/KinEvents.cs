using ImmutableGameObjects;

namespace KinCore;

public record BattleStartedEvent : GameEvent
{
	public string Name { get; init; } = "";
}

public record TurnStartedEvent : GameEvent
{
	public int TurnNumber { get; init; }
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

/// <summary>The Opponent put a body back in the line.</summary>
public record EnemySummonedEvent : GameEvent
{
	public string EnemyName { get; init; } = "";
	public int Lane { get; init; }
	public int Attack { get; init; }
}

/// <summary>The Opponent announced what it will summon NEXT turn. Always shown, never hidden.</summary>
public record EnemyTelegraphedEvent : GameEvent
{
	public string EnemyName { get; init; } = "";
	public int Lane { get; init; }
}

/// <summary>A lane you held with nothing opposing it landed on the Opponent.</summary>
public record OpponentDamagedEvent : GameEvent
{
	public int Amount { get; init; }
	public int HealthRemaining { get; init; }
}

/// <summary>The Opponent died. The only way a battle is WON.</summary>
public record OpponentDefeatedEvent : GameEvent;

/// <summary>Life hit 0. This ends the RUN, not just the battle.</summary>
public record PlayerDiedEvent : GameEvent;

/// <summary>Life came back. Rare enough to be worth announcing — nothing heals on its own.</summary>
public record LifeGainedEvent : GameEvent
{
	public int Amount { get; init; }
	public int LifeNow { get; init; }
}

/// <summary>
/// Something TOOK cards — off the board and out of every deck for a number of turns.
///
/// Carries the count and the wait so the UI need not work either out.
/// </summary>
public record CardsTakenEvent : GameEvent
{
	public int Count { get; init; }
	public int Turns { get; init; }
}

/// <summary>Taken cards have come back to your discard pile.</summary>
public record CardsReturnedEvent : GameEvent
{
	public int Count { get; init; }
}

/// <summary>
/// The units you placed this turn left the field at the end of it — Combat v3.
///
/// **Distinct from <see cref="UnitDiedEvent"/> on purpose.** Withdrawing is not dying, and the front
/// end must not animate it as a death: one is the turn ending, the other is something you lost.
/// </summary>
public record UnitsWithdrewEvent : GameEvent
{
	public int Count { get; init; }
}

/// <summary>
/// A unit's stats changed. Carries the DELTA, not the new totals, because that is what the front
/// end animates and what a log line wants to say.
/// </summary>
public record UnitBuffedEvent : GameEvent
{
	public int CardId { get; init; }
	public string CardName { get; init; } = "";
	public int Power { get; init; }
	public int Toughness { get; init; }
}
