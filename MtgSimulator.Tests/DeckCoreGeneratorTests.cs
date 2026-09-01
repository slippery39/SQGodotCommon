using MtgCore;

namespace MtgSimulator.Tests;

/// <summary>
/// **`DeckCore.For` reads an archetype off the payoff card's own demands.**
///
/// The question these exist to answer is "how does the search discover that Dragonstorm needs
/// dragons", and the answer is that it never had to: `HasStorm` produces a `SpellsCastDemand` and
/// the card's `SelectCardFromLibraryAction.Subtype` produces a Dragon filter, so
/// `PoolFeatures.DemandsOf("Dragonstorm")` has returned both for as long as the card has existed.
/// What was missing is the JOIN. `EngineDiscovery` keys a candidate on ONE `DemandIndex`, so it
/// probes "spells cast" and "Dragon" as two unrelated concepts, builds a deck for each, and
/// neither of them is Dragonstorm.
///
/// `TheSingleDemandView_CannotExpressTheConjunction` is the control for that claim and is the
/// most important test in the file — without it the rest only prove that a generator generates.
/// </summary>
[TestFixture]
public class DeckCoreGeneratorTests
{
	private const string Anchor = "Dragonstorm";
	private const string Dragon = "Bogardan Hellkite";
	private const string StormOnlyPayoff = "Tendrils of Agony";
	private const string Vanilla = "Grizzly Bears";

	private static readonly string[] Rituals = ["Rite of Flame", "Seething Song", "Lotus Bloom"];

	/// <summary>
	/// A small named pool rather than a whole set: these cards are chosen for their MECHANICS —
	/// storm, a Dragon to fetch, mana-positive rituals — none of which is a balance number, so a
	/// power-level pass cannot break these tests. A card changing shape here should fail loudly,
	/// because at that point it is no longer the card the test is about.
	/// </summary>
	private static PoolFeatures Features()
	{
		string[] names = [Anchor, Dragon, StormOnlyPayoff, Vanilla, .. Rituals];
		return PoolFeatures.Build(names.Select(CardLibrary.GetByName).ToList());
	}

	private static CoreSlot? SlotContaining(DeckCore core, string card) =>
		core.Slots.FirstOrDefault(s => s.Cards.Contains(card));

	// ===== The join, which is the whole point =====

	[Test]
	public void DragonstormsCore_HoldsBothHalvesOfItsConjunction()
	{
		var core = DeckCore.For(Features(), Anchor);

		Assert.That(
			core,
			Is.Not.Null,
			$"{Anchor} asks two answerable demands; it must have a core"
		);
		Assert.Multiple(() =>
		{
			Assert.That(
				SlotContaining(core!, Dragon),
				Is.Not.Null,
				"the fetch target's slot is missing — this is the half a demand-keyed candidate drops"
			);
			Assert.That(
				Rituals.Select(r => SlotContaining(core!, r)).Where(s => s is not null),
				Is.Not.Empty,
				"the storm half is missing"
			);
			Assert.That(
				core!.Slots.Where(s => s.IsIdentity).SelectMany(s => s.Cards),
				Does.Contain(Anchor)
			);
			// The anchor has a slot of its OWN, so mutation cannot satisfy the requirement with an
			// interchangeable payoff and cut the card that was asked for.
			Assert.That(
				core.Slots.Single(s => s.Role == $"Required: {Anchor}").MinCopies,
				Is.GreaterThan(0)
			);
		});
	}

	[Test]
	public void TheSingleDemandView_CannotExpressTheConjunction()
	{
		// **The control.** If any one demand of Dragonstorm already supplied both the dragons and
		// the rituals, then `EngineDiscovery`'s existing demand-keyed candidate would have found
		// this archetype and `DeckCore.For` would be solving nothing.
		var features = Features();
		var demands = features.DemandsOf(Anchor).Where(features.Informative).ToList();

		Assert.That(demands, Has.Count.GreaterThan(1), "the anchor must genuinely ask two things");

		foreach (var d in demands)
		{
			var suppliers = features.SuppliersOf(d).ToHashSet(StringComparer.Ordinal);
			Assert.That(
				suppliers.Contains(Dragon) && Rituals.Any(suppliers.Contains),
				Is.False,
				$"demand '{features.Describe(d)}' supplies both halves on its own, so the "
					+ "conjunction was never the problem and this feature is unnecessary"
			);
		}
	}

