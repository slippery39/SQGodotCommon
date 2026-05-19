using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Builds a Dragonstorm deck for a given player.
///
/// Dragonstorm (2006 Standard) is a combo deck that chains fast-mana spells
/// to cast Dragonstorm in one turn and deploy multiple Dragons via Storm.
///
/// Simplifications from the real deck:
///   - No lands (engine uses Hearthstone-style auto-mana)
///   - Lotus Bloom: free sorcery that adds 3 mana (suspend 3 omitted)
///   - Hunted Dragon: 6/6 flying haste only (no Knight token ETB)
///   - Gigadrowse: exhausts target creature (tap mechanic not implemented; replicate omitted)
///   - Remand: not included (counterspell stack interaction not implemented)
///   - Calciform Pools / Dreadship Reef / Shivan Reef: not included (no land system)
///
/// Deck list (56 cards — 20 lands + 36 spells):
///   20x Plains             (basic land)
///   4x Bogardan Hellkite   (8 mana 5/5 Flying Dragon, ETB deal 5)
///   2x Hunted Dragon       (6 mana 6/6 Flying Haste Dragon)
///   4x Dragonstorm         (9 mana sorcery, Storm — deploy Dragons)
///   4x Gigadrowse          (1 mana instant, exhaust target creature)
///   4x Rite of Flame       (1 mana instant, +2 mana + graveyard bonus)
///   4x Seething Song       (3 mana instant, +5 mana)
///   4x Sleight of Hand     (1 mana instant, look at top 2 keep 1)
///   4x Telling Time        (2 mana instant, library manipulation)
///   4x Lotus Bloom         (0 mana sorcery, +3 mana; simplified)
///   2x Dark Confidant      (2 mana 2/1; draw filler)
///   2x Rampant Growth      (2 mana sorcery, search for land)
/// </summary>
public static class DragonstormDeckFactory
{
	public static IReadOnlyList<Card> Build(int ownerId)
	{
		var deck = new List<Card>();
		AddCopies(deck, ownerId, 20, CardLibrary.Plains);
		AddCopies(deck, ownerId, 4, CardLibrary.BogardanHellkite);
		AddCopies(deck, ownerId, 4, CardLibrary.HuntedDragon);
		AddCopies(deck, ownerId, 4, CardLibrary.Dragonstorm);
		AddCopies(deck, ownerId, 4, CardLibrary.RiteOfFlame);
		AddCopies(deck, ownerId, 4, CardLibrary.SeethingSong);
		AddCopies(deck, ownerId, 4, CardLibrary.LotusBoom);
		AddCopies(deck, ownerId, 4, CardLibrary.AncestralRecall);
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Faithless Looting"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Mox"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Lightning Bolt"));
		return deck;
	}

	private static void AddCopies(List<Card> deck, int ownerId, int count, Func<Card> template)
	{
		for (var i = 0; i < count; i++)
			deck.Add(template() with { OwnerId = ownerId, ControllerId = ownerId });
	}
}
