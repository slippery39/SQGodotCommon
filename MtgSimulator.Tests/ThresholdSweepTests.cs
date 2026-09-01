using System.Collections.Immutable;
using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **Is critical mass real in this engine?**
///
/// The whole "threshold deck" harness rests on an assumption nobody has tested: that a tribal or
/// artifact deck has a *step* in it — that eight goblins does nothing, twenty is a deck, and the
/// curve between them is not a straight line. If that step exists, random mutation swapping four
/// cards at a time genuinely cannot cross it and needs a constraint to hold the deck above the
/// threshold. If it does not exist, the harness is premised on a thing that is not there.
///
/// The only two data points so far disagree about the shape and neither is a curve:
/// maximum-density goblins scored **24.4%** against a saved field, the AI's own half-goblin deck
/// **55.0%**. That is consistent with a peak somewhere in the middle, and equally consistent with
/// density simply being bad. This sweep distinguishes them.
///
/// **The fill is deterministic, and that is the experiment's whole design.** Evolving the free
/// slots would mix "how much theme does a deck want" with "how well did evolution do this time",
/// and the second is noisy enough to swamp the first. Here the ONLY thing that varies across arms
/// is the density; everything else is the same best-by-value fill, so the curve is readable.
/// </summary>
[TestFixture]
public class ThresholdSweepTests
{
	private static readonly int[] Densities = [4, 8, 12, 16, 20, 24, 28];

	private static void ChdirToSolutionRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null && dir.GetFiles("*.sln").Length == 0)
			dir = dir.Parent;
		Assert.That(dir, Is.Not.Null, "could not find the solution root");
		Directory.SetCurrentDirectory(dir!.FullName);
	}

	[TestCase("Goblin")]
	[TestCase("Artifact")]
	[TestCase("Spirit")]
	[Explicit("Plays a few hundred games per density step.")]
	public void HowMuchOfATheme_DoesADeckActuallyWant(string subtype)
	{
		ChdirToSolutionRoot();

		var set = SetRegistry.Get("ALL");
		var spells = set.Cards.Where(c => !c.HasSubtype("Land")).ToList();
		var index = ConstructedGameSetup.PoolIndex(spells);
		var values = ConstructedValuesStore.Load("ALL");

		// The hand-built precons are the yardstick, for the reason a closed round-robin cannot
		// be one: it averages 50% by construction, so a field measured against itself reports
		// nothing about whether any of it is good.
		var field = DeckRegistry
			.All.Select(d => ToDecklist(d.Name, DeckRegistry.Build(d.Name, 1)))
			.Where(d => d.IsValid)
			.ToList();

		var themeCards = spells.Where(c => c.HasSubtype(subtype)).ToList();
		var themeNames = themeCards.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
		var maxPossible = themeCards.Count * Decklist.MaxCopies;

		Console.WriteLine();
		Console.WriteLine($"=== {subtype}: how much theme does a deck want? ===");
		Console.WriteLine(
			$"  {themeCards.Count} {subtype} cards in pool (up to {maxPossible} copies), "
				+ $"{field.Count} precons in the field"
		);
		Console.WriteLine();
		Console.WriteLine($"{"theme", 8}{"actual", 8}{"win rate", 11}   worst matchup");

		foreach (var density in Densities)
		{
			if (density > maxPossible)
			{
				Console.WriteLine($"{density, 8}   (pool cannot supply this many)");
				continue;
			}

			var core = new DeckCore(
				subtype,
				[
					new CoreSlot(
						subtype,
						themeNames.ToImmutableHashSet(StringComparer.Ordinal),
						density
					),
				]
			);

			// Satisfy the core, then fill the rest with the format's best cards, EXCLUDING the
			// theme — otherwise a high-value theme card creeps back in through the fill and the
			// arms stop differing by the one variable they are supposed to differ by.
			var deck = core.Satisfy(
				Decklist.Empty($"{subtype}-{density}") with
				{
					Lands = 22,
				},
				values
			);
			deck = FillNonTheme(deck, spells, values, themeNames);

			var actual = core.Slots[0].CountIn(deck);
			if (!deck.IsValid)
			{
				Console.WriteLine($"{density, 8}{actual, 8}   (invalid: {deck.Validate()})");
				continue;
			}

			var (rate, perMatchup) = ArchetypeChallenge.PlayAgainstField(
				deck,
				field,
				index,
				gamesPerMatchup: 10,
				seed: 61_000
			);
			var worst = perMatchup.OrderBy(m => m.Rate).First();
			Console.WriteLine(
				$"{density, 8}{actual, 8}{rate, 10:P1}   {worst.Rate, 5:P0} vs {worst.Opponent}"
			);
		}

		Console.WriteLine();
		Console.WriteLine("  A PEAK means critical mass is real and the optimum is interior.");
		Console.WriteLine(
			"  MONOTONE DOWN means density is simply a cost and the premise is wrong."
		);
	}

	/// <summary>
	/// Best-by-value fill that never adds a theme card.
	///
	/// Reports rather than asserts, like `ArchetypeChallenge` — a pass/fail bar here would encode
	/// the very assumption the sweep exists to test.
	/// </summary>
	private static Decklist FillNonTheme(
		Decklist deck,
		IReadOnlyList<Card> spells,
		ConstructedValues values,
		IReadOnlySet<string> theme
	)
	{
		foreach (
			var card in spells
				.Where(c => !theme.Contains(c.Name))
				.OrderByDescending(c => values.CardDelta(c.Name))
				.ThenBy(c => c.Name, StringComparer.Ordinal)
		)
		{
			var need = Decklist.DeckSize - deck.Lands - deck.SpellCount;
			if (need <= 0)
				break;
			if (deck.CopiesOf(card.Name) == 0)
				deck = deck.WithCopies(card.Name, Math.Min(Decklist.MaxCopies, need));
		}
		return deck;
	}

	/// Precons predate the 4-of rule and some run 5+, so copies are clamped on the way in.
	private static Decklist ToDecklist(string name, IReadOnlyList<Card> cards)
	{
		var deck = Decklist.Empty(name);
		foreach (
			var group in cards
				.Where(c => !c.HasSubtype("Land"))
				.GroupBy(c => c.Name, StringComparer.Ordinal)
		)
			deck = deck.WithCopies(group.Key, Math.Min(Decklist.MaxCopies, group.Count()));
		return deck with { Lands = Decklist.DeckSize - deck.SpellCount };
	}
}
