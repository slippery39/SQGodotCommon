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

/// <summary>
/// Matches any card currently in the casting player's hand.
/// Use with IsSubtypeSpecification to target e.g. "a Goblin in your hand."
/// </summary>
public record IsInHandSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (!context.GameState.HasObject(candidateId))
			return false;

		if (context.GameState.GetObject(candidateId) is not Card)
			return false;

		var handId = context.GameState.GetPlayerZoneId(context.CastingPlayerId, ZoneType.Hand);
		return context.GameState.GetCardZoneId(candidateId) == handId;
	}
}

/// <summary>
/// Matches when the candidate ID equals the SourceCardId of the targeting context.
/// Used in EventTriggerCondition filters to express "when this specific card triggers"
/// (e.g. Goblin Lackey fires only when Lackey itself deals combat damage).
/// </summary>
public record IsSourceCardSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context) =>
		candidateId == context.SourceCardId;
}

/// <summary>
/// Matches any candidate that is NOT the source card of the targeting context.
/// Used with lord/anthem effects to express "other creatures" (e.g. Goblin Chieftain
/// buffs other Goblins, not itself).
/// </summary>
public record IsNotSelfSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context) =>
		candidateId != context.SourceCardId;
}

/// <summary>
/// Never matches any candidate. Used as a safe default on StaticAbilityComponent
/// so the filter must be explicitly set on every concrete ability.
/// </summary>
public record AlwaysFalseSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context) => false;
}
