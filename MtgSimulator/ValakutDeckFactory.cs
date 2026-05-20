using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Builds a Valakut ramp deck for a given player.
///
/// Strategy: play as many lands as possible to hit Valakut's 7-land threshold,
/// then deal 3 damage per land played. Primeval Titan accelerates the land count
/// directly; Exploration and Rampant Growth support the ramp plan. Land Elemental
/// scales into a giant threat as lands accumulate. Steppe Lynx and Bloodghast
/// provide early pressure that also benefits from all the land plays.
/// Ancestral Recall refuels the hand so ramp spells stay available.
/// Wrath of God buys time against aggressive decks.
///
/// Deck list (60 cards):
///   20x Plains             (basic land for mana)
///    4x Valakut            (the payoff: emblem deals 3 damage per land at 7+ lands)
///    4x Exploration        (extra land per turn — doubles the emblem's fire rate)
///    4x Rampant Growth     (2 mana: fetch a land directly into play)
///    4x Ancestral Recall   (0 mana: draw 3 — keeps ramp spells flowing)
///    4x Primeval Titan     (6 mana 6/6: ETB fetches two lands, triggers emblem twice)
///    4x Land Elemental     (3 mana: P/T = lands played — scales to 8/8+ mid-game)
///    4x Steppe Lynx        (0 mana 0/1: landfall +2/+2 until EOT — threats for free)
///    4x Bloodghast         (2 mana 2/1: returns from graveyard on any landfall)
///    4x Wrath of God       (4 mana: destroy all creatures — buys time against aggro)
/// </summary>
public static class ValakutDeckFactory
{
	public static IReadOnlyList<Card> Build(int ownerId)
	{
		var deck = new List<Card>();
		AddCopies(deck, ownerId, 22, CardLibrary.Plains);
		AddCopies(deck, ownerId, 4, CardLibrary.Valakut);
		AddCopies(deck, ownerId, 4, CardLibrary.Exploration);
		AddCopies(deck, ownerId, 4, CardLibrary.RampantGrowth);
		AddCopies(deck, ownerId, 4, CardLibrary.AncestralRecall);
		AddCopies(deck, ownerId, 4, CardLibrary.PrimevalTitan);
		AddCopies(deck, ownerId, 4, CardLibrary.LandElemental);
		AddCopies(deck, ownerId, 4, CardLibrary.LightningBolt);
		AddCopies(deck, ownerId, 2, CardLibrary.Slagstorm);
		AddCopies(deck, ownerId, 4, CardLibrary.WrathOfGod);
		return deck;
	}

	private static void AddCopies(List<Card> deck, int ownerId, int count, Func<Card> template)
	{
		for (var i = 0; i < count; i++)
			deck.Add(template() with { OwnerId = ownerId, ControllerId = ownerId });
	}
}
