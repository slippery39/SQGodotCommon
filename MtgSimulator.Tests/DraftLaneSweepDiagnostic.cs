using MtgCore;
using MtgSimulator;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// Does colour commitment actually WIN, or does it only look tidy? Half the seats draft with the
/// lane term and half without, in the SAME draft, then play a full round robin — so the two
/// pickers are competing for the same cards at the same table, which is the only way the question
/// means anything. A lane term measured against itself would always look good.
///
/// This is the measurement DraftPickers.Trained's synergyWeight comment demands and the reason its
/// default is 0: that term was obviously right too, and cost 20 points of win rate.
/// </summary>
[TestFixture]
[Explicit("Plays thousands of games — minutes, not seconds.")]
public class DraftLaneSweepDiagnostic
{
	private static void Run(double laneWeight, int drafts, int aiDepth)
	{
		TestPaths.ChdirToSolutionRoot();
		var set = SetRegistry.Get("CSC")!;
		var model = DraftTrainingStore.Load(
			"SQGodotCommon/MtgGame/Assets/draft_training_csc.json"
		)!;

		var laneWins = 0;
		var flatWins = 0;
		var draws = 0;

		for (var d = 0; d < drafts; d++)
		{
			var rng = new Random(d);
			// Alternating seats, so neither picker sits together and feeds itself.
			var pickers = Enumerable
				.Range(0, 8)
				.Select(seat =>
					DraftPickers.Trained(
						model,
						new Random(rng.Next()),
						laneWeight: seat % 2 == 0 ? laneWeight : 0.0
					)
				)
				.ToList();

			var final = Draft.RunToCompletion(
				Draft.Create(DraftFormat.Booster, set.Cards, seed: d, seatCount: 8),
				pickers
			);

			var pools = final.Seats.Select(s => (IReadOnlyList<Card>)s.Pool).ToList();
			var games = new List<(int A, int B, int Seed)>();
			for (var a = 0; a < 8; a++)
			for (var b = 0; b < 8; b++)
				if (a % 2 == 0 && b % 2 == 1) // lane seat vs flat seat only
					games.Add((a, b, d * 1000 + a * 10 + b));

			var results = new GameResult[games.Count];
			Parallel.For(
				0,
				games.Count,
				i =>
				{
					var (a, b, seed) = games[i];
					results[i] = DraftRunner.PlayGame(pools[a], pools[b], seed, aiDepth);
				}
			);

			foreach (var r in results)
			{
				if (
					r.EndReason
					is GameEndReason.TimeLimitReached
						or GameEndReason.UnhandledException
				)
					continue;
				if (r.IsDraw)
					draws++;
				else if (r.IsPlayer1Win)
					laneWins++;
				else
					flatWins++;
			}
		}

		var total = laneWins + flatWins;
		var rate = total == 0 ? 0 : 100.0 * laneWins / total;
		var se = total == 0 ? 0 : 100.0 * Math.Sqrt(0.25 / total);
		TestContext.Out.WriteLine(
			$"lane {laneWeight, 4:F1} | lane-aware {laneWins}-{flatWins} ({rate:F1}% +/- {se:F1}) "
				+ $"| draws {draws} | {total} games"
		);
	}

	[TestCase(2.0)]
	[TestCase(4.0)]
	[TestCase(8.0)]
	public void DoesCommitmentWin(double laneWeight) => Run(laneWeight, drafts: 12, aiDepth: 2);

	/// <summary>
	/// The same question at a sample that can actually answer it. 12 drafts gives +/- 3.6 points,
	/// which cannot separate a 2-point effect from nothing — and a 2-point effect is exactly the
	/// size worth arguing about here.
	/// </summary>
	[TestCase(2.0)]
	[TestCase(4.0)]
	public void DoesCommitmentWin_LargeSample(double laneWeight) =>
		Run(laneWeight, drafts: 60, aiDepth: 2);
}
