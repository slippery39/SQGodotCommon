using MtgCore;
using MtgCore.Cards.Builders;

namespace MtgSimulator.Tests;

/// <summary>
/// **Does a card that MOVES another card into a demand's zone get credited for it?**
///
/// The demand model is declarative — "which cards ANSWER this question" — and a graveyard spec is
/// answered by *being* a creature card in a graveyard. What a reanimator deck actually needs is a
/// card that PUTS one there, which is a relation between two cards rather than a property of one.
///
/// `PoolFeatures` has a movement rule for this already: if resolving a card moved another into the
/// demand's zone, it supplies the demand and is struck from its askers. It is gated on
/// `DemandZone` finding a single NON-battlefield zone for the demand's candidates. These tests
/// establish where that gate holds and where it does not, before anything is built on top of it.
/// </summary>
[TestFixture]
public class CausalSupplyTests
{
	private static readonly Lazy<(PoolFeatures Features, IReadOnlyList<Card> Spells)> Real =
		new(() =>
		{
			var spells = SetRegistry.Combined.Cards.Where(c => !c.HasSubtype("Land")).ToList();
			return (PoolFeatures.Build(spells), spells);
		});

	/// <summary>
	/// **A blind mover only enables a demand it is likely to hit, and a filling mover still does.**
	///
	/// The movement rule requires no filter match on the card it moved, because you choose what to
	/// pitch. You do NOT choose what you draw — so every draw spell in the pool was credited with
	/// putting "a Goblin in your hand" and Goblin Lackey's enabler slot came out at 72 cards.
	///
	/// Both halves are asserted in ONE pool, and the second is the vacuity guard: a "fix" that
	/// simply switched the causal channel off would pass the first assertion on its own.
	/// </summary>
	[Test]
	public void ABlindDrawDoesNotEnableANarrowHandDemand_ButAMillStillEnablesTheGraveyard()
	{
		// Deliberately NOT a Goblin, so the pool holds exactly one card the hand demand matches.
		var lackey = CardFactory
			.Creature("Lackey", manaCost: 1, power: 1, toughness: 1)
			.WithEtbTrigger(
				"Lackey Trigger",
				eb =>
					eb.WithAction(
						new PutIntoBattlefieldAction(),
						TargetingStrategy.RandomTarget(
							new IsInHandSpecification().And(
								new IsSubtypeSpecification { Subtype = "Goblin" }
							)
						)
					)
			)
			.Build();

		var grunt = CardFactory
			.Creature("Grunt", manaCost: 1, power: 1, toughness: 1)
			.WithSubtype("Goblin")
			.Build();

		Card Bear(string name) =>
			CardFactory.Creature(name, manaCost: 2, power: 2, toughness: 2).Build();

		var divination = CardFactory.Spell("Divination", manaCost: 3).WithDraw(2).Build();
		var millstone = CardFactory.Spell("Millstone", manaCost: 2).WithMill(2).Build();
		var reanimate = CardFactory
			.Spell("Reanimate", manaCost: 2)
			.WithReanimate()
			.WithTarget(TargetBuilder.Single().CreatureInYourGraveyard())
			.Build();

		var features = PoolFeatures.Build(
			[
				lackey,
				grunt,
				Bear("Bear1"),
				Bear("Bear2"),
				Bear("Bear3"),
				divination,
				millstone,
				reanimate,
			]
		);

		// Selected by what the demand SEPARATES rather than by how it is worded: only the Goblin
		// filter tells Grunt apart from a Bear, and only the graveyard spec is answered by a Bear.
		var handGoblin = features
			.DemandsOf("Lackey")
			.Single(d =>
			{
				var s = features.SuppliersOf(d);
				return s.Contains("Grunt") && !s.Contains("Bear1");
			});

		var graveyard = features
			.DemandsOf("Reanimate")
			.Single(d => features.SuppliersOf(d).Contains("Bear1"));

		Assert.Multiple(() =>
		{
			Assert.That(
				features.CausalSuppliersOf(handGoblin),
				Does.Not.Contain("Divination"),
				"a cantrip drawing 2 from a pool with one Goblin in it is not a Goblin enabler"
			);
			Assert.That(
				features.CausalSuppliersOf(graveyard),
				Does.Contain("Millstone"),
				"self-mill into a pool that is mostly creatures IS a graveyard enabler"
			);
		});
	}

