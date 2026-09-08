using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **Why does leverage fail to measure, and for which cards?**
///
/// `EngineCandidate.BlankFirstKey` sorts unmeasured candidates into the bottom tier on purpose —
/// a failed arm leaves `Bare` at its default 0, and letting that pass as "a blank until assembled"
/// would put every failed measurement at the top of the report. That rule is right.
///
/// The consequence is that a card whose leverage cannot be measured is **structurally unable to be
/// seeded as an engine**, however good it is. Measured on DES: every reanimation core came back
/// unmeasured — Second Burial, Necromantic Summons, Echo of the Drowned, Ashen Rite — and Second
/// Burial carries the highest supplied value in the whole report while ranking 20th.
///
/// So the failure reason is the thing to read, and nothing printed it. `CardLeverage.NotMeasured`
/// carries it; `EngineCandidate` keeps only the bool.
/// </summary>
[TestFixture]
public class LeverageFailureTests
{
	/// <summary>
	/// **A payoff that cannot be cast into an EMPTY board still gets a leverage number.**
	///
	/// That is the definition of the class the engine report exists to find — a card that does
	/// nothing until the deck assembles — and treating it as unmeasured excluded exactly those
	/// cards, because `BlankFirstKey` sorts unmeasured candidates into the bottom tier. Measured on
	/// ALL beforehand: 14 of 16 leverage failures were this one reason, and no reanimation core had
	/// ever been seeded into a field.
	///
	/// Asserted structurally over the real pool rather than on named cards, so a balance pass
	/// cannot break it. The pool has to be real: breadth is a property of the pool and a demand
	/// answered by most of an eight-card fixture is pruned as uninformative, which makes the case
	/// under test unrepresentable — the same reason `LeverageSweepTests` uses the combined set.
	/// </summary>
	[Test]
	public void BeingUncastableIntoAnEmptyBoard_IsAMeasurementNotAFailure()
	{
		var spells = SetRegistry.Combined.Cards.Where(c => !c.HasSubtype("Land")).ToList();
		var features = PoolFeatures.Build(spells);
		var pool = spells
			.GroupBy(c => c.Name, StringComparer.Ordinal)
			.ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

		var payoffs = spells
			.Select(c => DeckCore.For(features, c.Name))
			.OfType<DeckCore>()
			.Select(c => c.Name)
			.Distinct(StringComparer.Ordinal)
			.ToList();

		var results = CardValueSandbox.MeasureLeverage(payoffs, features, pool).ToList();

		Assert.Multiple(() =>
		{
			Assert.That(
				results.Where(r => r.NotMeasured == CardValueSandbox.Uncastable),
				Is.Empty,
				"a payoff uncastable into an empty board is worth zero there — a measurement, "
					+ "not a failure, and marking it unmeasured is what buried reanimator"
			);

			// The class the report exists to rank must actually be present and measured.
			Assert.That(
				results.Count(r => r.WasMeasured && r.Bare == 0f && r.Leverage > 0f),
				Is.GreaterThan(0),
				"no measured blank-with-leverage payoff at all — blank-first has nothing to rank"
			);

			// **The vacuity guard.** A change that simply marked everything measured, or that
			// reported every card as a blank, would pass both assertions above. The measurement
			// still has to DISCRIMINATE: some payoffs are worth real value on their own.
			Assert.That(
				results.Count(r => r.WasMeasured && r.Bare > 1f),
				Is.GreaterThan(0),
				"every payoff reads as a blank — the bare arm is measuring nothing"
			);
		});
	}

	[Test]
	[Explicit("Diagnostic — prints why leverage did not measure, asserts only that it CAN.")]
	public void WhyDoesLeverageFailToMeasure()
	{
		var spells = SetRegistry.Combined.Cards.Where(c => !c.HasSubtype("Land")).ToList();
		var features = PoolFeatures.Build(spells);
		var pool = spells
			.GroupBy(c => c.Name, StringComparer.Ordinal)
			.ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

		// Only cards that produce a core — the population the engine report actually ranks.
		var payoffs = spells
			.Select(c => DeckCore.For(features, c.Name))
			.OfType<DeckCore>()
			.Select(c => c.Name)
			.Distinct(StringComparer.Ordinal)
			.ToList();

		var results = CardValueSandbox.MeasureLeverage(payoffs, features, pool).ToList();
		var failed = results.Where(r => !r.WasMeasured).ToList();

		Console.WriteLine($"{results.Count} payoffs measured, {failed.Count} failed");
		Console.WriteLine();

		foreach (var group in failed.GroupBy(r => r.NotMeasured).OrderByDescending(g => g.Count()))
		{
			Console.WriteLine($"  {group.Count(), 3}x  {group.Key}");
			foreach (var r in group.OrderBy(r => r.Name, StringComparer.Ordinal).Take(12))
				Console.WriteLine(
					$"          {r.Name, -34} bare {r.Bare, 7:F2}  supp {r.Supplied, 7:F2}"
				);
		}

		// The control: a run where NOTHING measured would print a tidy report and mean nothing.
		Assert.That(
			results.Count(r => r.WasMeasured),
			Is.GreaterThan(0),
			"no payoff measured at all — the sandbox is broken, and the reasons above are noise"
		);
	}
}
