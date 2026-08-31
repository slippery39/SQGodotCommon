using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// What every card in a pool ASKS ABOUT the rest of your deck, and which cards ANSWER.
///
/// **Nothing here maps a mechanic to a meaning.** There is no table saying "AffinityComponent
/// wants artifacts" or "this subtype is a tribe" — such a table has to be hand-maintained, drifts
/// from what the cards do, and cannot say anything about a mechanic that does not exist yet. The
/// whole point of this mode is that the AI finds synergies with no input from us.
///
/// Two facts about the engine make that possible, and both were verified rather than assumed:
///
/// 1. **A <see cref="TargetSpecification"/> is already a serializable predicate over cards**, and
///    <c>IsSatisfiedBy</c> evaluates it against any <see cref="GameState"/>. So a demand is not a
///    translation of the card — it IS the card's own filter object, lifted off it and run against
///    the pool. Nothing in between has to know what a Goblin is.
/// 2. **The engine names that role consistently.** Every `TargetSpecification`-typed property in
///    MtgCore is called `Filter` or `AppliesTo` ("which cards qualify" — a demand) except
///    `TargetingStrategy.Specification` ("what am I aiming at" — not a demand, since Lightning
///    Bolt's "any creature" is about the opponent's board, not yours). Likewise a `string`
///    property named `Subtype` always means "cards of this subtype qualify".
///
/// So the harvest rule is two lines rather than a mechanic list, and a card using a mechanic
/// nobody has written yet is picked up the day it puts a filter in one of those slots.
///
/// `TargetSpecification` is an abstract **record**, so value equality dedupes demands pool-wide
/// for free: Goblin Chieftain and Goblin King produce equal `IsSubtypeSpecification{Goblin}`
/// objects and land in the same bucket with no key function written anywhere.
///
/// Verified worked examples, both read out of source rather than reasoned about:
/// <list type="bullet">
/// <item>Goblin Chieftain — `StaticPTBoostAbility.Filter = IsSubtypeSpecification{Goblin}.And(…)`
///   (`CardLibrary.cs:540`)</item>
/// <item>Atog — `SacrificeAdditionalCost.Filter = IsSubtypeSpecification{Artifact}`
///   (`CardLibrary.cs:1661`), which is the SAME demand Thoughtcast's affinity wants, so the two
///   are grouped with nothing said about either card</item>
/// <item>Dragonstorm — `SelectCardFromLibraryAction{Subtype = Dragon}`, nested inside a
///   `PipelineAction` inside a `CardEffect` (`CardLibrary.cs:1026`). This is why the walk is
///   generic: hand-navigating to that property would be its own maintenance burden.</item>
/// </list>
///
/// **What this deliberately does NOT do is score anything.** There are no weights here and no
/// learned synergy value. Features exist to GENERATE deck proposals; the measured win rate is
/// still the only thing that judges one. That is the same argument `DeckBuilder.Package` already
/// makes for the pair table, and it is why a wrong feature costs a few generations instead of
/// silently biasing every decision forever.
///
/// **Known gap: demands buried in engine CODE rather than card DATA are not found here.**
/// `AffinityComponent` carries no data at all — its meaning is a subtraction inside `CostEngine` —
/// so affinity cards come back with no demand from their own component (Thoughtcast is only
/// covered because other cards name Artifact). Same for storm, threshold and graveyard-count. The
/// planned answer is differential board probes (mutate the board, watch `ComputeEffectiveCost` /
/// `GetEffectiveStats` move), which is also generic. Read the dump first: it says how much of the
/// pool actually needs it.
/// </summary>
public sealed class PoolFeatures
{
	/// <summary>
	/// Property names that mean "which cards qualify". Checked against the property the spec was
	/// found ON, never against the spec's own type — the distinction between a demand and a
	/// targeting clause is positional, and the engine already holds that convention everywhere.
	/// </summary>
	private static readonly HashSet<string> DemandProperties =
		new(StringComparer.Ordinal) { "Filter", "AppliesTo" };

