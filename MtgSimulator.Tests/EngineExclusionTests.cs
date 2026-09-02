using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **A dead engine does not only waste its own slot — it inflates every other number in the run.**
///
/// Mere-Storm finished at 8.2% and 0.0% across two CMB runs, and every other deck's best matchup
/// was "vs Mere-Storm": the field spread, the viable count and each deck's overall rate were all
/// measured partly against a punching bag. Engine slots are never culled by design (mode 7 already
/// judged the archetype on whether it ASSEMBLES), so nothing in a run removes one — the exclusion
/// list is the only way it leaves the field.
///
/// These drive <see cref="MetagameEvolver.LoadEngines"/> directly rather than through
/// <c>Run()</c>, so they cost no games.
/// </summary>
[TestFixture]
public class EngineExclusionTests
{
	/// <summary>
	/// **Cores are deliberately EMPTY.** `LoadEngines` also runs a distinctness guard that drops
	/// near-duplicate archetypes, and it skips cores with no cards — so an empty core takes that
	/// filter out of the experiment and leaves exclusion as the only thing under test. A shared
	/// non-empty core would drop candidates for the wrong reason and the test would still pass.
	/// </summary>
	private static EngineCandidate Candidate(string concept) =>
		new(
			$"Engine-{concept}",
			new DeckCore(concept, []),
			concept,
			"test",
			10,
			Decklist.Empty(concept) with
			{
				Lands = Decklist.MinLands,
			},
			[],
			[],
			[],
			0.9,
			6.0,
			0.5,
			1.0,
			5.0,
			4.0,
			7.0,
			5
		);

	private static string ReportWith(params string[] concepts)
	{
		var path = Path.Combine(Path.GetTempPath(), $"engines_exclude_{Guid.NewGuid():N}.json");
		EngineReportStore.Save(
			new EngineReport("TEST", 1, 10, [.. concepts.Select(Candidate)]),
			path
		);
		return path;
	}

	private static List<string> SeededConcepts(string reportPath, params string[] excluded) =>
		new MetagameEvolver(
			SetRegistry.Default,
			deckCount: 4,
			engineSlots: 3,
			enginesPath: reportPath,
			excludedEngines: excluded
		)
			.LoadEngines()
			.Select(e => e.Engine.Concept)
			.ToList();

	[Test]
	public void AnExcludedEngineIsNotSeeded_AndItsSlotIsLeftForACurveDeck()
	{
		var path = ReportWith("Mere-Storm", "Kilnmother Vess", "Wirewood Symbiont");
		try
		{
			var seeded = SeededConcepts(path, "Mere-Storm");

			Assert.Multiple(() =>
			{
				Assert.That(seeded, Does.Not.Contain("Mere-Storm"));
				Assert.That(
					seeded,
					Is.EquivalentTo(new[] { "Kilnmother Vess", "Wirewood Symbiont" })
				);
			});
		}
		finally
		{
			File.Delete(path);
		}
	}

	/// <summary>
	/// The vacuity guard. Without it, a `LoadEngines` that returned nothing at all — or one whose
	/// tier cut happened to drop the engine anyway — would pass the test above.
	/// </summary>
	[Test]
	public void WithoutAnExclusionList_EveryEngineIsStillSeeded()
	{
		var path = ReportWith("Mere-Storm", "Kilnmother Vess", "Wirewood Symbiont");
		try
		{
			Assert.That(
				SeededConcepts(path),
				Is.EquivalentTo(new[] { "Mere-Storm", "Kilnmother Vess", "Wirewood Symbiont" })
			);
		}
		finally
		{
			File.Delete(path);
		}
	}

	/// <summary>
	/// The report prints a deck as `Engine-&lt;concept&gt;`, so that is the string a user has in
	/// front of them when they decide an archetype is dead. Requiring them to strip the prefix by
	/// hand is a silent no-op waiting to happen — an unmatched exclusion looks exactly like an
	/// effective one in the final matrix.
	/// </summary>
	[Test]
	public void ADeckNameCopiedFromTheReportExcludesTheEngine()
	{
		var path = ReportWith("Mere-Storm", "Kilnmother Vess");
		try
		{
			Assert.That(
				SeededConcepts(path, "engine-mere-storm"),
				Is.EquivalentTo(new[] { "Kilnmother Vess" })
			);
		}
		finally
		{
			File.Delete(path);
		}
	}
}
