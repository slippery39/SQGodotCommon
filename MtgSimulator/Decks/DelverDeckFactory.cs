using System.Collections.Immutable;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Builds a Delver deck for a given player.
///
public static class DelverDeckFactory
{
	public static IReadOnlyList<Card> Build(int ownerId)
	{
		var deck = new List<Card>();
		AddCopies(deck, ownerId, 16, CardLibrary.Plains);
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Delver of Secrets"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Ancestral Recall"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Lightning Bolt"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Tarmogoyf"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Geist of Saint Traft"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Path to Exile"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Mox"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Gitaxian Probe"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Gut Shot"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Lightning Helix"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Snapcaster Mage"));
		return deck;
	}

	private static void AddCopies(List<Card> deck, int ownerId, int count, Func<Card> template)
	{
		for (var i = 0; i < count; i++)
			deck.Add(template() with { OwnerId = ownerId, ControllerId = ownerId });
	}
}
