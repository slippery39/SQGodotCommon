using MtgCore;
using MtgSimulator;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// How well does the draft model sample GOLD cards now that a deck is chosen inside one identity?
/// A two-colour card is legal in exactly one of the fifteen, and a three-colour card in none.
/// </summary>
[TestFixture]
[Explicit("Diagnostic.")]
public class GoldCardSamplingDiagnostic
{
	[TestCase("LEG", "sim_results/draft_training.json")]
	[TestCase("CSC", "sim_results/draft_training_csc.json")]
	public void GamesByColourCount(string code, string path)
	{
		TestPaths.ChdirToSolutionRoot();
		var set = SetRegistry.Get(code)!;
		var model = DraftTrainingStore.Load(path)!;
		var games = model.Cards.ToDictionary(c => c.Name, c => c.Games, StringComparer.Ordinal);

		var buckets = new SortedDictionary<int, List<int>>();
		foreach (var card in set.Draftable.DistinctBy(c => c.Name))
		{
			if (!games.TryGetValue(card.Name, out var n))
				continue;
			var colors = ManaPool.Colors.Count(c => card.ColorPips[c] > 0);
			if (!buckets.TryGetValue(colors, out var list))
				buckets[colors] = list = [];
			list.Add(n);
		}

		var w = TestContext.Out;
		foreach (var (colors, list) in buckets)
		{
			list.Sort();
			var label = colors switch
			{
				0 => "colourless",
				1 => "mono",
				2 => "gold (2)",
				_ => "gold (3+)",
			};
			w.WriteLine(
				$"{code} {label, -11} cards {list.Count, 4}  median games {list[list.Count / 2], 6}  min {list[0], 6}"
			);
		}
	}
}