	// ===== The subset rule on the payoff slot =====

	[Test]
	public void ThePayoffSlot_TakesCardsNeedingLess_AndRejectsCardsNeedingMore()
	{
		// Redundancy without naming anything: a core that promises storm AND dragons also supports
		// a payoff that only wants storm. The reverse is false — a storm core promises no Dragon,
		// so Dragonstorm resolving in it is the blank card the dead-card rule is named after.
		var features = Features();

		var dragonstorm = DeckCore.For(features, Anchor);
		var tendrils = DeckCore.For(features, StormOnlyPayoff);

		Assert.That(dragonstorm, Is.Not.Null);
		Assert.That(tendrils, Is.Not.Null);
		Assert.Multiple(() =>
		{
			Assert.That(
				dragonstorm!.Slots.Where(s => s.IsIdentity).SelectMany(s => s.Cards),
				Does.Contain(StormOnlyPayoff),
				"a card asking strictly less belongs in the richer core's payoff slot"
			);
			Assert.That(
				tendrils!.Slots.Where(s => s.IsIdentity).SelectMany(s => s.Cards),
				Does.Not.Contain(Anchor),
				"a card asking MORE than the core promises would be dead in it"
			);
		});
	}

	// ===== Shape rules =====

	[Test]
	public void SlotMinimums_AreDerived_NotOneConstant()
	{
		// **The whole point of replacing the flat 8.** A Dragon slot, a ritual slot and a discard
		// outlet are three different requirements, and a single constant said they were the same.
		var features = Features();
		var core = DeckCore.For(features, Anchor);
		Assert.That(core, Is.Not.Null);

		foreach (var s in core!.Slots)
			TestContext.Out.WriteLine(
				$"  {s.MinCopies, 3}x  {s.Role[..Math.Min(58, s.Role.Length)]}"
			);

		var storm = core.Slots.Single(s => !s.IsIdentity && s.Role.Contains("SpellsCastDemand"));
		var dragons = core.Slots.Single(s => !s.IsIdentity && s.Role.Contains("Dragon"));

		Assert.Multiple(() =>
		{
			Assert.That(
				core.Slots.Where(s => !s.IsIdentity).Select(s => s.MinCopies).Distinct().Count(),
				Is.GreaterThan(1),
				"every slot got the same number, so nothing is actually being derived"
			);
			Assert.That(
				storm.MinCopies,
				Is.Not.EqualTo(dragons.MinCopies),
				"a storm count read off the card data and a draw-one consistency count are "
					+ "different questions and must not coincide by construction"
			);
		});
	}

	[Test]
	public void AFetchedCardIsNotCountedAsThoughItMustBeDrawn()
	{
		// **Dragonstorm SEARCHES its Dragons out of the library**, so the consistency rule — how
		// many copies to draw one by turn 5 — asks a question the deck never has to answer. It
		// asked for 11 of the 7 Dragons in the pool, which is a deck nobody would build.
		//
		// The replacement is not "one is enough" either: storm resolves the spell
		// `Math.Max(SpellsCastThisTurn, 1)` times and each copy fetches a Dragon, so the count is
		// coupled to the payoff's OTHER demand. This is the only place the model expresses a
		// dependency between two demands on one card.
		var features = Features();
		var core = DeckCore.For(features, Anchor);
		Assert.That(core, Is.Not.Null);

		var dragons = core!.Slots.Single(s => !s.IsIdentity && s.Role.Contains("Dragon"));
		var storm = core.Slots.Single(s => !s.IsIdentity && s.Role.Contains("SpellsCastDemand"));

		Assert.Multiple(() =>
		{
			Assert.That(
				dragons.MinCopies,
				Is.InRange(2, 6),
				"a fetched slot should sit near the fetch count, not near a draw-one density"
			);
			Assert.That(
				dragons.MinCopies,
				Is.LessThan(storm.MinCopies),
				"you draw rituals and you search for Dragons — the two must not converge"
			);
		});

		// **The floor is deliberately below the historical list.** Standard Dragonstorm ran six
		// Dragons because it wanted a storm count of ~4 turned into lethal, and "enough to kill" is
		// not in the card data. A core is a floor with slack — `ProtectedIn` locks a slot only AT
		// its minimum — so tuning stays free to find six.
		Assert.That(dragons.MinCopies, Is.LessThan(6));
	}

