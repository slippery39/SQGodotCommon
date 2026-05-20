using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// A persistent triggered ability owned by a player rather than a card.
/// Granted by special lands via GrantEmblemComponent when played.
/// Scanned by CheckStateBasedEffectsAction alongside battlefield/graveyard triggers.
/// </summary>
public record Emblem
{
	public string Name { get; init; } = "";
	public TriggerCondition Condition { get; init; } = null!;
	public CardEffect Effect { get; init; } = null!;
}
