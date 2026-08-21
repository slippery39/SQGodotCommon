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

	/// <summary>
	/// True when the effect is a mass/non-targeted effect (AllValid mode).
	/// Hexproof and Shroud only protect against targeted effects — mass effects
	/// like Wrath of God bypass them entirely.
	/// </summary>
	public bool IsNonTargeted { get; init; }

	/// <summary>
	/// The candidate object, or null when nothing with that id is in the game.
	/// **Every specification must look candidates up through this, never
	/// <c>GameState.GetObject</c>**, which is a raw dictionary indexer and throws.
	///
	/// A specification is asked about ids that may be stale — a stored `TargetIds` entry
	/// naming a creature that has since died, or an action being re-validated against an
	/// earlier state. The answer there is "not a legal target", which is exactly what a null
	/// flowing into the `is Card card` patterns below produces. Throwing instead killed a
	/// whole simulated game: `KeyNotFoundException` out of `IsCardTypeSpecification` via
	/// `CastSpellAction.ValidateTargets`, caught only as an UnhandledException game result.
	///
	/// Every other layer of the engine already guards this way — `HasObject` appears at 20+
	/// action sites. The targeting layer was the one that did not.
	/// </summary>
	public GameObject? Find(int candidateId) =>
		GameState.HasObject(candidateId) ? GameState.GetObject(candidateId) : null;
}
