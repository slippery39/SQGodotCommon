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
}