	/// <summary>
	/// **A discard outlet fills the graveyard by ASKING A QUESTION, and the probe used to stop at
	/// the question.**
	///
	/// `ProbeCardProfiles` ends in `ProcessAllActions`, which pauses on a `ChoiceAction` — and
	/// `WithDiscard` compiles to a `SelectCardsFromHandAction`. So a looter finished the probe with
	/// its discard still pending, moved nothing, and was struck from the causal channel by the
	/// `MovesInto` lookup that runs BEFORE the DIRECTED/LIKELY gate. Neither rule was ever
	/// consulted, which is why tuning the gate could not have found this.
	///
	/// Measured on ALL when it was fixed: the reanimation enabler slot went 44 → 58 cards, and all
	/// 14 additions were outlets (Faithless Looting, Careful Study, Cathartic Reunion, Tormenting
	/// Voice, Thrill of Possibility, Wild Guess, Smallpox…). Entomb passed throughout — it selects
	/// with a filter and asks nothing — which is exactly what made the gap look like a gate
	/// threshold rather than a missing capability.
	///
	/// **`Divination` is the vacuity guard.** A "fix" that credited every spell, or that resolved
	/// choices by crediting movement it never observed, passes the looter assertion on its own. A
	/// plain draw spell moves nothing to the graveyard and must stay out.
	/// </summary>
	[Test]
	public void ADiscardOutletEnablesTheGraveyard_EvenThoughItsDiscardIsAChoice()
	{
		Card Bear(string name) =>
			CardFactory.Creature(name, manaCost: 2, power: 2, toughness: 2).Build();

		// Draw-then-discard: the discard is a choice, which is the whole point of the fixture.
		var looting = CardFactory
			.Spell("Looting", manaCost: 1)
			.WithDraw(2)
			.WithDiscard()
			.Build();
		var divination = CardFactory.Spell("Divination", manaCost: 3).WithDraw(2).Build();
		var reanimate = CardFactory
			.Spell("Reanimate", manaCost: 2)
			.WithReanimate()
			.WithTarget(TargetBuilder.Single().CreatureInYourGraveyard())
			.Build();

		var features = PoolFeatures.Build(
			[looting, divination, reanimate, Bear("Bear1"), Bear("Bear2"), Bear("Bear3")]
		);

		var graveyard = features
			.DemandsOf("Reanimate")
			.Single(d => features.SuppliersOf(d).Contains("Bear1"));

		Assert.Multiple(() =>
		{
			Assert.That(
				features.CausalSuppliersOf(graveyard),
				Does.Contain("Looting"),
				"a discard outlet is the canonical reanimator enabler; if it is missing here the "
					+ "probe is stopping at the choice again"
			);
			Assert.That(
				features.CausalSuppliersOf(graveyard),
				Does.Not.Contain("Divination"),
				"a plain draw spell puts nothing in the graveyard — crediting it would mean "
					+ "movement is being assumed rather than observed"
			);
		});
	}

	/// <summary>
	/// For every demand, the top suppliers ranked by <c>SupplyOf</c> — the weight that decides what
	/// `SeedConcept` and `DeckCore.Satisfy` actually pick. **Read the graveyard rows**: if the top
	/// of that list is discard outlets and self-mill, causal supply is reaching the sampler. If it
	/// is a flat wall of creatures at supply 1, it is not.
	/// </summary>
	[Test]
	[Explicit("Diagnostic — prints the supply table for a real pool.")]
	public void DumpSupplyWeights()
	{
		var (features, spells) = Real.Value;
		var present = spells.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);

		var rows = Enumerable
			.Range(0, features.Demands.Count)
			.Where(features.Informative)
			.Select(d =>
				(Demand: d, Suppliers: features.SuppliersOf(d).Where(present.Contains).ToList())
			)
			.Where(x => x.Suppliers.Count > 0)
			.OrderByDescending(x => x.Suppliers.Count)
			.ToList();

		TestContext.Out.WriteLine($"{rows.Count} informative demands\n");

