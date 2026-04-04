using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Marks a card as having an activated ability.
/// A card may have multiple ActivatedAbilityComponents — one per ability.
///
/// Activated abilities can be used once per turn (HasActivated flag).
/// HasActivated is cleared by StartTurnAction at the start of the controller's turn,
/// the same way HasAttacked is cleared on CreatureComponent.
///
/// Cost is mana only for now. Tap costs and other costs may be added later.
/// The effect uses the existing CardEffect infrastructure — same targeting and
/// action template system as spells.
/// </summary>
public record ActivatedAbilityComponent : GameComponent
{
	public string Name { get; init; } = "";
	public int ManaCost { get; init; }
	public CardEffect Effect { get; init; } = null!;
	public bool HasActivated { get; init; } = false;
}
