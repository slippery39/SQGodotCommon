using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Builds a Valakut ramp deck for a given player.
///
/// Strategy: play as many lands as possible to hit Valakut's 7-land threshold,
/// then deal 3 damage per land played. Primeval Titan (cost 5) fetches two lands on ETB.
/// Exploration draws a card when it enters and allows an extra land per turn.
/// Cultivate puts a land into play and a second land into hand. Rampant Growth fetches
/// a land directly into play. Field of the Dead creates 2/2 Zombie tokens at 7+ lands.
/// Glimmervoid gains 2 life on play. Bounceland gives +2 MaxMana next turn and returns
/// a land from exile to hand for repeated landfall triggers.
/// Land Elemental scales into a giant threat as lands accumulate.
/// Ancestral Recall refuels the hand. Wrath of God buys time against aggressive decks.
///
/// Deck list (60 cards):
///   14x Plains             (basic land for mana)
///    4x Valakut            (emblem: deals 3 damage per land at 7+ lands)
///    4x Glimmervoid        (land: gain 2 life when played)
///    4x Field of the Dead  (land: create 2/2 Zombie at 7+ lands — stacks with Valakut)
///    2x Bounceland         (land: +2 MaxMana next turn; return a land from exile to hand)
///    4x Exploration        (1 mana: extra land per turn + draw a card on ETB)
///    4x Cultivate          (2 mana: land to play + land to hand)
///    4x Rampant Growth     (2 mana: fetch a land directly into play)
///    4x Ancestral Recall   (1 mana: draw 3 — keeps ramp spells flowing)
///    4x Primeval Titan     (5 mana 6/6: ETB fetches two lands, triggers emblems twice)
///    4x Land Elemental     (3 mana: P/T = lands played — scales to 8/8+ mid-game)
///    4x Lightning Bolt     (1 mana: 3 damage to any target)
///    4x Wrath of God       (3 mana: destroy all creatures — buys time against aggro)
/// </summary>
public static class ValakutDeckFactory
{
	public static IReadOnlyList<Card> Build(int ownerId)
	{
		var deck = new List<Card>();
		AddCopies(deck, ownerId, 14, CardLibrary.Plains);
		AddCopies(deck, ownerId, 4, CardLibrary.Valakut);
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Glimmervoid"));
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Field of the Dead"));
		AddCopies(deck, ownerId, 2, () => CardLibrary.GetByName("Bounceland"));
		AddCopies(deck, ownerId, 4, CardLibrary.Exploration);
		AddCopies(deck, ownerId, 4, () => CardLibrary.GetByName("Cultivate"));
		AddCopies(deck, ownerId, 4, CardLibrary.RampantGrowth);
		AddCopies(deck, ownerId, 4, CardLibrary.AncestralRecall);
		AddCopies(deck, ownerId, 4, CardLibrary.PrimevalTitan);
		AddCopies(deck, ownerId, 4, CardLibrary.LandElemental);
		AddCopies(deck, ownerId, 4, CardLibrary.LightningBolt);
		AddCopies(deck, ownerId, 4, CardLibrary.WrathOfGod);
		return deck;
	}

	private static void AddCopies(List<Card> deck, int ownerId, int count, Func<Card> template)
	{
		for (var i = 0; i < count; i++)
			deck.Add(template() with { OwnerId = ownerId, ControllerId = ownerId });
	}
}
