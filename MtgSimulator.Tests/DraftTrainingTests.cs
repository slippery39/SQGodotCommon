using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator.Tests;

[TestFixture]
public class DraftTrainingTests
{
	private static Card C(string name) => new() { Name = name };

	private static DraftTrainingData Data(
		int perspectives,
		int wins,
		CardStat[]? cards = null,
		PairStat[]? pairs = null
	) => new(perspectives, wins, cards ?? [], pairs ?? []);

	// ===== Shrinkage =====

	[Test]
	public void Shrink_TinySample_StaysNearPrior()
	{
		// A 2-for-2 pair must NOT read as 100%, or it dominates every pick it appears in.
		var shrunk = DraftTrainingData.Shrink(wins: 2, games: 2, prior: 0.5, k: 25);

		Assert.That(shrunk, Is.EqualTo(0.5).Within(0.05), "should sit next to the prior");
		Assert.That(shrunk, Is.LessThan(0.6), "and nowhere near the raw 100% observed rate");
	}

	[Test]
	public void Shrink_LargeSample_ApproachesObservedRate()
	{
		var shrunk = DraftTrainingData.Shrink(wins: 700, games: 1000, prior: 0.5, k: 25);

		Assert.That(shrunk, Is.EqualTo(0.7).Within(0.01));
	}

	[Test]
	public void Shrink_NoGames_ReturnsPrior()
	{
		Assert.That(DraftTrainingData.Shrink(0, 0, prior: 0.48, k: 25), Is.EqualTo(0.48));
	}

	// ===== Expected pair rate (the synergy baseline) =====

	[Test]
	public void ExpectedPairRate_TwoAverageCards_IsThePrior()
	{
		var expected = DraftTrainingData.ExpectedPairRate(0.5, 0.5, prior: 0.5);

		Assert.That(expected, Is.EqualTo(0.5).Within(1e-9));
	}

	[Test]
	public void ExpectedPairRate_OneStrongCard_RaisesTheBaseline()
	{
		// A 65% card paired with an average one should be expected to win well above the
		// prior on its own merits — that expectation is what synergy is measured against.
		var expected = DraftTrainingData.ExpectedPairRate(0.65, 0.5, prior: 0.5);

		Assert.That(expected, Is.EqualTo(0.65).Within(1e-9));
	}

	[Test]
	public void ExpectedPairRate_TwoStrongCards_CompoundsAboveEither()
	{
		var expected = DraftTrainingData.ExpectedPairRate(0.60, 0.60, prior: 0.5);

		Assert.That(expected, Is.GreaterThan(0.60));
		Assert.That(expected, Is.LessThan(1.0));
	}

	[Test]
	public void ExpectedPairRate_ExtremeRates_StaysFinite()
	{
		var expected = DraftTrainingData.ExpectedPairRate(1.0, 0.0, prior: 0.5);

		Assert.That(double.IsFinite(expected), Is.True);
		Assert.That(expected, Is.InRange(0.0, 1.0));
	}

	// ===== Merge / persistence =====

	[Test]
	public void Merge_SumsCountsForSharedCardsAndPairs()
	{
		var a = Data(
			10,
			5,
			[new CardStat("Bolt", 4, 3, 8)],
			[new PairStat("Bolt", "Goyf", 2, 2, 6)]
		);
		var b = Data(
			20,
			11,
			[new CardStat("Bolt", 6, 1, 12), new CardStat("Goyf", 3, 2, 9)],
			[new PairStat("Bolt", "Goyf", 5, 1, 14)]
		);

		var merged = DraftTrainingData.Merge(a, b);

		Assert.That(merged.Perspectives, Is.EqualTo(30));
		Assert.That(merged.Wins, Is.EqualTo(16));
		Assert.That(merged.Cards.Single(c => c.Name == "Bolt").Games, Is.EqualTo(10));
		Assert.That(merged.Cards.Single(c => c.Name == "Bolt").Wins, Is.EqualTo(4));
		Assert.That(
			merged.Cards.Single(c => c.Name == "Bolt").DeckGames,
			Is.EqualTo(20),
			"deck-presence counts must merge too, or draw probabilities drift on every merge"
		);
		Assert.That(merged.Cards, Has.Count.EqualTo(2));
		Assert.That(merged.Pairs.Single().Games, Is.EqualTo(7));
		Assert.That(merged.Pairs.Single().Wins, Is.EqualTo(3));
		Assert.That(merged.Pairs.Single().DeckGames, Is.EqualTo(20));
	}

