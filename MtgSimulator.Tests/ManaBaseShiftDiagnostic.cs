using MtgCore;
using MtgSimulator;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// How much did threshold-first allocation actually move? Measured before spending two hours on a
/// head-to-head: if the decks the project really builds barely change, the A/B has nothing to find.
///
/// Explicit — it is a measurement, not an assertion. Re-run it if <see cref="ManaBase"/>'s
/// allocation changes again.
/// </summary>
[TestFixture]
[Explicit("Diagnostic — prints a distribution, asserts nothing interesting.")]
public class ManaBaseShiftDiagnostic
{
	/// <summary>
	/// The allocator as it was before thresholds: split every land in proportion to SUMMED demand.
	/// A deliberate copy, because the point is to compare against code that no longer exists.
	/// </summary>
	private static Dictionary<ManaColor, int> Proportional(
		IReadOnlyList<Card> spells,
		int landCount
	)
	{
		var demand = ManaPool.Empty;
		foreach (var card in spells)
		{
			var pips = card.ColorPips;
			if (pips.IsEmpty)
				continue;
			var turn = Math.Clamp(Math.Max(card.ManaCost, pips.Total), 1, 6) - 1;
			foreach (var color in ManaPool.Colors)
				if (pips[color] > 0)
					demand = demand.Add(
						color,
						Math.Clamp(pips[color], 1, 3) switch
						{
							1 => new[] { 13, 12, 11, 10, 9, 9 }[turn],
							2 => new[] { 18, 18, 17, 17, 15, 15 }[turn],
							_ => new[] { 22, 22, 22, 22, 21, 20 }[turn],
						}
					);
		}

		var used = ManaPool.Colors.Where(c => demand[c] > 0).ToList();
		var counts = used.ToDictionary(c => c, _ => 0);
		if (used.Count == 0)
			return counts;

		var guaranteed = Math.Min(used.Count, landCount);
		foreach (var c in used)
			counts[c] = used.IndexOf(c) < guaranteed ? 1 : 0;

		var remaining = landCount - guaranteed;
		var exact = used.ToDictionary(c => c, c => (double)remaining * demand[c] / demand.Total);
		foreach (var c in used)
			counts[c] += (int)Math.Floor(exact[c]);

		foreach (
			var c in used.OrderByDescending(c => exact[c] - Math.Floor(exact[c]))
				.ThenBy(ManaPool.Colors.IndexOf)
				.Take(Math.Max(0, landCount - counts.Values.Sum()))
		)
			counts[c]++;

		return counts;
	}

	private readonly List<int> _totals = [];

	[Test]
	public void HowFarDidTheManabasesMove()
	{
		var pool = SetRegistry.Get("CSC")!.Cards.Where(c => !c.HasSubtype("Land")).ToList();
		const int landCount = 17;
		var moved = 0;
		var totalShift = 0;
		var worst = 0;
		var starved = 0;
		var runs = 0;

		foreach (var identity in ColorIdentity.Standard)
		{
			var playable = identity.Playable(pool);
			if (playable.Count < 23)
				continue;

			for (var seed = 0; seed < 40; seed++)
			{
				var rng = new Random(seed * 31 + identity.Code.GetHashCode());
				var spells = playable.OrderBy(_ => rng.Next()).Take(23).ToList();

				var before = Proportional(spells, landCount);
				var after = ManaBase
					.Build(spells, landCount, ownerId: 1)
					.GroupBy(l => l.GetComponent<LandColorComponent>()!.Produces)
					.ToDictionary(g => ManaPool.Colors.First(c => g.Key[c] > 0), g => g.Count());

				var shift =
					ManaPool.Colors.Sum(c =>
						Math.Abs(after.GetValueOrDefault(c) - before.GetValueOrDefault(c))
					) / 2;

				runs++;
				if (shift > 0)
					moved++;
				totalShift += shift;
				worst = Math.Max(worst, shift);

				// A colour whose threshold the OLD split missed and the new one meets.
				var required = ManaBase.Requirements(spells);
				_totals.Add(required.Total);
				if (required.Total > 0 && required.Total <= ManaBase.ReferenceLands)
					foreach (var c in ManaPool.Colors)
					{
						var need = required[c] * landCount / ManaBase.ReferenceLands;
						if (
							need > 0
							&& before.GetValueOrDefault(c) < need
							&& after.GetValueOrDefault(c) >= need
						)
							starved++;
					}
			}
		}

		TestContext.Out.WriteLine(
			"requirement totals (24 = the budget): "
				+ string.Join(
					" ",
					_totals.OrderBy(t => t).Where((_, i) => i % (_totals.Count / 10) == 0)
				)
				+ $" | fit inside 24: {_totals.Count(t => t <= 24)}/{_totals.Count}"
		);
		TestContext.Out.WriteLine(
			$"{runs} decks | manabase changed in {moved} ({100.0 * moved / runs:F0}%) | "
				+ $"mean shift {(double)totalShift / runs:F2} lands | worst {worst} | "
				+ $"colours rescued from below threshold: {starved}"
		);
	}

	/// <summary>
	/// The shape the threshold gate actually exists for, which the random sample above contains
	/// none of: a mono-colour deck with a two-card splash. A random 23-card draw from a whole
	/// identity always takes double pips in both colours, so its requirement never fits — that is
	/// a property of the SAMPLE, not of the allocator.
	/// </summary>
	[Test]
	public void HowFarDoSplashDecksMove()
	{
		var pool = SetRegistry.Get("CSC")!.Cards.Where(c => !c.HasSubtype("Land")).ToList();
		const int landCount = 17;
		var moved = 0;
		var fits = 0;
		var runs = 0;
		var splashLandsBefore = 0;
		var splashLandsAfter = 0;

		foreach (var main in ManaPool.Colors)
		foreach (var splash in ManaPool.Colors)
		{
			if (main == splash)
				continue;

			var mainCards = pool.Where(c =>
					c.ColorPips[main] > 0 && c.ColorPips.Total == c.ColorPips[main]
				)
				.ToList();
			var splashCards = pool.Where(c =>
					c.ColorPips[splash] > 0 && c.ColorPips.Total == c.ColorPips[splash]
				)
				.ToList();
			if (mainCards.Count < 21 || splashCards.Count < 2)
				continue;

			for (var seed = 0; seed < 20; seed++)
			{
				var rng = new Random(seed * 97 + (int)main * 13 + (int)splash);
				var spells = mainCards
					.OrderBy(_ => rng.Next())
					.Take(21)
					.Concat(splashCards.OrderBy(_ => rng.Next()).Take(2))
					.ToList();

				var before = Proportional(spells, landCount);
				var after = ManaBase
					.Build(spells, landCount, ownerId: 1)
					.GroupBy(l => l.GetComponent<LandColorComponent>()!.Produces)
					.ToDictionary(g => ManaPool.Colors.First(c => g.Key[c] > 0), g => g.Count());

				runs++;
				if (ManaBase.Requirements(spells).Total <= ManaBase.ReferenceLands)
					fits++;
				if (
					ManaPool.Colors.Any(c =>
						after.GetValueOrDefault(c) != before.GetValueOrDefault(c)
					)
				)
					moved++;
				splashLandsBefore += before.GetValueOrDefault(splash);
				splashLandsAfter += after.GetValueOrDefault(splash);
			}
		}

		TestContext.Out.WriteLine(
			$"SPLASH: {runs} decks | fit inside 24: {fits} | manabase changed in {moved} | "
				+ $"splash sources mean {(double)splashLandsBefore / runs:F1} -> {(double)splashLandsAfter / runs:F1} of {landCount}"
		);
	}
}