	[Test]
	public void AnEnablerSlotAsksForMoreThanATargetSlot()
	{
		// An outlet has to have RESOLVED before the payoff is worth casting, so it is held to an
		// earlier deadline and therefore a higher density. Without this the split would produce two
		// slots carrying the same number, which is the flat constant wearing two hats.
		var features = PoolFeatures.Build(
			SetRegistry.Combined.Cards.Where(c => !c.HasSubtype("Land")).ToList()
		);

		var payoff = features
			.Demands.Select((_, i) => i)
			.Where(features.Informative)
			.Where(d => features.CausalSuppliersOf(d).Count > 0)
			.Where(d =>
				features.Describe(d).Contains("Graveyard", StringComparison.OrdinalIgnoreCase)
			)
			.OrderByDescending(d => features.SuppliersOf(d).Count)
			.SelectMany(features.AskersOf)
			.First();

		var core = DeckCore.For(features, payoff);
		var enabler = core!.Slots.Single(s => s.Role.EndsWith("[enablers]"));
		var target = core.Slots.Single(s => !s.IsIdentity && !s.Role.EndsWith("[enablers]"));

		TestContext.Out.WriteLine(
			$"{payoff}: target {target.MinCopies}x, enabler {enabler.MinCopies}x"
		);
		Assert.That(enabler.MinCopies, Is.GreaterThan(target.MinCopies));
	}

	[Test]
	public void SatisfyAlwaysPlaysTheAnchor_NotJustSomethingFromItsSlot()
	{
		// **Found by playing the decks, not by reading them.** The payoff slot holds every card whose
		// demands are a SUBSET of the anchor's, so Tendrils of Agony legitimately belongs in a
		// Dragonstorm core — but filling the slot best-first by card value TOOK Tendrils and left
		// Dragonstorm out, producing a deck with four Dragons that nothing fetches. `Holds` returned
		// true the whole time, because the slot was satisfied; the core was not.
		var features = Features();
		var core = DeckCore.For(features, Anchor);
		Assert.That(core, Is.Not.Null);

		var deck = core!.Satisfy(
			Decklist.Empty("t") with
			{
				Lands = Decklist.MinLands,
			},
			new ConstructedValues(DraftTrainingData.Empty, null)
		);

		Assert.That(
			deck.CopiesOf(Anchor),
			Is.GreaterThan(0),
			"the rest of the core exists to serve the anchor's demands, so a deck without it "
				+ "carries slots that answer a question nothing in the list asks"
		);
	}

	[Test]
	public void ACardThatAsksNothing_HasNoCore()
	{
		// The majority of any pool. A vanilla creature is a good-stuff card, not an archetype, and
		// returning an empty core for it would put every card in the exploration queue.
		Assert.That(DeckCore.For(Features(), Vanilla), Is.Null);
	}

