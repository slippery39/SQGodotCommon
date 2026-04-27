using System.Collections.Immutable;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Builds a Goblins deck for a given player.
///
/// Red aggro built around cheap Goblin creatures, direct damage, and synergistic
/// effects (Lackey/Instigator board acceleration, Siege-Gang token generation, Krenko doubling).
///
/// Deck list (24 cards):
///   4x Goblin Guide        (1 mana 2/2 haste)
///   4x Goblin Lackey       (1 mana 1/1, combat damage → put Goblin from hand into play)
///   4x Goblin Grenade      (1 mana, sacrifice Goblin → 5 damage)
///   4x Lightning Bolt      (1 mana, 3 damage)
///   2x Warren Instigator   (2 mana 1/1 double strike, trigger fires twice)
///   2x Goblin Chieftain    (3 mana 2/2 haste; lord effect deferred)
///   2x Siege-Gang Commander (5 mana 2/2, ETB creates 3 tokens, sac Goblin → 2 damage)
///   2x Krenko, Mob Boss    (4 mana 3/3, activate → create X Goblin tokens)
/// </summary>
public static class GoblinsDeckFactory
{
	public static IReadOnlyList<Card> Build(int ownerId)
	{
		var deck = new List<Card>();
		AddCopies(deck, ownerId, 4, CardLibrary.GoblinGuide);
		AddCopies(deck, ownerId, 4, CardLibrary.GoblinLackey);
		AddCopies(deck, ownerId, 4, CardLibrary.GoblinGrenade);
		AddCopies(deck, ownerId, 4, CardLibrary.LightningBolt);
		AddCopies(deck, ownerId, 2, CardLibrary.WarrenInstigator);
		AddCopies(deck, ownerId, 2, CardLibrary.GoblinChieftain);
		AddCopies(deck, ownerId, 2, CardLibrary.SiegeGangCommander);
		AddCopies(deck, ownerId, 2, CardLibrary.KrenkoMobBoss);
		return deck;
	}

	private static void AddCopies(List<Card> deck, int ownerId, int count, Func<Card> template)
	{
		for (var i = 0; i < count; i++)
			deck.Add(template() with { OwnerId = ownerId, ControllerId = ownerId });
	}
}
