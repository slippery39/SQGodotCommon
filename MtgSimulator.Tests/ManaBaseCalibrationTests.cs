using MtgCore;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// Where <c>ManaBase.SourcesNeeded</c>'s numbers come from.
///
/// The table is MEASURED against this engine rather than lifted from paper Magic, and this is the
/// measurement. Explicit because it is a calibration rather than a guard — re-run it whenever the
/// opening-hand rule, the land-per-turn rule or the deck size changes, and update the table in
/// ManaBase if it moves.
///
/// **Paper tables do not transfer, which is the whole reason this exists.** The opening hand here
/// is guaranteed to hold exactly three lands (SetupGameAction.OpeningHandLandCount), sampled
/// uniformly from the manabase, so early colour access is far more reliable than a real seven-card
/// draw would give.
/// </summary>
[TestFixture]
public class ManaBaseCalibrationTests
{
	private const int DeckSize = 60;
	private const int Trials = 20000;
	private const double Target = 0.90;

	/// <summary>
	/// One game, on the play: three guaranteed lands, one draw per turn from turn two, one land
	/// drop per turn, and the target colour played first. Returns sources of that colour in play
	/// at the end of each turn.
	/// </summary>
	private static int[] Trial(int lands, int sources, int turns, Random rng)
	{
		var pool = Enumerable
			.Range(0, lands)
			.Select(i => i < sources ? 1 : 0)
			.OrderBy(_ => rng.Next())
			.ToList();

		var hand = pool.Take(3).ToList();
		var library = pool.Skip(3)
			.Select(l => (int?)l)
			.Concat(Enumerable.Repeat((int?)null, DeckSize - lands - 4))
			.OrderBy(_ => rng.Next())
			.ToList();

		var inPlay = 0;
		var next = 0;
		var result = new int[turns];

		for (var turn = 1; turn <= turns; turn++)
		{
			if (turn > 1 && next < library.Count)
			{
				var drawn = library[next++];
				if (drawn.HasValue)
					hand.Add(drawn.Value);
			}

			if (hand.Count > 0)
			{
				hand.Sort((a, b) => b.CompareTo(a)); // target colour first
				inPlay += hand[0];
				hand.RemoveAt(0);
			}

			result[turn - 1] = inPlay;
		}

		return result;
	}

	private static double Probability(int lands, int sources, int pips, int turn, int seed)
	{
		var rng = new Random(seed);
		var hits = 0;
		for (var i = 0; i < Trials; i++)
			if (Trial(lands, sources, turn, rng)[turn - 1] >= pips)
				hits++;
		return (double)hits / Trials;
	}

	/// <summary>
	/// The encoded table must still deliver the ~90% it claims. Tolerance is one source, because
	/// the table is integers and the measurement is stochastic.
	/// </summary>
	[Test, Explicit("Calibration — slow. Re-run when the opening-hand or land rules change.")]
	public void TheEncodedTable_StillDeliversNinetyPercentOnCurve()
	{
		int[][] expected =
		[
			[13, 12, 11, 10, 9, 9],
			[18, 18, 17, 17, 15, 15],
			[22, 22, 22, 22, 21, 20],
		];

		var report = new List<string>();
		var failures = new List<string>();

		for (var pips = 1; pips <= 3; pips++)
		for (var turn = Math.Max(pips, 1); turn <= 6; turn++)
		{
			var claimed = expected[pips - 1][turn - 1];
			var p = Probability(24, claimed, pips, turn, seed: 1000 * pips + turn);
			var oneFewer = Probability(24, claimed - 1, pips, turn, seed: 7000 + 100 * pips + turn);
			report.Add(
				$"pips {pips} turn {turn}: {claimed} sources -> {p:P0} ({claimed - 1} -> {oneFewer:P0})"
			);

			// Enough, and not wastefully more than enough.
			if (p < Target - 0.03)
				failures.Add($"pips {pips} turn {turn}: {claimed} sources only reaches {p:P0}");
			if (oneFewer >= Target + 0.03)
				failures.Add(
					$"pips {pips} turn {turn}: {claimed - 1} sources already reaches {oneFewer:P0}"
				);
		}

		TestContext.Out.WriteLine(string.Join(Environment.NewLine, report));
		Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
	}

	/// <summary>
	/// The finding that shapes the whole colour design: a two-colour deck supports single pips
	/// comfortably and cannot support double pips at all.
	/// </summary>
	[Test, Explicit("Calibration — slow.")]
	public void ATwoColourDeck_SupportsSinglePips_ButNotDoubleOnes()
	{
		var singleTurnOne = Probability(24, 12, pips: 1, turn: 1, seed: 11);
		var singleTurnThree = Probability(24, 12, pips: 1, turn: 3, seed: 12);
		var doubleTurnThree = Probability(24, 12, pips: 2, turn: 3, seed: 13);

		TestContext.Out.WriteLine(
			$"12 of 24 sources — 1 pip T1 {singleTurnOne:P0}, 1 pip T3 {singleTurnThree:P0}, "
				+ $"2 pips T3 {doubleTurnThree:P0}"
		);

		Assert.Multiple(() =>
		{
			Assert.That(singleTurnOne, Is.GreaterThan(0.85), "a single pip is nearly free");
			Assert.That(singleTurnThree, Is.GreaterThan(0.90));
			Assert.That(doubleTurnThree, Is.LessThan(0.70), "a double pip is effectively mono");
		});
	}
}
