using MtgCore;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// **Exploration always mutates; optimisation leaves winners alone.**
///
/// The leave-winners-alone rule predates the exploration/optimisation split, and applying it during
/// exploration inverts that phase's purpose. Measured on a 21-deck DES run before the split was
/// honoured: the Twin slot finished FIRST at 76.7% having been offered two mutations in six
/// generations, both in generation 1 — `lastRate` starts at 0 so everyone explores once, and from
/// generation 2 its 76% rate put it above `StableRate` and it was never offered a change again.
/// The best deck in the field was frozen at its seed.
/// </summary>
[TestFixture]
public class MutationBudgetTests
{
	private const int Mutants = 3;

	private static MetagameEvolver Evolver() =>
		new(SetRegistry.Get("CMB"), deckCount: 2, generations: 1, mutantsPerDeck: Mutants);

	/// <summary>
	/// The rate is not consulted while exploring — including at rates that would silence a slot
	/// completely during optimisation. 0.99 is the case that actually bit: a winning deck.
	/// </summary>
	[TestCase(0.0)]
	[TestCase(0.45)]
	[TestCase(0.60)]
	[TestCase(0.77)]
	[TestCase(0.99)]
	public void WhileExploring_EverySlotGetsTheFullBudget(double rate)
	{
		Assert.That(
			Evolver().MutantsFor(rate, exploring: true),
			Is.EqualTo(Mutants),
			"exploration exists to find which cards belong, and a winning deck is the one whose "
				+ "list is most worth learning from"
		);
	}

	/// <summary>
	/// **The optimisation half is the control**, and it is what makes the test above a statement
	/// about the PHASE rather than about the rule having been deleted.
	/// </summary>
	[Test]
	public void WhileOptimising_TheRuleStillApplies()
	{
		var e = Evolver();

		Assert.Multiple(() =>
		{
			Assert.That(
				e.MutantsFor(0.77, exploring: false),
				Is.Zero,
				"a deck already clearing the bar is left alone once the card list is settled"
			);
			Assert.That(
				e.MutantsFor(0.60, exploring: false),
				Is.Zero,
				"StableRate is inclusive"
			);
			Assert.That(e.MutantsFor(0.20, exploring: false), Is.EqualTo(Mutants));
			Assert.That(
				e.MutantsFor(0.52, exploring: false),
				Is.InRange(1, Mutants),
				"between the two rates the budget tapers rather than dropping to zero"
			);
		});
	}
}
