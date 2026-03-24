using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Base record for all cards in the game.
/// Cards are always children of a Zone in the GameState hierarchy.
/// The current zone is determined by querying the parent via GameState.GetParent().
/// </summary>
public record Card : GameObject
{
	public int ManaCost { get; init; }

	/// <summary>
	/// The player who owns this card (does not change when the card moves zones).
	/// </summary>
	public int OwnerId { get; init; }

	/// <summary>
	/// The player who currently controls this card.
	/// Starts equal to OwnerId. Can differ due to control-changing effects.
	/// </summary>
	public int ControllerId { get; init; }
}
