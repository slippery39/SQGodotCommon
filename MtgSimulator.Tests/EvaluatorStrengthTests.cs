using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// Head-to-head strength measurements. [Explicit] — each plays hundreds of full games.
///
/// **Run <see cref="HarnessCanSeeADifference"/> first, always.** A comparison harness that cannot
/// distinguish a crippled AI from a healthy one returns ~50% for everything, and a 50% result is
/// indistinguishable from "no effect" — which is exactly the answer most of these tests are
/// looking for. This project has already shipped a comparison that silently measured nothing
/// (raising maxBranching while expandBranching stayed capped, see BranchingCapStrengthTests).
/// </summary>
[TestFixture]
[Explicit("Slow — measurement harness, run on demand")]
public class EvaluatorStrengthTests
{
	/// <summary>
	/// The self-check. Branching 1 is a genuinely crippled search and must lose clearly.
	///
	/// Deliberately short (8 drafts, ~224 games): this is asking "can the harness see anything at
	/// all", which is a large effect, not "how much". The reference from the existing branching
	/// work is ~41%.
	/// </summary>
	[Test]
	public void HarnessCanSeeADifference()
	{
		var outcome = StrengthHarness.Measure(
			StrengthHarness.Crippled(),
			StrengthHarness.Default(),
			drafts: 8
		);
		TestContext.Out.WriteLine(outcome);

		Assert.That(
			outcome.Rate,
			Is.LessThan(45.0),
			"a branching-1 search must lose clearly. If this is near 50 the harness measures "
				+ "nothing and every other result in this file is noise."
		);
	}

	/// <summary>
	/// Self vs self. Must land on even — anything else means the harness has a bias, most likely
	/// in who is on the play, and would show up as a fake effect in every other comparison.
	/// </summary>
	[Test]
	public void DefaultAgainstItself_IsEven()
	{
		var outcome = StrengthHarness.Measure(
			StrengthHarness.Default("A"),
			StrengthHarness.Default("B"),
			drafts: 8
		);
		TestContext.Out.WriteLine(outcome);

		Assert.That(
			outcome.Rate,
			Is.EqualTo(50.0).Within(2 * outcome.StandardError),
			"identical configurations must draw even; a skew here is harness bias"
		);
	}

	/// <summary>
	/// Does the terminal discount actually help?
	///
	/// It shipped on reasoning — decaying terminals by how long they took makes a faster win and a
	/// later loss rank higher, and the arithmetic for that is sound. It was never played against
	/// its own absence. The effect should be small: it only fires when a terminal lands inside the
	/// 2-turn lookahead, so most decisions never touch it.
	///
	/// **A neutral result here is a perfectly good outcome and not a reason to revert.** The
	/// discount also fixed a real defect — every losing line collapsing to one number, leaving the
	/// search no gradient at the moment it most needs one — and correctness that does not show up
	/// in a win rate is still correctness. What would be worth acting on is a clear LOSS.
	/// </summary>
	[Test]
	public void TerminalDiscount_DoesNotCostStrength()
	{
		var outcome = StrengthHarness.Measure(
			StrengthHarness.Default("discount"),
			StrengthHarness.NoTerminalDiscount()
		);
		TestContext.Out.WriteLine(outcome);

		Assert.That(
			outcome.Rate,
			Is.GreaterThan(50.0 - 2 * outcome.StandardError),
			"the discount changes which action is returned; it must at least not lose strength"
		);
	}

	/// <summary>
	/// Does preferring the FASTEST win beat taking the first one found?
	///
	/// This is the change most likely to matter of the three shipped this session: it alters the
	/// returned action in precisely the positions that decide games. It was found by watching the
	/// AI bolt its own face — still a win in the rollout, since the board closed anyway, and it
	/// came first in beam order.
	///
	/// "Strictly better in principle" was said about several things this session that were not, so
	/// it gets played against its own absence like everything else.
	/// </summary>
	[Test]
	public void PreferringTheFastestWin_DoesNotCostStrength()
	{
		var outcome = StrengthHarness.Measure(
			StrengthHarness.Default("fastest-win"),
			StrengthHarness.FirstWinNotBestWin()
		);
		TestContext.Out.WriteLine(outcome);

		Assert.That(
			outcome.Rate,
			Is.GreaterThan(50.0 - 2 * outcome.StandardError),
			"taking a faster win must not lose strength"
		);
	}

