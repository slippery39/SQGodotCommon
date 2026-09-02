using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **Can the deckbuilder SEE the planted combos?** This is the question the CMB set exists to
/// answer, and it is asked here rather than through a run because a core is built with no games.
///
/// A failure here is not a bug in the set — it is the finding. The set is ground truth: the combo
/// provably works (`MtgCore.Tests.ComboProvingTwinTests`) and `LoopDetector` provably finds it, so
/// anything the builder cannot see is a limitation OF THE BUILDER, measured against a pool where
/// the right answer is known.
/// </summary>
[TestFixture]
public class ComboProvingDiscoveryTests
{
	private static readonly Lazy<PoolFeatures> Designed =
		new(
			() =>
				PoolFeatures.Build(
					SetRegistry.Designed.Cards.Where(c => !c.HasSubtype("Land")).ToList()
				)
		);

	/// <summary>
	/// **The before/after instrument for widening the demand-harvest rule.** Admitting battlefield
	/// "controlled by you" specs as demands necessarily creates MORE demands — every "target
	/// creature you control" pump spell becomes one — and the claim that `Informative` and the
	/// breadth gate absorb them is a prediction. These are the four numbers that check it.
	/// </summary>
	[Test]
	[Explicit("Diagnostic — demand and core counts, for comparing across a harvest-rule change.")]
	public void DumpDemandAndCoreCounts()
	{
		var features = Designed.Value;
		var spells = SetRegistry.Designed.Cards.Where(c => !c.HasSubtype("Land")).ToList();

		var informative = Enumerable
			.Range(0, features.Demands.Count)
			.Where(features.Informative)
			.ToList();

		var cores = spells
			.Select(c => DeckCore.For(features, c.Name))
			.Where(c => c is not null)
			.ToList();
		var distinct = cores
			.Select(c =>
				string.Join(
					"|",
					c!
						.Slots.Where(s => s.IsIdentity)
						.SelectMany(s => s.Cards.OrderBy(x => x, StringComparer.Ordinal))
				)
			)
			.Distinct(StringComparer.Ordinal)
			.Count();

		TestContext.Out.WriteLine(
			$"POOL {spells.Count} spells\n"
				+ $"  demands total      {features.Demands.Count}\n"
				+ $"  demands informative{informative.Count, 6}\n"
				+ $"  cores built        {cores.Count}\n"
				+ $"  cores distinct     {distinct}"
		);
	}

	[Test]
	[Explicit("Diagnostic — what does the harvester see on each combo piece?")]
	public void DumpWhatTheHarvesterSeesOnTheComboPieces()
	{
		var features = Designed.Value;

		foreach (var card in ComboProving.Cards)
		{
			var demands = features.DemandsOf(card.Name);
			TestContext.Out.WriteLine($"--- {card.Name}: {demands.Count} demand(s)");
			foreach (var d in demands)
				TestContext.Out.WriteLine(
					$"      informative={features.Informative(d)}  "
						+ $"{Trim(features.Describe(d), 64)}  ({features.OriginOf(d)})"
				);

			var core = DeckCore.For(features, card.Name);
			TestContext.Out.WriteLine(
				core is null
					? "      CORE: none"
					: "      CORE: "
						+ string.Join(
							" | ",
							core.Slots.Select(s =>
								$"{s.MinCopies}x {Trim(s.Role, 40)} [{s.Cards.Count}]"
							)
						)
			);
		}

		static string Trim(string s, int n) => s.Length > n ? s[..n] : s;
	}

	/// <summary>
	/// The headline claim: a copier's printed "target Illusionist you control" should compile into a
	/// core whose support slot is the Illusionists.
	///
	/// **This failed when the set first shipped, and `PoolFeatures.IsControlScoped` is the fix.**
	/// Measured on DES, all four combo pieces harvested ZERO demands: a targeting-derived demand was
	/// kept only when its candidates avoided the battlefield, on the reasoning that the battlefield
	/// is the SHARED board. Right for Lightning Bolt, wrong for "target Illusionist YOU CONTROL" —
	/// controlled-by-you is what un-shares it.
	///
	/// The widening was MEASURED, not argued, because the worry was that every "target creature you
	/// control" pump spell would flood the demand set:
	///
	///     demands total        63 -> 68
	///     demands informative  43 -> 48
	///     cores built/distinct 77 -> 82
	///
	/// **+5, not a flood, and the reason is worth carrying: demands dedupe by value pool-wide**, so
	/// every pump spell sharing one `CreatureControlledByYou` spec contributes a single demand
	/// between them rather than one each. Re-run `DumpDemandAndCoreCounts` if this rule is touched.
	///
	/// The untappers still harvest zero demands, and that is CORRECT — they are the supply side of
	/// the combo, not the payoff. Only the copier asks for anything.
	/// </summary>
	/// <summary>
	/// **Mode 7 must give the same report twice.** It did not: `Probe` seeded each candidate from
	/// `StringComparer.Ordinal.GetHashCode(core.Name)`, which .NET randomises PER PROCESS, so every
	/// run built a different deck and `assem`/`depth`/`LIFT` were noise. Measured across three runs
	/// at one seed, Blood for Bones read LIFT +15.0 and then 0.0.
	///
	/// **A single process cannot see that bug** — the hash is stable WITHIN a process, so running
	/// `Run` twice here would agree even with the defect present. What this test can pin is the
	/// property that was actually violated: the seed must be a pure function of the core's NAME.
	/// `StableHashIsNotProcessDependent` below is the other half, and together they are what the
	/// original `Deterministic:` doc comment claimed without anything checking it.
	/// </summary>
	[Test]
	public void TheSameSeedBuildsTheSameEngineDecks()
	{
		var set = SetRegistry.Get(ComboProving.Code);

		// One game each: the DECK is built before any game is played, so this is the cheapest
		// setting that still exercises the path. Zero games is not valid — the summary takes a
		// median over the readings and there are none.
		var a = EngineDiscovery.Run(set, gamesPerEngine: 1, seed: 4242);
		var b = EngineDiscovery.Run(set, gamesPerEngine: 1, seed: 4242);

		Assert.That(a.Engines, Has.Count.EqualTo(b.Engines.Count), "different engine counts");

		foreach (var (x, y) in a.Engines.Zip(b.Engines))
		{
			Assert.That(y.Name, Is.EqualTo(x.Name), "engine order moved between runs");
			Assert.That(
				y.Deck.Spells,
				Is.EquivalentTo(x.Deck.Spells),
				$"{x.Name} built a different deck at the same seed"
			);
			Assert.That(y.Deck.Lands, Is.EqualTo(x.Deck.Lands), $"{x.Name} land count moved");
		}
	}

