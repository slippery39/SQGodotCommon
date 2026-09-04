using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **The acceptance gate for pool-conditioned card value — the same known answer `ContextValueTests`
/// and `OutputProbeTests` are held to, against a third substrate.**
///
/// The two previous attempts are recorded in `MtgSimulator/CLAUDE.md`: `MeasureLeverage` cannot
/// discriminate (it keys on the DEMAND, so all three elves measure identically), and
/// `MeasureInContext` is blind by construction (`StateEvaluator` counts `MaxMana` only and permanent
/// power only, so a tap-for-mana elf and a tap-to-pump elf score exactly as doing nothing).
/// `OutputProbe` passes but measures a PROXY — accumulated damage against an opponent that cannot
/// die.
///
/// This substrate measures the thing the builder is actually selecting on: **win rate, in real
/// games, in decks drawn from the archetype's own pool.** No proxy, no evaluator, no fixture — the
/// same games-in-hand counting `PreSimulation` and `DraftTrainer` already do, with the pool
/// restricted from the format to one archetype.
///
/// **Why the restriction is what makes it work, in arithmetic rather than hope.** A real run's
/// presim measured 3587 games over the DES format: 731 cards at a median 83 games each, and 38 792
/// pairs at a median SIX games each — with the console reporting `0 pairs clear the 50-game gate`.
/// Redistribute the same games over a ~16-card pool and the pair space collapses from 38 792 to 120,
/// which is where a synergy term stops being noise. The per-pair coverage this fixture actually
/// achieves is printed, so the claim is checked rather than asserted.
/// </summary>
[TestFixture]
public class PoolSampledValueTests
{
	private const string Conduit = "Wirewood Conduit";
	private const string Timberwatch = "Timberwatch Elder";
	private const string Sage = "Reclamation Sage";
	private const string Archer = "Poison-Tip Archer";

	/// <summary>
	/// The elf archetype's card pool — the cards a Wirewood Herald deck draws from.
	///
	/// Real DES cards by name, following `ContextValueTests` and `OutputProbeTests` rather than this
	/// project's usual inline-definition rule: the known answer this gate tests IS a fact about these
	/// specific cards, so a fixture of synthetic creatures would have nothing to be right about.
	///
	/// Sized so that playset sampling has somewhere to go. 43 spell slots at 17 lands is ~11 playsets
	/// out of 16 cards, so every sampled deck plays two thirds of the pool and leaves a third out —
	/// the contrast that lets "is this card carrying the deck" be answered at all.
	/// </summary>
	private static readonly string[] ElfPool =
	[
		// The engine pieces the hand-built list plays and the builder historically would not.
		Conduit,
		Timberwatch,
		"Wirewood Symbiont",
		// What the builder prefers instead, on correct isolation values.
		Sage,
		Archer,
		// The shared shell both lists agree on.
		"Wirewood Herald",
		"Llanowar Elves",
		"Elvish Mystic",
		"Dwynen's Elite",
		"Elvish Visionary",
		"Elvish Archdruid",
		"Sylvan Ranger",
		"Nissa, Vastwood Seer",
		"Radha, Heart of Keld",
		"Dwynen, Gilt-Leaf Daen",
		"Fauna Shaman",
	];

	private sealed record Row(string Name, double Delta, int Games, int DeckGames);

	/// Land count held still: it is a confound, not a variable, when the question is about cards.
	private static int Lands => Math.Max(Decklist.MinLands, 17);

	private static (IReadOnlyList<Row> Rows, DraftTrainingData Data) Sample(
		int decks = 140,
		int opponents = 12,
		int seed = 90_210
	)
	{
		var byName = SetRegistry.Designed.Cards.ToDictionary(
			c => c.Name,
			c => c,
			StringComparer.OrdinalIgnoreCase
		);

		var missing = ElfPool.Where(n => !byName.ContainsKey(n)).ToList();
		Assert.That(
			missing,
			Is.Empty,
			$"pool names absent from DES — the fixture is measuring nothing: {string.Join(", ", missing)}"
		);

		var pool = ElfPool.Select(n => byName[n]).ToList();

		var data = PreSimulation.Run(
			pool,
			decks,
			opponents,
			seed,
			aiDepth: 2,
			fixedLands: Lands,
			copiesPerCard: Decklist.MaxCopies,
			label: "Pool sample (elf archetype)"
		);

		// Same shrink and the same k the rest of the project uses, so this column is directly
		// comparable to `CardDelta`. Reported in percentage points against the sample's own base
		// rate — not against 0.5, because a pool of mirror-ish decks need not average exactly even.
		var rows = data
			.Cards.Select(c => new Row(
				c.Name,
				100
					* (
						DraftTrainingData.Shrink(
							c.Wins,
							c.Games,
							data.Prior,
							ConstructedValues.CardShrinkK
						) - data.Prior
					),
				c.Games,
				c.DeckGames
			))
			.OrderByDescending(r => r.Delta)
			.ToList();

		return (rows, data);
	}

