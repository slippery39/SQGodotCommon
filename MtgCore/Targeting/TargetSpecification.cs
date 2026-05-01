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

	/// <summary>
	/// Returns the candidate IDs to evaluate. Override to return only a relevant zone's
	/// IDs instead of all game objects. IsSatisfiedBy is the correctness gate — this is
	/// a performance superset only.
	/// </summary>
	public virtual IEnumerable<int> GetCandidateIds(TargetingContext context) =>
		context.GameState.IdToGameObjectMap.Keys;

	public TargetSpecification And(TargetSpecification other) =>
		new AndSpecification { Left = this, Right = other };

	public TargetSpecification Or(TargetSpecification other) =>
		new OrSpecification { Left = this, Right = other };

	public TargetSpecification Not() => new NotSpecification { Inner = this };

	public static TargetSpecification PlayersOrCreatures() =>
		new IsPlayerSpecification().Or(new IsCreatureSpecification());

	public static TargetSpecification CreatureControlledByYou() =>
		new IsCreatureSpecification().And(new IsControlledByYouSpecification());

	/// <summary>
	/// Matches any creature you control except the source object itself. Used for cards like Goblin Chieftan. Assumes we are counting creatures in play.
	/// </summary>
	/// <returns></returns>
	public static TargetSpecification OtherCreaturesYouControl() =>
		new IsCreatureSpecification()
			.And(new IsControlledByYouSpecification())
			.And(new IsNotSelfSpecification());

	public static TargetSpecification OpponentCreatures() =>
		new IsCreatureSpecification().And(new IsControlledByOpponentSpecification());

	public static TargetSpecification OpponentOrOpponentCreatures() =>
		PlayersOrCreatures().And(new IsControlledByOpponentSpecification());
}

/// <summary>
/// Base for all zone-scoped specifications. Subclasses return only the IDs from
/// their target zone(s). AndSpecification automatically prefers a ZoneSpecification's
/// narrow candidate set over a full-map scan when composing specs.
/// </summary>
public abstract record ZoneSpecification : TargetSpecification
{
	public abstract override IEnumerable<int> GetCandidateIds(TargetingContext context);
}

// ===== COMPOSITES =====

public record AndSpecification : TargetSpecification
{
	public TargetSpecification Left { get; init; } = null!;
	public TargetSpecification Right { get; init; } = null!;

	public override bool IsSatisfiedBy(int candidateId, TargetingContext context) =>
		Left.IsSatisfiedBy(candidateId, context) && Right.IsSatisfiedBy(candidateId, context);

	/// <summary>
	/// Prefers the ZoneSpecification side's narrow candidates when one side is zone-scoped.
	/// Falls back to Left's candidates otherwise (which may itself be a narrowed spec).
	/// </summary>
	public override IEnumerable<int> GetCandidateIds(TargetingContext context)
	{
		if (Left is ZoneSpecification)
			return Left.GetCandidateIds(context);
		if (Right is ZoneSpecification)
			return Right.GetCandidateIds(context);
		return Left.GetCandidateIds(context);
	}
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
