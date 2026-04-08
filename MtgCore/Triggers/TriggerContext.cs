using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Context passed to TriggerCondition.IsSatisfiedBy when evaluating whether
/// a trigger event should fire a particular triggered ability.
///
/// Provides the game state at the time of evaluation, the card that owns
/// the triggered ability, and the player who controls it.
/// </summary>
public record TriggerContext
{
	public GameState GameState { get; init; } = null!;

	/// <summary>
	/// The card that owns the TriggeredAbilityComponent being evaluated.
	/// </summary>
	public int SourceCardId { get; init; }

	/// <summary>
	/// The player who controls the card with the triggered ability.
	/// Used for conditions like "when your creature dies" vs "when any creature dies".
	/// </summary>
	public int ControllingPlayerId { get; init; }
}
