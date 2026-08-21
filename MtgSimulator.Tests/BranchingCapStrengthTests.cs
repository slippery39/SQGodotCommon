using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// Does the branching cap cost play strength?
///
/// The cap rests on a claim — that on a board of twenty near-identical tokens, the difference
/// between orderings is not worth the compute to find. That is a hypothesis about this game, not
/// a fact, so it gets measured: a capped AI plays an uncapped one over real drafted decks, and
/// the cap only ships if it holds its own.
///
/// [Explicit] because it plays hundreds of full games. Run it by name, not in the normal suite.
/// </summary>
[TestFixture]
[Explicit("Slow — measurement harness, run on demand")]
public class BranchingCapStrengthTests
{
	private const int Seats = 8;
	private const int Drafts = 8;
	private const int AiDepth = 3;

	private static IReadOnlyList<IReadOnlyList<Card>> DraftPools(int seed)
	{
		var pickers = Enumerable
			.Range(0, Seats)
			.Select(i =>
				i % 2 == 0
					? (DraftPicker)DraftPickers.Curve
					: DraftPickers.Random(new Random(seed + i))
			)
			.ToList();

		var final = Draft.RunToCompletion(
			Draft.Create(DraftFormat.Booster, CoresetCube.Set.Cards, seed, Seats),
			pickers
		);
		return final.Seats.Select(s => (IReadOnlyList<Card>)s.Pool).ToList();
	}

	/// cappedIsPlayer1 alternates so the play/draw advantage cannot be read as a cap effect.
	private static GameResult PlayOne(
		IReadOnlyList<Card> pool1,
		IReadOnlyList<Card> pool2,
		int gameSeed,
		bool cappedIsPlayer1
	)
	{
		var (state, ids, cardNames) = DraftGameSetup.Build(pool1, pool2);
		var rng = new Random(gameSeed + 4);

		IAiStrategy Capped() => new MultiTurnBeamSearchAiStrategy(ids, AiDepth, rng: rng);

		// BOTH caps must be lifted. NarrowActions takes min(expandBranching, maxBranching), so
		// raising only maxBranching leaves the expansion levels capped and the comparison
		// silently measures nothing — which it did, returning a suspiciously identical result.
		IAiStrategy Uncapped() =>
			new MultiTurnBeamSearchAiStrategy(
				ids,
				AiDepth,
				rng: rng,
				maxBranching: int.MaxValue,
				expandBranching: int.MaxValue
			);

		var runner = cappedIsPlayer1
			? new GameRunner(Capped(), Uncapped())
			: new GameRunner(Uncapped(), Capped());

		var (result, _) = runner.Run(
			state,
			ids,
			cardNames,
			shuffleSeed: gameSeed + 2,
			gameRngSeed: gameSeed + 3
		);
		return result;
	}

	[Test]
	public void CappedHoldsItsOwnAgainstUncapped()
	{
		var cappedWins = 0;
		var decided = 0;
		var games = 0;
		var sw = System.Diagnostics.Stopwatch.StartNew();

		for (var d = 0; d < Drafts; d++)
		{
			var pools = DraftPools(4242 + d * 1000);
			for (var a = 0; a < Seats; a++)
			{
				for (var b = a + 1; b < Seats; b++)
				{
					var cappedIsPlayer1 = games % 2 == 0;
					var result = PlayOne(pools[a], pools[b], 900_000 + games * 5, cappedIsPlayer1);
					games++;

					if (result.IsDraw)
						continue;
					decided++;
					var cappedWon = cappedIsPlayer1 ? result.IsPlayer1Win : result.IsPlayer2Win;
					if (cappedWon)
						cappedWins++;
				}
			}
		}
		sw.Stop();

		var rate = 100.0 * cappedWins / Math.Max(1, decided);
		var se = 100.0 * Math.Sqrt(0.25 / Math.Max(1, decided));
		TestContext.Out.WriteLine(
			$"Capped {cappedWins}/{decided} decided ({rate:F1}%, 1 SE = {se:F1}pp) "
				+ $"over {games} games in {sw.Elapsed.TotalSeconds:F0}s"
		);

		// Two standard errors below even. A cap that costs real strength fails here rather than
		// being discovered later as a quietly worse trained model.
		Assert.That(rate, Is.GreaterThan(50.0 - 2 * se), "Branching cap lost measurable strength");
	}
}
