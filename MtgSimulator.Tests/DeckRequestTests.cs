using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **Give the builder a constraint and see whether the deck it returns looks planned.**
///
/// Identity and repeatability only — this says nothing about whether the decks WIN. Optimisation is
/// a separate pass, and win rate is what pulls decks back toward good stuff, so a green suite here
/// must not be read as "these decks are good".
///
/// **Aggro / Midrange / Control are the controls, and they are real decks rather than an absence.**
/// Their identity is a curve and nothing else, so they SHOULD come out as piles of good cards —
/// that is the correct answer for a request that states no mechanical requirement. The suite is
/// only meaningful as a contrast: a mechanical request must produce something a curve request
/// cannot stumble into.
/// </summary>
[TestFixture]
public class DeckRequestTests
{
	private static readonly Lazy<(
		PoolFeatures Features,
		IReadOnlyList<Card> Spells,
		ConstructedValues Values
	)> Pool =
		new(() =>
		{
			var dir = new DirectoryInfo(AppContext.BaseDirectory);
			while (dir is not null && dir.GetFiles("*.sln").Length == 0)
				dir = dir.Parent;
			if (dir is not null)
				Directory.SetCurrentDirectory(dir.FullName);

			var spells = SetRegistry.Combined.Cards.Where(c => !c.HasSubtype("Land")).ToList();
			return (
				PoolFeatures.Build(spells),
				spells,
				ConstructedValuesStore.Load(SetRegistry.Combined.Code)
			);
		});

	private static DeckRequestResult Resolve(DeckRequest request, int seed = 7)
	{
		var (features, spells, values) = Pool.Value;
		return request.Resolve(features, spells, values, seed);
	}

	/// The requests the suite is about. Named the way a person would ask.
	private static IEnumerable<TestCaseData> Requests()
	{
		yield return new TestCaseData(DeckRequest.ForCards("Dragonstorm")).SetName(
			"a Dragonstorm deck"
		);
		yield return new TestCaseData(DeckRequest.ForCards("Tendrils of Agony")).SetName(
			"a Tendrils deck"
		);
		yield return new TestCaseData(DeckRequest.ForCards("Atog")).SetName("an Atog deck");
		yield return new TestCaseData(DeckRequest.ForTheme("Graveyard")).SetName(
			"a graveyard deck"
		);
		yield return new TestCaseData(DeckRequest.ForTheme("Artifact")).SetName("an artifact deck");
		yield return new TestCaseData(DeckRequest.ForTheme("Goblin")).SetName("a goblins deck");
		yield return new TestCaseData(
			new DeckRequest(
				["Liliana of the Veil", "Tarmogoyf"],
				[],
				DeckBuilder.DeckProfile.Midrange
			)
		).SetName("Liliana + Tarmogoyf, midrange");
		yield return new TestCaseData(DeckRequest.ForCurve(DeckBuilder.DeckProfile.Aggro)).SetName(
			"an aggro deck"
		);
		yield return new TestCaseData(
			DeckRequest.ForCurve(DeckBuilder.DeckProfile.Midrange)
		).SetName("a midrange deck");
		yield return new TestCaseData(
			DeckRequest.ForCurve(DeckBuilder.DeckProfile.Control)
		).SetName("a control deck");
	}

	[TestCaseSource(nameof(Requests))]
	public void EveryRequestBuildsALegalDeckAndSaysWhatItDid(DeckRequest request)
	{
		var result = Resolve(request);
		var (features, spells, _) = Pool.Value;

		Assert.That(result.Deck, Is.Not.Null, string.Join("; ", result.Problems));

		var deck = result.Deck!;
		var coreCards = result
			.Core.Slots.SelectMany(s => s.Cards)
			.ToHashSet(StringComparer.Ordinal);
		var onTheme = deck.Spells.Where(kv => coreCards.Contains(kv.Key)).Sum(kv => kv.Value);

		TestContext.Out.WriteLine(
			$"=== {request.Describe()}  ({deck.Lands} lands, {deck.SpellCount} spells)"
		);
		foreach (var m in result.Matched)
			TestContext.Out.WriteLine($"    matched: {m}");
		foreach (var p in result.Problems)
			TestContext.Out.WriteLine($"    PROBLEM: {p}");
		foreach (var s in result.Core.Slots)
			TestContext.Out.WriteLine(
				$"    {s.CountIn(deck), 3}/{s.MinCopies, -3} {Short(s.Role)} [{s.Cards.Count}]"
			);

		TestContext.Out.WriteLine(
			$"    on-theme {(deck.SpellCount == 0 ? 0 : (double)onTheme / deck.SpellCount):P0}, "
				+ $"dead {features.DeadCards(deck).Count}"
		);
		foreach (
			var kv in deck
				.Spells.OrderBy(kv => spells.First(c => c.Name == kv.Key).ManaCost)
				.ThenBy(kv => kv.Key, StringComparer.Ordinal)
		)
			TestContext.Out.WriteLine(
				$"      {kv.Value}x [{spells.First(c => c.Name == kv.Key).ManaCost}] {kv.Key}"
			);

		Assert.Multiple(() =>
		{
			Assert.That(deck.Validate(), Is.Null);
			Assert.That(
				result.Core.Holds(deck),
				Is.True,
				string.Join(", ", result.Core.Missing(deck))
			);
			Assert.That(result.Problems, Is.Empty);
		});
	}

