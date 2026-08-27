using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// The Godot draft asset ships with its `Pairs` array emptied — 82 560 pairs is 11.5 MB against
/// 46.8 KB without them, and pair count is O(cards²) so it only gets worse.
///
/// That is only safe because `synergyWeight` defaults to 0, which is a *runtime* default sitting a
/// long way from the *build-time* decision to delete the data. This pins the connection: strip the
/// pairs and the picker must make identical picks, or the shipped game drafts differently from
/// everything that was measured.
///
/// **Inline data, not the generated files.** A test reading `sim_results/` passes or fails on
/// whether someone has run a training pass, and would not run in CI at all.
/// </summary>
[TestFixture]
public class StrippedModelTests
{
	private static DraftTrainingData Model()
	{
		var names = Enumerable.Range(0, 12).Select(i => $"C{i}").ToList();

		// Card rates spread across the range so picks are decidable rather than coin flips.
		var cards = names
			.Select((n, i) => new CardStat(n, Games: 100, Wins: 40 + i * 6, DeckGames: 220))
			.ToList();

		// Every pair, with lopsided rates — if pairs ever reached the score this would show.
		var pairs = new List<PairStat>();
		for (var a = 0; a < names.Count; a++)
		for (var b = a + 1; b < names.Count; b++)
			pairs.Add(
				new PairStat(
					names[a],
					names[b],
					Games: 90,
					Wins: (a * 7 + b * 3) % 90,
					DeckGames: 200
				)
			);

		return new DraftTrainingData(
			Perspectives: 12000,
			Wins: 6000,
			Cards: [.. cards],
			Pairs: [.. pairs]
		);
	}

	private static IReadOnlyList<Card> Offer(int seed) =>
		[
			.. Enumerable
				.Range(0, 12)
				.OrderBy(i => (i * 31 + seed) % 12)
				.Take(5)
				.Select(i => new Card { Name = $"C{i}", ManaCost = 1 + i % 5 }),
		];

	[Test]
	public void StrippingPairs_DoesNotChangeAnyPick_AtTheDefaultSynergyWeight()
	{
		var full = Model();
		var stripped = full with { Pairs = [] };

		for (var seed = 0; seed < 50; seed++)
		{
			// Same RNG seed per arm, so a difference is the data and not the softmax sample.
			var withPairs = DraftPickers.Trained(full, new Random(seed));
			var withoutPairs = DraftPickers.Trained(stripped, new Random(seed));

			var offer = Offer(seed);
			var pool = Offer(seed + 100);

			Assert.That(
				withoutPairs(offer, pool),
				Is.EqualTo(withPairs(offer, pool)),
				$"seed {seed}: the shipped asset must draft identically to the full model"
			);
		}
	}

	/// <summary>
	/// The guard on the guard. If pairs genuinely could not affect a pick, the test above would be
	/// vacuous — so raise the weight and confirm the two arms DO diverge. Verifying a regression
	/// gate by reintroducing the condition it protects against is a rule this project arrived at
	/// the hard way.
	/// </summary>
	[Test]
	public void AtANonZeroSynergyWeight_TheStrippedModelDoesDiverge()
	{
		var full = Model();
		var stripped = full with { Pairs = [] };

		var diverged = Enumerable
			.Range(0, 50)
			.Count(seed =>
				DraftPickers.Trained(stripped, new Random(seed), synergyWeight: 8.0)(
					Offer(seed),
					Offer(seed + 100)
				)
				!= DraftPickers.Trained(full, new Random(seed), synergyWeight: 8.0)(
					Offer(seed),
					Offer(seed + 100)
				)
			);

		Assert.That(
			diverged,
			Is.GreaterThan(0),
			"pairs must be capable of changing a pick, or the zero-weight test proves nothing"
		);
	}
}