	/// <summary>
	/// B3 — does scoring toughness help?
	///
	/// StateEvaluator has never had a toughness term: a creature is worth CreatureCountWeight plus
	/// TotalPowerWeight * power, so a 5/1 and a 5/5 are the same creature to it, including as
	/// removal targets where with three damage in hand one is killable and the other is not.
	///
	/// Toughness matters here despite there being no blocking, because AttackAction can target
	/// creatures as well as players — toughness decides whether a creature survives being attacked.
	/// Churchill's Attack-Value script targets by power/toughness and that ordering is provably
	/// optimal in 1-vs-n attrition.
	///
	/// Swept rather than set: Forge's ratio suggests ~1.33 against TotalPowerWeight 2.0, but that
	/// is a different game with blocking, and this project has been wrong about transferred
	/// intuitions before. A weight is a number to measure, not a number to reason about.
	/// </summary>
	[TestCase(0.5f)]
	[TestCase(1.33f)]
	public void ToughnessTerm_Sweep(float weight)
	{
		var outcome = StrengthHarness.Measure(
			StrengthHarness.Toughness(weight),
			StrengthHarness.Default("no-toughness")
		);
		TestContext.Out.WriteLine(outcome);

		// Reported, not asserted on a threshold. The question is "how much", and a sweep that
		// fails the build at its first losing value tells you nothing about the shape of the curve.
		Assert.That(outcome.Decided, Is.GreaterThan(900), "too many draws to read this");
	}

	/// <summary>
	/// Card-value scoring in ResolveChoice. See `DiscardQualityTests` for what it fixes: the rollout
	/// is provably blind to a card it cannot reach, so two discard options score identically and the
	/// choice falls through to a tiebreak.
	///
	/// **Read the population before reading the number.** `ChoiceCensus` measures 2.33 choice
	/// resolutions per game, so this touches a thin slice of a 1120-game head-to-head and a neutral
	/// result is the expected outcome even if the change is correct — the same reason the
	/// half-applied-action fix measured 50.2%. A clear LOSS is the informative result.
	///
	/// Unlike the evaluator version this replaced, it cannot reach a land drop or an attack:
	/// ResolveChoice is only called when the AI is answering a choice.
	/// </summary>
	[TestCase(0.1f)]
	[TestCase(0.5f)]
	[TestCase(2.0f)]
	public void CardValueChoiceScoring_Sweep(float weight)
	{
		// Fail in a second rather than after four minutes of measuring nothing. TryLoad returns
		// null when sim_results/card_values_csc.json is absent — run CardValueSweep first, in the
		// SAME configuration, since the path is relative to the test host's working directory.
		Assert.That(
			CardValueTable.TryLoad(CoresetCube.Set.Code, weight),
			Is.Not.Null,
			"no sandbox file — this arm would be identical to the baseline and the run would "
				+ "report a meaningless 50%"
		);

		var outcome = StrengthHarness.Measure(
			StrengthHarness.CardValues(weight),
			StrengthHarness.Default("no-card-values")
		);
		TestContext.Out.WriteLine(outcome);
		Assert.That(outcome.Decided, Is.GreaterThan(900), "too many draws to read this");
	}

	/// <summary>
	/// The keyword term, which is live in production at 2/15 and has never been measured.
	///
	/// It shipped because the evaluator scored a 4/4 flier exactly like a 4/4 vanilla — which also
	/// meant EQUIPPING scored nothing, since equipment grants keywords rather than power. The
	/// reasoning is sound and so was the reasoning behind four other changes this project shipped
	/// and then measured at neutral.
	///
	/// The per-keyword values are Forge's, and **Forge is a game with blocking**. Flying is
	/// offensive evasion there; here it is a defensive attack restriction. That transfer is the
	/// most likely thing to be wrong, and it is why a losing result is informative rather than
	/// merely disappointing.
	///
	/// A neutral result is fine and is not grounds to revert — see the discount and fastest-win
	/// tests above. A clear LOSS means defaulting KeywordWeight to 0 like ToughnessWeight and
	/// keeping the knob, which is the same call already made twice in this project.
	/// </summary>
	[Test]
	public void KeywordTerm_DoesNotCostStrength()
	{
		var outcome = StrengthHarness.Measure(
			StrengthHarness.Default("keywords-on"),
			StrengthHarness.Keywords(0f)
		);
		TestContext.Out.WriteLine(outcome);

		Assert.That(
			outcome.Rate,
			Is.GreaterThan(50.0 - 2 * outcome.StandardError),
			"the keyword term is on by default and changes which action is returned; "
				+ "it must at least not lose strength"
		);
	}

