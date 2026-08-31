using MtgCore;
using MtgCore.Cards.Builders;

namespace MtgSimulator.Tests;

/// <summary>
/// The goldfish is only useful if it can tell a fast deck from a slow one. These pin that, and the
/// sweep prints where the hand-built archetypes actually sit.
/// </summary>
[TestFixture]
public class GoldfishTests
{
	private static void ChdirToSolutionRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null && dir.GetFiles("*.sln").Length == 0)
			dir = dir.Parent;
		Assert.That(dir, Is.Not.Null, "could not find the solution root");
		Directory.SetCurrentDirectory(dir!.FullName);
	}

	/// <summary>
	/// **The self-check: a goldfish that cannot see a difference measures nothing.**
	///
	/// Inline cards, so a balance pass cannot break it. A deck of cheap 3/3s must kill an inert
	/// opponent faster than a deck of expensive 1/1s — if it cannot, the harness is broken and
	/// every number it produces later is noise.
	/// </summary>
	[Test]
	public void AFastDeckGoldfishesFasterThanASlowOne()
	{
		var fast = CardFactory.Creature("Swift", manaCost: 1, power: 3, toughness: 3).Build();
		var slow = CardFactory.Creature("Ponderous", manaCost: 6, power: 1, toughness: 1).Build();
		var pool = ConstructedGameSetup.PoolIndex([fast, slow]);

		var fastDeck = Decklist.Empty("Fast") with { Lands = 24 };
		var slowDeck = Decklist.Empty("Slow") with { Lands = 24 };
		// Only two cards exist, so the 4-of cap bounds each deck at 4 spells; the rest is lands.
		// That is fine — the comparison only needs the two curves to differ.
		fastDeck = fastDeck.WithCopies("Swift", 4);
		slowDeck = slowDeck.WithCopies("Ponderous", 4);
		fastDeck = fastDeck with { Lands = Decklist.DeckSize - fastDeck.SpellCount };
		slowDeck = slowDeck with { Lands = Decklist.DeckSize - slowDeck.SpellCount };

		var (fastSpeed, _, fastWins) = Goldfish.Measure(fastDeck, pool, games: 6, seed: 4_000);
		var (slowSpeed, _, slowWins) = Goldfish.Measure(slowDeck, pool, games: 6, seed: 4_000);

		Assert.That(
			fastSpeed,
			Is.LessThan(slowSpeed),
			$"fast deck goldfished in {fastSpeed} turns ({fastWins}/6 wins), slow in {slowSpeed} "
				+ $"({slowWins}/6) — the harness cannot see a difference it must be able to see"
		);
	}

	/// <summary>
	/// Where the hand-built archetypes actually sit. Combo decks should goldfish FAST even when
	/// their measured win rate against a field is mediocre — that gap is the whole reason this
	/// instrument exists.
	/// </summary>
	[Test]
	[Explicit("Plays solitaire games for every registry deck.")]
	public void GoldfishEveryRegistryDeck()
	{
		ChdirToSolutionRoot();

		var set = SetRegistry.Get("ALL");
		var pool = ConstructedGameSetup.PoolIndex(set.Cards.ToList());

		Console.WriteLine();
		Console.WriteLine($"{"deck", -22}{"median", 8}{"wins/10", 9}{"end score", 12}");

		foreach (var info in DeckRegistry.All)
		{
			var cards = DeckRegistry.Build(info.Name, 1);
			var lands = cards.Count(c => c.HasSubtype("Land"));
			var deck = cards
				.Where(c => !c.HasSubtype("Land"))
				.GroupBy(c => c.Name, StringComparer.Ordinal)
				.Aggregate(
					Decklist.Empty(info.Name) with
					{
						Lands = lands,
					},
					(d, g) => d.WithCopies(g.Key, g.Count())
				);
			deck = deck with { Lands = Decklist.DeckSize - deck.SpellCount };

			var (speed, score, wins) = Goldfish.Measure(deck, pool, games: 10, seed: 7_000);
			Console.WriteLine($"{info.Name, -22}{speed, 12:F1} {wins, 8}  {score, 10:F1}");
		}
	}
}
