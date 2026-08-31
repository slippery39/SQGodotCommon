using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgSimulator.Tests;

/// <summary>
/// Drives <see cref="PoolFeatures"/>. The fast tests pin the harvest rules; the sweep prints the
/// table and is <c>[Explicit]</c> because it walks and evaluates a whole set.
///
/// Inline card definitions throughout, never `CardLibrary` lookups, so a balance pass cannot
/// break these.
/// </summary>
[TestFixture]
public class CardDemandSweep
{
	private static Card Bear(string name = "Bear") =>
		CardFactory.Creature(name, manaCost: 2, power: 2, toughness: 2).Build();

	private static Card Goblin(string name) =>
		CardFactory
			.Creature(name, manaCost: 1, power: 1, toughness: 1)
			.WithSubtype("Goblin")
			.Build();

	/// A lord: "other Goblins you control get +1/+1", the Goblin Chieftain shape.
	private static Card Lord() =>
		CardFactory
			.Creature("Lord", manaCost: 3, power: 2, toughness: 2)
			.WithSubtype("Goblin")
			.WithComponent(
				new StaticPTBoostAbility
				{
					PowerBonus = 1,
					ToughnessBonus = 1,
					Filter = new IsSubtypeSpecification { Subtype = "Goblin" }.And(
						new IsNotSelfSpecification()
					),
				}
			)
			.Build();

	/// <summary>
	/// The core claim: a card's own filter is lifted off it and answered by the pool, with nothing
	/// anywhere naming a tribe.
	/// </summary>
	[Test]
	public void ALordDemandsItsTribe_AndOnlyTribeMembersAnswer()
	{
		var features = PoolFeatures.Build([Lord(), Goblin("Grunt"), Bear()]);

		var demands = features.DemandsOf("Lord");
		Assert.That(demands, Has.Count.EqualTo(1), $"harvested: {Describe(features, "Lord")}");

		var goblinDemand = demands[0];
		Assert.Multiple(() =>
		{
			Assert.That(features.SupplyOf(goblinDemand, "Grunt"), Is.EqualTo(1));
			Assert.That(features.SupplyOf(goblinDemand, "Bear"), Is.EqualTo(0));
		});
	}

