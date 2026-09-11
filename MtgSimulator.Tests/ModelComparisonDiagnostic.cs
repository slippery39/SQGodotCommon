using MtgCore;
using MtgSimulator;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// The new model against the shipped one, drafting AT THE SAME TABLE and then playing. A model
/// measured against itself always looks fine; the question is whether it wins the cards.
///
/// This is the check to run before copying a freshly trained model into the Godot assets — that
/// copy is what changes the game, and nothing else verifies it.
/// </summary>
[TestFixture]
[Explicit("Plays hundreds of games.")]
public class ModelComparisonDiagnostic
{
	[Test]
	public void NewModelVersusShipped()
	{
		TestPaths.ChdirToSolutionRoot();
		var set = SetRegistry.Get("CSC")!;
		var fresh = DraftTrainingStore.Load("sim_results/draft_training_csc.json")!;
		var shipped = DraftTrainingStore.Load(
			"SQGodotCommon/MtgGame/Assets/draft_training_csc.json"
		)!;

		var freshWins = 0;
		var shippedWins = 0;

		for (var d = 0; d < 40; d++)
		{
			var rng = new Random(d);
			var pickers = Enumerable
				.Range(0, 8)
				.Select(seat =>
					DraftPickers.Trained(seat % 2 == 0 ? fresh : shipped, new Random(rng.Next()))
				)
				.ToList();

			var final = Draft.RunToCompletion(
				Draft.Create(DraftFormat.Booster, set.Cards, seed: d, seatCount: 8),
				pickers
			);

			var pools = final.Seats.Select(s => (IReadOnlyList<Card>)s.Pool).ToList();
			var games = new List<(int A, int B, int Seed)>();
			for (var a = 0; a < 8; a += 2)
			for (var b = 1; b < 8; b += 2)
				games.Add((a, b, d * 1000 + a * 10 + b));

			var results = new GameResult[games.Count];
			Parallel.For(
				0,
				games.Count,
				i =>
					results[i] = DraftRunner.PlayGame(
						pools[games[i].A],
						pools[games[i].B],
						games[i].Seed,
						aiDepth: 2
					)
			);

			foreach (var r in results)
			{
				if (
					r.EndReason
					is GameEndReason.TimeLimitReached
						or GameEndReason.UnhandledException
				)
					continue;
				if (r.IsPlayer1Win)
					freshWins++;
				else if (!r.IsDraw)
					shippedWins++;
			}
		}

		var total = freshWins + shippedWins;
		TestContext.Out.WriteLine(
			$"fresh {freshWins}-{shippedWins} ({100.0 * freshWins / total:F1}% "
				+ $"+/- {100.0 * Math.Sqrt(0.25 / total):F1}) over {total} games"
		);
	}
}
