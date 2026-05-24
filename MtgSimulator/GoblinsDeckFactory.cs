using System.Collections.Immutable;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Builds a Goblins deck for a given player.
///
/// Red aggro built around cheap Goblin creatures, direct damage, and synergistic
/// effects (Lackey/Instigator board acceleration, Siege-Gang token generation, Krenko doubling,
/// Matron tutoring, Ringleader card advantage).

/// </summary>
public static class GoblinsDeckFactory
{
	public static IReadOnlyList<Card> Build(int ownerId)
	{
		var deck = new List<Card>();
		AddCopies(deck, ownerId, 16, CardLibrary.Plains);
		AddCopies(deck, ownerId, 4, CardLibrary.GoblinLackey);
		AddCopies(deck, ownerId, 4, CardLibrary.MoggWarmaster);
		AddCopies(deck, ownerId, 4, CardLibrary.LightningBolt);
		AddCopies(deck, ownerId, 4, CardLibrary.AncestralRecall);
		AddCopies(deck, ownerId, 4, CardLibrary.Mox);
		AddCopies(deck, ownerId, 4, CardLibrary.WarrenInstigator);
		AddCopies(deck, ownerId, 3, CardLibrary.GoblinMatron);
		AddCopies(deck, ownerId, 4, CardLibrary.GoblinChieftain);
		AddCopies(deck, ownerId, 4, CardLibrary.SiegeGangCommander);
		AddCopies(deck, ownerId, 4, CardLibrary.KrenkoMobBoss);
		AddCopies(deck, ownerId, 2, CardLibrary.GloriousAnthem);
		AddCopies(deck, ownerId, 2, CardLibrary.GoblinRingleader);
		return deck;
	}

	private static void AddCopies(List<Card> deck, int ownerId, int count, Func<Card> template)
	{
		for (var i = 0; i < count; i++)
			deck.Add(template() with { OwnerId = ownerId, ControllerId = ownerId });
	}
}