		foreach (var (d, suppliers) in rows)
		{
			var weights = suppliers.Select(n => features.SupplyOf(d, n)).ToList();
			var above1 = weights.Count(w => w > 1);

			TestContext.Out.WriteLine(
				$"--- {Trim(features.Describe(d), 70)}\n"
					+ $"    {suppliers.Count} suppliers, max supply {weights.Max()}, "
					+ $"{above1} above 1  ({features.OriginOf(d)})"
			);
			TestContext.Out.WriteLine(
				"    top: "
					+ string.Join(
						", ",
						suppliers.Take(8).Select(n => $"{n}({features.SupplyOf(d, n)})")
					)
			);
		}

		static string Trim(string s, int n) => s.Length > n ? s[..n] : s;
	}

	/// <summary>
	/// **Are there any 2- or 3-card cycles in the produce/consume graph?**
	///
	/// A payoff-anchored `DeckCore` finds every archetype where ONE card declares the whole
	/// conjunction — Dragonstorm names storm and dragons, Atog names artifacts. It cannot reach a
	/// combo whose requirement is split across cards that do not mention each other: Splinter Twin's
	/// demand is "a creature to enchant" and Exarch's is nothing at all, so under demand harvesting
	/// they are two unremarkable cards and the interaction lives only in the composition.
	///
	/// **The edge needs no new machinery.** `A -> B` when A supplies a demand that B asks, which is
	/// `SupplyOf` and `DemandsOf` — both already computed. A card is struck from the askers of any
	/// demand it supplies, so a self-loop cannot occur.
	///
	/// **This is a LOOK, not a feature.** The point is to find out whether this pool contains any
	/// cycles at all. An empty result is a real answer and cancels the combo-verifier work outright;
	/// a curated core-set cube is often deliberately built without infinite combos.
	///
	/// Restricted to NARROW demands. "A creature entered the battlefield" has 453 suppliers and
	/// every creature deck trades it with every other, so at full breadth every pair is a cycle and
	/// the output means nothing.
	/// </summary>
	[Test]
	[Explicit("Diagnostic — enumerates produce/consume cycles. Read the list.")]
	public void DumpProduceConsumeCycles()
	{
		var maxSuppliers = int.TryParse(
			Environment.GetEnvironmentVariable("MTG_MAX_SUPPLIERS"),
			out var m
		)
			? m
			: 60;

		var (features, spells) = Real.Value;
		var present = spells.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);

		var narrow = Enumerable
			.Range(0, features.Demands.Count)
			.Where(features.Informative)
			.Where(d =>
				features.SuppliersOf(d).Count(present.Contains) is > 0 and var n
				&& n <= maxSuppliers
			)
			.ToList();

		var produces = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
		var consumes = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

		foreach (var d in narrow)
		{
			foreach (var n in features.SuppliersOf(d).Where(present.Contains))
				(produces.TryGetValue(n, out var p) ? p : produces[n] = []).Add(d);
			foreach (var n in features.AskersOf(d).Where(present.Contains))
				(consumes.TryGetValue(n, out var c) ? c : consumes[n] = []).Add(d);
		}

		// Only cards that both give and take can sit on a cycle. On ALL this is the difference
		// between a few hundred nodes and eight hundred.
		var nodes = produces
			.Keys.Where(consumes.ContainsKey)
			.Order(StringComparer.Ordinal)
			.ToList();

		var edges = nodes.ToDictionary(
			a => a,
			a => nodes.Where(b => b != a && produces[a].Overlaps(consumes[b])).ToList(),
			StringComparer.Ordinal
		);

		TestContext.Out.WriteLine(
			$"{narrow.Count} narrow demands (<= {maxSuppliers} suppliers), "
				+ $"{nodes.Count} cards that both give and take, "
				+ $"{edges.Sum(e => e.Value.Count)} edges"
		);

		var twos = (
			from a in nodes
			from b in edges[a]
			where StringComparer.Ordinal.Compare(a, b) < 0 && edges[b].Contains(a)
			select (a, b)
		).ToList();

		// **Symmetric on ONE demand is a tribe, not a combo, and it accounts for every cycle found
		// on ALL.** A Goblin lord IS a Goblin and WANTS Goblins, so it sits in that demand's
		// supplier list and its asker list at once — and therefore points at every other Goblin lord
		// automatically. Eight goblin payoffs give 28 pairs that are all one archetype.
		//
		// A real combo trades DIFFERENT resources each way: A gives X, B gives Y. That asymmetry is
		// the discriminator, and it costs one set comparison.
		var (tribal, asymmetric) = (
			twos.Where(t => OnlyDemand(produces, consumes, t.a, t.b) is not null).ToList(),
			twos.Where(t => OnlyDemand(produces, consumes, t.a, t.b) is null).ToList()
		);

		TestContext.Out.WriteLine(
			$"\n=== {twos.Count} two-card cycles: {tribal.Count} symmetric (tribal), "
				+ $"{asymmetric.Count} ASYMMETRIC (combo candidates) ==="
		);

		foreach (var (a, b) in asymmetric.Take(40))
			TestContext.Out.WriteLine(
				$"  CANDIDATE  {a}  <->  {b}\n      {Trade(features, produces, consumes, a, b)}"
			);

		foreach (var (a, b) in tribal.Take(5))
			TestContext.Out.WriteLine($"  (tribal)   {a}  <->  {b}");

		var threes = (
			from a in nodes
			from b in edges[a]
			where StringComparer.Ordinal.Compare(a, b) < 0
			from c in edges[b]
			where StringComparer.Ordinal.Compare(a, c) < 0 && c != b && edges[c].Contains(a)
			select (a, b, c)
		).ToList();

		TestContext.Out.WriteLine($"\n=== {threes.Count} three-card cycles ===");
		foreach (var (a, b, c) in threes.Take(40))
			TestContext.Out.WriteLine($"  {a}  ->  {b}  ->  {c}  -> back");
	}

	/// <summary>
	/// The single demand a pair trades in BOTH directions, or null if they trade different ones.
	/// Null is the interesting answer: the two cards are giving each other different things.
	/// </summary>
	private static int? OnlyDemand(
		Dictionary<string, HashSet<int>> produces,
		Dictionary<string, HashSet<int>> consumes,
		string a,
		string b
	)
	{
		var forward = produces[a].Intersect(consumes[b]).ToHashSet();
		var back = produces[b].Intersect(consumes[a]).ToHashSet();
		return forward.Count == 1 && back.SetEquals(forward) ? forward.Single() : null;
	}

	private static string Trade(
		PoolFeatures features,
		Dictionary<string, HashSet<int>> produces,
		Dictionary<string, HashSet<int>> consumes,
		string a,
		string b
	)
	{
		var forward = produces[a].Intersect(consumes[b]).Select(features.Describe).FirstOrDefault();
		var back = produces[b].Intersect(consumes[a]).Select(features.Describe).FirstOrDefault();
		return $"{a} gives [{Short(forward)}] | {b} gives [{Short(back)}]";

		static string Short(string? s) =>
			s is null ? "?"
			: s.Length > 52 ? s[..52]
			: s;
	}

	/// <summary>
	/// The specific claim CLAUDE.md records as an open limitation: a reanimation demand is answered
	/// by every creature, so a value-ranked sample never draws the dozen cards that actually fill a
	/// graveyard. If causal supply works, the outlets outrank the creatures.
	/// </summary>
	[Test]
	[Explicit("Diagnostic — is the reanimator slot still a wall of creatures?")]
	public void DumpGraveyardDemandSupply()
	{
		var (features, spells) = Real.Value;
		var present = spells.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);

		var graveyard = Enumerable
			.Range(0, features.Demands.Count)
			.Where(features.Informative)
			.Where(d =>
				features.Describe(d).Contains("Graveyard", StringComparison.OrdinalIgnoreCase)
			)
			.ToList();

		TestContext.Out.WriteLine($"{graveyard.Count} graveyard-named demands\n");

		foreach (var d in graveyard)
		{
			var suppliers = features.SuppliersOf(d).Where(present.Contains).ToList();
			var weighted = suppliers.Where(n => features.SupplyOf(d, n) > 1).ToList();

			TestContext.Out.WriteLine($"--- {features.Describe(d)}   ({features.OriginOf(d)})");
			TestContext.Out.WriteLine(
				$"    {suppliers.Count} suppliers, {weighted.Count} with supply > 1"
			);
			TestContext.Out.WriteLine(
				"    top 12: "
					+ string.Join(
						", ",
						suppliers.Take(12).Select(n => $"{n}({features.SupplyOf(d, n)})")
					)
			);
			TestContext.Out.WriteLine(
				"    askers: " + string.Join(", ", features.AskersOf(d).Take(12))
			);
		}
	}
}