	[TestCaseSource(nameof(Requests))]
	public void TheSameRequestBuildsTheSameDeckTwice(DeckRequest request)
	{
		// **Repeatability, asserted rather than assumed.** This is the check that would have caught
		// the `string.GetHashCode` bug in the gauntlet harness, where an arm whose code path had not
		// changed silently resampled between runs because the seed was per-process randomised.
		var a = Resolve(request, seed: 11).Deck;
		var b = Resolve(request, seed: 11).Deck;

		Assert.That(a, Is.Not.Null);
		Assert.That(b!.Lands, Is.EqualTo(a!.Lands));
		Assert.That(b.Spells, Is.EqualTo(a.Spells));
	}

	[Test]
	public void ANamedCardIsAlwaysInTheDeckItAskedFor()
	{
		// The payoff slot holds interchangeable payoffs, so "a Dragonstorm deck" could otherwise be
		// satisfied by Tendrils — which asks strictly less and therefore leaves the dragons dead.
		foreach (var name in new[] { "Dragonstorm", "Tendrils of Agony", "Atog" })
			Assert.That(
				Resolve(DeckRequest.ForCards(name)).Deck!.CopiesOf(name),
				Is.GreaterThan(0),
				name
			);
	}

	/// <summary>
	/// **The control with teeth: can a curve request stumble into a mechanical archetype?**
	///
	/// Aggro, Midrange and Control state no mechanical requirement, so they are piles of the
	/// format's best cards. If such a pile satisfies a core nobody asked for, that core is not an
	/// identity — and every on-theme figure measured against it means nothing.
	///
	/// **Measured, and the answer differs by archetype, which is itself the finding:**
	///
	/// | core | overlap with an Aggro pile | is it an identity? |
	/// |---|---|---|
	/// | Dragonstorm | **0 of 45** | yes |
	/// | Tendrils of Agony | **0 of 45** | yes |
	/// | Atog | **29 of 45, holds = TRUE** | **no, in this pool** |
	///
	/// **An artifact deck is not a distinguishable archetype in this cube**, because the format's
	/// best cheap cards ARE artifacts — Sol Ring, the Moxen, equipment, Frogmite, Myr Enforcer. The
	/// good-stuff pile satisfies the Atog core by accident. That is the same thing `GoodStuffControl`
	/// says about LIFT and the same thing `ArchetypeChallenge` measured for goblins: *if the format's
	/// best cards genuinely are artifacts, then an artifact deck in that format is the good cards*.
	///
	/// So this asserts on the archetypes the pool can distinguish and REPORTS the rest. Widening the
	/// assertion to Atog would be asserting a fact about the cube, not about the builder.
	/// </summary>
	[Test]
	public void ACurveRequestCannotStumbleIntoANarrowArchetype()
	{
		var (features, spells, values) = Pool.Value;

		// Narrow conjunctions only. Atog is excluded deliberately and measured below.
		var mustBeDistinct = new[] { "Dragonstorm", "Tendrils of Agony" };
		var alsoReport = new[] { "Atog" };

		var cores = mustBeDistinct
			.Concat(alsoReport)
			.Select(n => (Name: n, Core: DeckCore.For(features, n)))
			.Where(x => x.Core is not null)
			.ToList();

		Assert.That(
			cores.Select(c => c.Name),
			Is.SupersetOf(mustBeDistinct),
			"a core went missing"
		);

		foreach (
			var profile in new[]
			{
				DeckBuilder.DeckProfile.Aggro,
				DeckBuilder.DeckProfile.Midrange,
				DeckBuilder.DeckProfile.Control,
			}
		)
		{
			var pile = DeckRequest.ForCurve(profile).Resolve(features, spells, values, 7).Deck!;

			foreach (var (name, core) in cores)
			{
				var share = core!
					.Slots.SelectMany(s => s.Cards)
					.Distinct(StringComparer.Ordinal)
					.Sum(pile.CopiesOf);

				TestContext.Out.WriteLine(
					$"{profile, -9} vs {name, -18} holds={core.Holds(pile), -6} "
						+ $"{share}/{pile.SpellCount} cards in that archetype's pool"
				);

				if (mustBeDistinct.Contains(name, StringComparer.Ordinal))
					Assert.That(
						core.Holds(pile),
						Is.False,
						$"a {profile} pile satisfies the {name} core, so that core is not an identity"
					);
			}
		}
	}

	private static string Short(string role) => role.Length > 44 ? role[..44] : role;
}
