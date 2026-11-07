using MagicCore.Cards;
using MagicCore.Limited;
using MagicCore.Sets;

namespace MagicCore.Tests.Limited;

/// <summary>A set 17lands hasn't seen is rated INTERIM, by rarity and measured value (MC-5.4).</summary>
public class CardRatingsTests
{
	[Test]
	public void Interim_IsTheRaritysMedian_MovedByTheValuesRank_AnUnmeasuredCardAtTheMedian()
	{
		var p = CardRegistry
			.Printings("FRA")
			.Where(c => !c.Card.TypeLine.StartsWith("Basic", StringComparison.Ordinal))
			.Take(6)
			.ToList();
		CardPrinting Common(int i) => p[i] with { Rarity = "common" };
		var reference = new[] { Common(0), Common(1), p[2] with { Rarity = "rare" } };
		var referenceRatings = new Dictionary<string, double>
		{
			[p[0].Card.Name] = 0.50,
			[p[1].Card.Name] = 0.54,
			[p[2].Card.Name] = 0.60,
		};
		var median = CardRatings.Median([0.50, 0.54]);
		var (low, high, unmeasured) = (p[3].Card.Name, p[4].Card.Name, p[5].Card.Name);

		var rated = CardRatings.Interim(
			[Common(3), Common(4), Common(5)],
			new Dictionary<string, double> { [low] = 1, [high] = 9 },
			reference,
			referenceRatings
		);

		Assert.That(rated[unmeasured], Is.EqualTo(median));
		Assert.That(rated[low], Is.LessThan(median));
		Assert.That(rated[high], Is.GreaterThan(median));
		Assert.That(
			rated[high] - rated[low],
			Is.EqualTo(CardRatings.ValueSpread / 2).Within(1e-12)
		);
	}

	[Test]
	public void Trk_RatesEveryCardItsPacksHold_ButTheBasics()
	{
		var ratings = CardRatings.Of("TRK");
		var unrated = CardRegistry
			.Printings("TRK")
			.Where(c => !c.Card.TypeLine.StartsWith("Basic", StringComparison.Ordinal))
			.Select(c => c.Card.Name)
			.Where(n => !ratings.ContainsKey(n));
		Assert.That(unrated, Is.Empty);
		Assert.That(
			CardRatings.TryOf("TRK"),
			Is.Not.Null,
			"Kai Winn's blind pick finds TRK's table"
		);
	}
}