	[Test]
	public void AGraveyardCoreSeparatesTheTargetsFromTheOutlets()
	{
		// **The "engine decks are built badly" fix, asserted on the case that produced it.** A
		// reanimation demand is answered by every creature (they can BE in the graveyard) and by the
		// far rarer cards that PUT one there. Merged into one slot, `Satisfy` fills it best-first
		// and the core comes out as fatties with no discard outlet.
		var features = PoolFeatures.Build(
			SetRegistry.Combined.Cards.Where(c => !c.HasSubtype("Land")).ToList()
		);

		// **The GRAVEYARD demand specifically, not "whichever demand has the most causal
		// suppliers".** Picking by count lands on "a Goblin in your hand", where the causal channel
		// is 72 cards against 23 declarative — because the movement rule deliberately requires no
		// filter match on the moved card, so every draw spell counts as putting a Goblin in your
		// hand. That is loose but not wrong, and it means "causal is the scarcer role" holds for
		// zones you have to work to fill and not for the one you draw from every turn.
		var reanimation = features
			.Demands.Select((_, i) => i)
			.Where(features.Informative)
			.Where(d => features.CausalSuppliersOf(d).Count > 0)
			.Where(d =>
				features.Describe(d).Contains("Graveyard", StringComparison.OrdinalIgnoreCase)
			)
			.OrderByDescending(d => features.SuppliersOf(d).Count)
			.First();

		var payoff = features.AskersOf(reanimation).First();
		var core = DeckCore.For(features, payoff);

		Assert.That(core, Is.Not.Null);
		var enablerSlot = core!.Slots.FirstOrDefault(s => s.Role.EndsWith("[enablers]"));
		var targetSlot = core.Slots.FirstOrDefault(s =>
			!s.IsIdentity && !s.Role.EndsWith("[enablers]")
		);

		TestContext.Out.WriteLine(
			$"{payoff}: {core.Slots.Count} slots\n"
				+ string.Join(
					"\n",
					core.Slots.Select(s =>
						$"   {s.MinCopies}x {s.Role[..Math.Min(60, s.Role.Length)]} [{s.Cards.Count}]"
					)
				)
		);

		Assert.Multiple(() =>
		{
			Assert.That(enablerSlot, Is.Not.Null, "no causal slot — the split did not happen");
			Assert.That(targetSlot, Is.Not.Null, "no declarative slot");
			Assert.That(
				enablerSlot!.Cards.Count,
				Is.LessThan(targetSlot!.Cards.Count),
				"causal suppliers must be the SCARCER role — if they are not, the split is "
					+ "separating something other than cause from membership"
			);
			Assert.That(
				enablerSlot.Cards.Intersect(targetSlot.Cards, StringComparer.Ordinal),
				Is.Empty,
				"the two slots must be disjoint or one copy satisfies both"
			);
		});
	}

	[Test]
	public void APayoffNeverSuppliesItsOwnSlot()
	{
		// Slots are counted independently, so a card sitting in two of them would satisfy both off
		// one copy. It is also what gives tribal the right shape — 4 lords in the payoff slot and N
		// OTHER goblins in the support slot, rather than one slot the lords satisfy by themselves.
		var core = DeckCore.For(Features(), Anchor);
		Assert.That(core, Is.Not.Null);

		var identity = core!
			.Slots.Where(s => s.IsIdentity)
			.SelectMany(s => s.Cards)
			.ToHashSet(StringComparer.Ordinal);
		foreach (var slot in core.Slots.Where(s => !s.IsIdentity))
			Assert.That(
				slot.Cards.Intersect(identity, StringComparer.Ordinal),
				Is.Empty,
				$"slot '{slot.Role}' overlaps the payoff slot"
			);
	}

