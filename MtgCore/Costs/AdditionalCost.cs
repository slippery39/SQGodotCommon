using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Base record for all additional costs beyond mana.
///
/// Two categories:
///   - Resource costs (Life): validate against player state, no selection needed.
///   - Selection costs (Sacrifice, Discard): player must choose game objects to pay.
///
/// Mana is always the primary cost (ManaCost on Card / ActivatedAbilityComponent).
/// Additional costs are paid before mana in Execute, before the card hits the stack.
/// All subclasses must be fully serializable (no delegates or lambdas).
/// </summary>
public abstract record AdditionalCost
{
	/// <summary>True if the player must select game objects to pay this cost.</summary>
	public abstract bool RequiresSelection { get; }

	/// <summary>
	/// Player-facing instruction for paying this cost, e.g. "Discard a card". Lives here rather
	/// than in a presentation layer so console and Godot say the same thing, and so a new cost
	/// type cannot ship without one.
	/// </summary>
	public abstract string Describe();

	/// <summary>
	/// Returns the IDs the player can choose as payment.
	/// Always returns empty for resource costs.
	/// </summary>
	public abstract ImmutableList<int> GetValidPayments(
		GameState state,
		int castingPlayerId,
		int sourceCardId
	);

	/// <summary>
	/// Validates whether the payment is legal.
	/// For resource costs paymentIds is always empty — validation checks player state directly.
	/// </summary>
	public abstract ValidationResult Validate(
		GameState state,
		int castingPlayerId,
		int sourceCardId,
		ImmutableList<int> paymentIds
	);

	/// <summary>Executes the payment and returns the updated state.</summary>
	public abstract GameState Pay(
		GameState state,
		int castingPlayerId,
		int sourceCardId,
		ImmutableList<int> paymentIds
	);
}