	/// <summary>
	/// The gate, asserted as the SAME two orderings `ContextValueTests` demands — an ordering rather
	/// than a threshold, because the scale is arbitrary and a threshold becomes something people
	/// delete instead of trust.
	/// </summary>
	[Test]
	[Explicit("Plays ~1700 constructed games; run deliberately, not in the default suite.")]
	public void InAnElfPool_TheEngineElvesOutrankTheGenericBodies()
	{
		var (rows, data) = Sample();
		var byName = rows.ToDictionary(r => r.Name, r => r, StringComparer.Ordinal);

		TestContext.Out.WriteLine(
			$"  base rate {data.Prior:P1} over {data.Perspectives} deck-games"
		);
		foreach (var r in rows)
			TestContext.Out.WriteLine(
				$"  {r.Name, -26}{r.Delta, 8:+0.00;-0.00}pp   drawn in {r.Games, 5} of {r.DeckGames, 5} deck-games"
			);

		// Printed rather than asserted: this is the arithmetic claim that motivated the whole
		// substrate, and it should be readable even on a run where the orderings fail.
		var pairGames = data.Pairs.Select(p => p.Games).OrderBy(g => g).ToList();
		var usable = data.Pairs.Count(p => p.Games >= ConstructedValues.MinPairGames);
		TestContext.Out.WriteLine(
			$"  pairs: {data.Pairs.Count}, median {(pairGames.Count > 0 ? pairGames[pairGames.Count / 2] : 0)}"
				+ $" games/pair, {usable} clear the {ConstructedValues.MinPairGames}-game gate"
		);

		Assert.Multiple(() =>
		{
			Assert.That(
				byName[Conduit].Delta,
				Is.GreaterThan(byName[Sage].Delta),
				$"{Conduit} still ranks below {Sage} in decks built from the elf pool"
			);
			Assert.That(
				byName[Timberwatch].Delta,
				Is.GreaterThan(byName[Archer].Delta),
				$"{Timberwatch} still ranks below {Archer} in decks built from the elf pool"
			);
		});
	}

	/// <summary>
	/// **The vacuity guard, and the more important of the two.** A pool-conditioned table that simply
	/// reproduces the format-wide isolation ranking is an expensive way to recompute a number already
	/// on disk — and it would only pass the gate above by accident. Same control
	/// `ContextValueTests` and the constructed-vs-limited Spearman check apply to themselves.
	/// </summary>
	[Test]
	[Explicit("Plays ~1700 constructed games; run deliberately, not in the default suite.")]
	public void PoolSampledValueDisagreesWithIsolationValue()
	{
		// `sim_results/` resolves against the process cwd, which under a test run is `bin/` — an
		// empty table where every card reads 0.00pp. Anchor on the solution root, exactly as
		// `ContextValueTests` and `ArchetypeChallenge` do.
		Directory.SetCurrentDirectory(
			Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../.."))
		);
		var isolation = ConstructedValuesStore.Load(SetRegistry.Designed.Code, useDraftPrior: true);

		var (rows, _) = Sample();
		Assert.That(rows, Has.Count.GreaterThan(3), "too few measured cards to compare rankings");

		var byPool = rows.Select(r => r.Name).ToList();
		var byIsolation = rows.OrderByDescending(r => isolation.CardDelta(r.Name))
			.Select(r => r.Name)
			.ToList();

		TestContext.Out.WriteLine($"  pool      {string.Join(" > ", byPool)}");
		TestContext.Out.WriteLine($"  isolation {string.Join(" > ", byIsolation)}");

		// A table of all-zeros would rank by nothing and could pass the inequality below by
		// accident, which is how a vacuous control gets shipped.
		Assert.That(
			rows.Count(r => Math.Abs(isolation.CardDelta(r.Name)) > 0.01),
			Is.GreaterThan(3),
			"the isolation table reads ~0 for these cards — wrong working directory, or no presim "
				+ "has ever measured this pool. The comparison below would be vacuous."
		);

		Assert.That(
			byPool,
			Is.Not.EqualTo(byIsolation),
			"pool-conditioned value reproduces the isolation ranking exactly — measuring nothing new"
		);
	}
}
