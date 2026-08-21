using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Matches a card with ANY of the given types — "target artifact or enchantment" (Disenchant)
/// is <c>new IsCardTypeSpecification { Types = CardType.Artifact | CardType.Enchantment }</c>.
///
/// Zone-agnostic: compose with a zone spec, or rely on the zone-first narrowing that
/// AndSpecification already does.
/// </summary>
public record IsCardTypeSpecification : TargetSpecification
{
	public CardType Types { get; init; } = CardType.AnyPermanent;

	public override bool IsSatisfiedBy(int candidateId, TargetingContext context) =>
		context.Find(candidateId) is Card card && card.HasType(Types);
}

/// <summary>
/// Matches a card with NONE of the given types — "nonland permanent" (Oblivion Ring) is
/// <c>new IsNotCardTypeSpecification { Types = CardType.Land }</c> composed with a
/// battlefield spec.
///
/// A separate type rather than wrapping IsCardTypeSpecification in NotSpecification, because
/// the negation must still require the candidate to BE a card — a plain Not would happily match
/// a player.
/// </summary>
public record IsNotCardTypeSpecification : TargetSpecification
{
	public CardType Types { get; init; } = CardType.Land;

	public override bool IsSatisfiedBy(int candidateId, TargetingContext context) =>
		context.Find(candidateId) is Card card && !card.HasType(Types);
}
