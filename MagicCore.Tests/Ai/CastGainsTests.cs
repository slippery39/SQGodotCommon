using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Sets;

namespace MagicCore.Tests.Ai;

/// <summary>
/// The option value's arithmetic (AI-13.4, Shayne: "the AI doesn't just play out its spells ASAP, but
/// also … doesn't just hold onto them for the whole game"): the expected best of k offers is the mean at
/// one, rises with more turns left, and never passes the best offer seen.
/// </summary>
public class CastGainsTests
{
	static readonly double[] Offers =
	[
		0,
		0,
		0,
		0,
		0,
		0,
		0,
		0,
		0,
		0,
		1,
		2,
		3,
		4,
		5,
		6,
		8,
		10,
		12,
		16,
	];

	[Test]
	public void TheExpectedBest_IsTheMeanAtOneOffer_RisesWithMore_AndStaysUnderTheBest()
	{
		Assert.That(CastGains.ExpectedBest(Offers, 1), Is.EqualTo(Offers.Average()).Within(1e-9));
		var byTurns = Enumerable
			.Range(1, 6)
			.Select(k => CastGains.ExpectedBest(Offers, k))
			.ToList();
		Assert.That(byTurns, Is.Ordered.Ascending, "more turns left, a higher bar to cast now");
		Assert.That(byTurns[^1], Is.LessThan(Offers.Max()));
	}

	[Test]
	public void TheTable_HoldsTheSetsInstantSpeedCards()
	{
		Assert.That(
			CastGains.Fra,
			Does.ContainKey("Unsummon"),
			"run --cast-gains if the table is missing"
		);
		// every set's own table is in (AI-13.9: TRK's instants had no option value without one)
		foreach (var set in CastGains.TableSets)
			Assert.That(
				CardDataSet.ResourceText(CastGains.TableFile(set)),
				Is.Not.Null,
				$"run --cast-gains --set={set}"
			);
		Assert.That(CastGains.Fra.Values.Select(q => q.Length), Has.All.EqualTo(20));
		Assert.That(
			CastGains.Fra.Values.All(q => q.Zip(q.Skip(1)).All(p => p.First <= p.Second)),
			"ascending"
		);
	}
}
