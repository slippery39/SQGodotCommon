using System;
using System.IO;
using System.Linq;
using MtgCore;
using MtgSimulator;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

/// <summary>
/// The model the GAME actually drafts against, not the one in sim_results/.
///
/// These are two separate files and regenerating the trained one does not update the shipped
/// one — the documented failure is a retrain that visibly produces new numbers while the game
/// keeps drafting against the old model, with no symptom anywhere. Adding a colour and
/// forgetting the copy would leave 67 cards scored at exactly the prior, which looks like a
/// plausible draft rather than a broken one.
///
/// DraftPickers.Trained is keyed by card NAME, so a card missing from the model is not an error
/// — it silently scores at the prior. Only a test can see it.
/// </summary>
[TestFixture]
public class ShippedDraftModelTests
{
	private static DraftTrainingData Load()
	{
		// The asset lives under res:// at runtime; from the test host it is a plain file beside
		// the repo. Walk up from the test binary rather than hardcoding an absolute path.
		var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
		while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "SQGodotCommon")))
			dir = dir.Parent;

		Assert.That(dir, Is.Not.Null, "Could not locate the repository root from the test binary");

		var path = Path.Combine(
			dir!.FullName,
			"SQGodotCommon",
			"MtgGame",
			"Assets",
			Path.GetFileName(DraftTrainingStore.PathFor(CoresetCube.Code))
		);

		Assert.That(File.Exists(path), Is.True, $"Shipped model missing: {path}");

		var data = DraftTrainingStore.FromJson(File.ReadAllText(path));
		Assert.That(data, Is.Not.Null, "Shipped model failed to parse");
		return data!;
	}

	[Test]
	public void ShippedModel_CoversEveryDraftableCardInTheSet()
	{
		var data = Load();
		var known = data.Cards.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

		var missing = CoresetCube
			.Set.Draftable.Select(c => c.Name)
			.Distinct()
			.Where(n => !known.Contains(n))
			.ToList();

		Assert.That(
			missing,
			Is.Empty,
			"Cards absent from the shipped model score at exactly the prior — they will be drafted "
				+ "as if average regardless of how good they are: "
				+ string.Join(", ", missing)
		);
	}

	/// <summary>
	/// A merged-in model keeps the old cards but trains the new ones thinly. Requiring real
	/// sample depth on every card is what distinguishes a from-scratch retrain from a merge.
	/// </summary>
	[Test]
	public void ShippedModel_HasRealSampleDepthOnEveryCard()
	{
		var data = Load();
		var thin = data
			.Cards.Where(c => c.Games < 100)
			.Select(c => $"{c.Name} ({c.Games})")
			.ToList();

		Assert.That(
			thin,
			Is.Empty,
			"Cards with too few games to have learned anything: " + string.Join(", ", thin)
		);
	}

	/// <summary>
	/// Pairs are stripped from the shipped copy because synergyWeight defaults to 0 and they are
	/// O(cards²) — 2.8 MB of JSON that is never read at pick time. If this starts failing, either
	/// the strip step was skipped or synergy was turned on and the asset needs them back.
	/// </summary>
	[Test]
	public void ShippedModel_ShipsWithoutPairs()
	{
		Assert.That(Load().Pairs, Is.Empty);
	}
}
