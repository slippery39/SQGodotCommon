using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **The climb spent four of its accepted mutations undoing itself.**
///
/// Measured on one elf slot over 12 generations: `+2x Wirewood Symbiont −2x Fauna Shaman` accepted
/// at +6.1pp in generation 5, then the exact reverse accepted at +6.1pp in generation 6, and the
/// same pair again at generations 11 and 12. Both directions score as improvements because ±6pp is
/// inside the noise of a paired 6-game evaluation, so the search reads two coin flips as two gains
/// and finishes where it began. 18 of that slot's 27 proposals involved the same single card.
///
/// Same shape as the equip oscillation `MtgCore/CLAUDE.md` records — the evaluator rating both
/// directions of one move as better, with no tie to break.
///
/// The guard is exercised through <see cref="MetagameEvolver.Reverses"/> directly: reproducing it
/// through a run would need the same noisy accept twice in a row, which is exactly the coin flip
/// that makes the defect hard to see in the first place.
/// </summary>
[TestFixture]
public class ReversalGuardTests
{
	private static List<(int Gen, HashSet<string> Added, HashSet<string> Removed)> History(
		int gen,
		string[] added,
		string[] removed
	) =>
		[(gen, added.ToHashSet(StringComparer.Ordinal), removed.ToHashSet(StringComparer.Ordinal))];

	[Test]
	public void TheExactSwapBackIsRefused()
	{
		var history = History(5, ["Wirewood Symbiont"], ["Fauna Shaman"]);

		Assert.That(
			MetagameEvolver.Reverses(history, ("2x Fauna Shaman", "2x Wirewood Symbiont"), gen: 6),
			Is.True
		);
	}

	/// <summary>
	/// The measured oscillation left at three copies and came back at one. A count-sensitive
	/// comparison would call that a different mutation and let the flip-flop through.
	/// </summary>
	[Test]
	public void ADifferentNUMBEROfCopiesIsStillAReversal()
	{
		var history = History(11, ["Wirewood Symbiont"], ["Fauna Shaman"]);

		Assert.That(
			MetagameEvolver.Reverses(history, ("1x Fauna Shaman", "1x Wirewood Symbiont"), gen: 12),
			Is.True
		);
	}

	/// <summary>
	/// **The vacuity guard: this must not simply refuse everything.** A guard that blocked all
	/// mutations would also stop the oscillation, pass the tests above, and freeze every slot solid
	/// — which is the failure mode the pool-quota version of `DeckCore` already produced once.
	/// </summary>
	[Test]
	public void AnUnrelatedMutationIsNotBlocked()
	{
		var history = History(5, ["Wirewood Symbiont"], ["Fauna Shaman"]);

		Assert.Multiple(() =>
		{
			Assert.That(
				MetagameEvolver.Reverses(history, ("4x Elvish Archdruid", "4x Nissa"), gen: 6),
				Is.False,
				"a mutation touching neither card was blocked"
			);
			Assert.That(
				MetagameEvolver.Reverses(history, ("2x Timberwatch Elder", "2x Fauna Shaman"), 6),
				Is.False,
				"cutting the SAME card again is not a reversal — only putting it back is"
			);
		});
	}

	/// <summary>
	/// A window, never a ban. A swap wrong at generation 2 can be right at generation 8 once the
	/// shell has changed, and a permanent block would make the search unable to revisit a card as
	/// its deck evolves.
	/// </summary>
	[Test]
	public void OutsideTheMemoryWindowTheSearchMayChangeItsMind()
	{
		var history = History(1, ["Wirewood Symbiont"], ["Fauna Shaman"]);

		Assert.That(
			MetagameEvolver.Reverses(history, ("2x Fauna Shaman", "2x Wirewood Symbiont"), gen: 99),
			Is.False,
			"an old acceptance is still blocking a reversal — this is a window, not a ban"
		);
	}
}