	/// Depth cap on the object walk. Purely a runaway guard; the deepest real card is ~6.
	private const int MaxWalkDepth = 12;

	/// <summary>
	/// Specs that ask about ONE SPECIFIC OBJECT rather than about a kind of card — this card
	/// itself, the creature wearing it, the other player's stuff.
	///
	/// **Found by reading the first dump, not guessed.** All three came back answered by nothing
	/// in the pool, which would have marked every death trigger, every equipment and every
	/// opponent-watching trigger as a DEAD CARD — the precise failure the dead-card rule exists to
	/// prevent, running backwards. A death trigger asks nothing of your deck; it is not starving.
	///
	/// This is a list of three specification semantics, not of mechanics or cards, and it sits in
	/// the same category as the Filter-versus-Specification positional rule. The general form is
	/// empirical — a spec is object-referential if its answer moves when `SourceCardId` or the
	/// controller varies while the candidate is held fixed — and is worth building only if this
	/// list ever needs a fourth entry.
	/// </summary>
	private static readonly HashSet<Type> ObjectReferentialSpecs =
	[
		typeof(IsSourceCardSpecification),
		typeof(IsEquippedBySourceSpecification),
		typeof(IsControlledByOpponentSpecification),
	];

	/// <summary>
	/// Above this share of the pool, a demand is dropped as uninformative.
	///
	/// **This is not the rarity cutoff that was considered and rejected.** Rarity says nothing
	/// about whether a concept is real: "creatures you control" is answered by 58% of CSC and is a
	/// perfectly good concept — anthems plus cheap token makers is a synergy deck, while the same
	/// card in a six-creature midrange pile is a bad card. Density decides that, not rarity, and
	/// nothing here touches it.
	///
	/// What this drops is the degenerate tail: the first dump had `IsNotSelfSpecification` and
	/// `IsControlledByYouSpecification` answered by **408 of 408** cards, because they are
	/// structural qualifiers on a trigger ("whenever ANOTHER creature YOU CONTROL enters") rather
	/// than questions about your deck. A demand every card answers contributes the same constant
	/// to every possible deck, so it cannot separate two decks and cannot inform any decision.
	/// The threshold is set just under 1.0 for exactly that reason, not tuned for selectivity.
	/// </summary>
	private const double UninformativeShare = 0.99;

	/// <summary>
	/// A demand is either a <see cref="TargetSpecification"/> (a filter naming a kind of card) or
	/// a <see cref="TriggerCondition"/> (an event the card reacts to). Held as `object` because
	/// both are records — value equality dedupes either kind pool-wide for free — and because
	/// nothing downstream needs to tell them apart: supply is resolved at build time and every
	/// consumer works on indices.
	/// </summary>
	private readonly List<object> _demands;
	private readonly List<string> _origins;
	private readonly Dictionary<string, int[]> _demandsOf;
	private readonly Dictionary<string, int>[] _supply;
	private readonly bool[] _landSupply;

	private PoolFeatures(
		List<object> demands,
		List<string> origins,
		Dictionary<string, int[]> demandsOf,
		Dictionary<string, int>[] supply,
		bool[] landSupply,
		IReadOnlyList<string> failures
	)
	{
		_demands = demands;
		_origins = origins;
		_demandsOf = demandsOf;
		_supply = supply;
		_landSupply = landSupply;
		Failures = failures;
	}

	public IReadOnlyList<object> Demands => _demands;

	/// <summary>
	/// Specs that threw when evaluated against a candidate, by description.
	///
	/// Reported rather than swallowed. A spec must return "not a match" for an id it cannot make
	/// sense of — see the "targeting specs must never index the object map" rule in
	/// MtgCore/CLAUDE.md, which is a live crash this project has already paid for once. An entry
	/// here is an engine bug, not a quirk of this file.
	/// </summary>
	public IReadOnlyList<string> Failures { get; }

	/// Demand indices this card asks about. Empty means the card asks nothing of your deck.
	public IReadOnlyList<int> DemandsOf(string cardName) =>
		_demandsOf.TryGetValue(cardName, out var d) ? d : [];

