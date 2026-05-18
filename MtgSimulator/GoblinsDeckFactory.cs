using System.Collections.Immutable;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Builds a Goblins deck for a given player.
///
/// Red aggro built around cheap Goblin creatures, direct damage, and synergistic
/// effects (Lackey/Instigator board acceleration, Siege-Gang token generation, Krenko doubling).
///
/// Deck list (56 cards — 20 lands + 36 spells):
///   20x Plains             (basic land)
///   4x Goblin Guide        (1 mana 2/2 haste)
///   4x Goblin Lackey       (1 mana 1/1, combat damage → put Goblin from hand into play)
///   4x Goblin Grenade      (1 mana, sacrifice Goblin → 5 damage)
///   4x Lightning Bolt      (1 mana, 3 damage)
///   4x Raging Goblin       (1 mana 1/1 haste goblin)
///   4x Warren Instigator   (2 mana 1/1 double strike, trigger fires twice)
///   4x Goblin Chieftain    (3 mana 2/2 haste; lord effect deferred)
///   4x Siege-Gang Commander (5 mana 2/2, ETB creates 3 tokens, sac Goblin → 2 damage)
///   4x Krenko, Mob Boss    (4 mana 3/3, activate → create X Goblin tokens)
///   2x Steppe Lynx         (0 mana 0/1, Landfall: +2/+2)
///   2x Exploration         (1 mana artifact, extra land per turn)
/// </summary>
public static class GoblinsDeckFactory
{
	public static IReadOnlyList<Card> Build(int ownerId)
	{
		var deck = new List<Card>();
		AddCopies(deck, ownerId, 20, CardLibrary.Plains);
		AddCopies(deck, ownerId, 4, CardLibrary.GoblinGuide);
		AddCopies(deck, ownerId, 4, CardLibrary.GoblinLackey);
		AddCopies(deck, ownerId, 4, CardLibrary.GoblinGrenade);
		AddCopies(deck, ownerId, 4, CardLibrary.LightningBolt);
		AddCopies(deck, ownerId, 4, CardLibrary.RagingGoblin);
		AddCopies(deck, ownerId, 4, CardLibrary.WarrenInstigator);
		AddCopies(deck, ownerId, 4, CardLibrary.GoblinChieftain);
		AddCopies(deck, ownerId, 4, CardLibrary.SiegeGangCommander);
		AddCopies(deck, ownerId, 4, CardLibrary.KrenkoMobBoss);
		AddCopies(deck, ownerId, 2, CardLibrary.SteppeLynx);
		AddCopies(deck, ownerId, 2, CardLibrary.Exploration);
		return deck;
	}

	private static void AddCopies(List<Card> deck, int ownerId, int count, Func<Card> template)
	{
		for (var i = 0; i < count; i++)
			deck.Add(template() with { OwnerId = ownerId, ControllerId = ownerId });
	}
}
