using MtgCore;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// **Exploration must sample the pool, not re-pick the best card.**
///
/// Scores are dominated by `CardDelta`, which comes from pre-simulation — cards measured in
/// uniformly RANDOM decks — and at `MutateTemperature` (2.0pp) that softmax is nearly an argmax.
/// Measured on DES: Zombie Horde Leader +24.26pp against Skim the Surface +2.18pp is a 22.1pp gap,
/// which at T=2.0 is e^11.04 ≈ 62 000 : 1.
///
/// The consequence, from a real run's mutation log: the Twin slot's eight proposals were War Horn
/// three times, Zombie Horde Leader twice, and three other good-stuff creatures. Not one
/// card-selection effect was ever offered — while its accept rate, 3/8, was ABOVE the field's. The
/// search was not rejecting the cards it needed, it was never shown them.
///
/// That bias is the one exploration cannot afford, because presim structurally cannot price a
/// conditional card: a cantrip does nothing in a random pile, so the table steering proposals is
/// exactly the table blind to what a combo deck wants.
/// </summary>
[TestFixture]
public class ExplorationSamplingTests
{
	private static readonly CardSet Set = SetRegistry.Get("CSC");

	private static IReadOnlyList<Card> Spells =>
		[.. Set.Cards.Where(c => !c.HasSubtype("Land")).OrderBy(c => c.Name, StringComparer.Ordinal)];

	/// <summary>
	/// A values table where one card is far and away the best, mirroring the real spread that made
	/// the softmax an argmax.
	/// </summary>
	private static ConstructedValues Values(string star)
	{
		var acc = new CardStatAccumulator();
		// A SMALL deck list, not the whole pool: CardStatAccumulator records a pair per card pair,
		// so passing 408 names would be ~83k pairs a game and the test would never finish. Cards
		// outside it are simply unmeasured, which is the realistic case anyway.
		var shell = Spells.Take(12).Select(c => c.Name).ToList();
		if (!shell.Contains(star))
			shell[0] = star;
		for (var i = 0; i < 400; i++)
		{
			var won = i % 2 == 0;
			var drawn = won ? new List<string> { star } : [];
			drawn.AddRange(shell.Where(n => n != star).Take(5));
			acc.Add(drawn, shell, won);
		}
		return new ConstructedValues(acc.ToData(), null);
	}

	private static Decklist Seed()
	{
		var d = Decklist.Empty("S") with { Lands = 20 };
		foreach (var c in Spells.Take(10))
			d = d.WithCopies(c.Name, 4);
		return d;
	}

	/// <summary>
	/// The distinct cards a phase actually proposes over many rolls. Breadth is the quantity in
	/// question — not which card wins, but how many ever get a hearing.
	/// </summary>
	private static int DistinctCardsProposed(bool exploring, int rolls = 400)
	{
		var deck = Seed();
		var values = Values(Spells[0].Name);
		var rng = new Random(19);
		var seen = new HashSet<string>(StringComparer.Ordinal);

		for (var i = 0; i < rolls; i++)
		{
			var m = DeckBuilder.Mutate(deck, Set.Cards, values, rng, exploring: exploring);
			if (m is null)
				continue;
			foreach (var n in m.Spells.Keys.Where(n => m.CopiesOf(n) > deck.CopiesOf(n)))
				seen.Add(n);
		}

		return seen.Count;
	}

	[Test]
	public void ExploringProposesAWiderSetOfCardsThanOptimising()
	{
		var exploring = DistinctCardsProposed(exploring: true);
		var optimising = DistinctCardsProposed(exploring: false);

		TestContext.Out.WriteLine(
			$"distinct cards proposed — exploring {exploring}, optimising {optimising}"
		);

		Assert.That(
			exploring,
			Is.GreaterThan(optimising),
			"if exploration proposes no more of the pool than optimisation does, it is not "
				+ "exploring — it is doing a considered swap several more times"
		);
	}

	/// <summary>
	/// **The control: optimisation must stay sharp.** Flattening both phases would trade one
	/// failure for another — a search that never converges instead of one that never explores.
	/// </summary>
	[Test]
	public void OptimisingStillConcentratesOnTheBestCards()
	{
		Assert.That(
			DistinctCardsProposed(exploring: false),
			Is.LessThan(Spells.Count),
			"optimisation samples at 2.0pp and must not be offering the whole pool"
		);
	}

	/// <summary>
	/// The arithmetic the change rests on, asserted rather than described: at the optimisation
	/// temperature a 22pp gap is a verdict, at the exploration temperature it is a preference.
	/// </summary>
	[Test]
	public void TheTemperatureTurnsAVerdictIntoAPreference()
	{
		const double Gap = 22.1;
		var atMutate = Math.Exp(Gap / DeckBuilder.MutateTemperature);
		var atExplore = Math.Exp(Gap / DeckBuilder.ExploreTemperature);

		TestContext.Out.WriteLine($"odds at T={DeckBuilder.MutateTemperature}: {atMutate:N0}:1");
		TestContext.Out.WriteLine($"odds at T={DeckBuilder.ExploreTemperature}: {atExplore:N1}:1");

		Assert.Multiple(() =>
		{
			Assert.That(atMutate, Is.GreaterThan(10_000), "the measured DES gap really is an argmax");
			Assert.That(
				atExplore,
				Is.LessThan(10),
				"exploration must give a weak card a real chance of being tried"
			);
			Assert.That(
				atExplore,
				Is.GreaterThan(1.5),
				"but not uniform — flat over 732 spells spends the budget proving random cards are bad"
			);
		});
	}
}