	/// <summary>
	/// What the keyword term COSTS. It adds a <c>GetEffectiveStats</c> allocation per creature per
	/// evaluation, on the most-called function in the engine, gated only on the weight being
	/// non-zero — and that gate is currently open.
	///
	/// Two mirror matches rather than a worktree at the pre-keyword commit: both arms identical
	/// within a run, so the run time is purely the cost of that configuration, and
	/// <c>Keywords(0f)</c> is behaviourally identical to the commit before the term existed. Same
	/// binary, so a stale <c>bin/</c> cannot measure code that was never compiled in.
	///
	/// **Read actions/game beside the clock, and do not skip this step.** If actions are equal and
	/// time is up, the gap is the allocation and it is real. If actions MOVED, the two arms are not
	/// doing the same work and the clock is not comparing what you think it is — the search changed
	/// its own workload, which is a strength question, not a cost one. That distinction has been
	/// missed three times in this project and cost a duplicated evaluator the first time.
	/// </summary>
	[Test]
	public void KeywordTerm_Cost()
	{
		// Mirrors, so each number is one configuration playing itself. Identical seeds across all
		// four runs, so the schedule and the decks are the same and only the evaluator differs.
		//
		// Run ABBA, not AB. The first measurement of this pair ran ON first and reported +10.7%,
		// which is the direction first-run JIT warmup pushes it — so the ordering was doing some
		// unknown share of the work. Same principle the harness already applies to its two arms
		// (interleave, never block), one level up.
		StrengthHarness.Outcome Mirror(string tag, float weight) =>
			StrengthHarness.Measure(
				StrengthHarness.Keywords(weight) with
				{
					Name = $"{tag}-A",
				},
				StrengthHarness.Keywords(weight) with
				{
					Name = $"{tag}-B",
				},
				drafts: 12
			);

		var on1 = Mirror("on1", WeightedStateEvaluator.Default.KeywordWeight);
		var off1 = Mirror("off1", 0f);
		var off2 = Mirror("off2", 0f);
		var on2 = Mirror("on2", WeightedStateEvaluator.Default.KeywordWeight);

		var onSeconds = on1.Seconds + on2.Seconds;
		var offSeconds = off1.Seconds + off2.Seconds;

		foreach (var o in new[] { on1, off1, off2, on2 })
			TestContext.Out.WriteLine(o);

		TestContext.Out.WriteLine(
			$"delta: {100.0 * (onSeconds - offSeconds) / offSeconds:F1}% time, "
				+ $"{100.0 * (on1.AvgActions - off1.AvgActions) / off1.AvgActions:F1}% actions"
		);

		// Reported, not thresholded. A wall-clock assertion in a test suite is a flaky test, and
		// this project has a standing rule that wall-clock must never decide anything.
		Assert.That(
			new[] { off1.Games, off2.Games, on2.Games },
			Is.All.EqualTo(on1.Games),
			"all four runs must play the same schedule"
		);
	}

	/// <summary>
	/// Does fully resolving an action before scoring it actually make the AI stronger?
	///
	/// This is the biggest behaviour change of the session: ExecuteAction is called from every
	/// ranking site in the search, so draining a raised choice there changes both what the AI sees
	/// and what it costs. Unlike the evaluator experiments, this one fixes a case where the search
	/// was scoring a position that did not exist — the turn had not ended, the triggers had not
	/// fired, and the rollout then double-counted them.
	/// </summary>
	[Test]
	public void ResolvingChoicesBeforeScoring_DoesNotCostStrength()
	{
		var outcome = StrengthHarness.Measure(
			StrengthHarness.Default("resolved"),
			StrengthHarness.UnresolvedChoices()
		);
		TestContext.Out.WriteLine(outcome);

		Assert.That(
			outcome.Rate,
			Is.GreaterThan(50.0 - 2 * outcome.StandardError),
			"scoring fully-applied positions must not lose strength"
		);
	}
}
