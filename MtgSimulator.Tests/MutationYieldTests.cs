using System.Collections.Immutable;
using MtgCore;
using MtgCore.Cards.Builders;

namespace MtgSimulator.Tests;

/// <summary>
/// **Why does `Mutate` return null, and how often?**
///
/// A real 10-deck DES run measured two engine slots at 3 and 2 REAL proposals out of ~36 attempts
/// each, both on the full mutation budget, and both finished NON-VIABLE — frozen at their seeded
/// list for twelve generations and then reported as bad archetypes. A third slot with no pool lock
/// at all measured 0 real against 6 dry, so a narrow core is not the whole story.
///
/// These attribute the null rate instead of arguing about it. `[Explicit]`: they build
/// `PoolFeatures` over DES and run thousands of mutations.
/// </summary>
[TestFixture]
public class MutationYieldTests
{
	private static IReadOnlyList<Card> Spells() =>
		SetRegistry.Designed.Cards.Where(c => !c.HasSubtype("Land")).ToList();

	private static double NullRate(
		Decklist deck,
		IReadOnlyList<Card> spells,
		ConstructedValues values,
		PoolFeatures? features,
		DeckCore? core,
		DeckBuilder.DeckProfile profile,
		int trials = 600
	)
	{
		var nulls = 0;
		for (var i = 0; i < trials; i++)
		{
			var m = DeckBuilder.Mutate(
				deck,
				spells,
				values,
				new Random(90_000 + i),
				history: null,
				features: features,
				core: core,
				profile: profile
			);
			if (m is null)
				nulls++;
		}
		return (double)nulls / trials;
	}

	/// <summary>
	/// **The defect, pinned at the yield rather than through a run.**
	///
	/// A mutation budget means "mutants to EVALUATE". It was spent as "calls to a function that
	/// often fails" — `Mutate` rolls one operator and returns null when that operator cannot produce
	/// a legal, distinct, core-holding list, and the slot was consumed either way. Measured at 95%
	/// null for a five-card core pool, so a budget of 3 delivered 0.15 proposals per generation;
	/// two engine slots got 2 and 3 real proposals across twelve generations and were then reported
	/// NON-VIABLE, having never been optimised at all.
	///
	/// The pool here is deliberately tiny, because that is the condition under which the defect
	/// bites. A wide pool hides it — the null rate is 3–9% and three calls almost always yield three
	/// proposals, which is why this went unnoticed until a run seeded narrow cores.
	/// </summary>
	[Test]
	public void ANarrowPoolStillYieldsProposals_RatherThanBurningTheBudgetOnNulls()
	{
		// Inline, not looked up: a balance pass on a real card must not decide whether this passes.
		var spells = Enumerable
			.Range(1, 14)
			.Select(i =>
				CardFactory
					.Creature($"Elf {i}", manaCost: 1 + i % 4, power: 2, toughness: 2)
					.WithSubtype("Elf")
					.Build()
			)
			.ToList();
		var values = new ConstructedValues(DraftTrainingData.Empty, null);

		// **Seeded from the WHOLE pool, locked to a narrow core — which is the real shape.** An
		// engine slot's starting list comes from mode 7's sample deck, built from the format; only
		// MUTATION is confined to the core's cards. A deck built from five cards could not be legal
		// at all (five playsets is 20 spells against a 60-card list), so a fixture that tried would
		// be testing something the evolver never does.
		// 40 spells + 20 lands. Two core cards sit BELOW a playset so the fill has somewhere to go —
		// a core saturated at 4-of everything has no legal addition and would test the wrong thing.
		var copies = new Dictionary<string, int>(StringComparer.Ordinal)
		{
			["Elf 1"] = 2,
			["Elf 2"] = 2,
		};
		foreach (var c in spells.Skip(2).Take(9))
			copies[c.Name] = 4;

		var deck = new Decklist(
			"narrow",
			copies.ToImmutableSortedDictionary(StringComparer.Ordinal),
			20
		);
		Assert.That(deck.Validate(), Is.Null, "fixture deck is not a legal list");

		var core = new DeckCore(
			"narrow",
			[
				new CoreSlot(
					"Elves",
					spells.Take(5).Select(c => c.Name).ToImmutableHashSet(StringComparer.Ordinal),
					MinCopies: 4,
					IsIdentity: true
				),
			]
		);

		int Yield(int retries)
		{
			var got = 0;
			for (var i = 0; i < 200; i++)
			{
				var rng = new Random(4_000 + i);
				for (var attempt = 0; attempt < retries; attempt++)
				{
					if (DeckBuilder.Mutate(deck, spells, values, rng, core: core) is not null)
					{
						got++;
						break;
					}
				}
			}
			return got;
		}

		var single = Yield(1);
		var retried = Yield(20);
		TestContext.Out.WriteLine($"  1 attempt: {single}/200    20 attempts: {retried}/200");

		// Asserted as an IMPROVEMENT, not as an absolute. A narrow pool genuinely has few distinct
		// legal lists and re-rolling cannot invent options that do not exist — what it must stop is
		// the budget being consumed by an operator roll that was never going to work.
		Assert.That(
			retried,
			Is.GreaterThan(single * 2),
			"re-rolling barely helps — the budget is still being burned on nulls"
		);
	}

