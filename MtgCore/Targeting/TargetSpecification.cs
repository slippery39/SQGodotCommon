using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Base record for all target specifications.
/// A specification answers one question: is this candidate a valid target?
/// Specifications are pure data — fully serializable, no delegates.
///
/// Compose them with And(), Or(), Not() to express complex targeting rules.
/// Example: new IsPlayerSpecification().Or(new IsCreatureSpecification())
/// </summary>
public abstract record TargetSpecification
{
	public abstract bool IsSatisfiedBy(int candidateId, TargetingContext context);

	public TargetSpecification And(TargetSpecification other) =>
		new AndSpecification { Left = this, Right = other };

	public TargetSpecification Or(TargetSpecification other) =>
		new OrSpecification { Left = this, Right = other };

	public TargetSpecification Not() => new NotSpecification { Inner = this };
}

// ===== COMPOSITES =====

public record AndSpecification : TargetSpecification
{
	public TargetSpecification Left { get; init; } = null!;
	public TargetSpecification Right { get; init; } = null!;

	public override bool IsSatisfiedBy(int candidateId, TargetingContext context) =>
		Left.IsSatisfiedBy(candidateId, context) && Right.IsSatisfiedBy(candidateId, context);
}

public record OrSpecification : TargetSpecification
{
	public TargetSpecification Left { get; init; } = null!;
	public TargetSpecification Right { get; init; } = null!;

	public override bool IsSatisfiedBy(int candidateId, TargetingContext context) =>
		Left.IsSatisfiedBy(candidateId, context) || Right.IsSatisfiedBy(candidateId, context);
}

public record NotSpecification : TargetSpecification
{
	public TargetSpecification Inner { get; init; } = null!;

	public override bool IsSatisfiedBy(int candidateId, TargetingContext context) =>
		!Inner.IsSatisfiedBy(candidateId, context);
}
