using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Marks a card as a creature and holds its combat-relevant data.
/// A card with this component can enter the battlefield, attack, block,
/// and receive damage.
///
/// Base power and toughness are the printed values on the card.
/// Damage is marked damage accumulated this turn and resets at end of turn.
/// HasSummoningSickness prevents attacking the turn the creature enters
/// the battlefield — cleared at the start of the controller's next turn.
/// HasAttacked prevents a creature from attacking more than once per turn —
/// cleared at the start of the controller's next turn alongside summoning sickness.
/// </summary>
public record CreatureComponent : GameComponent
{
	public int Power { get; init; }
	public int Toughness { get; init; }
	public int Damage { get; init; } = 0;
	public bool HasSummoningSickness { get; init; } = true;
	public bool HasAttacked { get; init; } = false;
	public bool HasHaste { get; init; } = false;
	public bool HasDoubleStrike { get; init; } = false;
}
