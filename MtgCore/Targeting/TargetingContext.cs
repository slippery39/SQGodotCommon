using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Context passed to TargetSpecifications when evaluating whether a candidate
/// is a valid target. Contains everything a specification needs to make its decision.
/// </summary>
public record TargetingContext
{
	public GameState GameState { get; init; } = null!;

	/// <summary>
	/// The ID of the card or ability that is looking for targets.
	/// </summary>
	public int SourceCardId { get; init; }

	/// <summary>
	/// The player who controls the source — used to determine
	/// "your creatures", "opponent's creatures", etc.
	/// </summary>
	public int CastingPlayerId { get; init; }
}
