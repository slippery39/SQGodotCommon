using System.Collections.Immutable;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Builds a Delver deck for a given player.
///
public static class ReanimatorDeckFactory
{
	public static IReadOnlyList<Card> Build(int ownerId)
	{
		var deck = new List<Card>();
		AddCopies(deck, ownerId, 18, CardLibrary.Plains);
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Ancestral Recall"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Bloodghast"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Tarmogoyf"));
		AddCopies(deck, ownerId, 3, () => CardLibrary.GetByName("Path to Exile"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Mox Pearl"));
		AddCopies(deck, ownerId, 2, () => CardLibrary.GetByName("Lotus Bloom"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Reanimate"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Hunted Dragon"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Carnage Tyrant"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Faithless Looting"));
		AddCopies(deck, ownerId, 3, () => CardLibrary.GetByName("Careful Study"));
		AddCopies(deck, ownerId, 2, () => CardLibrary.GetByName("Wrath of God"));
		return deck;
	}

	private static void AddCopies(List<Card> deck, int ownerId, int count, Func<Card> template)
	{
		for (var i = 0; i < count; i++)
			deck.Add(template() with { OwnerId = ownerId, ControllerId = ownerId });
	}
}