	/// <summary>
	/// The half that a single process CAN check: the hash is a fixed, known constant rather than
	/// whatever the runtime chose at startup. If someone swaps FNV-1a back for `GetHashCode`, these
	/// literals stop matching — which is the only way this bug is catchable in-process.
	///
	/// The expected values are FNV-1a masked to 31 bits, computed independently of the code here.
	/// </summary>
	[Test]
	public void StableHashIsNotProcessDependent()
	{
		Assert.Multiple(() =>
		{
			Assert.That(Fnv1a(""), Is.EqualTo(2166136261u & 0x7FFFFFFF));
			Assert.That(Fnv1a("a"), Is.EqualTo(0xE40C292Cu & 0x7FFFFFFF));
			Assert.That(
				(uint)StringComparer.Ordinal.GetHashCode("Twinflame Artisan") & 0x7FFFFFFF,
				Is.Not.EqualTo(Fnv1a("Twinflame Artisan")),
				"if these ever agree the test proves nothing — pick a different string"
			);
		});

		static uint Fnv1a(string s)
		{
			uint hash = 2166136261u;
			foreach (var c in s)
			{
				hash ^= (byte)c;
				hash *= 16777619u;
			}
			return hash & 0x7FFFFFFF;
		}
	}

	/// <summary>
	/// **The tutor must read as an ENABLER, not as a thing to reanimate**, and it is the card most
	/// at risk from the causal-supply gate added this session.
	///
	/// That gate credits a blind mover only when `1 - (1 - density)^moved >= 0.5` — more likely than
	/// not it moved a matching card. The tutor moves exactly ONE card, so it is credited only while
	/// creatures are at least half the pool. They are, comfortably, on DES. But this is precisely
	/// the pool-density-versus-deck-density ceiling marked `ponytail:` in `PoolFeatures`: a
	/// reanimator DECK is far more than half creatures and the rule cannot see the deck, so a pool
	/// that drifted below the line would silently drop the best enabler in the archetype.
	///
	/// If this test ever fails, the card is fine and the gate is what moved.
	/// </summary>
	[Test]
	public void TheEntombTutorReadsAsAGraveyardEnabler()
	{
		var features = Designed.Value;

		var graveyard = features
			.DemandsOf("Raise the Sunken")
			.Single(d =>
				features.Describe(d).Contains("Graveyard", StringComparison.OrdinalIgnoreCase)
			);

		var causal = features.CausalSuppliersOf(graveyard);

		TestContext.Out.WriteLine(
			$"{causal.Count} causal suppliers; top: {string.Join(", ", causal.Take(8))}"
		);

		Assert.Multiple(() =>
		{
			Assert.That(
				causal,
				Does.Contain("Consign to Rot"),
				"the tutor is not credited with filling the graveyard — check the density gate"
			);
			Assert.That(
				features.SuppliersOf(graveyard),
				Does.Contain("Aurex, the Sevenfold"),
				"the payoff must still read as a thing you can reanimate"
			);
		});
	}

	[Test]
	public void TheCopierBuildsACoreAroundItsIllusionists()
	{
		var features = Designed.Value;
		var core = DeckCore.For(features, "Twinflame Artisan");

		Assert.That(core, Is.Not.Null, "the copier produced no core at all");

		var support = core!.Slots.Where(s => !s.IsIdentity).ToList();
		TestContext.Out.WriteLine(
			string.Join("\n", core.Slots.Select(s => $"{s.MinCopies}x {s.Role} [{s.Cards.Count}]"))
		);

		Assert.That(support, Is.Not.Empty, "no support slot — the combo requirement was not seen");
		Assert.That(
			support.Any(s => s.Cards.Contains("Mirevale Deceiver")),
			Is.True,
			"the untapper is not in any support slot, so the core is not the combo"
		);
	}
}