	/// How many bodies/cards <paramref name="supplier"/> contributes to one demand. 0 is "no".
	public int SupplyOf(int demandIndex, string supplier) =>
		_supply[demandIndex].GetValueOrDefault(supplier);

	/// How many distinct pool cards answer a demand — how much deck a concept could fill.
	public int SuppliersInPool(int demandIndex) => _supply[demandIndex].Count;

	/// Whether the mana base answers this demand. Lands are a count on Decklist, not pool cards,
	/// so a demand can read 0 suppliers here and still be the best-supplied thing in a deck.
	public bool LandsAnswer(int demandIndex) => _landSupply[demandIndex];

	/// <summary>
	/// Whether a demand can inform any decision — i.e. whether anything available answers it.
	///
	/// **A demand nothing answers is ignored rather than treated as starving, and that direction
	/// is deliberate.** Some triggers fire on things no card supplies: "at the start of your
	/// upkeep", "whenever you gain 5 life", "whenever a creature attacks" (which this probe does
	/// not perform). Those are unconditional or unprobed, not unsatisfied, and counting them would
	/// mark every mana dork and every upkeep payoff in the pool as a DEAD CARD — the rule running
	/// backwards, which is exactly how the object-referential specs went wrong one step earlier.
	///
	/// The cost is a false NEGATIVE: in a pool containing no Dragons at all, Dragonstorm is dead
	/// in every deck and this will not say so. That is the safe direction — a missed warning
	/// leaves a bad card in a deck, while a false positive deletes a good one — and it does not
	/// touch the case the rule exists for, since a pool that HAS dragons gives the demand real
	/// suppliers and an actual dragonless deck still reads zero.
	/// </summary>
	private bool Informative(int demandIndex) =>
		_supply[demandIndex].Count > 0 || _landSupply[demandIndex];

	/// Every pool card answering a demand, best supply first. The candidate set for a concept.
	public IReadOnlyList<string> SuppliersOf(int demandIndex) =>
		_supply[demandIndex]
			.OrderByDescending(kv => kv.Value)
			.ThenBy(kv => kv.Key, StringComparer.Ordinal)
			.Select(kv => kv.Key)
			.ToList();

	/// Human-readable form of a demand. Records generate this, so it costs nothing and cannot
	/// drift from the object being evaluated.
	public string Describe(int demandIndex) => _demands[demandIndex].ToString() ?? "?";

	/// Where in the card graph this demand was found — diagnostics for reading the dump.
	public string OriginOf(int demandIndex) => _origins[demandIndex];

	/// <summary>
	/// How much of what this card asks for the deck actually supplies.
	///
	/// **<see cref="double.NaN"/> means the card asks nothing** — a vanilla creature, a burn
	/// spell — and is emphatically NOT the same as 0, which means it asks and the deck answers
	/// with nothing. That distinction is the whole dead-card rule: Dragonstorm in a deck with no
	/// dragons is a blank card holding a slot, and it sat in AI-built decks for generations
	/// because nothing in the mode could tell those two states apart.
	///
	/// A card never supplies its own demand. Four Goblin Chieftains genuinely do support each
	/// other, so this understates that case — deliberately, because the question being asked is
	/// "does the REST of the deck support this card", and understating is the safe direction for
	/// a rule that deletes cards.
	/// </summary>
	public double Satisfaction(string cardName, Decklist deck)
	{
		var demands = DemandsOf(cardName).Where(Informative).ToList();
		if (demands.Count == 0)
			return double.NaN;

		var total = 0.0;
		foreach (var d in demands)
		{
			var supply = _supply[d];
			foreach (var (name, copies) in deck.Spells)
			{
				if (string.Equals(name, cardName, StringComparison.Ordinal))
					continue;
				if (supply.TryGetValue(name, out var per))
					total += per * copies;
			}

			// The mana base is a count on Decklist rather than entries in Spells, so a demand
			// answered by a land has to read it from there or it reports zero — "discard a land
			// card" in a 24-land deck is the best-supplied demand there is.
			if (_landSupply[d])
				total += deck.Lands;
		}
		return total;
	}

