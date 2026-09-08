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
