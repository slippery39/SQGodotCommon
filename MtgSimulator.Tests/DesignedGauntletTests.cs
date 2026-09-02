using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **A gauntlet deck whose cards are not in the pool is a benchmark the field can never reach**, and
/// nothing about that failure is visible in a run — the deck simply loses, or the evolver warns once
/// and carries on. DES had no gauntlet at all for exactly this reason: every original registry deck
/// is a Legacy list, and Designed is defined as every set EXCEPT Legacy.
/// </summary>
[TestFixture]
public class DesignedGauntletTests
{
	[TestCaseSource(nameof(DeckNames))]
	public void ADesignedGauntletDeckIsFullyBuildableFromTheDesignedPool(string name)
	{
		var pool = SetRegistry.Designed.Cards;
		var missing = Gauntlet.MissingFrom(name, pool);

		Assert.That(
			missing,
			Is.Empty,
			$"'{name}' names cards absent from {SetRegistry.Designed.Code}: {string.Join(", ", missing)}"
		);
	}

	/// <summary>
	/// 60 cards, max 4 copies. Built as a raw card list rather than a `Decklist`, so nothing
	/// validates these except this test — a 56-card gauntlet deck would just quietly lose.
	/// </summary>
	[TestCaseSource(nameof(DeckNames))]
	public void ADesignedGauntletDeckIsALegalSixtyCardList(string name)
	{
		var cards = DeckRegistry.Build(name, ownerId: 1);
		var spells = cards.Where(c => !c.HasSubtype("Land")).ToList();

		Assert.Multiple(() =>
		{
			Assert.That(cards, Has.Count.EqualTo(60), "not a 60-card deck");
			Assert.That(
				spells.GroupBy(c => c.Name, StringComparer.Ordinal).Max(g => g.Count()),
				Is.LessThanOrEqualTo(Decklist.MaxCopies),
				"more than the copy limit of some card"
			);
		});
	}

	/// <summary>
	/// The wiring, which is the part that was actually broken. `Gauntlet.For` returned `[]` for DES
	/// while the prompt happily accepted a game count, so a run asked for a gauntlet and measured
	/// against nothing.
	/// </summary>
	[Test]
	public void TheDesignedPoolNowHasAGauntlet()
	{
		Assert.That(Gauntlet.For(SetRegistry.DesignedCode), Is.EquivalentTo(DeckNames()));
	}

	/// <summary>
	/// The other half, and the reason this is not simply "return every registry deck". Legacy
	/// cannot build the CMB lists, so offering them there would recreate on LEG exactly the
	/// unreachable-benchmark problem these decks exist to fix on DES.
	/// </summary>
	[Test]
	public void TheLegacyGauntletDoesNotOfferDesignedDecks()
	{
		Assert.That(
			Gauntlet.For(SetRegistry.LegacyCode),
			Has.No.AnyOf(DeckNames),
			"a Designed deck was offered to the Legacy pool, which cannot build it"
		);
	}

	private static IEnumerable<string> DeckNames() => DesignedGauntletDecks.Names;
}