	/// <summary>
	/// Cards in the deck that ask for something and get nothing — the Dragonstorm-with-no-dragons
	/// set. Cards asking nothing are never listed.
	/// </summary>
	public IReadOnlyList<string> DeadCards(Decklist deck) =>
		deck
			.Spells.Keys.Where(n => Satisfaction(n, deck) == 0)
			.OrderBy(n => n, StringComparer.Ordinal)
			.ToList();

	/// <summary>
	/// Harvests every demand in the pool and measures which pool cards answer it.
	///
	/// Deterministic and pure: same pool in, same features out. Cost is
	/// demands x cards x zones evaluations of a cheap predicate.
	/// </summary>
	public static PoolFeatures Build(IReadOnlyList<Card> pool)
	{
		// --- 1. Harvest, deduping by record value equality ---
		var index = new Dictionary<object, int>();
		var demands = new List<object>();
		var origins = new List<string>();
		var demandsOf = new Dictionary<string, int[]>(StringComparer.Ordinal);
		var produces = new Dictionary<string, List<(Card Token, int Count)>>(
			StringComparer.Ordinal
		);

		foreach (var card in pool)
		{
			if (demandsOf.ContainsKey(card.Name))
				continue; // duplicate name across merged sets; last-registered-wins, same as the pool

			var found = new List<(object Demand, string Origin)>();
			var tokens = new List<(Card, int)>();
			Walk(card, found, tokens, new HashSet<object>(ReferenceEqualityComparer.Instance), 0);

			var mine = new List<int>();
			foreach (var (spec, origin) in found)
			{
				if (IsObjectReferential(spec))
					continue;
				if (!index.TryGetValue(spec, out var i))
				{
					i = demands.Count;
					index[spec] = i;
					demands.Add(spec);
					origins.Add(origin);
				}
				if (!mine.Contains(i))
					mine.Add(i);
			}

			demandsOf[card.Name] = [.. mine];
			produces[card.Name] = tokens;
		}

		// --- 2. Fixture: every pool card placed in every zone a filter might look in ---
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var zones = new[]
		{
			ids.Player1BattlefieldId,
			ids.Player1HandId,
			ids.Player1GraveyardId,
			ids.Player1LibraryId,
		};

		// A source that is none of the candidates, so IsNotSelfSpecification passes for all of
		// them — a lord's "other creatures you control" must not exclude the card being tested.
		Card sourceCard =
			new()
			{
				Name = "__source",
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
			};
		(state, var placedSource) = state.AddObject(sourceCard, parentId: ids.Player1BattlefieldId);
		var sourceId = placedSource.Id;

		var placements = new Dictionary<string, List<int>>(StringComparer.Ordinal);

		void Place(Card template, string key)
		{
			if (placements.ContainsKey(key))
				return;
			var spots = new List<int>(zones.Length);
			foreach (var zone in zones)
			{
				(state, var placed) = state.AddObject(
					template with
					{
						OwnerId = ids.Player1Id,
						ControllerId = ids.Player1Id,
					},
					parentId: zone
				);
				spots.Add(placed.Id);
			}
			placements[key] = spots;
		}

		foreach (var card in pool)
			Place(card, card.Name);

		// A mana base is a scalar on Decklist, not entries in the spell pool, so nothing above can
		// answer "discard a land card" — `DiscardAdditionalCost.Filter = Land` came back with zero
		// suppliers on the first dump and would have marked Molten Vortex dead in every deck ever
		// built. A deck holds 20-26 of these; they are the most reliably present cards in it.
		const string LandKey = "__land";
		Place(CardLibrary.Plains(), LandKey);

		// Produced bodies count as supply too: a token maker puts creatures on the board without
		// being one, which is exactly the Glorious-Anthem-plus-cheap-tokens case.
		var tokenKeys = new Dictionary<string, List<(string Key, int Count)>>(
			StringComparer.Ordinal
		);
		foreach (var (owner, tokens) in produces)
		{
			var keys = new List<(string, int)>();
			for (var t = 0; t < tokens.Count; t++)
			{
				var key = $"__token:{owner}#{t}";
				Place(tokens[t].Token, key);
				keys.Add((key, Math.Max(1, tokens[t].Count)));
			}
			tokenKeys[owner] = keys;
		}

		// --- 3. Which cards answer which demand ---
		var context = new TargetingContext
		{
			GameState = state,
			SourceCardId = sourceId,
			CastingPlayerId = ids.Player1Id,
			// Asking about deck composition, not target legality — a hexproof creature still
			// supplies "Goblin" to a lord.
			IsNonTargeted = true,
		};

		var failures = new List<string>();
		var supply = new Dictionary<string, int>[demands.Count];
		var landSupply = new bool[demands.Count];

		for (var d = 0; d < demands.Count; d++)
		{
			supply[d] = new Dictionary<string, int>(StringComparer.Ordinal);

			// Trigger demands are answered by the probe pass below, which has to PLAY a card to
			// find out what it causes. Nothing here can answer them.
			if (demands[d] is not TargetSpecification spec)
				continue;

			bool Matches(string key)
			{
				foreach (var id in placements[key])
				{
					try
					{
						if (spec.IsSatisfiedBy(id, context))
							return true;
					}
					catch (Exception ex)
					{
						failures.Add($"{spec} threw {ex.GetType().Name}");
						return false;
					}
				}
				return false;
			}

			foreach (var card in pool)
			{
				var count = Matches(card.Name) ? 1 : 0;
				foreach (var (key, n) in tokenKeys.GetValueOrDefault(card.Name, []))
					if (Matches(key))
						count += n;
				if (count > 0)
					supply[d][card.Name] = count;
			}

			landSupply[d] = Matches(LandKey);
		}

		// --- 3b. Probe: which cards, when played, fire which trigger ---
		ProbeTriggers(pool, demands, supply, failures);

		// --- 3c. Probe: whose COST moves when a demand is supplied ---
		ProbeCostDemands(pool, demands, supply, demandsOf, failures);

		// --- 4. Drop demands the whole pool answers: they cannot separate two decks ---
		var ceiling = pool.Count * UninformativeShare;
		var keep = Enumerable
			.Range(0, demands.Count)
			.Where(d => supply[d].Count < ceiling)
			.ToList();

		var remap = new int[demands.Count];
		Array.Fill(remap, -1);
		for (var i = 0; i < keep.Count; i++)
			remap[keep[i]] = i;

		return new PoolFeatures(
			keep.Select(d => demands[d]).ToList(),
			keep.Select(d => origins[d]).ToList(),
			demandsOf.ToDictionary(
				kv => kv.Key,
				kv => kv.Value.Select(d => remap[d]).Where(d => d >= 0).ToArray(),
				StringComparer.Ordinal
			),
			keep.Select(d => supply[d]).ToArray(),
			keep.Select(d => landSupply[d]).ToArray(),
			failures.Distinct(StringComparer.Ordinal).ToList()
		);
	}