	[Test]
	public void ACoreSurvivesTheEngineReportRoundTrip()
	{
		// **The evolver reads this file, and `CoreSlot.Cards` is an `IReadOnlySet<string>` — an
		// interface, which is exactly the shape that serializes fine and deserializes to nothing.**
		// `Program.cs` only compares engine COUNTS after reloading, and a report whose slots came
		// back empty would pass that check while silently un-constraining every engine slot in the
		// run that consumed it.
		var core = DeckCore.For(Features(), Anchor);
		Assert.That(core, Is.Not.Null);

		var candidate = new EngineCandidate(
			"Engine-0",
			core!,
			core.Name,
			"storm + dragons",
			7,
			Decklist.Empty("d") with
			{
				Lands = Decklist.MinLands,
			},
			[Anchor],
			[Dragon],
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

		var path = Path.Combine(Path.GetTempPath(), $"engines_roundtrip_{Guid.NewGuid():N}.json");
		try
		{
			EngineReportStore.Save(new EngineReport("TEST", 1, 10, [candidate]), path);
			var loaded = EngineReportStore.Load(path);

			Assert.That(loaded, Is.Not.Null);
			var reloaded = loaded!.Engines[0].Core;

			Assert.Multiple(() =>
			{
				Assert.That(reloaded.Slots, Has.Count.EqualTo(core.Slots.Count));
				for (var i = 0; i < core.Slots.Count; i++)
				{
					Assert.That(reloaded.Slots[i].Role, Is.EqualTo(core.Slots[i].Role));
					Assert.That(reloaded.Slots[i].MinCopies, Is.EqualTo(core.Slots[i].MinCopies));
					Assert.That(
						reloaded.Slots[i].Cards,
						Is.EquivalentTo(core.Slots[i].Cards),
						$"slot '{core.Slots[i].Role}' lost its cards in the round trip"
					);
				}
			});
		}
		finally
		{
			if (File.Exists(path))
				File.Delete(path);
		}
	}

	/// <summary>
	/// Prints every core a real pool generates. **Not an assertion — the thing you READ.**
	/// This project has shipped three measurements that produced a plausible column of numbers and
	/// measured nothing, and each was caught by looking at the output rather than the code. Set
	/// `MTG_SET` to a registered code (default ALL) and run it explicitly.
	/// </summary>
	[Test]
	[Explicit("Dump — reads a real pool and prints cores. Minutes, not seconds.")]
	public void DumpCoresForARealPool()
	{
		var code = Environment.GetEnvironmentVariable("MTG_SET") ?? "ALL";
		var set = SetRegistry.Get(code);
		var spells = set.Cards.Where(c => !c.HasSubtype("Land")).ToList();
		var features = PoolFeatures.Build(spells);

		var cores = spells
			.Select(c => DeckCore.For(features, c.Name))
			.OfType<DeckCore>()
			.OrderByDescending(c => c.Slots.Count)
			.ThenBy(c => c.Name, StringComparer.Ordinal)
			.ToList();

		TestContext.Out.WriteLine(
			$"{code}: {spells.Count} spells, {cores.Count} cores "
				+ $"({cores.Count(c => c.Slots.Count > 2)} with a real conjunction)"
		);

		foreach (var core in cores)
		{
			TestContext.Out.WriteLine($"\n--- {core.Name}   ({core.Slots.Count} slots)");
			foreach (var slot in core.Slots)
				TestContext.Out.WriteLine(
					$"    {slot.MinCopies, 3}x  {Trim(slot.Role, 46)}  "
						+ $"[{slot.Cards.Count}] {string.Join(", ", slot.Cards.Order(StringComparer.Ordinal).Take(8))}"
				);
		}

		static string Trim(string s, int n) => s.Length > n ? s[..n] : s.PadRight(n);
	}

	[Test]
	public void EveryGeneratedCore_IsSatisfiableAndLegal()
	{
		// A core nobody can build is worse than no core: `Satisfy` would return an invalid deck and
		// `ProtectedIn` would freeze a slot that can never reach its floor.
		var features = Features();
		var values = new ConstructedValues(DraftTrainingData.Empty, null);
		string[] names = [Anchor, StormOnlyPayoff];

		foreach (var name in names)
		{
			var core = DeckCore.For(features, name);
			Assert.That(core, Is.Not.Null, name);

			var seed = Decklist.Empty("t") with { Lands = Decklist.MinLands };
			var deck = core!.Satisfy(seed, values);

			Assert.Multiple(() =>
			{
				Assert.That(
					core.Holds(deck),
					Is.True,
					$"{name}: Satisfy did not reach the floor — {string.Join(", ", core.Missing(deck))}"
				);
				Assert.That(
					deck.SpellCount + deck.Lands,
					Is.LessThanOrEqualTo(Decklist.DeckSize),
					name
				);
			});
		}
	}
}
