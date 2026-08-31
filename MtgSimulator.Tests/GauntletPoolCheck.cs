using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// Diagnostic for the gauntlet design: which `DeckRegistry` decks could a challenger actually
/// copy, per pool? A gauntlet deck whose cards are absent from the evolution pool is a benchmark
/// the field can never converge on — which matters, because converging on a gauntlet deck is a
/// desired outcome, not a failure.
/// </summary>
[TestFixture]
public class GauntletPoolCheck
{
	[Test]
	[Explicit("Diagnostic only.")]
	public void WhichGauntletDecksAreBuildableFromWhichPool()
	{
		foreach (var setCode in new[] { "CSC", "ALL" })
		{
			var set = SetRegistry.Get(setCode);
			var pool = set.Cards.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
			Console.WriteLine($"--- {setCode} ({set.Cards.Count} cards) ---");

			foreach (var info in DeckRegistry.All)
			{
				var names = DeckRegistry
					.Build(info.Name, 1)
					.Select(c => c.Name)
					.Distinct(StringComparer.Ordinal)
					.ToList();
				var missing = names.Where(n => !pool.Contains(n)).ToList();
				Console.WriteLine(
					$"  {info.Name, -20} {names.Count - missing.Count, 2}/{names.Count, 2} in pool"
						+ (
							missing.Count > 0
								? $"   missing e.g. {string.Join(", ", missing.Take(4))}"
								: "   FULLY BUILDABLE"
						)
				);
			}
		}
	}
}