	/// <summary>
	/// Plays every card in the pool and records which triggers it fires.
	///
	/// **This is the half of a demand that is not written on the card.** A trigger says "whenever
	/// a creature you control enters"; which cards ARE creatures that enter is a fact about the
	/// engine, not about the trigger, and the only honest way to get it without a hand-written
	/// event-name-to-predicate table is to play each card and see what it emits.
	///
	/// Two perturbations per card — it enters the battlefield, then it is destroyed — which covers
	/// the enters/dies/leaves families and everything `MoveCardTracked` announces along the way.
	/// **Casting is deliberately not probed**: it needs targets, mana and cost payments (all of
	/// which `CardValueSandbox` solves at much greater cost), so "whenever you cast a spell"
	/// demands are still unanswered. The dump reports how many demands that leaves at zero.
	///
	/// Conditions are asked directly via `IsSatisfiedBy` against the REAL events, so no event type
	/// name appears anywhere in this file and a new event works the day it is emitted.
	/// </summary>
	private static void ProbeTriggers(
		IReadOnlyList<Card> pool,
		List<object> demands,
		Dictionary<string, int>[] supply,
		List<string> failures
	)
	{
		var triggerDemands = Enumerable
			.Range(0, demands.Count)
			.Where(d => demands[d] is TriggerCondition)
			.ToList();

		if (triggerDemands.Count == 0)
			return;

		foreach (var card in pool)
		{
			GameState probed;
			ImmutableList<GameEvent> events;

			try
			{
				var (fixture, ids) = MtgGameFactory.CreateForTesting();

				// A source that is not the probe, so a condition asking "not self" or "you
				// control it" answers about the DECK relationship rather than about identity.
				(fixture, var source) = fixture.AddObject(
					new Card
					{
						Name = "__source",
						OwnerId = ids.Player1Id,
						ControllerId = ids.Player1Id,
					},
					parentId: ids.Player1BattlefieldId
				);

				var subject = card with { OwnerId = ids.Player1Id, ControllerId = ids.Player1Id };

				(probed, events) = fixture
					.AddActions([new PutIntoBattlefieldAction { CardTemplate = subject }])
					.ProcessAllActions();

				var landed = probed
					.GetCardsInZone(ids.Player1BattlefieldId)
					.LastOrDefault(c => string.Equals(c.Name, card.Name, StringComparison.Ordinal));

				if (landed is not null)
				{
					var (afterDeath, deathEvents) = probed
						.AddActions([new DestroyCreatureAction { TargetIds = [landed.Id] }])
						.ProcessAllActions();
					probed = afterDeath;
					events = events.AddRange(deathEvents);
				}

				var context = new TriggerContext
				{
					GameState = probed,
					SourceCardId = source.Id,
					ControllingPlayerId = ids.Player1Id,
				};

				foreach (var d in triggerDemands)
				{
					var condition = (TriggerCondition)demands[d];
					if (events.Any(e => condition.IsSatisfiedBy(e, context)))
						supply[d][card.Name] = 1;
				}
			}
			catch (Exception ex)
			{
				// A card that cannot be played into a bare fixture answers nothing rather than
				// killing the build. Surfaced, because a card that throws on entering the
				// battlefield is an engine bug worth knowing about.
				failures.Add($"probe {card.Name} threw {ex.GetType().Name}");
			}
		}
	}

