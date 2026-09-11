using MtgCore;
using MtgSimulator;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// How deep is a drafted pool in its OWN best identity? A real drafter ends with 23+ playables and
/// a comfortable margin; 19 is a trainwreck that should be rare and caused by contested colours.
/// </summary>
[TestFixture]
[Explicit("Diagnostic.")]
public class DraftLaneDepthDiagnostic
{
	[TestCase(0.0)]
	[TestCase(2.0)]
	[TestCase(4.0)]
	[TestCase(6.0)]
	[TestCase(10.0)]
	public void PlayablesInTheBestIdentity(double laneWeight)
	{
		TestPaths.ChdirToSolutionRoot();
		var set = SetRegistry.Get("CSC")!;
		var model = DraftTrainingStore.Load(
			"SQGodotCommon/MtgGame/Assets/draft_training_csc.json"
		)!;
		var depths = new List<int>();
		var spread = new List<double>();

		for (var seed = 0; seed < 25; seed++)
		{
			var rng = new Random(seed);
			var final = Draft.RunToCompletion(
				Draft.Create(DraftFormat.Booster, set.Cards, seed, seatCount: 8),
				[
					.. Enumerable
						.Range(0, 8)
						.Select(_ =>
							DraftPickers.Trained(
								model,
								new Random(rng.Next()),
								laneWeight: laneWeight
							)
						),
				]
			);

			foreach (var seat in final.Seats)
			{
				var pool = seat.Pool.Where(c => !c.HasSubtype("Land")).ToList();
				depths.Add(ColorIdentity.Standard.Max(i => i.Playable(pool).Count));

				// How evenly the picks are spread over the five colours: 1.0 is perfectly even,
				// 0.2 is mono. A drafter who commits should be well under 0.6.
				var byColor = ManaPool
					.Colors.Select(c => (double)pool.Count(x => x.ColorPips[c] > 0))
					.ToList();
				var total = byColor.Sum();
				spread.Add(total == 0 ? 0 : byColor.Sum(n => n / total * (n / total)));
			}
		}

		var w = TestContext.Out;
		w.WriteLine(
			$"lane {laneWeight, 4:F1} | playables mean {depths.Average():F1} "
				+ $"min {depths.Min()} max {depths.Max()} | short of 23: "
				+ $"{100.0 * depths.Count(d => d < 23) / depths.Count:F0}% | "
				+ $"concentration {spread.Average():F2}"
		);
	}
}
