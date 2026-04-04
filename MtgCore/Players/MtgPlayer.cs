using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Represents a player in the game.
/// A player owns their personal zones (Hand, Library, Graveyard, Battlefield, Exile)
/// as children in the GameState hierarchy.
///
/// CurrentMana is how much mana the player has available to spend this turn.
/// MaxMana is the cap that grows by 1 each turn (Hearthstone style), capped at 10.
/// Both are set to 0 at creation — MtgGameFactory stamps the starting player's
/// mana to 1/1 after setup.
/// </summary>
public record MtgPlayer : GameObject
{
	public int Life { get; init; } = 20;
	public bool HasLost { get; init; } = false;
	public int CurrentMana { get; init; } = 0;
	public int MaxMana { get; init; } = 0;
}