	/// <summary>
	/// Finds demands that live in ENGINE CODE rather than on the card, by watching what changes
	/// the card's mana cost.
	///
	/// **`AffinityComponent` is a marker with no data at all** — its meaning is a subtraction
	/// inside `CostEngine`, so Thoughtcast states nothing and the harvest above cannot see it. The
	/// artifact concept exists only because *Atog* names artifacts in a sacrifice cost; seeding a
	/// deck on that concept would pull in the artifacts and miss the payoff that wants them, which
	/// is the "half-built deck" failure this whole feature exists to fix.
	///
	/// The probe needs no new vocabulary: it reuses the demands already harvested. Put a few
	/// suppliers of demand D on the battlefield, recompute every card's effective cost, and any
	/// card that got cheaper demands D — whatever mechanic did it, including one written later.
	/// `CostEngine.ComputeEffectiveCost` takes a card TEMPLATE, so no candidate has to be placed
	/// and this is a pure function call per card.
	///
	/// Catches affinity, convoke and `ConditionalCostReductionComponent`. **Does not catch
	/// threshold, graveyard-count or storm**, whose payoff shows up in P/T or in resolution count
	/// rather than in cost — those are separate observables and are not built.
	/// </summary>
	private static void ProbeCostDemands(
		IReadOnlyList<Card> pool,
		List<object> demands,
		Dictionary<string, int>[] supply,
		Dictionary<string, int[]> demandsOf,
		List<string> failures
	)
	{
		// Enough copies to move a cost past any floor, few enough to stay cheap.
		const int Copies = 4;

		var byName = pool.ToDictionary(c => c.Name, StringComparer.Ordinal);
		var (empty, ids) = MtgGameFactory.CreateForTesting();

		int CostIn(GameState state, Card card) =>
			state.ComputeEffectiveCost(
				card with
				{
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
				},
				ids.Player1Id
			);

		Dictionary<string, int> baseline;
		try
		{
			baseline = pool.ToDictionary(
				c => c.Name,
				c => CostIn(empty, c),
				StringComparer.Ordinal
			);
		}
		catch (Exception ex)
		{
			failures.Add($"cost baseline threw {ex.GetType().Name}");
			return;
		}

		var extra = new Dictionary<string, List<int>>(StringComparer.Ordinal);

		for (var d = 0; d < demands.Count; d++)
		{
			// A supplier that can sit on a battlefield. Cost reducers count permanents, so a
			// representative that is only ever a spell tells us nothing.
			var representative = supply[d]
				.Keys.OrderBy(n => n, StringComparer.Ordinal)
				.Select(n => byName.GetValueOrDefault(n))
				.FirstOrDefault(c => c is not null && c.HasComponent<PermanentComponent>());

			if (representative is null)
				continue;

			try
			{
				var state = empty;
				for (var i = 0; i < Copies; i++)
					(state, _) = state.AddObject(
						representative with
						{
							OwnerId = ids.Player1Id,
							ControllerId = ids.Player1Id,
						},
						parentId: ids.Player1BattlefieldId
					);

				foreach (var card in pool)
				{
					if (CostIn(state, card) == baseline[card.Name])
						continue;
					if (!extra.TryGetValue(card.Name, out var list))
						extra[card.Name] = list = [];
					if (!list.Contains(d))
						list.Add(d);
				}
			}
			catch (Exception ex)
			{
				failures.Add($"cost probe of demand {d} threw {ex.GetType().Name}");
			}
		}

		foreach (var (name, found) in extra)
		{
			var existing = demandsOf.GetValueOrDefault(name, []);
			demandsOf[name] = [.. existing.Concat(found.Where(d => !existing.Contains(d)))];
		}
	}

