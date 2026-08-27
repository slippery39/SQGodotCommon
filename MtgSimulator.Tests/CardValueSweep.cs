using MtgCore;
using MtgCore.Cards.Builders;

namespace MtgSimulator.Tests;

/// <summary>
/// Drives <see cref="CardValueSandbox"/>. The fast test pins the sandbox's core claim; the sweep
/// prints the table and is <c>[Explicit]</c> because it casts and rolls out every card in a set.
/// </summary>
[TestFixture]
public class CardValueSweep
{
	/// <summary>
	/// The whole premise in one assertion: at equal cost, the bigger body is worth more.
	///
	/// This is the smallest thing that fails if the arithmetic breaks. Subtract the control the
	/// wrong way round, fail to cast the subject, or score the opponent's perspective, and these
	/// two collapse to the same number or invert.
	///
	/// Inline cards rather than a set lookup, so a card-balance pass cannot break it.
	/// </summary>
	[Test]
	public void BiggerBodyAtTheSameCostIsWorthMore()
	{
		var big = CardFactory.Creature("Big", manaCost: 3, power: 5, toughness: 5).Build();
		var small = CardFactory.Creature("Small", manaCost: 3, power: 1, toughness: 1).Build();

		var values = CardValueSandbox.Measure([big, small]);

		Assert.That(
			values.Where(v => v.WasMeasured).Select(v => v.Name),
			Is.EquivalentTo(new[] { "Big", "Small" }),
			"both fixtures must actually cast — a skipped card scores 0 and would pass the "
				+ "comparison below for the wrong reason"
		);

		var bigValue = values.Single(v => v.Name == "Big").Value;
		var smallValue = values.Single(v => v.Name == "Small").Value;

		Assert.That(bigValue, Is.GreaterThan(smallValue));
		Assert.That(
			smallValue,
			Is.GreaterThan(0f),
			"a 1/1 for 3 is a weak card but it is not worthless; a value at or below zero means the "
				+ "control is not the counterfactual it is meant to be"
		);
	}

	[Test]
	[Explicit("Casts and rolls out every card in the set")]
	public void SweepTheCoreSetCube() => Sweep(CoresetCube.Set);

	private static void Sweep(CardSet set)
	{
		var values = CardValueSandbox.Measure(set.Cards);
		CardValueSandbox.Save(CardValueSandbox.PathFor(set.Code), values);

		var measured = values.Where(v => v.WasMeasured).ToList();
		var ranked = measured.OrderByDescending(v => v.Value).ToList();

		TestContext.Out.WriteLine(
			$"=== {set.Code}: measured {measured.Count} of {values.Count} cards ==="
		);
		TestContext.Out.WriteLine($"written to {CardValueSandbox.PathFor(set.Code)}");

		TestContext.Out.WriteLine("\n--- highest value when castable ---");
		foreach (var v in ranked.Take(20))
			TestContext.Out.WriteLine(
				$"  {v.Value, 8:F2}  (stress {v.StressValue, 7:F2})  {{{v.ManaCost}}} {v.Name}"
			);

		TestContext.Out.WriteLine("\n--- lowest value when castable ---");
		foreach (var v in Enumerable.Reverse(ranked).Take(20))
			TestContext.Out.WriteLine(
				$"  {v.Value, 8:F2}  (stress {v.StressValue, 7:F2})  {{{v.ManaCost}}} {v.Name}"
			);

		// The cards whose headline number is least trustworthy. A big drop between the two arms
		// means the value assumes a quiet board the card will rarely get.
		TestContext.Out.WriteLine("\n--- most fragile (value lost when the opponent attacks) ---");
		foreach (var v in measured.OrderByDescending(v => v.Fragility).Take(20))
			TestContext.Out.WriteLine(
				$"  {v.Fragility, 8:F2}  ({v.Value, 7:F2} -> {v.StressValue, 7:F2})  {{{v.ManaCost}}} {v.Name}"
			);

		// An inert card and a merely weak card both score ~0, and only one is a bug. A card that
		// never casts at all is the stronger signal of the two, so it gets its own list.
		TestContext.Out.WriteLine("\n--- not measured ---");
		foreach (
			var (reason, n) in values
				.Where(v => !v.WasMeasured)
				.GroupBy(v => v.NotMeasured!)
				.Select(g => (g.Key, g.Count()))
				.OrderByDescending(x => x.Item2)
		)
			TestContext.Out.WriteLine($"  {n, 4}  {reason}");

		// Coverage as a ratio so growing the set cannot break this, while a change that makes a
		// chunk of the set uncastable still fails rather than quietly narrowing the sweep to
		// whatever still happens to work.
		var castable = values.Count(v => v.NotMeasured != "land");
		Assert.That(
			100.0 * measured.Count / Math.Max(1, castable),
			Is.GreaterThan(85.0),
			$"only {measured.Count} of {castable} non-land cards were measured"
		);
	}
}
