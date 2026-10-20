using System.Text.Json.Nodes;
using MagicCore.Cards;
using MagicCore.Limited;

namespace MagicCore.Tests.Limited;

/// <summary>
/// FRA packs are collated as printed (story LIM-1): the shape of every pack, the stated rules, and the
/// rates against the imported weights — never restated numbers, except the ones Wizards publishes
/// outside the data ("14 Magic: The Gathering cards"; 1 Special Guest pack in 55).
/// </summary>
public class BoosterTests
{
	static readonly JsonNode Data = JsonNode.Parse(CardDataSet.ResourceText("FRA.boosters.json")!)!;
	static readonly Dictionary<string, CardData> Fra = CardDataSet
		.Load("FRA")
		.ToDictionary(c => c.Name);

	static List<IReadOnlyList<PackCard>> Packs(int n) => [.. Boosters.Sealed(seed: 7, packs: n)];

	static IEnumerable<string> SheetCards(string sheet) =>
		Data["sheets"]![sheet]!["cards"]!.AsObject().Select(c => c.Key);

	[Test]
	public void EveryPack_Is14Cards_AWholePair_AndAThirdFromADifferentPair()
	{
		var pairCards = Data["sheets"]!
			.AsObject()
			.Where(s => s.Key.StartsWith("pair"))
			.SelectMany(s => SheetCards(s.Key))
			.ToHashSet();
		foreach (var pack in Packs(2000))
		{
			Assert.That(pack, Has.Count.EqualTo(14));
			var pair = pack.Where(c => c.Sheet.StartsWith("pair")).ToList();
			var third = pack.Single(c => c.Sheet.StartsWith("thirdNot"));
			Assert.That(pair, Has.Count.EqualTo(2));
			Assert.That(pair.Select(c => c.Card), Is.EquivalentTo(SheetCards(pair[0].Sheet)));
			Assert.That(
				Fra[pair[0].Card].Rarity,
				Is.EqualTo(Fra[pair[1].Card].Rarity),
				"a pair is one rarity"
			);
			Assert.That(third.Sheet, Is.EqualTo("thirdNot" + pair[0].Sheet["pair".Length..]));
			Assert.That(pairCards, Does.Contain(third.Card), "the third is an echoed-pair card");
			Assert.That(pair.Select(c => c.Card), Does.Not.Contain(third.Card));
		}
	}

	[Test]
	public void NoSheetGivesACardTwice_AndTheCommonsShowFourColours()
	{
		foreach (var pack in Packs(2000))
		{
			foreach (var sheet in pack.GroupBy(c => c.Sheet))
				Assert.That(sheet.Select(c => c.Card), Is.Unique, sheet.Key);
			var colors = pack.Where(c => c.Sheet == "common")
				.SelectMany(c => Fra[c.Card].Colors ?? []);
			Assert.That(
				colors.Distinct().Count(),
				Is.GreaterThanOrEqualTo(Boosters.MinCommonColors)
			);
		}
	}

	[Test]
	public void TheRareSlotsMythicRate_MatchesTheSheet()
	{
		var sheet = Data["sheets"]!["rareMythic"]!["cards"]!.AsObject();
		var expected =
			sheet.Where(c => Fra[c.Key].Rarity == "mythic").Sum(c => (double)c.Value!)
			/ sheet.Sum(c => (double)c.Value!);
		const int n = 20000;
		var mythics = Packs(n)
			.Count(p => Fra[p.Single(c => c.Sheet == "rareMythic").Card].Rarity == "mythic");
		var se = Math.Sqrt(expected * (1 - expected) / n);
		Assert.That((double)mythics / n, Is.EqualTo(expected).Within(4 * se));
	}

	[Test]
	public void SpecialGuests_AreOnePackIn55_AndNoneWhileUnscripted()
	{
		var variants = Data["boosters"]!.AsArray();
		var guest = variants
			.Where(v => v!["contents"]!["specialGuest"] is not null)
			.Sum(v => (double)v!["weight"]!);
		Assert.That(
			guest / variants.Sum(v => (double)v!["weight"]!),
			Is.EqualTo(1.0 / 55).Within(1e-9)
		);

		if (!Boosters.GuestsPlayable)
			Assert.That(Packs(2000).SelectMany(p => p).All(c => Fra.ContainsKey(c.Card)));
	}

	[Test]
	public void TheSameSeed_OpensTheSamePool() =>
		Assert.That(
			Boosters.Sealed(42).SelectMany(p => p),
			Is.EqualTo(Boosters.Sealed(42).SelectMany(p => p))
		);
}
