using MtgCore;
using MtgSimulator;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// Why does ChooseSpells pick the identity it picks? Prints the whole scoreboard for real pools.
[TestFixture]
[Explicit("Diagnostic.")]
public class LaneChoiceDiagnostic
{
	private static double Cast(Card card, ManaPool sources, int lands)
	{
		if (card.ColorPips.IsEmpty || lands <= 0)
			return 1.0;
		var needed = ManaBase.Requirements([card]);
		var worst = 1.0;
		foreach (var c in ManaPool.Colors)
			if (needed[c] > 0)
				worst = Math.Min(
					worst,
					Math.Min(
						1.0,
						sources[c] / ((double)needed[c] * lands / ManaBase.ReferenceLands)
					)
				);
		return worst;
	}

	[Test]
	public void ScoreboardForOneDraftedPool()
	{
		TestPaths.ChdirToSolutionRoot();
		var set = SetRegistry.Get("CSC")!;
		var model = DraftTrainingStore.Load(
			"SQGodotCommon/MtgGame/Assets/draft_training_csc.json"
		)!;
		var rng = new Random(7);
		var final = Draft.RunToCompletion(
			Draft.Create(DraftFormat.Booster, set.Cards, seed: 7, seatCount: 8),
			[
				.. Enumerable
					.Range(0, 8)
					.Select(_ => DraftPickers.Trained(model, new Random(rng.Next()))),
			]
		);

		var w = TestContext.Out;
		for (var seat = 0; seat < 3; seat++)
		{
			var pool = final.Seats[seat].Pool;
			w.WriteLine(
				$"--- seat {seat}: pool colours "
					+ string.Join(
						" ",
						ManaPool.Colors.Select(c =>
							$"{ManaPool.Symbol(c)}{pool.Count(x => x.ColorPips[c] > 0)}"
						)
					)
			);

			var rows = new List<(string Code, int N, double Q, double Cast, double Score)>();
			foreach (var identity in ColorIdentity.Standard)
			{
				var picked = new List<Card>();
				var q = 0.0;
				for (var i = 0; i < pool.Count && picked.Count < 23; i++)
					if (identity.Allows(pool[i]))
					{
						picked.Add(pool[i]);
						q += 1.0 - (double)i / pool.Count;
					}
				var lands = Math.Max(0, 40 - picked.Count);
				var sources = ManaBase
					.Build(picked, lands, ownerId: 0)
					.Aggregate(
						ManaPool.Empty,
						(sum, l) => sum.Add(l.GetComponent<LandColorComponent>()!.Produces)
					);
				var cast = picked.Count == 0 ? 0 : picked.Average(c => Cast(c, sources, lands));
				rows.Add((identity.Code, picked.Count, q, cast, q * cast));
			}

			foreach (var r in rows.OrderByDescending(r => r.Score).Take(6))
				w.WriteLine(
					$"  {r.Code, -2} cards {r.N, 2}  quality {r.Q, 5:F1}  castable {r.Cast, 4:F2}  score {r.Score, 6:F1}"
				);
		}
	}
}
