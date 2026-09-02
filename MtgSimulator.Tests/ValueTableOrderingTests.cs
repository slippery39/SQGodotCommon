using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **Does the isolation table order cards WITHIN an archetype slot differently from the mixed one?**
///
/// The presim/evolved split fixed a real feedback loop — a card played in a strong deck inflated its
/// own rating and every other deck then converged on it. But `ConstructedValues.CardDelta` is used
/// for two different questions, and only one of them is the question presim answers:
///
/// | question | right table |
/// |---|---|
/// | should a NORMAL deck play this card? | isolation — that is exactly what presim measures |
/// | which storm payoff should the STORM deck play? | **neither** — isolation says Tendrils is bad |
///
/// `DeckCore.Satisfy` and `Complete` both order a slot's interchangeable cards by `CardDelta`, so
/// the second question is being answered with the first question's table. Tendrils of Agony reads
/// **−8.09 in isolation**, which is correct in isolation and useless for ranking storm's own cards
/// against each other.
///
/// This builds the same request from both tables with no games at all. If the lists are near
/// identical the concern is dead; if the isolation build swaps in visibly worse archetype cards,
/// it is a boundary violation and the fix belongs inside the slot.
/// </summary>
[TestFixture]
public class ValueTableOrderingTests
{
	private const string MixedPath = "sim_results/constructed_values_all.json";
	private const string IsolationPath = "sim_results/constructed_values_all_presim.json";

	private static void ChdirToSolutionRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null && dir.GetFiles("*.sln").Length == 0)
			dir = dir.Parent;
		Assert.That(dir, Is.Not.Null);
		Directory.SetCurrentDirectory(dir!.FullName);
	}

	[TestCase("Mere-Storm", "DES")]
	[TestCase("Dragonstorm")]
	[TestCase("Tendrils of Agony")]
	[TestCase("Atog")]
	[Explicit("Diagnostic — builds one request from two value tables and diffs. No games.")]
	public void TheIsolationTableIsComparedAgainstTheMixedOne(string payoff, string setCode = "ALL")
	{
		ChdirToSolutionRoot();
		if (setCode == "DES")
		{
			// A core built IN PROCESS carries its supply weights; one loaded from a report written
			// before they existed does not, and silently orders by card value instead.
			var desSpells = SetRegistry.Get("DES").Cards.Where(c => !c.HasSubtype("Land")).ToList();
			var desFeatures = PoolFeatures.Build(desSpells);
			var desValues = new ConstructedValues(
				DraftTrainingStore.Load("sim_results/constructed_values_des_presim.json")!,
				ConstructedValuesStore.LoadDraftPrior("DES")
			);
			var built = DeckRequest
				.ForCards(payoff)
				.Resolve(desFeatures, desSpells, desValues, 7)
				.Deck;
			Assert.That(built, Is.Not.Null);
			Console.WriteLine($"=== {payoff} on DES, freshly built core ({built!.Lands} lands)");
			foreach (
				var (n, c) in built
					.Spells.OrderByDescending(kv => kv.Value)
					.ThenBy(kv => kv.Key, StringComparer.Ordinal)
			)
				Console.WriteLine($"      {c}x {n}");
			return;
		}

		Assert.That(File.Exists(MixedPath), Is.True, $"{MixedPath} is gone — nothing to compare");
		Assert.That(File.Exists(IsolationPath), Is.True, $"{IsolationPath} is missing");

		var spells = SetRegistry.Combined.Cards.Where(c => !c.HasSubtype("Land")).ToList();
		var features = PoolFeatures.Build(spells);
		var prior = ConstructedValuesStore.LoadDraftPrior(SetRegistry.Combined.Code);

		var mixed = new ConstructedValues(DraftTrainingStore.Load(MixedPath)!, prior);
		var isolation = new ConstructedValues(DraftTrainingStore.Load(IsolationPath)!, prior);

		var request = DeckRequest.ForCards(payoff);
		var a = request.Resolve(features, spells, mixed, seed: 7).Deck;
		var b = request.Resolve(features, spells, isolation, seed: 7).Deck;

		Assert.That(a, Is.Not.Null);
		Assert.That(b, Is.Not.Null);

		var onlyMixed = a!.Spells.Where(kv => b!.CopiesOf(kv.Key) == 0).ToList();
		var onlyIsolation = b!.Spells.Where(kv => a.CopiesOf(kv.Key) == 0).ToList();

		Console.WriteLine($"=== {payoff}");
		Console.WriteLine($"  MIXED     {a.Lands} lands, {a.DistinctSpells} distinct");
		Console.WriteLine($"  ISOLATION {b.Lands} lands, {b.DistinctSpells} distinct");
		Console.WriteLine(
			$"  difference {Decklist.Difference(a, b):P0}, "
				+ $"{onlyMixed.Count} cards only in mixed, {onlyIsolation.Count} only in isolation"
		);

		// The cards each table chose that the other did not — the whole question in one list.
		foreach (var (n, c) in onlyMixed.OrderByDescending(kv => kv.Value))
			Console.WriteLine(
				$"    MIXED only      {c}x {n, -32} iso {isolation.CardDelta(n), 7:+0.00;-0.00}"
			);
		Console.WriteLine("  ISOLATION build in full:");
		foreach (
			var (n, c) in b
				.Spells.OrderByDescending(kv => kv.Value)
				.ThenBy(kv => kv.Key, StringComparer.Ordinal)
		)
			Console.WriteLine($"      {c}x {n}");

		foreach (var (n, c) in onlyIsolation.OrderByDescending(kv => kv.Value))
			Console.WriteLine(
				$"    ISOLATION only  {c}x {n, -32} mix {mixed.CardDelta(n), 7:+0.00;-0.00}"
			);
	}
}
