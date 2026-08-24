using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// The search must still recognise a win after the terminal discount has decayed it.
///
/// This is the regression gate for a bug that shipped. Three places compared a ROLLOUT score
/// against StateEvaluator.WinScore (10000), but DiscountTerminal decays terminals by how many
/// half-turns they took, so a real win arrives as 9500 or 9025 and `>= 10000` is false for every
/// win the search will ever find. FindWinner silently returned null, both ResolveChoice early-outs
/// stopped firing, and the beam burned its whole rollout budget instead of stopping — while the AI
/// stopped taking a winning line the moment it found one.
///
/// **TerminalDiscountTests alone did NOT catch this, and that is the point of this fixture.**
/// Those tests prove StateEvaluator.IsWin classifies a discounted score correctly. They say
/// nothing about whether the search actually CALLS it — put `>= WinScore` back into FindWinner and
/// every one of them still passes. A helper being correct and a call site using it are two
/// different claims, and only the second one was broken.
/// </summary>
[TestFixture]
public class WinDetectionTests
{
	private static MultiTurnBeamSearchAiStrategy.BeamNode NodeScoring(float score) =>
		new(new GameState(), ImmutableList.Create<GameAction>(new EndTurnAction()), score);

	/// <summary>
	/// Reintroduce `>= StateEvaluator.WinScore` in FindWinner and THIS is the test that fails.
	/// Verified by doing exactly that before committing — a regression gate nobody has watched
	/// fail is a guess about what it covers.
	/// </summary>
	[Test]
	public void FindWinner_FindsAWinThatTheDiscountDecayed()
	{
		// Exactly the shape the bug produced: a genuine win, decayed below WinScore by the number
		// of half-turns it took to reach.
		for (var halfTurns = 0; halfTurns <= 20; halfTurns++)
		{
			var decayed = MultiTurnBeamSearchAiStrategy.DiscountTerminal(
				StateEvaluator.WinScore,
				halfTurns
			);
			var beam = new List<MultiTurnBeamSearchAiStrategy.BeamNode>
			{
				NodeScoring(12.5f),
				NodeScoring(decayed),
				NodeScoring(-3f),
			};

			var winner = MultiTurnBeamSearchAiStrategy.FindWinner(beam);

			Assert.That(
				winner,
				Is.Not.Null,
				$"a win decayed over {halfTurns} half-turns (score {decayed:F2}) must still be "
					+ "found; comparing against WinScore directly is the bug this pins"
			);
			Assert.That(winner!.ConcreteScore, Is.EqualTo(decayed));
		}
	}

	[Test]
	public void FindWinner_IgnoresAMerelyGoodBoard()
	{
		// The other half of the separation. If an ordinary board score could clear the threshold
		// the search would return early believing it had won a game still in progress, which is a
		// worse failure than the one above.
		var beam = new List<MultiTurnBeamSearchAiStrategy.BeamNode>
		{
			NodeScoring(150f),
			NodeScoring(88.25f),
			NodeScoring(-150f),
		};

		Assert.That(MultiTurnBeamSearchAiStrategy.FindWinner(beam), Is.Null);
	}

	[Test]
	public void FindWinner_IgnoresADecayedLoss()
	{
		// A loss decays TOWARD zero, so it climbs as it gets later. It must never climb far enough
		// to read as a win.
		for (var halfTurns = 0; halfTurns <= 20; halfTurns++)
		{
			var decayed = MultiTurnBeamSearchAiStrategy.DiscountTerminal(
				StateEvaluator.LossScore,
				halfTurns
			);

			Assert.That(
				MultiTurnBeamSearchAiStrategy.FindWinner([NodeScoring(decayed)]),
				Is.Null,
				$"a loss decayed over {halfTurns} half-turns scored {decayed:F2} and must not read "
					+ "as a win"
			);
		}
	}

	[Test]
	public void FindWinner_ReturnsNullOnAnEmptyBeam()
	{
		Assert.That(MultiTurnBeamSearchAiStrategy.FindWinner([]), Is.Null);
	}

	/// <summary>
	/// Decision capture is what makes the inspector work, and it costs a full ExecuteAction per
	/// displayed candidate to replay the position. Every SetLastDecision call site is guarded by
	/// captureDecisions for that reason — the simulator and the trainer must pay nothing.
	///
	/// Remove a guard and nothing fails; the trainer just gets quietly slower, which is exactly the
	/// kind of regression that gets blamed on the card pool three weeks later.
	/// </summary>
	[Test]
	public void WithCaptureOff_NoDecisionIsRecorded()
	{
		var (state, ids) = MtgGameFactory.Create();
		state = state.WithoutDeckingLoss();
		var strategy = new MultiTurnBeamSearchAiStrategy(
			ids,
			rng: new Random(1),
			captureDecisions: false
		);

		strategy.SelectAction(state, ids, ids.Player1Id);

		Assert.That(
			strategy.LastDecision,
			Is.Null,
			"capture must stay off on the simulator path — it replays every displayed candidate"
		);
	}

	/// <summary>
	/// Every capture path must carry <c>StateBefore</c> — including the forced-move fast path,
	/// which originally did not. The inspector renders the current position from it, so a decision
	/// without it makes the panel go blank, which reads as broken tooling rather than as "the AI
	/// had no choice here". Caught by writing this test, not in review.
	/// </summary>
	[Test]
	public void WithCaptureOn_EvenAForcedMoveCarriesTheStartingPosition()
	{
		var (state, ids) = MtgGameFactory.Create();
		state = state.WithoutDeckingLoss();
		var strategy = new MultiTurnBeamSearchAiStrategy(
			ids,
			rng: new Random(1),
			captureDecisions: true
		);

		// A bare board offers exactly one legal action, so this takes the fast path.
		strategy.SelectAction(state, ids, ids.Player1Id);

		Assert.Multiple(() =>
		{
			Assert.That(strategy.LastDecision, Is.Not.Null);
			Assert.That(strategy.LastDecision!.StateBefore, Is.Not.Null);
		});
	}

	[Test]
	public void WithCaptureOn_ARealDecisionCarriesPerCandidateBreakdowns()
	{
		var (state, ids) = MtgGameFactory.Create();
		state = state.WithoutDeckingLoss();

		// Castable cards in hand with the mana to pay for them, so there is more than one legal
		// action and the search goes through SetLastDecision rather than the forced-move path.
		// Creatures placed straight onto the battlefield would not do — they arrive summoning
		// sick, so EndTurn stays the only legal action and this silently retests the fast path.
		var p1 = state.GetPlayer(ids.Player1Id);
		state = state.UpdateObject(ids.Player1Id, p1 with { MaxMana = 2, CurrentMana = 2 });

		foreach (var name in new[] { "Bear", "Wolf" })
		{
			var card = new Card
			{
				Name = name,
				ManaCost = 1,
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
				Components = [new CreatureComponent { Power = 2, Toughness = 2 }],
			};
			(state, _) = state.AddObject(card, parentId: ids.Player1HandId);
		}

		var strategy = new MultiTurnBeamSearchAiStrategy(
			ids,
			rng: new Random(1),
			captureDecisions: true
		);
		strategy.SelectAction(state, ids, ids.Player1Id);

		var decision = strategy.LastDecision;

		Assert.Multiple(() =>
		{
			Assert.That(decision, Is.Not.Null);
			Assert.That(decision!.StateBefore, Is.Not.Null);
			Assert.That(decision.Candidates, Is.Not.Empty);
			Assert.That(
				decision.Candidates.Any(c => c.Breakdown != null),
				Is.True,
				"the inspector's delta column needs the position each candidate leads to"
			);
		});
	}
}
