using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// A rollout that reaches a win or a loss breaks out of MultiTurnGreedyRollout early, so unlike
/// every other rollout it does NOT describe the state at the full lookahead horizon. Returning the
/// raw +/-WinScore therefore made two lines that end at different times score identically: winning
/// next half-turn and winning in two both read 10000, and dying next half-turn and dying in two
/// both read -10000.
///
/// The losing half is the damaging one. Every line that ends in death inside the lookahead window
/// collapsed to a single number, so the search had no gradient to rank them by and fell through to
/// its tiebreak — it stopped playing for the extra draw step at precisely the point where that is
/// the only thing left to play for.
///
/// TESTED AT THE FUNCTION, NOT THROUGH A GAME. Reaching a terminal inside a 2-turn rollout needs a
/// board that is already lethal, and the beam would then be choosing between lines that mostly win
/// anyway — so a game-level assertion passes with or without the discount, which is worse than no
/// test. What is worth pinning is the ordering itself.
/// </summary>
[TestFixture]
public class TerminalDiscountTests
{
	[Test]
	public void AFasterWin_OutscoresASlowerOne()
	{
		var winNow = MultiTurnBeamSearchAiStrategy.DiscountTerminal(StateEvaluator.WinScore, 1);
		var winLater = MultiTurnBeamSearchAiStrategy.DiscountTerminal(StateEvaluator.WinScore, 2);

		Assert.Multiple(() =>
		{
			Assert.That(
				winNow,
				Is.GreaterThan(winLater),
				"a win one half-turn sooner must rank higher"
			);
			Assert.That(winNow, Is.Positive, "a win must stay a win after discounting");
			Assert.That(winLater, Is.Positive);
		});
	}

	[Test]
	public void ALaterLoss_OutscoresAnEarlierOne()
	{
		var dieNow = MultiTurnBeamSearchAiStrategy.DiscountTerminal(StateEvaluator.LossScore, 1);
		var dieLater = MultiTurnBeamSearchAiStrategy.DiscountTerminal(StateEvaluator.LossScore, 2);

		// The whole point: a loss is negative, so decaying it moves it TOWARD zero. Delaying death
		// buys a draw step, and that now falls out of the arithmetic instead of needing a rule.
		Assert.Multiple(() =>
		{
			Assert.That(
				dieLater,
				Is.GreaterThan(dieNow),
				"dying one half-turn later must rank higher"
			);
			Assert.That(dieLater, Is.Negative, "a loss must stay a loss after discounting");
			Assert.That(dieNow, Is.Negative);
		});
	}

	[Test]
	public void ANonTerminalScore_IsNotTouched()
	{
		// Non-terminal rollouts all run the full lookahead, so they already share a horizon and
		// there is nothing to correct. Discounting them would make board quality decay with depth.
		Assert.Multiple(() =>
		{
			Assert.That(
				MultiTurnBeamSearchAiStrategy.DiscountTerminal(12.5f, 2),
				Is.EqualTo(12.5f)
			);
			Assert.That(MultiTurnBeamSearchAiStrategy.DiscountTerminal(-40f, 5), Is.EqualTo(-40f));
			Assert.That(MultiTurnBeamSearchAiStrategy.DiscountTerminal(0f, 1), Is.EqualTo(0f));
		});
	}

	[Test]
	public void ADiscountedTerminal_StillDominatesAnyBoardScore()
	{
		// The margin the discount factor is chosen against. If a decayed win ever fell into the
		// range of ordinary board scores the search would start preferring a good board to a win,
		// which is the one way this change could do real damage. ~150 is a generous ceiling on the
		// current weights; the assertion holds far past the default 2-turn lookahead.
		const float boardScoreCeiling = 150f;

		var winAtTheDefaultHorizon = MultiTurnBeamSearchAiStrategy.DiscountTerminal(
			StateEvaluator.WinScore,
			2
		);
		var winAtAMuchDeeperHorizon = MultiTurnBeamSearchAiStrategy.DiscountTerminal(
			StateEvaluator.WinScore,
			20
		);

		Assert.Multiple(() =>
		{
			Assert.That(winAtTheDefaultHorizon, Is.GreaterThan(boardScoreCeiling));
			Assert.That(winAtAMuchDeeperHorizon, Is.GreaterThan(boardScoreCeiling));
		});
	}

	[Test]
	public void AnImmediateTerminal_IsUndiscounted()
	{
		Assert.Multiple(() =>
		{
			Assert.That(
				MultiTurnBeamSearchAiStrategy.DiscountTerminal(StateEvaluator.WinScore, 0),
				Is.EqualTo(StateEvaluator.WinScore)
			);
			Assert.That(
				MultiTurnBeamSearchAiStrategy.DiscountTerminal(StateEvaluator.LossScore, 0),
				Is.EqualTo(StateEvaluator.LossScore)
			);
		});
	}

	[Test]
	public void ADiscountedWin_IsStillRecognisedAsAWin()
	{
		// The regression this exists to prevent, and it shipped once. Every "did someone win?"
		// check in the search compared a ROLLOUT score against WinScore (10000) — but a discounted
		// win comes back as 9500 or 9025, so those comparisons became permanently false.
		// FindWinner stopped returning winners and both ResolveChoice early-outs stopped firing:
		// the search burned its full budget instead of stopping, and stopped taking a winning line
		// the moment it found one.
		for (var halfTurns = 0; halfTurns <= 20; halfTurns++)
		{
			var win = MultiTurnBeamSearchAiStrategy.DiscountTerminal(
				StateEvaluator.WinScore,
				halfTurns
			);
			var loss = MultiTurnBeamSearchAiStrategy.DiscountTerminal(
				StateEvaluator.LossScore,
				halfTurns
			);

			Assert.Multiple(() =>
			{
				Assert.That(
					StateEvaluator.IsWin(win),
					Is.True,
					$"a win discounted over {halfTurns} half-turns must still read as a win"
				);
				Assert.That(
					StateEvaluator.IsDecisive(loss),
					Is.True,
					$"a loss discounted over {halfTurns} half-turns must still read as decisive"
				);
				Assert.That(StateEvaluator.IsWin(loss), Is.False);
			});
		}
	}

	[Test]
	public void TheWinThreshold_SitsAboveAnyBoardScore()
	{
		// The other half of the separation: if an ordinary board score could clear the threshold,
		// the search would start believing it had won games it had not.
		const float boardScoreCeiling = 150f;

		Assert.Multiple(() =>
		{
			Assert.That(StateEvaluator.WinThreshold, Is.GreaterThan(boardScoreCeiling));
			Assert.That(StateEvaluator.IsWin(boardScoreCeiling), Is.False);
			Assert.That(StateEvaluator.IsWin(-boardScoreCeiling), Is.False);
			Assert.That(StateEvaluator.IsDecisive(boardScoreCeiling), Is.False);
		});
	}
}
