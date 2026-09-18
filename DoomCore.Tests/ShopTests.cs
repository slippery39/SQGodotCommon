using DoomCore;
using ImmutableGameObjects;

namespace DoomCore.Tests;

/// <summary>
/// The shop, and the first thing in the game other than an apocalypse that can take a card out of a
/// run. Every test asserts the CONSEQUENCE — a transaction that silently does nothing looks exactly
/// like one that worked.
/// </summary>
public class ShopTests
{
	private static RunCard Card(string name, int id) =>
		new()
		{
			Name = name,
			Cost = 1,
			IsUnit = true,
			Power = 4,
			Toughness = 4,
			RunCardId = id,
		};

	private static Run RunWith(int cards, int gold) =>
		new Run
		{
			Life = 100,
			MaxLife = 120,
			Gold = gold,
		}.WithCards(Enumerable.Range(0, cards).Select(i => Card($"Card {i}", 0)));

	[Test]
	public void BuyingACardSpendsGoldAndAddsIt()
	{
		var run = RunWith(cards: 10, gold: 100);
		var after = run.BuyCard(Card("Bought", 0), price: 60);

		Assert.That(after.Deck, Has.Count.EqualTo(11));
		Assert.That(after.Deck.Any(c => c.Name == "Bought"), Is.True);
		Assert.That(after.Gold, Is.EqualTo(40));
	}

	[Test]
	public void ACardYouCannotAffordIsRefusedAndCostsNothing()
	{
		var run = RunWith(cards: 10, gold: 10);
		var after = run.BuyCard(Card("Bought", 0), price: 60);

		Assert.That(after.Deck, Has.Count.EqualTo(10));
		Assert.That(after.Gold, Is.EqualTo(10));
	}

	/// <summary>**Removal is the point of the shop**, so it had better actually remove.</summary>
	[Test]
	public void RemovingACardTakesItOutOfTheRun()
	{
		var run = RunWith(cards: 12, gold: 100);
		var target = run.Deck[3].RunCardId;

		var after = run.RemoveCard(target, price: 70);

		Assert.That(after.Deck, Has.Count.EqualTo(11));
		Assert.That(after.Deck.Any(c => c.RunCardId == target), Is.False);
		Assert.That(after.Gold, Is.EqualTo(30));
		Assert.That(after.CardsRemoved, Is.EqualTo(1));
	}

	/// <summary>
	/// **A deck cannot be thinned into a loss.** `HasNoCards` ends a run, and until the shop existed
	/// nothing a player chose could reach it.
	/// </summary>
	[Test]
	public void ADeckCannotBeThinnedBelowTheFloor()
	{
		var run = RunWith(cards: Run.MinDeckSize, gold: 1000);
		var target = run.Deck[0].RunCardId;

		var after = run.RemoveCard(target, price: 10);

		Assert.That(after.Deck, Has.Count.EqualTo(Run.MinDeckSize), "refused, not thinned");
		Assert.That(after.Gold, Is.EqualTo(1000), "and it charged nothing for refusing");
	}

	/// <summary>Each removal costs more than the last, or thinning is the only thing gold buys.</summary>
	[Test]
	public void RemovalGetsMoreExpensiveEachTime()
	{
		var first = StarterContent.ShopFor(DoomTheme.Reckoning, seed: 1, floor: 5, cardsRemoved: 0);
		var third = StarterContent.ShopFor(DoomTheme.Reckoning, seed: 1, floor: 5, cardsRemoved: 2);

		Assert.That(third.RemovalPrice, Is.GreaterThan(first.RemovalPrice));
	}

	[Test]
	public void BuyingHealthRestoresItAndIsCappedAtMax()
	{
		var run = RunWith(cards: 10, gold: 100) with { Life = 115, MaxLife = 120 };
		var after = run.BuyHeal(price: 40, amount: 30);

		Assert.That(after.Life, Is.EqualTo(120), "capped");
		Assert.That(after.Gold, Is.EqualTo(60), "and still paid for");
	}

	/// <summary>
	/// A shop is deterministic from (seed, floor), like a reward screen — the property that makes a
	/// run replay exactly and a bug report actionable.
	/// </summary>
	[Test]
	public void AShopIsTheSameEveryTimeItIsAskedFor()
	{
		var a = StarterContent.ShopFor(DoomTheme.Rising, seed: 42, floor: 20, cardsRemoved: 1);
		var b = StarterContent.ShopFor(DoomTheme.Rising, seed: 42, floor: 20, cardsRemoved: 1);

		Assert.That(a.Cards.Select(c => c.Name), Is.EqualTo(b.Cards.Select(c => c.Name)));
		Assert.That(a.CardPrice, Is.EqualTo(b.CardPrice));
		Assert.That(a.Cards, Is.Not.Empty, "a shop with nothing in it is not a shop");
	}

	/// <summary>Prices follow the act, so a late shop is reachable on late earnings.</summary>
	[Test]
	public void PricesScaleWithTheAct()
	{
		var early = StarterContent.ShopFor(DoomTheme.Reckoning, seed: 1, floor: 5, cardsRemoved: 0);
		var late = StarterContent.ShopFor(
			DoomTheme.Rising,
			seed: 1,
			floor: ActMap.ActLength * 2 + 5,
			cardsRemoved: 0
		);

		Assert.That(late.CardPrice, Is.GreaterThan(early.CardPrice));
		Assert.That(
			StarterContent.GoldFor(ActMap.ActLength * 2 + 5),
			Is.GreaterThan(StarterContent.GoldFor(5))
		);
	}
}
