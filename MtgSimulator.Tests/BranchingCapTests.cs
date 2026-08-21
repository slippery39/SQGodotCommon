using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// The branching cap narrows a search level to the actions worth a rollout, using the cheap
/// evaluator to rank. Two properties have to hold, and they pull in opposite directions:
/// it must not change anything on an ordinary board, and it must not break on a huge one.
/// </summary>
[TestFixture]
public class BranchingCapTests
{
	private static (GameState State, MtgGameIds Ids) BoardOf(int creatureCount)
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();

		for (var i = 0; i < creatureCount; i++)
		{
			// Distinct P/T so dedup cannot collapse them — this test is about the cap, and
			// identical tokens would be removed before the cap ever saw them.
			var (mine, _) = state.AddObject(
				new Card
				{
					Name = $"Attacker {i}",
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
					Components = ImmutableArray.Create<GameComponent>(
						new PermanentComponent(),
						new CreatureComponent
						{
							Power = 1 + i,
							Toughness = 2 + i,
							HasSummoningSickness = false,
						}
					),
				},
				parentId: ids.Player1BattlefieldId
			);
			state = mine;

			var (theirs, _) = state.AddObject(
				new Card
				{
					Name = $"Blocker {i}",
					OwnerId = ids.Player2Id,
					ControllerId = ids.Player2Id,
					Components = ImmutableArray.Create<GameComponent>(
						new PermanentComponent(),
						new CreatureComponent { Power = 1 + i, Toughness = 3 + i }
					),
				},
				parentId: ids.Player2BattlefieldId
			);
			state = theirs;
		}

		return (state, ids);
	}

	private static GameAction Choose(
		GameState state,
		MtgGameIds ids,
		int maxBranching,
		int seed = 7
	) =>
		new MultiTurnBeamSearchAiStrategy(
			ids,
			currentTurnDepth: 2,
			lookaheadTurns: 1,
			rng: new Random(seed),
			maxBranching: maxBranching
		).SelectAction(state, ids, ids.Player1Id);

	/// Below the cap it must be a pass-through — same action, bit for bit, as no cap at all.
	/// This is what "99% of decisions pay nothing" means in practice.
	[Test]
	public void ABoardBelowTheCapIsUnaffected()
	{
		var (state, ids) = BoardOf(2);

		var capped = Choose(state, ids, MultiTurnBeamSearchAiStrategy.DefaultMaxBranching);
		var uncapped = Choose(state, ids, int.MaxValue);

		Assert.That(
			ActionDescriber.Describe(capped, state),
			Is.EqualTo(ActionDescriber.Describe(uncapped, state)),
			"The cap changed a decision on a board that never reached it"
		);
	}

	/// The ranking runs in Parallel.For, so a sort that depended on completion order would
	/// show up here as an intermittently different action.
	[Test]
	public void AWideBoardSearchesDeterministically()
	{
		var (state, ids) = BoardOf(12);

		var first = ActionDescriber.Describe(Choose(state, ids, 8), state);
		for (var i = 0; i < 5; i++)
			Assert.That(
				ActionDescriber.Describe(Choose(state, ids, 8), state),
				Is.EqualTo(first),
				"Capped search is not deterministic"
			);
	}

	/// Degrade, do not fall over: a cap of 1 leaves exactly one candidate per level.
	[Test]
	public void AnExtremeCapStillReturnsALegalAction()
	{
		var (state, ids) = BoardOf(12);
		var legal = MtgActionGenerator.GetLegalActions(state, ids, ids.Player1Id);

		var chosen = Choose(state, ids, 1);

		Assert.That(legal.Any(a => ReferenceEquals(a, chosen) || a.GetType() == chosen.GetType()));
	}
}
