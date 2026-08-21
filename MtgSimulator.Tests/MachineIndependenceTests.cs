using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// A game's outcome must depend on the cards and the seed — never on how fast the machine
/// happened to be running.
///
/// This is not a theoretical concern. GameRunner used to end a game as a draw at 20 seconds of
/// wall-clock, so simply holding more finished games in memory — a change that cannot touch
/// gameplay — moved 2 578 outcomes in a 28 000-game training batch. Draws scaled with batch
/// size (0.4% at 1 120 games, 15.2% at 28 000) and polluted every trained model with games
/// that no rule had decided.
/// </summary>
[TestFixture]
public class MachineIndependenceTests
{
	private const int Seed = 42;

	private static GameResult PlayGame(
		int rolloutBudget = MultiTurnBeamSearchAiStrategy.DefaultRolloutBudget
	)
	{
		var (state, ids) = MtgGameFactory.Create();
		var cardNames = new Dictionary<int, string>();
		var pool = CardLibrary.All.ToList();

		foreach (
			var (deck, libraryId) in new[]
			{
				(
					CardPool.BuildRandomDeck(ids.Player1Id, pool, rng: new Random(Seed)),
					ids.Player1LibraryId
				),
				(
					CardPool.BuildRandomDeck(ids.Player2Id, pool, rng: new Random(Seed + 1)),
					ids.Player2LibraryId
				),
			}
		)
		{
			foreach (var card in deck)
			{
				var (newState, added) = state.AddObject(card, parentId: libraryId);
				state = newState;
				cardNames[added.Id] = added.Name;
			}
		}

		var rng = new Random(Seed + 4);
		var (result, _) = new GameRunner(
			new MultiTurnBeamSearchAiStrategy(
				ids,
				currentTurnDepth: 1,
				lookaheadTurns: 0,
				rng: rng,
				rolloutBudget: rolloutBudget
			),
			new MultiTurnBeamSearchAiStrategy(
				ids,
				currentTurnDepth: 1,
				lookaheadTurns: 0,
				rng: rng,
				rolloutBudget: rolloutBudget
			)
		).Run(state, ids, cardNames, shuffleSeed: Seed + 2, gameRngSeed: Seed + 3);
		return result;
	}

	/// The regression test proper: compete for CPU and the game must still play out identically.
	///
	/// **The load must not come from the thread pool.** Written with Task.Run this test failed
	/// consistently, and not because the fix was wrong: the beam search parallelises over the
	/// same pool, so busy Tasks starve it of workers rather than merely slowing it down, and a
	/// single move stretched past the 300-second safety net. Dedicated below-normal-priority
	/// threads contend for cores — which is what a loaded machine actually does — without taking
	/// the workers the AI needs.
	///
	/// This asserts equality, which holds by construction unless a wall-clock reading has crept
	/// back into a decision. The one honest exception is the safety net itself: starve the
	/// process hard enough and it will fire, which is why it is excluded from training data
	/// rather than counted.
	[Test]
	public void GameOutcomeIsUnchangedByCpuLoad()
	{
		var quiet = PlayGame();

		using var loaded = new CancellationTokenSource();
		var hogs = Enumerable
			.Range(0, Math.Max(1, Environment.ProcessorCount - 1))
			.Select(_ =>
			{
				var t = new Thread(() =>
				{
					while (!loaded.IsCancellationRequested) { }
				})
				{
					IsBackground = true,
					Priority = ThreadPriority.BelowNormal,
				};
				t.Start();
				return t;
			})
			.ToArray();

		GameResult underLoad;
		try
		{
			underLoad = PlayGame();
		}
		finally
		{
			loaded.Cancel();
			foreach (var t in hogs)
				t.Join();
		}

		Assert.That(
			underLoad.EndReason,
			Is.Not.EqualTo(GameEndReason.TimeLimitReached),
			"The safety net fired, so this run proves nothing about determinism — the load "
				+ "generator is starving the process rather than competing with it."
		);

		Assert.Multiple(() =>
		{
			Assert.That(
				underLoad.WinnerPlayerId,
				Is.EqualTo(quiet.WinnerPlayerId),
				"Winner changed under load"
			);
			Assert.That(
				underLoad.EndReason,
				Is.EqualTo(quiet.EndReason),
				"End reason changed under load"
			);
			Assert.That(
				underLoad.TurnCount,
				Is.EqualTo(quiet.TurnCount),
				"Turn count changed under load"
			);
			Assert.That(
				underLoad.TotalActions,
				Is.EqualTo(quiet.TotalActions),
				"Action count changed under load"
			);
		});
	}

	/// The wall clock survives only as a safety net for a genuine engine hang. A normal game
	/// must never be decided by it.
	[Test]
	public void NormalGameIsNotDecidedByTheClock()
	{
		Assert.That(PlayGame().EndReason, Is.Not.EqualTo(GameEndReason.TimeLimitReached));
	}

	/// The rollout budget bounds the search, so it must degrade gracefully rather than
	/// throw or stall when it is too small to finish a level.
	[Test]
	public void TinyRolloutBudgetStillPlaysALegalGame()
	{
		var result = PlayGame(rolloutBudget: 1);

		Assert.That(result.EndReason, Is.Not.EqualTo(GameEndReason.TimeLimitReached));
		Assert.That(result.TurnCount, Is.GreaterThan(0));
	}
}
