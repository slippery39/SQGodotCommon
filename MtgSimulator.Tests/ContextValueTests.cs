using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **The acceptance gate for context-conditioned card value, written before the metric was tuned.**
///
/// This project has shipped a plausible column of numbers that measured nothing three times — LIFT
/// was vacuous twice invisibly, and the synergy gate sat at a value no real pair could reach for
/// three full runs. Each looked like a working feature. So the metric gets a known answer to hit
/// before it is allowed anywhere near the deck builder.
///
/// **The known answer is measured, not assumed.** On DES the hand-built elf deck — which plays
/// Wirewood Conduit and Timberwatch Elder — beat the builder's own elf deck 65-35, from the same
///24-card core pool at the same 17 lands. The builder plays Reclamation Sage and Poison-Tip Archer
/// instead, and it does so for a correct reason: in RANDOM decks those cards genuinely rate higher
/// (66.8% and 63.1% against 51.6% and 47.8%). Isolation value is not wrong, it answers the wrong
/// question.
/// </summary>
[TestFixture]
public class ContextValueTests
{
	private sealed record Row(string Name, float OverAverage, string? NotMeasured);

	private const string Conduit = "Wirewood Conduit";
	private const string Timberwatch = "Timberwatch Elder";
	private const string Sage = "Reclamation Sage";
	private const string Archer = "Poison-Tip Archer";

	/// The elf shell the value is conditioned on — the deck these cards would actually live in.
	private static readonly string[] ElfContext =
	[
		"Wirewood Symbiont",
		"Wirewood Herald",
		"Llanowar Elves",
		"Elvish Mystic",
		"Dwynen's Elite",
		"Elvish Archdruid",
	];

	private static IReadOnlyList<Row> Measure()
	{
		var pool = SetRegistry.Designed.Cards.ToDictionary(
			c => c.Name,
			c => c,
			StringComparer.OrdinalIgnoreCase
		);

		string[] candidates =
		[
			Conduit,
			Timberwatch,
			Sage,
			Archer,
			"Elvish Visionary",
			"Sylvan Ranger",
		];

		return
		[
			.. CardValueSandbox
				.MeasureInContext(
					[.. candidates.Select(n => pool[n])],
					[.. ElfContext.Select(n => pool[n])]
				)
				.Select(v => new Row(v.Name, v.OverAverage, v.NotMeasured)),
		];
	}

	/// <summary>
	/// The gate. In an elf shell the two engine elves must beat the two the builder prefers.
	///
	/// Asserted as an ORDERING, never as a threshold: the scale is arbitrary and would need
	/// retuning every time the fixture or the evaluator weights move, which is how a test becomes
	/// something people delete rather than trust.
	/// </summary>
	[Test]
	[Explicit(
		"TARGET, not yet met — and the reason is in StateEvaluator, not in this fixture. "
			+ "Wirewood Conduit is 'Exhaust: add mana equal to the number of Elves you control' and "
			+ "Timberwatch Elder taps for an until-end-of-turn pump. The evaluator counts MaxMana "
			+ "only (temporary mana excluded) and permanent power only (UntilEndOfTurn excluded) — "
			+ "both deliberate and both correct for their original purpose, and together they make "
			+ "these cards score EXACTLY the same as doing nothing. Measured: Conduit, Timberwatch "
			+ "and Sylvan Ranger all read an identical -0.70, which is just the card leaving hand. "
			+ "No amount of context conditioning fixes that; the substrate cannot see the effect. "
			+ "Un-Explicit this when either the rollout is long enough to convert temporary mana "
			+ "into board, or mana engines are valued from ProbeManaProfit instead."
	)]
	public void InAnElfShell_TheEngineElvesOutrankTheGenericBodies()
	{
		var byName = Measure().ToDictionary(r => r.Name, r => r, StringComparer.Ordinal);

		foreach (var r in Measure())
			TestContext.Out.WriteLine($"  {r.Name, -24}{r.OverAverage, 9:F2}  {r.NotMeasured}");

		Assert.Multiple(() =>
		{
			Assert.That(
				byName[Conduit].OverAverage,
				Is.GreaterThan(byName[Sage].OverAverage),
				$"{Conduit} still ranks below {Sage} in a deck full of Elves"
			);
			Assert.That(
				byName[Timberwatch].OverAverage,
				Is.GreaterThan(byName[Archer].OverAverage),
				$"{Timberwatch} still ranks below {Archer} in a deck full of Elves"
			);
		});
	}

	/// <summary>
	/// **The vacuity guard, and the more important of the two.** A context value that simply
	/// reproduces the isolation ranking is an expensive way to compute a number we already have —
	/// and it would pass nothing above by accident only if the isolation ranking happened to be
	/// right. Same control the constructed-vs-limited Spearman check applies to its own table.
	/// </summary>
	[Test]
	public void ContextValueDisagreesWithIsolationValue()
	{
		Directory.SetCurrentDirectory(
			Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../.."))
		);
		var values = ConstructedValuesStore.Load(SetRegistry.Designed.Code, useDraftPrior: true);

		var rows = Measure().Where(r => r.NotMeasured is null).ToList();
		Assert.That(rows, Has.Count.GreaterThan(3), "too few measured cards to compare rankings");

		var byContext = rows.OrderByDescending(r => r.OverAverage).Select(r => r.Name).ToList();
		var byIsolation = rows.OrderByDescending(r => values.CardDelta(r.Name))
			.Select(r => r.Name)
			.ToList();

		TestContext.Out.WriteLine($"  context   {string.Join(" > ", byContext)}");
		TestContext.Out.WriteLine($"  isolation {string.Join(" > ", byIsolation)}");

		Assert.That(
			byContext,
			Is.Not.EqualTo(byIsolation),
			"context value reproduces the isolation ranking exactly — it is measuring nothing new"
		);
	}
}