	[Test]
	public void SaveAndLoad_RoundTripsCounts()
	{
		var path = Path.Combine(Path.GetTempPath(), $"draft_training_{Guid.NewGuid():N}.json");
		var data = Data(
			100,
			50,
			[new CardStat("Bolt", 40, 30)],
			[new PairStat("Bolt", "Goyf", 12, 9)]
		);

		try
		{
			DraftTrainingStore.Save(data, path);
			var loaded = DraftTrainingStore.Load(path);

			Assert.That(loaded, Is.Not.Null);
			Assert.That(loaded!.Perspectives, Is.EqualTo(100));
			Assert.That(loaded.Prior, Is.EqualTo(0.5));
			Assert.That(loaded.Cards.Single().Wins, Is.EqualTo(30));
			Assert.That(loaded.Pairs.Single().A, Is.EqualTo("Bolt"));
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Test]
	public void Load_MissingFile_ReturnsNullSoCallersFallBack()
	{
		var path = Path.Combine(Path.GetTempPath(), $"absent_{Guid.NewGuid():N}.json");

		Assert.That(DraftTrainingStore.Load(path), Is.Null);
	}

	// ===== Softmax =====

	[Test]
	public void SampleSoftmax_ZeroTemperature_AlwaysPicksBest()
	{
		var scores = new[] { 1.0, 9.0, 3.0 };
		var rng = new Random(1);

		for (var i = 0; i < 20; i++)
			Assert.That(DraftPickers.SampleSoftmax(scores, 0, rng), Is.EqualTo(1));
	}

	[Test]
	public void SampleSoftmax_LowTemperature_StronglyFavoursBest()
	{
		var scores = new[] { 0.0, 6.0 };
		var rng = new Random(7);

		var bestCount = Enumerable
			.Range(0, 1000)
			.Count(_ => DraftPickers.SampleSoftmax(scores, 1.0, rng) == 1);

		// exp(6) : exp(0) is ~403:1, so the top card should win almost always but the
		// sampler must still be capable of choosing the other one.
		Assert.That(bestCount, Is.GreaterThan(980));
	}

	[Test]
	public void SampleSoftmax_HighTemperature_SpreadsPicks()
	{
		var scores = new[] { 0.0, 6.0 };
		var rng = new Random(7);

		var bestCount = Enumerable
			.Range(0, 1000)
			.Count(_ => DraftPickers.SampleSoftmax(scores, 100.0, rng) == 1);

		Assert.That(bestCount, Is.InRange(400, 600));
	}

	[Test]
	public void SampleSoftmax_AllScoresEqual_UsesEveryOption()
	{
		var scores = new[] { 2.0, 2.0, 2.0 };
		var rng = new Random(3);

		var picked = Enumerable
			.Range(0, 300)
			.Select(_ => DraftPickers.SampleSoftmax(scores, 2.0, rng))
			.ToHashSet();

		Assert.That(picked, Is.EquivalentTo(new[] { 0, 1, 2 }));
	}

	// ===== Trained picker =====

	[Test]
	public void Trained_ZeroTemperature_PicksHighestWinRateCard()
	{
		var data = Data(1000, 500, [new CardStat("Good", 500, 400), new CardStat("Bad", 500, 100)]);
		var picker = DraftPickers.Trained(data, new Random(1), temperature: 0);

		Assert.That(picker([C("Bad"), C("Good")], []), Is.EqualTo(1));
		Assert.That(picker([C("Good"), C("Bad")], []), Is.EqualTo(0));
	}

	[Test]
	public void Trained_SynergyFlipsThePickWhenPoolMatches()
	{
		// Both cards are equally good alone; "Partner" only wins alongside "Owned".
		var data = Data(
			1000,
			500,
			[new CardStat("Partner", 500, 250), new CardStat("Loner", 500, 250)],
			[new PairStat("Owned", "Partner", 400, 380)]
		);
		var picker = DraftPickers.Trained(data, new Random(1), temperature: 0, synergyWeight: 1);

		Assert.That(
			picker([C("Partner"), C("Loner")], []),
			Is.EqualTo(0),
			"With an empty pool the two cards tie, so the first wins."
		);
		Assert.That(
			picker([C("Loner"), C("Partner")], [C("Owned")]),
			Is.EqualTo(1),
			"Holding Owned should pull the pick onto Partner."
		);
	}

	[Test]
	public void Trained_StrongCardDoesNotManufactureFakeSynergy()
	{
		// THE regression test. "Bomb" wins 70% with everything. Its pair with "Filler" posts
		// exactly what Bomb alone predicts, so there is NO interaction and synergy must be ~0.
		// Measuring that pair against the global prior instead (the old behaviour) scores it
		// at roughly +20 points and makes every pair containing Bomb look synergistic.
		//
		// "Rival" is given a deliberate half-point edge on raw card quality. Correct code
		// takes Rival; anything inflating Filler's synergy takes Filler by a mile.
		var bombRate = DraftTrainingData.Shrink(3500, 5000, 0.5, 25);
		var fillerRate = DraftTrainingData.Shrink(2500, 5000, 0.5, 25);
		var expected = DraftTrainingData.ExpectedPairRate(bombRate, fillerRate, 0.5);
		var pairWins = (int)Math.Round(1000 * expected);

		var data = Data(
			10_000,
			5_000,
			[
				new CardStat("Bomb", 5000, 3500), // 70%
				new CardStat("Filler", 5000, 2500), // 50.0%
				new CardStat("Rival", 5000, 2525), // 50.5% — the half-point edge
			],
			[new PairStat("Bomb", "Filler", 1000, pairWins)]
		);
		var picker = DraftPickers.Trained(data, new Random(1), temperature: 0, synergyWeight: 1);

		Assert.That(
			picker([C("Filler"), C("Rival")], [C("Bomb")]),
			Is.EqualTo(1),
			"Rival's real half-point edge must beat Filler's non-existent synergy."
		);
		Assert.That(
			picker([C("Rival"), C("Filler")], [C("Bomb")]),
			Is.EqualTo(0),
			"Same verdict with the offer order reversed."
		);
	}

	[Test]
	public void Trained_RealSynergyBeatsTheIndependenceBaseline()
	{
		// Same setup, but now the pair genuinely overperforms what Bomb alone predicts.
		var expected = DraftTrainingData.ExpectedPairRate(0.70, 0.50, prior: 0.5);
		var overperforming = (int)Math.Round(1000 * Math.Min(0.99, expected + 0.15));
		var data = Data(
			10_000,
			5_000,
			[
				new CardStat("Bomb", 5000, 3500),
				new CardStat("Filler", 5000, 2500),
				new CardStat("Rival", 5000, 2500),
			],
			[new PairStat("Bomb", "Filler", 1000, overperforming)]
		);
		var picker = DraftPickers.Trained(data, new Random(1), temperature: 0, synergyWeight: 1);

		Assert.That(
			picker([C("Rival"), C("Filler")], [C("Bomb")]),
			Is.EqualTo(1),
			"Filler should be taken over the identical Rival because of real synergy."
		);
	}

	[Test]
	public void Trained_ThinPairEvidenceCountsForFarLessThanThickEvidence()
	{
		// Both candidates pair with "Owned" at the same observed 90% rate, but one has 1000
		// games behind it and the other only 2. Shrinkage must weight by sample size, so the
		// well-evidenced pair wins. Without it, two lucky games would rank as high as a
		// thousand and every draft would chase noise.
		var data = Data(
			10_000,
			5_000,
			[
				new CardStat("Owned", 5000, 3250),
				new CardStat("ThickPair", 5000, 2500),
				new CardStat("ThinPair", 5000, 2500),
			],
			[new PairStat("Owned", "ThickPair", 1000, 900), new PairStat("Owned", "ThinPair", 2, 2)]
		);
		var picker = DraftPickers.Trained(data, new Random(1), temperature: 0, synergyWeight: 1);

		Assert.That(picker([C("ThinPair"), C("ThickPair")], [C("Owned")]), Is.EqualTo(1));
		Assert.That(picker([C("ThickPair"), C("ThinPair")], [C("Owned")]), Is.EqualTo(0));
	}

	[Test]
	public void Trained_UnknownCard_ScoresAsAverageRatherThanBeingIgnored()
	{
		var data = Data(1000, 500, [new CardStat("Known", 500, 100)]);
		var picker = DraftPickers.Trained(data, new Random(1), temperature: 0);

		// "Unknown" has no data (delta 0); "Known" is measurably bad (delta < 0).
		Assert.That(picker([C("Known"), C("Unknown")], []), Is.EqualTo(1));
	}

	[Test]
	public void Trained_EmptyData_DoesNotCrashAndStaysInRange()
	{
		var picker = DraftPickers.Trained(DraftTrainingData.Empty, new Random(1));

		var pick = picker([C("A"), C("B"), C("C")], []);

		Assert.That(pick, Is.InRange(0, 2));
	}

	[Test]
	public void PairDrawRatio_MeasuresHowMuchRarerAPairPayoffIs()
	{
		// Cards drawn 40% of the games they are decked, pairs only 20% — so a synergy is
		// collected half as often as a single card's effect and must be discounted 0.5x.
		var data = Data(
			1000,
			500,
			[new CardStat("A", 400, 200, 1000), new CardStat("B", 400, 200, 1000)],
			[new PairStat("A", "B", 200, 100, 1000)]
		);

		Assert.That(DraftPickers.PairDrawRatio(data), Is.EqualTo(0.5).Within(1e-9));
	}

	[Test]
	public void PairDrawRatio_LegacyDataWithoutDeckGames_FallsBackToOne()
	{
		// Files written before DeckGames existed must still load and behave as they did.
		var data = Data(
			1000,
			500,
			[new CardStat("A", 400, 200)],
			[new PairStat("A", "B", 200, 100)]
		);

		Assert.That(DraftPickers.PairDrawRatio(data), Is.EqualTo(1.0));
	}

	[Test]
	public void Trained_CardTermIsNotRescaled_SoTemperatureStaysCalibrated()
	{
		// The draw discount applies to synergy only. Scaling the card term as well would
		// compress the whole score range and make a fixed temperature behave far more
		// randomly — a bigger effect than the correction itself. Pin the raw scale: a card
		// 20 points above the prior must score ~+20, not ~+9.
		var data = Data(
			10_000,
			5_000,
			[new CardStat("Strong", 5000, 3500, 12_000)], // 70% when drawn, drawn ~42%
			[]
		);
		var picker = DraftPickers.Trained(data, new Random(1), temperature: 0);

		// A 70%-vs-50% card beats an unknown (0-delta) card even at a temperature that would
		// wash out a compressed score. Sanity-check the ratio helper stayed out of the card term.
		Assert.That(picker([C("Unknown"), C("Strong")], []), Is.EqualTo(1));

		var warm = DraftPickers.Trained(data, new Random(1), temperature: 2.0);
		var strongPicks = Enumerable
			.Range(0, 500)
			.Count(_ => warm([C("Unknown"), C("Strong")], []) == 1);
		Assert.That(
			strongPicks,
			Is.GreaterThan(490),
			"a ~20 point edge at temperature 2 should be near-deterministic"
		);
	}

	[Test]
	public void PairKey_IsOrderIndependent()
	{
		Assert.That(
			DraftPickers.PairKey("Zed", "Alpha"),
			Is.EqualTo(DraftPickers.PairKey("Alpha", "Zed"))
		);
	}

	// ===== End-to-end: a trained picker drives a real draft =====

	[Test]
	public void Trained_DrivesAFullDraftToCompletion()
	{
		var pool = Enumerable.Range(0, 40).Select(i => C($"Card{i}")).ToList();
		var data = Data(100, 50, pool.Select(c => new CardStat(c.Name, 50, 25)).ToArray());
		var state = Draft.Create(DraftFormat.Booster, pool, seed: 5, seatCount: 4, packSize: 5);
		var pickers = Enumerable
			.Range(0, 4)
			.Select(i => DraftPickers.Trained(data, new Random(i)))
			.ToList();

		var final = Draft.RunToCompletion(state, pickers);

		Assert.That(final.IsComplete, Is.True);
		Assert.That(final.Seats.Select(s => s.Pool.Count), Is.All.EqualTo(15));
	}
}
