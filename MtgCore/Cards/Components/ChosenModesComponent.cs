using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Records which modes of a repeating modal ability have already been taken — "choose one that
/// hasn't been chosen" (Demonic Pact).
///
/// It lives on the CARD rather than on the ability because the exclusion has to survive between
/// separate resolutions of the same trigger, turn after turn. An ActivatedAbilityComponent's
/// ActivationCount is reset every turn by StartTurnAction, which is the opposite of what this
/// needs; and a pipeline's context does not outlive one resolution at all.
///
/// Plain data — a list of indices into the ability's mode list — so the card stays serializable.
/// </summary>
public record ChosenModesComponent : GameComponent
{
	public ImmutableList<int> ChosenIndices { get; init; } = ImmutableList<int>.Empty;
}