	/// <summary>
	/// True when a spec asks about one specific object rather than a kind of card. Walks
	/// composites, so `And(IsSourceCard, …)` is caught as well as the bare form.
	/// </summary>
	private static bool IsObjectReferential(object demand)
	{
		if (ObjectReferentialSpecs.Contains(demand.GetType()))
			return true;

		// Walks composites (And/Or/Not) and reaches a trigger condition's own Filter, which is how
		// a death trigger — `EventTriggerCondition{Filter = IsSourceCardSpecification}` — is
		// recognised as asking about ITSELF rather than about your deck.
		foreach (var prop in PropertiesOf(demand.GetType()))
		{
			if (
				!typeof(TargetSpecification).IsAssignableFrom(prop.PropertyType)
				&& !typeof(TriggerCondition).IsAssignableFrom(prop.PropertyType)
			)
				continue;
			if (prop.GetValue(demand) is { } inner && IsObjectReferential(inner))
				return true;
		}
		return false;
	}

	/// <summary>
	/// Walks a card's object graph collecting demands and produced cards.
	///
	/// Generic rather than hand-navigated because the interesting filters are arbitrarily deep —
	/// Dragonstorm's is inside a `PipelineAction` inside a `CardEffect` inside a `SpellComponent`.
	/// Same reasoning as `StateJson`'s reflection over abstract types: enumerating the paths by
	/// hand means a forgotten path silently drops a card's demand.
	/// </summary>
	private static void Walk(
		object? node,
		List<(object, string)> demands,
		List<(Card, int)> tokens,
		HashSet<object> seen,
		int depth
	)
	{
		if (node is null || depth > MaxWalkDepth || node is string)
			return;

		// **Sequences are tested BEFORE the reference-type guard, and that ordering is the whole
		// method working or silently doing nothing.** `Card.Components` is `ImmutableArray<T>`,
		// which is a STRUCT, so a `node.GetType().IsClass` guard placed first drops every
		// component on every card — while `AdditionalCastCosts` (an `ImmutableList`, a class) kept
		// working, so sacrifice costs were harvested and nothing else was. A half-working
		// extractor is the worst outcome here: it returns plausible demands for some cards and
		// silently none for the rest.
		if (node is IEnumerable sequence)
		{
			foreach (var item in sequence)
				Walk(item, demands, tokens, seen, depth + 1);
			return;
		}

		// Everything past here must be a reference type to be worth walking or worth deduping —
		// ints, bools and enums have no properties we want and box to a fresh object every time,
		// which would defeat the seen-set.
		if (!node.GetType().IsClass || !seen.Add(node))
			return;

		var owner = node.GetType().Name;

		foreach (var prop in PropertiesOf(node.GetType()))
		{
			object? value;
			try
			{
				value = prop.GetValue(node);
			}
			catch
			{
				continue; // a computed property that does not like this fixture is not a demand
			}

			if (value is null)
				continue;

			switch (value)
			{
				// A filter IS the demand. Never recurse into it: its inner Subtype is part of the
				// question being asked, not a second question.
				case TargetSpecification spec:
					if (DemandProperties.Contains(prop.Name))
						demands.Add((spec, $"{owner}.{prop.Name}"));
					continue;

				// **A trigger's demand is the WHOLE condition, not its Filter.** Taking the filter
				// alone loses the half of the question carried by the event type: "whenever
				// ANOTHER CREATURE YOU CONTROL enters" has its creature-ness in the event, so the
				// filter on its own is `{ControlledByYou, NotSelf}` — answered by every card in
				// the pool, dropped as uninformative, and 155 CSC cards fell out of the count that
				// way. Captured whole and answered by probe (which cards, when played, actually
				// fire it), the same demand reads as the 237 creatures.
				case TriggerCondition condition when prop.Name == "Condition":
					demands.Add((condition, $"{owner}.Condition"));
					continue;

				// "Cards of this subtype qualify" — tutors, subtype counters, tribal P/T.
				case string s when prop.Name == "Subtype" && s.Length > 0:
					demands.Add((new IsSubtypeSpecification { Subtype = s }, $"{owner}.Subtype"));
					continue;

				// A card-typed property is a card this card MAKES. Recorded as supply, not walked:
				// a token's own demands belong to the token, not to its maker.
				case Card token:
					tokens.Add((token, CountOn(node)));
					continue;

				case string:
					continue;
			}

			// GameObject.Children is documented view-only and its graph is redundant with the
			// parent map — StateJson drops it for the same reason.
			if (prop.Name == "Children")
				continue;

			Walk(value, demands, tokens, seen, depth + 1);
		}
	}

