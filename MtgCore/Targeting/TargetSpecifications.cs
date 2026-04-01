using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Matches any MtgPlayer object that hasn't lost.
/// </summary>
public record IsPlayerSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (!context.GameState.HasObject(candidateId))
			return false;

		var obj = context.GameState.GetObject(candidateId);
		return obj is MtgPlayer player && !player.HasLost;
	}
}

/// <summary>
/// Matches any card with a CreatureComponent currently on any battlefield.
/// </summary>
public record IsCreatureSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (!context.GameState.HasObject(candidateId))
			return false;

		var obj = context.GameState.GetObject(candidateId);
		if (obj is not Card card)
			return false;

		if (!card.HasComponent<CreatureComponent>())
			return false;

		var zone = context.GameState.GetCardZone(candidateId);
		return zone.ZoneType == ZoneType.Battlefield;
	}
}

/// <summary>
/// Matches only objects controlled by the casting player.
/// </summary>
public record IsControlledByYouSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (!context.GameState.HasObject(candidateId))
			return false;

		var obj = context.GameState.GetObject(candidateId);

		return obj switch
		{
			MtgPlayer player => player.Id == context.CastingPlayerId,
			Card card => card.ControllerId == context.CastingPlayerId,
			_ => false,
		};
	}
}

/// <summary>
/// Matches only objects controlled by an opponent of the casting player.
/// </summary>
public record IsControlledByOpponentSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (!context.GameState.HasObject(candidateId))
			return false;

		var obj = context.GameState.GetObject(candidateId);

		return obj switch
		{
			MtgPlayer player => player.Id != context.CastingPlayerId,
			Card card => card.ControllerId != context.CastingPlayerId,
			_ => false,
		};
	}
}
