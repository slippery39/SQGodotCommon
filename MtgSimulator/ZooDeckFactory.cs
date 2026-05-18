using System.Collections.Immutable;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Builds a Zoo deck for a given player.
///
/// Zoo is a RGW aggro deck built around efficient creatures and burn.
/// Adapted for the engine: no domain condition on Nacatl/Kird Ape, no Forest
/// requirement on Kird Ape, no land search on Path to Exile.
/// Tarmogoyf scales with all graveyards via GraveyardCountComponent.
///
/// Deck list (56 cards — 20 lands + 36 spells):
///   20x Plains           (basic land)
///   4x Wild Nacatl       (1 mana 2/2 Cat)
///   4x Kird Ape          (1 mana 2/3 Ape)
///   4x Loam Lion         (1 mana 2/3 Cat)
///   4x Tarmogoyf         (2 mana */1+* Lhurgoyf)
///   4x Qasali Pridemage  (2 mana 2/2 Cat Wizard, destroy ability)
///   4x Lightning Bolt    (1 mana, 3 damage)
///   4x Lightning Helix   (2 mana, 3 damage + 3 life)
///   4x Path to Exile     (1 mana, exile opponent creature)
///   4x Steppe Lynx       (0 mana 0/1, Landfall: +2/+2)
///   2x Rampant Growth    (2 mana, search for a land)
///   2x Primeval Titan    (6 mana 6/6, ETB: put 2 lands into play)
/// </summary>
public static class ZooDeckFactory
{
	public static IReadOnlyList<Card> Build(int ownerId)
	{
		var deck = new List<Card>();
		AddCopies(deck, ownerId, 20, CardLibrary.Plains);
		AddCopies(deck, ownerId, 4, CardLibrary.WildNacatl);
		AddCopies(deck, ownerId, 4, CardLibrary.KirdApe);
		AddCopies(deck, ownerId, 4, CardLibrary.LoamLion);
		AddCopies(deck, ownerId, 4, CardLibrary.Tarmogoyf);
		AddCopies(deck, ownerId, 4, CardLibrary.QasaliPridemage);
		AddCopies(deck, ownerId, 4, CardLibrary.LightningBolt);
		AddCopies(deck, ownerId, 4, CardLibrary.LightningHelix);
		AddCopies(deck, ownerId, 4, CardLibrary.PathToExile);
		AddCopies(deck, ownerId, 4, CardLibrary.SteppeLynx);
		AddCopies(deck, ownerId, 2, CardLibrary.RampantGrowth);
		AddCopies(deck, ownerId, 2, CardLibrary.PrimevalTitan);
		return deck;
	}

	private static void AddCopies(List<Card> deck, int ownerId, int count, Func<Card> template)
	{
		for (var i = 0; i < count; i++)
			deck.Add(template() with { OwnerId = ownerId, ControllerId = ownerId });
	}
}
