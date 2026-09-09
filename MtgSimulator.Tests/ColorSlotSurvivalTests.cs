using MtgCore;
using MtgSimulator;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// A slot's colour identity has to survive everything the evolver does to it, not just its seed.
///
/// **This is written from a real failure.** A 16-slot run came back with eight decks playing
/// colours they could not cast — U-Midrange holding fourteen white cards, RG-Control holding
/// twenty-four — because the cull path re-seeded without passing the slot's identity. Seeding and
/// mutation were both covered by passing tests; the third path was not, and nothing in the report
/// said so. Deck AGE separated the clean slots from the contaminated ones perfectly: every deck
/// that survived from generation 1 kept its colours, every re-seeded deck lost them.
/// </summary>
[TestFixture]
public class ColorSlotSurvivalTests
{
	/// <summary>
	/// Culling is forced by an impossible viability floor and no grace period, so several slots
	/// re-seed within a handful of generations. Kept deliberately tiny — the invariant is
	/// structural, so it does not need a realistic field to show up.
	/// </summary>
	[Test]
	public void EveryDeckStaysInsideItsColours_EvenAfterBeingCulledAndReseeded()
	{
		var evolver = new MetagameEvolver(
			CoresetCube.Set,
			deckCount: 6,
			generations: 6,
			mutantsPerDeck: 1,
			gamesPerMatchup: 1,
			finalGamesPerMatchup: 1,
			seed: 4242,
			aiDepth: 1,
			// Nothing can clear a 99% floor, so a slot is culled every generation it is allowed to
			// be — which is the path under test.
			viabilityFloor: 0.99,
			graceGenerations: 0,
			preSimDecks: 0,
			useDraftPrior: false
		);

		var result = evolver.Run();
		var pool = CoresetCube.Set.Cards.ToDictionary(c => c.Name, StringComparer.Ordinal);

		var reseeded = 0;
		for (var i = 0; i < result.Decks.Count; i++)
		{
			var identity = DeckBuilder.IdentityForSlot(i, result.Decks.Count, wildcard: i == 5);
			if (identity == null)
				continue;

			foreach (var name in result.Decks[i].Spells.Keys)
				if (pool.TryGetValue(name, out var card))
					Assert.That(
						identity.Allows(card),
						Is.True,
						$"{result.Decks[i].Name} holds {name}, which {identity.Code} cannot cast"
					);

			reseeded++;
		}

		Assert.That(reseeded, Is.GreaterThan(0), "the run must have produced constrained slots");
	}
}
