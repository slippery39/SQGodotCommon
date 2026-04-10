using ImmutableGameObjects;

namespace MtgCore;

public record PlayerDamagedEvent : GameEvent
{
	public int PlayerId { get; init; }
	public int Amount { get; init; }
}

public record CreatureDamagedEvent : GameEvent
{
	public int CreatureId { get; init; }
	public int Amount { get; init; }
}

public record CreatureDestroyedEvent : GameEvent
{
	public int CreatureId { get; init; }
}

public record CreatureAttackedEvent : GameEvent
{
	public int CreatureId { get; init; }
	public int AttackingPlayerId { get; init; }
}

public record SpellCastEvent : GameEvent
{
	public int CardId { get; init; }
	public int CastingPlayerId { get; init; }
}

public record PlayerGainedLifeEvent : GameEvent
{
	public int PlayerId { get; init; }
	public int Amount { get; init; }
}

public record SpellResolvedEvent : GameEvent
{
	public int CardId { get; init; }
}

public record CardDrawnEvent : GameEvent
{
	public int PlayerId { get; init; }
	public int CardId { get; init; }
}

public record LibraryEmptyEvent : GameEvent
{
	public int PlayerId { get; init; }
}

public record CardRevealedEvent : GameEvent
{
	public int PlayerId { get; init; }
	public int CardId { get; init; }
	public int ManaCost { get; init; }
}

public record PlayerLostLifeEvent : GameEvent
{
	public int PlayerId { get; init; }
	public int Amount { get; init; }
}

public record CardDiscardedEvent : GameEvent
{
	public int PlayerId { get; init; }
	public int CardId { get; init; }
}

public record CreaturePlayedEvent : GameEvent
{
	public int CardId { get; init; }
	public int PlayerId { get; init; }
}

/// <summary>
/// Emitted when a player's loss condition is triggered (life <= 0 or empty library).
/// </summary>
public record PlayerLostEvent : GameEvent
{
	public int PlayerId { get; init; }
	public string Reason { get; init; } = "";
}

/// <summary>
/// Emitted when the game ends. Contains the winning player ID, or -1 for a draw.
/// </summary>
public record GameOverEvent : GameEvent
{
	/// <summary>
	/// The winning player's ID, or -1 if the game ended in a draw.
	/// </summary>
	public int WinnerPlayerId { get; init; }
}

public record CreatureModifiedEvent : GameEvent
{
	public int CreatureId { get; init; }
	public int PowerBonus { get; init; }
	public int ToughnessBonus { get; init; }
}

public record TurnStartedEvent : GameEvent
{
	public int PlayerId { get; init; }
}

public record TurnEndedEvent : GameEvent
{
	public int PlayerId { get; init; }
}