	/// <summary>
	/// The walk has to be generic, and this is why: a tutor's subtype sits inside a
	/// `PipelineAction` inside a `CardEffect` inside a `SpellComponent`. This is the Dragonstorm
	/// shape verbatim, and Dragonstorm sitting in dragonless AI decks for whole runs is the defect
	/// this whole feature exists to catch.
	/// </summary>
	[Test]
	public void ATutorDemandsWhatItSearchesFor_EvenNestedInsideAPipeline()
	{
		var stormCall = new Card
		{
			Name = "Storm Call",
			ManaCost = 7,
			Components = ImmutableArray.Create<GameComponent>(
				new SpellComponent
				{
					Effects = ImmutableList.Create(
						new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new SelectCardFromLibraryAction
									{
										Subtype = "Dragon",
										OutputKey = "fetched",
									},
									new PutIntoBattlefieldAction { CardIdContextKey = "fetched" }
								),
							},
						}
					),
				}
			),
		};

		var wyrm = CardFactory
			.Creature("Wyrm", manaCost: 6, power: 5, toughness: 5)
			.WithSubtype("Dragon")
			.Build();

		var features = PoolFeatures.Build([stormCall, wyrm, Bear()]);

		var demands = features.DemandsOf("Storm Call");
		Assert.That(
			demands,
			Has.Count.EqualTo(1),
			"a filter four levels deep must still be found, or the walk is not generic"
		);

		Assert.Multiple(() =>
		{
			Assert.That(features.SupplyOf(demands[0], "Wyrm"), Is.EqualTo(1));
			Assert.That(features.SupplyOf(demands[0], "Bear"), Is.EqualTo(0));
		});
	}

	/// <summary>
	/// **The counterweight, and the test that stops this being vacuous.** A version that harvested
	/// every spec on a card would pass every other test in this file while making a burn spell
	/// "demand creatures" — which would then read as satisfied by any creature deck and make the
	/// whole signal meaningless.
	///
	/// The separation is positional: `TargetingStrategy.Specification` is what a spell aims at
	/// (usually the opponent's board), `Filter` is which cards qualify.
	/// </summary>
	[Test]
	public void ATargetingClauseIsNotADemand()
	{
		var bolt = CardFactory
			.Spell("Bolt", manaCost: 1)
			.WithDamage(3)
			.WithTarget(Single().PlayersOrCreatures())
			.Build();

		var features = PoolFeatures.Build([bolt, Bear()]);

		Assert.That(
			features.DemandsOf("Bolt"),
			Is.Empty,
			$"harvested: {Describe(features, "Bolt")} — a spell aiming at the opponent's board "
				+ "does not ask anything of YOUR deck"
		);
	}

	/// <summary>
	/// **"Asks nothing" and "asks and gets nothing" must never collapse to the same number.**
	/// The entire dead-card rule rests on this: Bolt is fine in any deck, a dragonless Dragonstorm
	/// is a blank card holding a slot, and before this they were indistinguishable.
	/// </summary>
	[Test]
	public void ACardThatAsksNothingIsNotDead_ButAnUnansweredOneIs()
	{
		var bolt = CardFactory
			.Spell("Bolt", manaCost: 1)
			.WithDamage(3)
			.WithTarget(Single().PlayersOrCreatures())
			.Build();

		var features = PoolFeatures.Build([Lord(), Goblin("Grunt"), Bear(), bolt]);

		var dragonless = Decklist
			.Empty("no tribe")
			.WithCopies("Lord", 4)
			.WithCopies("Bear", 4)
			.WithCopies("Bolt", 4);

		Assert.Multiple(() =>
		{
			Assert.That(
				features.Satisfaction("Bolt", dragonless),
				Is.NaN,
				"a card with no demands asks nothing and can never be dead"
			);
			Assert.That(
				features.Satisfaction("Lord", dragonless),
				Is.Zero,
				"a lord with no tribe alongside it is asking and getting nothing"
			);
			Assert.That(features.DeadCards(dragonless), Is.EquivalentTo(new[] { "Lord" }));
		});

		var tribal = dragonless.WithCopies("Grunt", 4);
		Assert.That(
			features.Satisfaction("Lord", tribal),
			Is.EqualTo(4),
			"four Goblins is four bodies of supply"
		);
		Assert.That(features.DeadCards(tribal), Is.Empty);
	}

	/// <summary>
	/// A token maker supplies the bodies it makes without being one itself — the
	/// anthem-plus-cheap-tokens deck, which is a real synergy deck even though "creature" is the
	/// least restrictive demand there is. Rarity of a demand says nothing; density does.
	/// </summary>
	[Test]
	public void ATokenMakerSuppliesTheBodiesItMakes()
	{
		var token = CardFactory
			.Creature("Goblin Token", manaCost: 0, power: 1, toughness: 1)
			.WithSubtype("Goblin")
			.Build();

		var swarm = CardFactory.Spell("Swarm", manaCost: 3).WithCreateTokens(token, 3).Build();

		var features = PoolFeatures.Build([Lord(), swarm, Bear()]);
		var goblinDemand = features.DemandsOf("Lord")[0];

		Assert.That(
			features.SupplyOf(goblinDemand, "Swarm"),
			Is.EqualTo(3),
			"one card, three bodies — counting the maker as a single Goblin would understate "
				+ "every token deck in the pool"
		);
	}

	/// <summary>
	/// Atog and Thoughtcast want the same deck, and this is where that falls out: a sacrifice cost
	/// filtered to Artifact produces the SAME demand object as any other card asking about
	/// artifacts, so record equality groups them with nothing said about either card.
	/// </summary>
	[Test]
	public void TwoCardsAskingTheSameQuestionShareOneDemand()
	{
		var atog = CardFactory
			.Creature("Atog", manaCost: 2, power: 1, toughness: 2)
			.WithSacrificeSubtypeCost("Artifact")
			.Build();

		var ravager = CardFactory
			.Creature("Ravager", manaCost: 3, power: 2, toughness: 2)
			.WithSacrificeSubtypeCost("Artifact")
			.Build();

		var trinket = CardFactory
			.Creature("Trinket", manaCost: 1, power: 1, toughness: 1)
			.WithSubtype("Artifact")
			.Build();

		var features = PoolFeatures.Build([atog, ravager, trinket, Bear()]);

		Assert.That(
			features.DemandsOf("Atog"),
			Is.EquivalentTo(features.DemandsOf("Ravager")),
			"the same question asked by two cards must dedupe to one demand, or a concept "
				+ "fragments into one bucket per card and nothing ever reaches critical mass"
		);
		Assert.That(features.SupplyOf(features.DemandsOf("Atog")[0], "Trinket"), Is.EqualTo(1));
	}

	/// <summary>
	/// A spec that throws when asked about a candidate is an engine bug — see the "targeting specs
	/// must never index the object map" rule in MtgCore/CLAUDE.md, which cost a killed training
	/// game. Surfaced rather than swallowed, and asserted clean on a real set by the sweep below.
	/// </summary>
	[Test]
	public void NoSpecThrowsOnAnOrdinaryPool()
	{
		var features = PoolFeatures.Build([Lord(), Goblin("Grunt"), Bear()]);
		Assert.That(features.Failures, Is.Empty);
	}

	/// <summary>
	/// The probe, and the reason it had to exist: a trigger's demand is split between its event
	/// type and its filter, and reading the filter alone gives `{ControlledByYou, NotSelf}` —
	/// answered by every card in the pool, and therefore dropped as saying nothing.
	///
	/// Answered by playing each card and asking the condition about the events it really emits,
	/// the same trigger correctly demands creatures. **No event type name appears in
	/// `PoolFeatures`** — the engine supplies the events, so a new one works the day it is added.
	/// </summary>
	[Test]
	public void ATriggerDemandsWhateverActuallyFiresIt()
	{
		var watcher = CardFactory
			.Creature("Watcher", manaCost: 3, power: 2, toughness: 2)
			.WithComponent(
				new TriggeredAbilityComponent
				{
					Name = "Draw on entry",
					Condition = new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
						Filter = new IsControlledByYouSpecification().And(
							new IsNotSelfSpecification()
						),
					},
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.NoTarget(),
						ActionTemplate = new DrawCardsAction { Amount = 1 },
					},
				}
			)
			.Build();

		var enchantment = CardFactory.Enchantment("Shrine", manaCost: 2).Build();

		var features = PoolFeatures.Build([watcher, Bear(), Goblin("Grunt"), enchantment]);

		var demands = features.DemandsOf("Watcher");
		Assert.That(demands, Has.Count.EqualTo(1), $"harvested: {Describe(features, "Watcher")}");

		Assert.Multiple(() =>
		{
			Assert.That(
				features.SupplyOf(demands[0], "Bear"),
				Is.EqualTo(1),
				"a creature entering the battlefield is what fires this trigger"
			);
			Assert.That(features.SupplyOf(demands[0], "Grunt"), Is.EqualTo(1));
			Assert.That(
				features.SupplyOf(demands[0], "Shrine"),
				Is.Zero,
				"an enchantment fires no creature-entered trigger, so it does not answer this "
					+ "demand — without that the demand is 'every card' and says nothing"
			);
		});
	}

	/// <summary>
	/// The cost probe, and the case it exists for. `AffinityComponent` is a marker with no data —
	/// its meaning is a subtraction inside `CostEngine` — so Thoughtcast states nothing at all and
	/// the harvest cannot see it. Without this, seeding a deck on the artifact concept would pull
	/// in the artifacts and leave out the payoff that wants them: a half-built Affinity deck,
	/// which is the exact failure this feature exists to fix.
	///
	/// Note what the test does NOT do: it never mentions affinity. The artifact demand exists only
	/// because a *different* card (a sacrifice cost) names artifacts, and the probe discovers that
	/// the affinity card wants the same thing purely because its cost moved.
	/// </summary>
	[Test]
	public void ACardWhoseCostMovesDiscoversTheDemandItNeverStates()
	{
		var trinket = CardFactory
			.Creature("Trinket", manaCost: 1, power: 1, toughness: 1)
			.WithSubtype("Artifact")
			.Build();

		// Names artifacts, which is the only reason the demand exists at all.
		var atog = CardFactory
			.Creature("Atog", manaCost: 2, power: 1, toughness: 2)
			.WithSacrificeSubtypeCost("Artifact")
			.Build();

		// States nothing. Its whole relationship to artifacts lives in CostEngine.
		var thoughtcast = CardFactory
			.Spell("Thoughtcast", manaCost: 4)
			.WithDraw(2)
			.WithComponent(new AffinityComponent())
			.Build();

		var features = PoolFeatures.Build([trinket, atog, thoughtcast, Bear()]);

		var artifactDemand = features.DemandsOf("Atog").Single();

		Assert.That(
			features.DemandsOf("Thoughtcast"),
			Does.Contain(artifactDemand),
			"the affinity card must land on the SAME demand the sacrifice cost named, or the two "
				+ "halves of an artifact deck are separate concepts and neither can build it"
		);

		Assert.That(
			features.DemandsOf("Bear"),
			Is.Empty,
			"a card whose cost does not move must not pick up the demand — without this the probe "
				+ "would hand every card in the pool every demand"
		);

		var artifactless = Decklist.Empty("no artifacts").WithCopies("Bear", 4);
		var artifacts = Decklist.Empty("artifacts").WithCopies("Trinket", 4);

		Assert.Multiple(() =>
		{
			Assert.That(features.Satisfaction("Thoughtcast", artifactless), Is.Zero);
			Assert.That(features.Satisfaction("Thoughtcast", artifacts), Is.EqualTo(4));
		});
	}

	private static string Describe(PoolFeatures features, string card) =>
		features.DemandsOf(card).Count == 0
			? "(none)"
			: string.Join("; ", features.DemandsOf(card).Select(features.Describe));

	/// <summary>
	/// Prints what the extractor finds on a real set. **Read this before anything is wired into
	/// `DeckBuilder`** — the verification the plan calls for is done by eye:
	///
	/// 1. Are the top demands mechanically legible, or noise?
	/// 2. Do artifact-sacrifice and affinity land on the same demand?
	/// 3. How many cards ask for something the pool cannot answer at all?
	/// 4. How many cards have NO demand — that is the size of the gap board probes would fill.
	/// </summary>
	[Test]
	[Explicit("Dump — walks, evaluates and probes a whole set.")]
	public void DumpDemands() =>
		Dump(
			SetRegistry.All.FirstOrDefault(s =>
				string.Equals(s.Code, "CSC", StringComparison.OrdinalIgnoreCase)
			) ?? SetRegistry.Default
		);

	/// <summary>
	/// The combined pool is the real target — LGC + HLM + CSC as one format. Separate from the CSC
	/// dump because it is the one that has to stay affordable as sets are added, and because the
	/// cross-set demands only exist here: Atog is in LGC and every artifact it wants is in CSC, a
	/// pair no amount of measured co-occurrence could ever have data for.
	/// </summary>
	[Test]
	[Explicit("Dump — the whole combined card pool.")]
	public void DumpDemandsForTheWholePool() => Dump(SetRegistry.Combined);

	private static void Dump(CardSet set)
	{
		var pool = set.Cards.Where(c => !c.HasSubtype("Land")).ToList();
		var clock = System.Diagnostics.Stopwatch.StartNew();
		var features = PoolFeatures.Build(pool);
		clock.Stop();

		var withDemands = pool.Count(c => features.DemandsOf(c.Name).Count > 0);

		Console.WriteLine(
			$"{set.Code}: {pool.Count} spells, {features.Demands.Count} distinct "
				+ $"demands, {withDemands} cards asking for something ({(double)withDemands / pool.Count:P0}), "
				+ $"built in {clock.ElapsedMilliseconds} ms"
		);

		if (features.Failures.Count > 0)
		{
			Console.WriteLine($"\n!! {features.Failures.Count} SPECS THREW — engine bugs:");
			foreach (var f in features.Failures)
				Console.WriteLine($"   {f}");
		}

		Console.WriteLine("\nDemands by how much deck they could fill:");
		var ranked = Enumerable
			.Range(0, features.Demands.Count)
			.OrderByDescending(features.SuppliersInPool)
			.ToList();

		foreach (var d in ranked.Take(30))
			Console.WriteLine(
				$"  {features.SuppliersInPool(d), 4} suppliers  [{features.OriginOf(d)}]  {features.Describe(d)}"
			);

		var unanswerable = ranked
			.Where(d => features.SuppliersInPool(d) == 0 && !features.LandsAnswer(d))
			.ToList();
		Console.WriteLine(
			$"\n{unanswerable.Count} demands NOTHING in the pool answers "
				+ "(a card carrying only these is dead in every deck):"
		);
		foreach (var d in unanswerable.Take(20))
			Console.WriteLine($"  [{features.OriginOf(d)}]  {features.Describe(d)}");

		Console.WriteLine("\nCards asking nothing (the board-probe gap):");
		var silent = pool.Where(c => features.DemandsOf(c.Name).Count == 0).Select(c => c.Name);
		Console.WriteLine("  " + string.Join(", ", silent.Take(40)));
	}
}
