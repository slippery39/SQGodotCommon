using MtgSimulator;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// The per-colour-identity value table.
///
/// The property worth defending is that an identity rate is a DELTA from the pooled rate rather
/// than a second opinion added to it. Every game in an identity's table is also in the pooled
/// table, so summing them would count the same evidence twice and inflate exactly the cards that
/// already have the most data.
/// </summary>
[TestFixture]
public class IdentityValuesTests
{
	private static DraftTrainingData Data(params (string Name, int Games, int Wins)[] cards)
	{
		var acc = new CardStatAccumulator();
		foreach (var (name, games, wins) in cards)
			for (var i = 0; i < games; i++)
				acc.Add([name], [name], i < wins);
		return acc.ToData();
	}

	private static IdentityValues Table(string identity, params (string, int, int)[] cards) =>
		new(new Dictionary<string, DraftTrainingData> { [identity] = Data(cards) });

	[Test]
	public void WithNoGamesInAnIdentity_TheAnswerIsThePooledRate()
	{
		// No evidence must mean no reason to differ, not a fabricated zero.
		var values = IdentityValues.Empty;

		Assert.That(values.RateOf("WU", "Anything", pooledRate: 0.62), Is.EqualTo(0.62));
	}

	[Test]
	public void AnUnknownIdentity_FallsBackRatherThanThrowing()
	{
		var values = Table("W", ("Bolt", 100, 80));

		Assert.That(values.RateOf("BG", "Bolt", pooledRate: 0.5), Is.EqualTo(0.5));
	}

	[Test]
	public void TheIdentityRate_MovesTowardTheIdentityEvidence_ButNeverPastIt()
	{
		// A card winning 80% in mono-white, pooled at 50%: the answer must sit between the two,
		// nearer the pooled rate while the evidence is thin.
		var values = Table("W", ("Bomb", 100, 80));

		var rate = values.RateOf("W", "Bomb", pooledRate: 0.50);

		Assert.That(rate, Is.GreaterThan(0.50), "identity evidence must move it");
		Assert.That(rate, Is.LessThan(0.80), "but shrinkage must hold it short of the raw rate");
	}

	[Test]
	public void MoreGames_MoveTheRateFurtherFromThePooledOne()
	{
		// The shrinkage doing its job: the same 80% rate is trusted more with more games behind it.
		var thin = Table("W", ("Bomb", 20, 16)).RateOf("W", "Bomb", 0.50);
		var thick = Table("W", ("Bomb", 400, 320)).RateOf("W", "Bomb", 0.50);

		Assert.That(thick, Is.GreaterThan(thin));
	}

	[Test]
	public void TheIdentityAnswerReplacesThePooledOne_RatherThanAddingToIt()
	{
		// The double-counting guard. A card at the pooled rate in its identity must come back at
		// the pooled rate — not at twice the delta.
		var pooled = Data(("Card", 200, 120));
		var values = new ConstructedValues(pooled, null, Table("W", ("Card", 200, 120)));

		Assert.That(
			values.CardDelta("Card", "W"),
			Is.EqualTo(values.CardDelta("Card")).Within(0.5),
			"an identity that agrees with the pool must not double the delta"
		);
	}

	[Test]
	public void WithoutAnIdentityTable_TheConditionedCallEqualsThePlainOne()
	{
		var values = new ConstructedValues(Data(("Card", 200, 120)), null);

		Assert.That(values.CardDelta("Card", "W"), Is.EqualTo(values.CardDelta("Card")));
		Assert.That(values.CardDelta("Card", null), Is.EqualTo(values.CardDelta("Card")));
	}

	[Test]
	public void ACardCanBeWorthDifferentThingsInDifferentIdentities()
	{
		// The whole reason the table exists: the pooled figure averages over identities nobody
		// plays, so a card excellent in one and poor in another reads as mediocre in both.
		var pooled = Data(("Split", 400, 200));
		var identities = new IdentityValues(
			new Dictionary<string, DraftTrainingData>
			{
				["W"] = Data(("Split", 200, 160)),
				["U"] = Data(("Split", 200, 40)),
			}
		);
		var values = new ConstructedValues(pooled, null, identities);

		Assert.That(values.CardDelta("Split", "W"), Is.GreaterThan(values.CardDelta("Split")));
		Assert.That(values.CardDelta("Split", "U"), Is.LessThan(values.CardDelta("Split")));
	}

	/// <summary>
	/// The calibration, pinned. A MEASURED run over CSC put the median cell at 24 games, p10 at 9
	/// and p90 at 50, and `IdentityShrinkK` is 75 so a median cell speaks at about a quarter volume
	/// rather than the half it would get at the pooled constant of 25.
	///
	/// Lower the constant and these move together, and this test says so. Half a card's rating
	/// decided by 24 games — standard error around ten percentage points — is noise speaking
	/// confidently, which is the direction that made merging the evolved table damaging.
	/// </summary>
	[TestCase(9, 0.05, 0.18, TestName = "A p10 cell is nearly ignored")]
	[TestCase(24, 0.15, 0.35, TestName = "A median cell speaks at about a quarter volume")]
	[TestCase(72, 0.40, 0.60, TestName = "Three runs of evidence reach roughly half")]
	public void ACellsWeight_MatchesTheMeasuredCalibration(int games, double low, double high)
	{
		// A cell that wins every game pulls the rate from the pooled 0.50 toward 1.0; how far it
		// gets IS the weight its own evidence carries.
		var values = Table("W", ("Card", games, games));

		var weight = (values.RateOf("W", "Card", pooledRate: 0.50) - 0.50) / 0.50;

		Assert.That(weight, Is.InRange(low, high));
	}

	[Test]
	public void MergingAccumulatesPerIdentity_AndKeepsIdentitiesApart()
	{
		var a = new Dictionary<string, DraftTrainingData>
		{
			["W"] = Data(("Card", 100, 50)),
			["U"] = Data(("Card", 100, 50)),
		};
		var b = new Dictionary<string, DraftTrainingData> { ["W"] = Data(("Card", 100, 50)) };

		var merged = IdentityValues.Merge(a, b);

		Assert.Multiple(() =>
		{
			Assert.That(new IdentityValues(merged).GamesFor("W", "Card"), Is.EqualTo(200));
			Assert.That(new IdentityValues(merged).GamesFor("U", "Card"), Is.EqualTo(100));
		});
	}
}
