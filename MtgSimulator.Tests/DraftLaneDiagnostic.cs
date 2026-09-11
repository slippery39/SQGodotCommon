using MtgCore;
using MtgSimulator;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// What colour-aware deck assembly did to real drafted decks. Explicit — a measurement, not an
/// assertion. The comparison is against the OLD rule (first 23 non-lands in pick order), copied
/// here because it no longer exists in the source.
/// </summary>
[TestFixture]
[Explicit("Diagnostic — prints a comparison, asserts nothing interesting.")]
public class DraftLaneDiagnostic
{
	private static double Deep;

	private readonly record struct Built(IReadOnlyList<Card> Spells, IReadOnlyList<Card> Lands);

	private static (double Colors, double Shortfall, double Uncastable, double Spells) Measure(
		IReadOnlyList<Built> decks
	)
	{
		var colors = 0.0;
		var shortfall = 0.0;
		var uncastable = 0.0;

		foreach (var entry in decks)
		{
			var deck = entry.Spells;
			var lands = entry.Lands;
			var sources = lands.Aggregate(
				ManaPool.Empty,
				(sum, l) => sum.Add(l.GetComponent<LandColorComponent>()!.Produces)
			);

			colors += ManaPool.Colors.Count(c => deck.Any(card => card.ColorPips[c] > 0));
			shortfall += ManaBase.Shortfall(deck);

			// A card is a dead draw when its own colour has fewer sources than the table asks for,
			// scaled to this deck's 17 lands.
			foreach (var card in deck)
			{
				var short_ = ManaPool.Colors.Any(c =>
					card.ColorPips[c] > 0
					&& sources[c]
						< ManaBase.Requirements([card])[c]
							* entry.Lands.Count
							/ ManaBase.ReferenceLands
				);
				if (!short_)
					continue;
				uncastable++;
				if (ManaPool.Colors.Max(c => card.ColorPips[c]) >= 2)
					Deep++;
			}
		}

		var n = decks.Count;
		return (colors / n, shortfall / n, uncastable / n, decks.Average(d => d.Spells.Count));
	}

	[Test]
	public void WhatDidLaneDisciplineDoToDraftedDecks()
	{
		TestPaths.ChdirToSolutionRoot();
		var set = SetRegistry.Get("CSC")!;
		var model = DraftTrainingStore.Load("SQGodotCommon/MtgGame/Assets/draft_training_csc.json");
		Assert.That(model, Is.Not.Null, "run from the solution root — see TestPaths");

		var before = new List<Built>();
		var after = new List<Built>();

		for (var seed = 0; seed < 25; seed++)
		{
			var rng = new Random(seed);
			var pickers = Enumerable
				.Range(0, 8)
				.Select(_ => DraftPickers.Trained(model!, new Random(rng.Next())))
				.ToList();
			var final = Draft.RunToCompletion(
				Draft.Create(DraftFormat.Booster, set.Cards, seed, seatCount: 8),
				pickers
			);

			foreach (var seat in final.Seats)
			{
				// The OLD rule: first 23 non-lands, manabase over all of them.
				var old = seat.Pool.Where(c => !c.HasSubtype("Land")).Take(23).ToList();
				before.Add(new Built(old, ManaBase.Build(old, 40 - old.Count, ownerId: 1)));

				// The real thing, straight out of BuildDeck — including its core-only manabase.
				var built = Draft.BuildDeck(seat.Pool, ownerId: 1);
				after.Add(
					new Built(
						[.. built.Where(c => !c.HasSubtype("Land"))],
						[.. built.Where(c => c.HasSubtype("Land"))]
					)
				);
			}
		}

		Deep = 0;
		var b = Measure(before);
		Deep = 0;
		var a = Measure(after);
		var w = TestContext.Out;
		w.WriteLine($"{before.Count} drafted decks, CSC, trained picker");
		w.WriteLine($"  colours per deck : {b.Colors:F2} -> {a.Colors:F2}");
		w.WriteLine($"  shortfall        : {b.Shortfall:F1} -> {a.Shortfall:F1}");
		w.WriteLine($"  dead draws/deck  : {b.Uncastable:F1} -> {a.Uncastable:F1} of 23");
		w.WriteLine($"  of the dead draws, double-pipped: {Deep / after.Count:F1} per deck");
		w.WriteLine($"  spells played    : {b.Spells:F1} -> {a.Spells:F1} (rest becomes lands)");
	}

	/// What the manabases actually look like — total lands, and the split. A real limited deck is
	/// 17 lands at 9/8 or 10/7, so that is the yardstick.
	[Test]
	public void WhatDoTheManabasesLookLike()
	{
		TestPaths.ChdirToSolutionRoot();
		var set = SetRegistry.Get("CSC")!;
		var model = DraftTrainingStore.Load(
			"SQGodotCommon/MtgGame/Assets/draft_training_csc.json"
		)!;
		var splits = new List<string>();
		var totals = new List<int>();
		var offColour = new List<int>();

		for (var seed = 0; seed < 25; seed++)
		{
			var rng = new Random(seed);
			var final = Draft.RunToCompletion(
				Draft.Create(DraftFormat.Booster, set.Cards, seed, seatCount: 8),
				[
					.. Enumerable
						.Range(0, 8)
						.Select(_ => DraftPickers.Trained(model, new Random(rng.Next()))),
				]
			);

			foreach (var seat in final.Seats)
			{
				var deck = Draft.BuildDeck(seat.Pool, ownerId: 1);
				var lands = deck.Where(c => c.HasSubtype("Land")).ToList();
				var byColor = ManaPool
					.Colors.Select(c =>
						lands.Count(l => l.GetComponent<LandColorComponent>()!.Produces[c] > 0)
					)
					.Where(n => n > 0)
					.OrderByDescending(n => n)
					.ToList();

				totals.Add(lands.Count);
				splits.Add(string.Join("/", byColor));
				if (byColor.Count > 1)
					offColour.Add(byColor[1]);
			}
		}

		var w = TestContext.Out;
		w.WriteLine(
			$"lands per deck: mean {totals.Average():F1}, min {totals.Min()}, max {totals.Max()}"
		);
		foreach (var g in totals.GroupBy(t => t).OrderBy(g => g.Key))
			w.WriteLine($"    {g.Key} lands x{g.Count()}");
		w.WriteLine(
			$"second colour sources: mean {(offColour.Count == 0 ? 0 : offColour.Average()):F1}, "
				+ $"min {(offColour.Count == 0 ? 0 : offColour.Min())}, max {(offColour.Count == 0 ? 0 : offColour.Max())}"
		);
		foreach (var g in splits.GroupBy(x => x).OrderByDescending(g => g.Count()).Take(12))
			w.WriteLine($"  {g.Key, -10} x{g.Count()}");
	}
}
