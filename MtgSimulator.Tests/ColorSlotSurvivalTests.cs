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
/// said so.
///
/// **Culling has since been removed entirely**, so the path that caused that failure is gone and
/// this test no longer forces it. What remains under test is the invariant itself over a real run:
/// mutation must not drift a slot out of its colours either, which is the surviving half of the
/// same bug and the reason MetagameEvolver.ValidateFieldIdentities runs every generation.
/// </summary>
[TestFixture]
public class ColorSlotSurvivalTests
{
	/// Kept deliberately tiny — the invariant is structural, so it does not need a realistic field.
	[Test]
	public void EveryDeckStaysInsideItsColours_AcrossAWholeRun()
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
			preSimDecks: 0,
			useDraftPrior: false
		);

		// **Run inside a scratch directory.** `Run` reads and writes `sim_results/` relative to the
		// working directory, which other tests move to the solution root (`TestPaths`) — so depending
		// on test ORDER this test read the real value tables and wrote a metagame file and evolved
		// counts into the user's `sim_results/` on every full run. Isolated, it reads nothing and
		// leaves nothing; the invariant under test needs neither.
		var previous = Directory.GetCurrentDirectory();
		var scratch = Directory.CreateTempSubdirectory("color-slot-survival-").FullName;
		MetagameResult result;
		try
		{
			Directory.SetCurrentDirectory(scratch);
			result = evolver.Run();
		}
		finally
		{
			Directory.SetCurrentDirectory(previous);
			Directory.Delete(scratch, recursive: true);
		}

		var pool = CoresetCube.Set.Cards.ToDictionary(c => c.Name, StringComparer.Ordinal);

		var constrained = 0;
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

			constrained++;
		}

		Assert.That(constrained, Is.GreaterThan(0), "the run must have produced constrained slots");
	}
}