	/// <summary>
	/// The bisection: the same deck under its profile and under `Any`, with and without features.
	/// Whatever moves the null rate is the cause.
	/// </summary>
	[Test]
	[Explicit("Diagnostic — attributes Mutate's null rate on the decks a real run produced.")]
	public void WhereDoTheNullProposalsComeFrom()
	{
		var result = DecklistStore.Load(
			Directory
				.GetFiles("../../../../sim_results", "metagame_des_*.json")
				.OrderBy(f => f)
				.Last()
		);
		Assert.That(result, Is.Not.Null, "no saved metagame to read");

		var spells = Spells();
		var values = ConstructedValuesStore.Load(SetRegistry.Designed.Code, useDraftPrior: true);
		var features = PoolFeatures.Build(spells);
		var costs = spells.ToDictionary(c => c.Name, StringComparer.Ordinal);

		// The engine slots' cores, matched by the `Engine-<Concept>` name the evolver stamps.
		var report = EngineReportStore.Load(
			Directory
				.GetFiles("../../../../sim_results", "engines_des_*.json")
				.OrderBy(f => f)
				.Last()
		);
		var cores =
			report?.Engines.ToDictionary(
				e => $"Engine-{e.Concept}",
				e => e.Core,
				StringComparer.Ordinal
			) ?? [];

		TestContext.Out.WriteLine(
			$"  {"deck", -32}{"lands", 6}{"cost", 7}{"pool", 6}{"Any", 8}{"profile", 9}{"+core", 8}"
		);

		foreach (var deck in result!.Decks)
		{
			var profile = deck.Name switch
			{
				var n when n.StartsWith("Aggro", StringComparison.Ordinal) => DeckBuilder
					.DeckProfile
					.Aggro,
				var n when n.StartsWith("Midrange", StringComparison.Ordinal) => DeckBuilder
					.DeckProfile
					.Midrange,
				var n when n.StartsWith("Control", StringComparison.Ordinal) => DeckBuilder
					.DeckProfile
					.Control,
				_ => DeckBuilder.DeckProfile.Any,
			};

			var core = cores.GetValueOrDefault(deck.Name);
			var poolSize = core
				?.Slots.SelectMany(s => s.Cards)
				.Distinct(StringComparer.Ordinal)
				.Count();

			var any = NullRate(deck, spells, values, features, null, DeckBuilder.DeckProfile.Any);
			var withProfile = NullRate(deck, spells, values, features, null, profile);
			var withCore = core is null
				? (double?)null
				: NullRate(deck, spells, values, features, core, DeckBuilder.DeckProfile.Any);

			TestContext.Out.WriteLine(
				$"  {deck.Name, -32}{deck.Lands, 6}{deck.AverageCost(costs), 7:F2}"
					+ $"{poolSize?.ToString() ?? "-", 6}"
					+ $"{any, 8:P0}{withProfile, 9:P0}"
					+ $"{(withCore is null ? "-" : $"{withCore:P0}"), 8}"
			);
		}
	}
}
