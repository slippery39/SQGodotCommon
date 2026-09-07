using MtgCore;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// **At the exploration/optimisation boundary, rebuild each deck from what exploration measured.**
///
/// Without it the field handed to optimisation is wherever the last accepted mutation left it — a
/// hill-climb endpoint rather than a summary of the run's findings — and two generations of
/// evidence about which cards work in this shell is discarded.
/// </summary>
[TestFixture]
public class HarvestTests
{
	private static readonly CardSet Set = SetRegistry.Get("CMB");

	private static IReadOnlyList<string> Spells =>
		[.. Set.Cards.Where(c => !c.HasSubtype("Land")).Select(c => c.Name).Order(StringComparer.Ordinal)];

	private static ConstructedValues Values() =>
		new(new CardStatAccumulator().ToData(), null);

	private static MetagameEvolver Evolver() =>
		new(Set, deckCount: 2, generations: 1, mutantsPerDeck: 1);

	/// <summary>
	/// A history in which <paramref name="hero"/> is drawn in games that are won and
	/// <paramref name="dud"/> in games that are lost, with every other card split evenly so the
	/// deck's own base rate stays near 50% and the two deltas are about the card, not the shell.
	/// </summary>
	private static DeckHistory History(string hero, string dud, int games = 200)
	{
		var acc = new CardStatAccumulator();
		var all = Spells.ToList();

		for (var i = 0; i < games; i++)
		{
			var won = i % 2 == 0;
			// Everything is IN the deck every game; what varies is what was DRAWN, which is the
			// quantity games-in-hand counting is built on.
			var drawn = new List<string> { won ? hero : dud };
			drawn.AddRange(all.Where(n => n != hero && n != dud).Take(6));
			acc.Add(drawn, all, won);
		}

		return new DeckHistory(acc.ToData());
	}

	private static Decklist Deck(params string[] names)
	{
		var d = Decklist.Empty("Slot") with { Lands = 20 };
		foreach (var n in names)
			d = d.WithCopies(n, 4);
		return d;
	}

	[Test]
	public void ItPlaysTheCardsThatWonAndDropsTheOnesThatLost()
	{
		var hero = Spells[0];
		var dud = Spells[1];

		var harvested = Evolver()
			.Harvest(Deck(dud), core: null, DeckBuilder.DeckProfile.Any, History(hero, dud), Values());

		TestContext.Out.WriteLine(harvested.Format(ConstructedGameSetup.PoolIndex(Set.Cards)));

		Assert.Multiple(() =>
		{
			Assert.That(
				harvested.CopiesOf(hero),
				Is.EqualTo(Decklist.MaxCopies),
				"the card measured winning should be a playset in the harvested list"
			);
			Assert.That(
				harvested.CopiesOf(dud),
				Is.LessThan(harvested.CopiesOf(hero)),
				"the card measured losing must not outrank it — the starting deck held only the dud, "
					+ "so this also shows the harvest is not just echoing its input"
			);
			Assert.That(harvested.IsValid, Is.True);
			Assert.That(harvested.Lands, Is.EqualTo(20), "lands are carried over, not harvested");
		});
	}

	/// <summary>
	/// **The pool lock, which is the check that keeps a harvest on-archetype.** `DeckBuilder.Mutate`
	/// draws only from a core's identity cards; a harvest that ignored that could reach cards
	/// mutation never could, and the good-stuff drift the lock exists to prevent would re-enter
	/// through this door.
	/// </summary>
	[Test]
	public void ItNeverReachesOutsideAPoolLockedCore()
	{
		// **Wide enough to FILL, or the test proves nothing.** A 40-spell capacity needs ten
		// playsets; with a six-card core the harvest cannot fill, falls back to the current deck,
		// and the outside card is absent for the wrong reason. The first version of this test was
		// vacuous exactly that way.
		var inCore = Spells.Take(12).ToList();
		var outside = Spells.Last();
		Assert.That(inCore, Does.Not.Contain(outside), "fixture: the outside card must be outside");

		var core = new DeckCore(
			"locked",
			[new CoreSlot("Payoff", [.. inCore], 4, IsIdentity: true)]
		);

		// The dud is INSIDE the core and the hero OUTSIDE it, so a harvest that ignored the lock
		// would visibly reach for the hero — it is the single best-measured card available.
		var harvested = Evolver()
			.Harvest(
				Deck(inCore[0]),
				core,
				DeckBuilder.DeckProfile.Any,
				History(outside, inCore[0]),
				Values()
			);

		Assert.Multiple(() =>
		{
			Assert.That(
				harvested.IsValid,
				Is.True,
				"the harvest must have actually built something, or the lock is untested"
			);
			Assert.That(
				harvested.CopiesOf(outside),
				Is.Zero,
				"a card outside the core's identity is off-archetype however well it measured"
			);
			Assert.That(
				harvested.Spells.Keys,
				Is.SubsetOf(inCore),
				"every card in a pool-locked harvest comes from the core's identity"
			);
		});
	}

	/// <summary>
	/// With nothing measured there is nothing to harvest, and the current deck stands. A step that
	/// returned an empty or half-built list here would hand optimisation something worse than the
	/// hill-climb endpoint it replaced.
	/// </summary>
	[Test]
	public void WithNoEvidence_TheCurrentDeckStands()
	{
		var current = Deck(Spells[0], Spells[1], Spells[2]);
		var empty = new DeckHistory(new CardStatAccumulator().ToData());

		Assert.That(
			Evolver().Harvest(current, null, DeckBuilder.DeckProfile.Any, empty, Values()),
			Is.EqualTo(current)
		);
	}
}
