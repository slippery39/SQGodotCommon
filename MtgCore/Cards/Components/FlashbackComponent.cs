using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Marks a card as castable from the graveyard for an alternate mana cost.
///
/// On an instant or sorcery this is Flashback: the spell resolves and the card is then
/// exiled instead of returning to the graveyard, so it can only be replayed once.
///
/// On a creature this is graveyard recursion (Gravecrawler / unearth / disturb): the
/// creature enters the battlefield and is NOT exiled, so it can be recurred every time it
/// dies. That is intentionally repeatable — the mana cost is the only limiter, which keeps
/// the loop bounded without needing an extra restriction mechanism.
/// </summary>
public record FlashbackComponent : GameComponent
{
	public int FlashbackManaCost { get; init; }
}
