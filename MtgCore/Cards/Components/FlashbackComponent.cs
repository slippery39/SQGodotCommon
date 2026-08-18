using System.Collections.Immutable;
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

	/// <summary>
	/// Costs beyond mana that must be paid to cast from the graveyard — Despoiler of Souls'
	/// "exile two other creature cards from your graveyard", Demonic Embrace's discard.
	///
	/// On a repeatable creature recursion this is the only thing that can bound the loop other
	/// than mana, and it is usually the point of the card: a recursion that eats its own
	/// graveyard has a hard limit and interacts with graveyard hate, while a purely mana-limited
	/// one just comes back forever.
	/// </summary>
	public ImmutableList<AdditionalCost> AdditionalCosts { get; init; } =
		ImmutableList<AdditionalCost>.Empty;
}
