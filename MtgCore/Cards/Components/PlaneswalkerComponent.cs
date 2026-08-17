using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Marks a card as a planeswalker and holds its loyalty.
///
/// Sits alongside PermanentComponent, never alongside CreatureComponent, so a planeswalker
/// routes through CastPermanentAction like any other non-creature permanent.
///
/// Loyalty is a hit-point pool that only combat and the walker's own abilities touch. It is NOT
/// a PowerToughnessModifier and not a +1/+1 counter — a planeswalker has no power or toughness.
///
/// HasActivatedThisTurn is per-PLANESWALKER, not per-ability: the real rule is one loyalty
/// ability per walker per turn, so it cannot live on ActivatedAbilityComponent.ActivationCount
/// (which is per-ability and would let a walker use its +1 and its -3 in the same turn).
/// Cleared by StartTurnAction for the active player.
/// </summary>
public record PlaneswalkerComponent : GameComponent
{
	public int StartingLoyalty { get; init; }

	/// <summary>
	/// Current loyalty. Stamped to StartingLoyalty by PutIntoBattlefieldAction's ETB ceremony,
	/// so a walker that dies and is later reanimated comes back at full loyalty.
	/// </summary>
	public int Loyalty { get; init; }

	public bool HasActivatedThisTurn { get; init; }
}
