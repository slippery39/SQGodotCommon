using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Abstract base for static abilities on battlefield permanents.
/// A static ability applies a continuous effect to cards that satisfy its Filter.
///
/// Subclasses own their specific effect logic. CreatureEvaluator scans all
/// battlefield permanents for StaticAbilityComponents and applies them.
///
/// Filter is evaluated with SourceCardId set to the permanent that carries
/// the ability, so IsNotSelfSpecification correctly excludes the source card.
/// </summary>
public abstract record StaticAbilityComponent : GameComponent
{
	/// <summary>
	/// Determines which cards this static ability applies to.
	/// Evaluated against each candidate creature at read time.
	/// </summary>
	public TargetSpecification Filter { get; init; } = new AlwaysFalseSpecification();
}
