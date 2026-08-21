using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Matches any card on the battlefield that has the given subtype.
/// Used for tribal effects (e.g. target Goblin, target Beast).
/// </summary>
public record IsSubtypeSpecification : TargetSpecification
{
	public string Subtype { get; init; } = "";

	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (!context.GameState.HasObject(candidateId))
			return false;

		return context.Find(candidateId) is Card card && card.HasSubtype(Subtype);
	}
}
