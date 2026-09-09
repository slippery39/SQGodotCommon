using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// The colours a land adds to its controller's pool when played.
///
/// Independent of how much GENERIC mana the land makes — that stays with
/// <see cref="BonusManaLandComponent"/>. The two axes are deliberately separate, because the
/// land types they combine to express are separate:
///   - basic         — 1 generic, 1 of its colour        (this component alone)
///   - dual          — 1 generic, 1 of EACH of two colours (Produces with two entries set)
///   - tap land      — as either of the above, but nothing until next turn (BonusMana.Deferred)
///   - colourless    — 1 generic, no colour                (neither component)
///
/// A dual granting both colours is not the strict upgrade it looks like: generic mana still caps
/// total spend, so the second colour buys breadth within a turn, never more mana. See
/// <see cref="ManaPool"/>.
/// </summary>
public record LandColorComponent : GameComponent
{
	public ManaPool Produces { get; init; } = ManaPool.Empty;
}
