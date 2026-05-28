using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

[TestFixture]
public class DeterminismTests
{
	// Replicates SimulatorRunner.SetupGame using a fixed seed.
	private static (
		GameState State,
		MtgGameIds Ids,
		IReadOnlyDictionary<int, string> CardNames
	) BuildGame(int gameSeed)
	{
		var (state, ids) = MtgGameFactory.Create();
		var cardNames = new Dictionary<int, string>();
		var pool = CardLibrary.All.ToList();

		var deck1 = CardPool.BuildRandomDeck(ids.Player1Id, pool, rng: new Random(gameSeed));
		foreach (var card in deck1)
		{
			var (newState, added) = state.AddObject(card, parentId: ids.Player1LibraryId);
			state = newState;
			cardNames[added.Id] = added.Name;
		}

		var deck2 = CardPool.BuildRandomDeck(ids.Player2Id, pool, rng: new Random(gameSeed + 1));
		foreach (var card in deck2)
		{
			var (newState, added) = state.AddObject(card, parentId: ids.Player2LibraryId);
			state = newState;
			cardNames[added.Id] = added.Name;
		}

		return (state, ids, cardNames);
	}

	private static void AssertIdenticalResults(GameResult result1, GameResult result2)
	{
		Assert.Multiple(() =>
		{
			Assert.That(result2.IsPlayer1Win, Is.EqualTo(result1.IsPlayer1Win), "Winner differs");
			Assert.That(result2.IsDraw, Is.EqualTo(result1.IsDraw), "Draw status differs");
			Assert.That(result2.TurnCount, Is.EqualTo(result1.TurnCount), "Turn count differs");
			Assert.That(
				result2.TotalActions,
				Is.EqualTo(result1.TotalActions),
				"Total actions differs"
			);
			Assert.That(
				result2.Player1DrawnCards,
				Is.EqualTo(result1.Player1DrawnCards),
				"P1 drawn cards differ"
			);
			Assert.That(
				result2.Player2DrawnCards,
				Is.EqualTo(result1.Player2DrawnCards),
				"P2 drawn cards differ"
			);
		});
	}

	/// <summary>
	/// MultiTurnBeamSearchAiStrategy is what SimulatorRunner uses by default.
	/// Beam scoring runs in parallel (.AsParallel().AsOrdered()) — ordering is deterministic
	/// because AsOrdered preserves input order across threads.
	/// Uses depth=1 / lookaheadTurns=0 to keep the test fast.
	/// </summary>
	[Test]
	public void SameSeed_MultiTurnBeamSearch_ProducesIdenticalResult()
	{
		const int seed = 42;

		var (state1, ids1, cardNames1) = BuildGame(seed);
		var rng1 = new Random(seed + 4);
		var (result1, _) = new GameRunner(
			new MultiTurnBeamSearchAiStrategy(
				ids1,
				currentTurnDepth: 1,
				lookaheadTurns: 0,
				rng: rng1
			),
			new MultiTurnBeamSearchAiStrategy(
				ids1,
				currentTurnDepth: 1,
				lookaheadTurns: 0,
				rng: rng1
			)
		).Run(state1, ids1, cardNames1, shuffleSeed: seed + 2, gameRngSeed: seed + 3);

		var (state2, ids2, cardNames2) = BuildGame(seed);
		var rng2 = new Random(seed + 4);
		var (result2, _) = new GameRunner(
			new MultiTurnBeamSearchAiStrategy(
				ids2,
				currentTurnDepth: 1,
				lookaheadTurns: 0,
				rng: rng2
			),
			new MultiTurnBeamSearchAiStrategy(
				ids2,
				currentTurnDepth: 1,
				lookaheadTurns: 0,
				rng: rng2
			)
		).Run(state2, ids2, cardNames2, shuffleSeed: seed + 2, gameRngSeed: seed + 3);

		AssertIdenticalResults(result1, result2);
	}

	/// <summary>
	/// RandomAiStrategy calls RNG on every action, so unseeded randomness anywhere in the
	/// game setup or turn loop will cause divergence here.
	/// </summary>
	[Test]
	public void SameSeed_RandomStrategy_ProducesIdenticalResult()
	{
		const int seed = 42;

		var (state1, ids1, cardNames1) = BuildGame(seed);
		var rng1 = new Random(seed + 4);
		var (result1, _) = new GameRunner(
			new RandomAiStrategy(rng: rng1),
			new RandomAiStrategy(rng: rng1)
		).Run(state1, ids1, cardNames1, shuffleSeed: seed + 2, gameRngSeed: seed + 3);

		var (state2, ids2, cardNames2) = BuildGame(seed);
		var rng2 = new Random(seed + 4);
		var (result2, _) = new GameRunner(
			new RandomAiStrategy(rng: rng2),
			new RandomAiStrategy(rng: rng2)
		).Run(state2, ids2, cardNames2, shuffleSeed: seed + 2, gameRngSeed: seed + 3);

		AssertIdenticalResults(result1, result2);
	}
}
