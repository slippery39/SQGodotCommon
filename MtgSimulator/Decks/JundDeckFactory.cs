using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Builds a Jund midrange deck for a given player.
///
/// Deck list (64 cards — 16 Plains + 48 spells):
///   16x Plains
///   4x  Mox              (0 mana artifact, +1 fast mana)
///   2x  Sol Ring         (1 mana artifact, +2 fast mana)
///   4x  Lightning Bolt   (1 mana, 3 damage)
///   4x  Thoughtseize     (1 mana, opponent discards highest mana cost non-land)
///   4x  Inquisition of Kozilek (1 mana, opponent discards lowest mana cost non-land)
///   4x  Ancestral Recall (1 mana, draw 3)
///   4x  Dark Confidant   (2 mana 1/4, upkeep: reveal + draw + lose life equal to mana cost)
///   4x  Tarmogoyf        (2 mana */1+*, scales with graveyard count)
///   4x  Scavenging Ooze  (2 mana 2/2, activated: exile 2 opponent graveyard cards, gain 1 life, +1/+1)
///   4x  Doom Blade       (2 mana, destroy target creature)
///   2x  Phyrexian Arena  (2 mana enchantment, upkeep: draw + lose 1 life)
///   4x  Liliana of the Veil (3 mana 2/2, ETB: destroy cheapest opponent creature; upkeep: opponent discards)
///   4x  Siege Rhino      (4 mana 4/5, Trample + Lifelink, ETB: opponent loses 3 / you gain 3)
/// </summary>
public static class JundDeckFactory
{
	public static IReadOnlyList<Card> Build(int ownerId)
	{
		var deck = new List<Card>();
		AddCopies(deck, ownerId, 16, CardLibrary.Plains);
		AddCopies(deck, ownerId, 3, () => CardLibrary.GetByName("Mox"));
		AddCopies(deck, ownerId, 2, () => CardLibrary.GetByName("Sol Ring"));
		AddCopies(deck, ownerId, 3, () => CardLibrary.GetByName("Lightning Bolt"));
		AddCopies(deck, ownerId, 3, () => CardLibrary.GetByName("Thoughtseize"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Inquisition of Kozilek"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Ancestral Recall"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Dark Confidant"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Tarmogoyf"));
		AddCopies(deck, ownerId, 4, CardLibrary.ScavengingOoze);
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Doom Blade"));
		AddCopies(deck, ownerId, 1, () => CardLibrary.GetByName("Phyrexian Arena"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Liliana of the Veil"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Siege Rhino"));
		return deck;
	}

	private static void AddCopies(List<Card> deck, int ownerId, int count, Func<Card> template)
	{
		for (var i = 0; i < count; i++)
			deck.Add(template() with { OwnerId = ownerId, ControllerId = ownerId });
	}
}
