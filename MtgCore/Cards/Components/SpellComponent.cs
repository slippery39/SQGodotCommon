using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Marks a card as a spell and holds the effects that resolve when it is cast.
/// Cards with this component are moved to the graveyard after resolving.
///
/// Instants and Sorceries both use this component — the timing difference
/// is enforced by game rules, not by a separate type.
///
/// HasStorm: when true, ResolveSpellAction repeats the Effects once per spell cast
/// this turn (including this spell). Storm count = Math.Max(SpellsCastThisTurn, 1).
/// </summary>
public record SpellComponent : GameComponent
{
	public ImmutableList<CardEffect> Effects { get; init; } = ImmutableList<CardEffect>.Empty;
	public bool HasStorm { get; init; } = false;
}
