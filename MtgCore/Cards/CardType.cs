namespace MtgCore;

/// <summary>
/// A card's types. Flags, because a card can genuinely be several at once — an artifact
/// creature, an enchantment creature, a creature land.
///
/// Before this existed, type was inferred from two unrelated places: components
/// (CreatureComponent meant "creature") and magic strings in Card.Subtypes ("Artifact",
/// "Enchantment"). Instants and sorceries carried no type marker at all, so "noncreature spell",
/// "nonland permanent" and Delirium's "N card types in your graveyard" were all unexpressible.
/// </summary>
[Flags]
public enum CardType
{
	None = 0,
	Creature = 1 << 0,
	Instant = 1 << 1,
	Sorcery = 1 << 2,
	Artifact = 1 << 3,
	Enchantment = 1 << 4,
	Land = 1 << 5,
	Planeswalker = 1 << 6,

	/// <summary>Anything that stays on the battlefield. Used by "nonland permanent" wordings.</summary>
	AnyPermanent = Creature | Artifact | Enchantment | Land | Planeswalker,

	/// <summary>Instant or sorcery — the things that go to the graveyard on resolution.</summary>
	AnySpell = Instant | Sorcery,
}
