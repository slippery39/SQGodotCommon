using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **Leverage is the ranking that turns 50 generated cores into an exploration queue.**
///
/// `DeckCore.For` builds a core for every card that asks anything answerable, which on ALL is 203
/// cores and 50 distinct after dedupe — and most of them are one broad demand ("a creature
/// entered") that no deck needs to be built around. Supplier count cannot separate those from real
/// payoffs, because it cannot tell a broad demand with a real payoff from a broad demand with a
/// fake one. What separates them is whether the card is a BLANK until its demands are met.
/// </summary>
[TestFixture]
public class LeverageSweepTests
{
	private const string Anchor = "Dragonstorm";

	/// <summary>
	/// **The whole combined pool, not a handful of named cards, and the first attempt at the small
	/// version is why.** A demand answered by ~every card in scope is pruned as uninformative, so in
	/// an eight-card fixture a BROAD payoff has no demands at all and comes back "asks nothing
	/// answerable" — the exact case this test exists to compare against, made unrepresentable by the
	/// fixture. Breadth is a property of the pool; it cannot be mocked at eight cards.
	/// </summary>
	private static readonly Lazy<(PoolFeatures Features, Dictionary<string, Card> Cards)> Real =
		new(() =>
		{
			var spells = SetRegistry.Combined.Cards.Where(c => !c.HasSubtype("Land")).ToList();
			return (
				PoolFeatures.Build(spells),
				spells
					.GroupBy(c => c.Name, StringComparer.Ordinal)
					.ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal)
			);
		});

	[Test]
	public void ADemandedPayoffHasLeverage_AndABroadOneDoesNot()
	{
		// **The discrimination this exists for, both directions.** Asserting only that Dragonstorm
		// scores well would pass on a measurement that rates everything well, which is exactly the
		// vacuous-column failure LIFT shipped with twice.
		var (features, cards) = Real.Value;

		// Chosen by MEASUREMENT rather than by name: the payoff whose core has the widest support
		// slots is the broadest concept the pool contains, so this cannot rot when a card is
		// renamed or the cube changes.
		var broadest = cards
			.Keys.Select(n => (Name: n, Core: DeckCore.For(features, n)))
			.Where(x => x.Core is not null)
			.OrderByDescending(x => x.Core!.Slots.Skip(1).Sum(s => s.Cards.Count))
			.ThenBy(x => x.Name, StringComparer.Ordinal)
			.First()
			.Name;

		var results = CardValueSandbox
			.MeasureLeverage([Anchor, broadest], features, cards)
			.ToDictionary(r => r.Name, r => r, StringComparer.Ordinal);

		var storm = results[Anchor];
		var broad = results[broadest];
		TestContext.Out.WriteLine($"broadest payoff in the pool: {broadest}");

		TestContext.Out.WriteLine(
			$"{Anchor}: bare {storm.Bare:F2} supplied {storm.Supplied:F2} "
				+ $"leverage {storm.Leverage:+0.00;-0.00} ({storm.NotMeasured ?? "ok"})"
		);
		TestContext.Out.WriteLine(
			$"{broadest}: bare {broad.Bare:F2} supplied {broad.Supplied:F2} "
				+ $"leverage {broad.Leverage:+0.00;-0.00} ({broad.NotMeasured ?? "ok"})"
		);

		Assert.Multiple(() =>
		{
			Assert.That(storm.WasMeasured, Is.True, storm.NotMeasured);
			Assert.That(broad.WasMeasured, Is.True, broad.NotMeasured);
			Assert.That(
				storm.Leverage,
				Is.GreaterThan(broad.Leverage),
				"a card that is a blank without its demands must gain more from having them than "
					+ "an aura that targets any creature does"
			);
		});
	}

	/// <summary>
	/// The ranked queue for a real pool. **Read it** — the top should be cards you recognise as
	/// needing a deck built around them, and the bottom should be good-stuff cards.
	/// </summary>
	[Test]
	[Explicit("Sweep — rolls out two fixtures per payoff across a whole set. Minutes.")]
	public void DumpLeverageForARealPool()
	{
		var code = "ALL";
		var (features, cards) = Real.Value;

		// Only cards that generate a core — the rest are good stuff by definition and there is
		// nothing to rank them against.
		var payoffs = cards
			.Keys.Where(n => DeckCore.For(features, n) is not null)
			.Order(StringComparer.Ordinal)
			.ToList();

		var measured = CardValueSandbox.MeasureLeverage(payoffs, features, cards);
		var ok = measured.Where(r => r.WasMeasured).OrderByDescending(r => r.Leverage).ToList();

		TestContext.Out.WriteLine(
			$"{code}: {payoffs.Count} payoffs with a core, {ok.Count} measured, "
				+ $"{measured.Count - ok.Count} skipped"
		);
		TestContext.Out.WriteLine($"\n{"payoff", -38}{"bare", 10}{"supplied", 10}{"LEVERAGE", 11}");

		foreach (var r in ok.Take(25).Concat(ok.TakeLast(10)))
			TestContext.Out.WriteLine(
				$"{(r.Name.Length > 36 ? r.Name[..36] : r.Name), -38}"
					+ $"{r.Bare, 10:F2}{r.Supplied, 10:F2}{r.Leverage, 11:+0.00;-0.00}"
			);

		foreach (var group in measured.Where(r => !r.WasMeasured).GroupBy(r => r.NotMeasured))
			TestContext.Out.WriteLine($"\nskipped ({group.Count()}): {group.Key}");
	}
}
