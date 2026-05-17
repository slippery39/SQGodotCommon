using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Represents a player in the game.
/// A player owns their personal zones (Hand, Library, Graveyard, Battlefield, Exile)
/// as children in the GameState hierarchy.
///
/// CurrentMana is how much mana the player can spend this turn.
/// MaxMana is permanent mana from lands — increases each time a land is played.
/// CurrentMana is refilled to MaxMana at the start of each turn.
/// LandsPlayedThisTurn resets each turn; limits land plays to 1 (or more with Exploration).
/// LandsPlayedTotal never resets; used by Land Elemental's dynamic P/T.
/// </summary>
public record MtgPlayer : GameObject
{
	public int Life { get; init; } = 20;
	public bool HasLost { get; init; } = false;
	public int CurrentMana { get; init; } = 0;
	public int MaxMana { get; init; } = 0;
	public int LandsPlayedThisTurn { get; init; } = 0;
	public int LandsPlayedTotal { get; init; } = 0;
}
