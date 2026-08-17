using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Exalted — "whenever a creature you control attacks alone, that creature gets +1/+1 until
/// end of turn."
///
/// Modelled as a COUNT rather than a boolean because exalted stacks: a player with three
/// instances gives their lone attacker +3/+3. Sublime Archangel grants exalted to every other
/// creature you control, so instances scale with the board and a boolean would lose that.
///
/// The bonus is applied by AttackAction.CountExaltedIfAttackingAlone at the moment of attack,
/// not by StaticAbilityEngine. It has to be: "attacks alone" is only knowable during an attack,
/// and the push model has no event to re-stamp on.
///
/// Granted instances live on AppliedKeywordComponent.GrantsExalted and are counted alongside
/// these.
/// </summary>
public record ExaltedComponent : GameComponent
{
	public int Count { get; init; } = 1;
}
