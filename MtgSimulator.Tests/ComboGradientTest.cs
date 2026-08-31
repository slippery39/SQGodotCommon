using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **Does a gradient exist between a pile of good cards and an assembled combo deck?**
///
/// This decides whether a goldfish-driven exploration phase can work at all. `Goldfish` measures
/// how fast a deck kills an inert opponent, and the plan was to use it as the fitness for combo
/// slots — win rate being useless for a half-built engine (it loses every game either way).
///
/// But a fitness is only followable if it CHANGES as you get closer. Two possibilities:
///
/// | Shape | Meaning |
/// |---|---|
/// | 4 -> 6 -> 9 -> never, smoothly | a gradient exists; hill climbing on goldfish speed can walk it |
/// | 4 -> never -> never -> never | a cliff; the signal is flat everywhere except the summit and no local search can find it |
///
/// The path measured is the one the real search would have to walk: start from assembled Storm and
/// progressively swap its pieces for the format's best individual cards, cutting the storm cards
/// that look WORST on their own first — which is exactly what `DeckBuilder.PickWeakest` does.
/// </summary>
[TestFixture]
public class ComboGradientTest
{
	private static void ChdirToSolutionRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null && dir.GetFiles("*.sln").Length == 0)
			dir = dir.Parent;
		Assert.That(dir, Is.Not.Null, "could not find the solution root");
		Directory.SetCurrentDirectory(dir!.FullName);
	}

	[TestCase("Traditional Storm")]
	[TestCase("Reanimator")]
	[TestCase("Affinity")]
	[Explicit("Plays solitaire games at each interpolation step.")]
	public void GoldfishGradient_FromAssembledComboToAPileOfGoodCards(string comboDeck)
	{
		ChdirToSolutionRoot();

		var set = SetRegistry.Get("ALL");
		var spells = set.Cards.Where(c => !c.HasSubtype("Land")).ToList();
		var pool = ConstructedGameSetup.PoolIndex(set.Cards.ToList());
		var values = ConstructedValuesStore.Load("ALL");

		var cards = DeckRegistry.Build(comboDeck, 1);
		var lands = cards.Count(c => c.HasSubtype("Land"));
		var comboCounts = cards
			.Where(c => !c.HasSubtype("Land"))
			.GroupBy(c => c.Name, StringComparer.Ordinal)
			.ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

		// The "good cards" a hill climber would drift toward: highest measured constructed value,
		// excluding anything already in the combo deck.
		var goodStuff = spells
			.Where(c => !comboCounts.ContainsKey(c.Name))
			.OrderByDescending(c => values.CardDelta(c.Name))
			.Take(12)
			.Select(c => c.Name)
			.ToList();

		Console.WriteLine();
		Console.WriteLine($"=== {comboDeck}: assembled -> good-stuff pile ===");
		Console.WriteLine($"  drifting toward: {string.Join(", ", goodStuff.Take(6))}");
		Console.WriteLine();
		Console.WriteLine($"{"swapped", 8}{"median turns", 14}{"wins/10", 9}");

		var comboSpellCount = comboCounts.Values.Sum();

		for (var swapped = 0; swapped <= comboSpellCount; swapped += 4)
		{
			// Cut the combo cards that look worst individually — PickWeakest's ordering.
			var deck = Decklist.Empty($"{comboDeck}-{swapped}") with
			{
				Lands = lands,
			};
			var toRemove = swapped;
			foreach (
				var (name, count) in comboCounts
					.OrderBy(kv => values.CardDelta(kv.Key))
					.ThenBy(kv => kv.Key, StringComparer.Ordinal)
			)
			{
				var keep = Math.Max(0, count - Math.Max(0, toRemove));
				toRemove -= count - keep;
				if (keep > 0)
					deck = deck.WithCopies(name, Math.Min(Decklist.MaxCopies, keep));
			}

			// Backfill with the good cards, 4 at a time.
			foreach (var name in goodStuff)
			{
				var need = Decklist.DeckSize - deck.Lands - deck.SpellCount;
				if (need <= 0)
					break;
				deck = deck.WithCopies(name, Math.Min(Decklist.MaxCopies, need));
			}

			deck = deck with { Lands = Decklist.DeckSize - deck.SpellCount };
			if (deck.Validate() is not null)
			{
				Console.WriteLine($"{swapped, 8}  (invalid: {deck.Validate()})");
				continue;
			}

			var (speed, _, wins) = Goldfish.Measure(deck, pool, games: 10, seed: 8_000);
			Console.WriteLine($"{swapped, 8}{speed, 14:F1}{wins, 9}");
		}
	}
}
