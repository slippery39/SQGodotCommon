using System.Collections.Immutable;
using MtgCore;
using MtgCore.Cards.Builders;

namespace MtgSimulator.Tests;

/// <summary>
/// **Exploration moves in playsets; optimisation trims.**
///
/// Measured on a real run: **59% of all proposals moved exactly one copy**, and those were the least
/// accepted — 22% against 50% for two-copy moves. That is not a preference finding. A candidate
/// plays ~66 games, so one standard error is ~6pp, while a one-copy change is ~2% of a deck whose
/// true effect is a fraction of a point. Every such accept/reject is a coin flip, which is what the
/// oscillation guard was really patching over: the same pair swapped back and forth scoring +6.1pp
/// in BOTH directions.
/// </summary>
[TestFixture]
public class ExplorationPhaseTests
{
	private static IReadOnlyList<Card> Spells() =>
		[
			.. Enumerable
				.Range(1, 11)
				.Select(i =>
					CardFactory
						.Creature($"Card {i:D2}", manaCost: 1 + i % 4, power: 2, toughness: 2)
						.Build()
				),
		];

	private static Decklist Deck(IReadOnlyList<Card> spells, int lands)
	{
		var copies = new Dictionary<string, int>(StringComparer.Ordinal);
		var need = Decklist.DeckSize - lands;
		for (var i = 0; copies.Values.Sum() < need; i++)
		{
			var name = spells[i % spells.Count].Name;
			if (copies.GetValueOrDefault(name) >= Decklist.MaxCopies)
				continue;
			copies[name] = copies.GetValueOrDefault(name) + 1;
		}
		return new Decklist("d", copies.ToImmutableSortedDictionary(StringComparer.Ordinal), lands);
	}

	/// Total copies moved by a proposal, in or out.
	private static int StepSize(Decklist before, Decklist after)
	{
		var names = before.Spells.Keys.Union(after.Spells.Keys);
		return names.Sum(n => Math.Abs(after.CopiesOf(n) - before.CopiesOf(n)));
	}

	private static List<int> Steps(bool exploring)
	{
		var spells = Spells();
		var lands = Math.Max(Decklist.MinLands, 17);
		var deck = Deck(spells, lands);
		var values = new ConstructedValues(DraftTrainingData.Empty, null);

		var steps = new List<int>();
		for (var i = 0; i < 120; i++)
		{
			var m = DeckBuilder.Mutate(
				deck,
				spells,
				values,
				new Random(7_000 + i),
				exploring: exploring
			);
			if (m is not null)
				steps.Add(StepSize(deck, m));
		}
		return steps;
	}

	/// <summary>
	/// The headline, asserted as a COMPARISON rather than against a threshold.
	///
	/// An absolute bar would be a number I picked, and it would move with the fixture — a deck of
	/// 2-ofs cannot make a 4-copy cut however the phase is configured. What the design promises is
	/// that exploration takes bigger steps than optimisation, and that is what is checked.
	/// </summary>
	[Test]
	public void ExploringTakesBiggerStepsThanOptimising()
	{
		var exploring = Steps(exploring: true);
		var optimising = Steps(exploring: false);
		Assert.That(exploring, Is.Not.Empty, "exploration produced no proposals at all");

		double Small(List<int> xs) => (double)xs.Count(s => s <= 2) / xs.Count;

		TestContext.Out.WriteLine(
			$"  exploring:  {exploring.Count, 3} proposals, mean step {exploring.Average(), 4:F1}, "
				+ $"{Small(exploring), 4:P0} of size <= 2"
		);
		TestContext.Out.WriteLine(
			$"  optimising: {optimising.Count, 3} proposals, mean step {optimising.Average(), 4:F1}, "
				+ $"{Small(optimising), 4:P0} of size <= 2"
		);

		Assert.Multiple(() =>
		{
			Assert.That(
				exploring.Average(),
				Is.GreaterThan(optimising.Average()),
				"exploration is not taking bigger steps than optimisation"
			);
			Assert.That(
				Small(exploring),
				Is.LessThan(Small(optimising)),
				"exploration proposes tiny changes as often as optimisation does"
			);
		});
	}

	/// <summary>
	/// **The vacuity guard.** A change that simply made every mutation bigger — including in the
	/// optimisation phase — would pass the test above and destroy the phase split. Trimming is the
	/// other phase's whole job.
	/// </summary>
	[Test]
	public void OptimisingStillMakesSmallChanges()
	{
		var optimising = Steps(exploring: false);
		Assert.That(optimising, Is.Not.Empty);

		var singles = optimising.Count(s => s <= 2);
		TestContext.Out.WriteLine(
			$"  optimising: {optimising.Count} proposals, median step "
				+ $"{optimising.Order().ElementAt(optimising.Count / 2)}, {singles} of size <= 2"
		);

		Assert.That(
			singles,
			Is.GreaterThan(0),
			"the optimisation phase stopped making small changes too — the split is gone"
		);
	}

	/// <summary>
	/// Exploration must still yield proposals. A phase that produces nothing legal would be a
	/// silent freeze, which this project has already shipped once as the pool-quota version of
	/// `DeckCore` and once as the mutation budget spent on nulls.
	/// </summary>
	[Test]
	public void ExplorationStillYieldsProposals()
	{
		Assert.That(
			Steps(exploring: true),
			Has.Count.GreaterThan(40),
			"exploration proposes almost nothing legal — it would freeze every slot"
		);
	}
}
