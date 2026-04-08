using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Base record for all trigger conditions.
/// A condition answers: does this GameEvent fire this triggered ability?
///
/// Conditions are pure data — fully serializable, no delegates.
/// IsSatisfiedBy receives the GameEvent that occurred and the context
/// of the card that owns the triggered ability.
///
/// More complex filtering (e.g. "when a green creature dies") will be added
/// by extending concrete condition types with filter specifications,
/// following the same pattern as TargetSpecification.
/// </summary>
public abstract record TriggerCondition
{
	public abstract bool IsSatisfiedBy(GameEvent gameEvent, TriggerContext context);
}

/// <summary>
/// Fires when a creature is destroyed (CreatureDestroyedEvent).
///
/// OnlyYourCreatures — if true, only fires for creatures you controlled.
/// OnlyOpponentCreatures — if true, only fires for creatures your opponent controlled.
/// Both false means fire on any creature death.
/// </summary>
public record CreatureDiesCondition : TriggerCondition
{
	public bool OnlyYourCreatures { get; init; } = false;
	public bool OnlyOpponentCreatures { get; init; } = false;

	public override bool IsSatisfiedBy(GameEvent gameEvent, TriggerContext context)
	{
		if (gameEvent is not CreatureDestroyedEvent destroyed)
			return false;

		// Look up the creature's controller from the game state.
		// The creature has already moved to the graveyard at this point,
		// but the object still exists and retains its ControllerId.
		if (!context.GameState.HasObject(destroyed.CreatureId))
			return false;

		var creature = context.GameState.GetObject(destroyed.CreatureId) as Card;
		if (creature == null)
			return false;

		if (OnlyYourCreatures && creature.ControllerId != context.ControllingPlayerId)
			return false;

		if (OnlyOpponentCreatures && creature.ControllerId == context.ControllingPlayerId)
			return false;

		return true;
	}
}

/// <summary>
/// Fires when a creature enters the battlefield (CreaturePlayedEvent).
///
/// OnlyYourCreatures — if true, only fires when a creature you control enters.
/// OnlyOpponentCreatures — if true, only fires when an opponent's creature enters.
/// Both false means fire on any creature entering.
/// </summary>
public record CreatureEntersBattlefieldCondition : TriggerCondition
{
	public bool OnlyYourCreatures { get; init; } = false;
	public bool OnlyOpponentCreatures { get; init; } = false;

	public override bool IsSatisfiedBy(GameEvent gameEvent, TriggerContext context)
	{
		if (gameEvent is not CreaturePlayedEvent played)
			return false;

		if (OnlyYourCreatures && played.PlayerId != context.ControllingPlayerId)
			return false;

		if (OnlyOpponentCreatures && played.PlayerId == context.ControllingPlayerId)
			return false;

		return true;
	}
}

/// <summary>
/// Fires when a creature attacks (CreatureAttackedEvent).
///
/// OnlyYourCreatures — if true, only fires when a creature you control attacks.
/// OnlyOpponentCreatures — if true, only fires when an opponent's creature attacks.
/// Both false means fire on any attack.
/// </summary>
public record CreatureAttacksCondition : TriggerCondition
{
	public bool OnlyYourCreatures { get; init; } = false;
	public bool OnlyOpponentCreatures { get; init; } = false;

	public override bool IsSatisfiedBy(GameEvent gameEvent, TriggerContext context)
	{
		if (gameEvent is not CreatureAttackedEvent attacked)
			return false;

		if (OnlyYourCreatures && attacked.AttackingPlayerId != context.ControllingPlayerId)
			return false;

		if (OnlyOpponentCreatures && attacked.AttackingPlayerId == context.ControllingPlayerId)
			return false;

		return true;
	}
}