	/// <summary>
	/// How many copies the action alongside a card template makes.
	///
	/// ponytail: reads a sibling `Count` property and defaults to 1. A count driven from pipeline
	/// context (`CountInputKey`) is unknowable without running the game and lands here as 1, which
	/// understates Krenko-style makers. Upgrade by probing if the dump shows it mattering.
	/// </summary>
	private static int CountOn(object node)
	{
		var prop = node.GetType().GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
		return prop?.PropertyType == typeof(int) && prop.GetValue(node) is int n && n > 0 ? n : 1;
	}

	/// <summary>
	/// **Concurrent, not a plain Dictionary.** This shipped as an unsynchronised static and
	/// produced a flaky suite within one run: two fixtures calling <see cref="Build"/> on
	/// different threads corrupt it, and a torn read out of a reflection cache surfaces as an
	/// unrelated test failing once in several runs. `MetagameEvolver` also runs its games under
	/// `Parallel.For`, so any static touched from a build path has to be thread-safe here.
	/// </summary>
	private static readonly System.Collections.Concurrent.ConcurrentDictionary<
		Type,
		PropertyInfo[]
	> PropertyCache = new();

	private static PropertyInfo[] PropertiesOf(Type type) =>
		PropertyCache.GetOrAdd(
			type,
			t =>
				t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
					.Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
					.ToArray()
		);
}
