using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Marks a card as having an activated ability.
/// A card may have multiple ActivatedAbilityComponents — one per ability.
///
/// MaxActivationsPerTurn controls how many times the ability may fire per turn.
/// 1 (default) = once per turn. 0 = unlimited (e.g. Arcbound Ravager).
/// ActivationCount tracks uses this turn and is reset to 0 by StartTurnAction.
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
	public int MaxActivationsPerTurn { get; init; } = 1;
	public int ActivationCount { get; init; } = 0;
	public bool RequiresTap { get; init; } = false;
}
