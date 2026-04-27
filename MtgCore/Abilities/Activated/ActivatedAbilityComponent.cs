using System.Collections.Immutable;
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
/// ManaCost is the primary mana cost (0 = free). AdditionalCosts holds any
/// extra costs beyond mana (sacrifice, discard, life payment, etc.).
/// The effect uses the existing CardEffect infrastructure — same targeting and
/// action template system as spells.
/// </summary>
public record ActivatedAbilityComponent : GameComponent
{
	public string Name { get; init; } = "";
	public int ManaCost { get; init; }
	public ImmutableList<AdditionalCost> AdditionalCosts { get; init; } =
		ImmutableList<AdditionalCost>.Empty;
	public CardEffect Effect { get; init; } = null!;
	public bool HasActivated { get; init; } = false;
}
