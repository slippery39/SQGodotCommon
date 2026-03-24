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

public record SpellCastEvent : GameEvent
{
	public int CardId { get; init; }
	public int CastingPlayerId { get; init; }
}

public record SpellResolvedEvent : GameEvent
{
	public int CardId { get; init; }
}
