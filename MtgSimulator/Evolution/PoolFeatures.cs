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

	/// <summary>
	/// "What am I aiming at" — a demand only when it aims at YOUR OWN cards.
	///
	/// **This was excluded outright and that cost the mode reanimator.** The exclusion is right
	/// for Lightning Bolt, whose "any creature" is a question about the opponent's board. It is
	/// exactly backwards for Reanimate, which is
	/// `SingleTarget(IsCreatureInOwnGraveyardSpecification)` — a card that does nothing except ask
	/// your deck for creatures in your graveyard, and which was therefore a payoff for nothing.
	/// The whole reanimator archetype was unreachable, the third demand found living somewhere
	/// the harvest rule did not look.
	///
	/// The two cases are separated empirically rather than by a card list or a type list: a spec
	/// is a demand about your DECK when its candidates come from a non-battlefield zone. The
	/// battlefield is the shared board and anything aimed there is a targeting clause; your hand,
	/// graveyard and library are where your own cards live. `ZoneSpecification.GetCandidateIds`
	/// already answers this and composites already delegate to it, so nothing new is needed —
	/// see <see cref="IsDeckScoped"/>.
	/// </summary>
	private const string TargetingProperty = "Specification";

	/// Depth cap on the object walk. Purely a runaway guard; the deepest real card is ~6.
	private const int MaxWalkDepth = 12;

	/// <summary>
	/// "You must cast N spells before me this turn" — storm, prowess, Illusory Angel.
	///
	/// **The one demand here that is NOT the card's own filter object**, and it exists because the
	/// question it asks is not written on the card at all: storm is a `bool` that
	/// `ResolveSpellAction` multiplies by `MtgGame.SpellsCastThisTurn`, and
	/// `RequiresSpellsCastThisTurnRestriction` reads the same counter from `CanCast`. Nothing in
	/// either is a `TargetSpecification`, so the positional harvest rule cannot see them and
	/// **Traditional Storm — the strongest deck in the precon round-robin — was undiscoverable.**
	///
	/// A record like every other demand, so value equality still dedupes it pool-wide and every
	/// consumer works on indices without knowing this one is special.
	/// </summary>
	public sealed record SpellsCastDemand
	{
		public int Minimum { get; init; } = 1;
	}

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
	private readonly Dictionary<string, int>[] _causalSupply;
	private readonly bool[] _landSupply;

	private PoolFeatures(
		List<object> demands,
		List<string> origins,
		Dictionary<string, int[]> demandsOf,
		Dictionary<string, int>[] supply,
		Dictionary<string, int>[] causalSupply,
		bool[] landSupply,
		IReadOnlyList<string> failures
	)
	{
		_demands = demands;
		_origins = origins;
		_demandsOf = demandsOf;
		_supply = supply;
		_causalSupply = causalSupply;
		_landSupply = landSupply;
		Failures = failures;
	}

	public IReadOnlyList<object> Demands => _demands;

	/// How many cards this was built from. Breadth is only meaningful as a SHARE of the pool: 300
	/// suppliers is most of CSC and a third of ALL.
	public int PoolSize { get; private init; }

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

	/// <summary>
	/// How much this card CAUSES the demand, as opposed to matching it. 0 is "it does not".
	///
	/// **`SupplyOf` merges channels that mean different things, and this is the one that had to come
	/// back out.** That number covers every way a card can answer a demand at once — net mana and
	/// cards for a spells-cast demand, cards moved for a graveyard one, bodies for a token maker,
	/// flat 1 for a plain filter match — so nothing downstream could ask for one kind specifically.
	///
	/// Measured on ALL, `IsCreatureInOwnGraveyardSpecification` has 513 suppliers, and merged they
	/// are indistinguishable: `Grave Titan(5)` and `Hornet Queen(5)` sit level with `Drown in the
	/// Mere(5)` and `Grim Excavation(4)`. A reanimator deck wants both a fatty to bring back and an
	/// outlet to put it there, but in **different quantities and different roles**, and a value-
	/// ranked sample from one merged list draws the fatties — which is exactly the "engine decks are
	/// built badly" complaint.
	///
	/// Only the movement pass writes here, because it is the only channel whose meaning is causal:
	/// resolving this card MOVED another card into the demand's zone. Everything else is a statement
	/// about what a card IS.
	/// </summary>
	public int CausalSupplyOf(int demandIndex, string supplier) =>
		_causalSupply[demandIndex].GetValueOrDefault(supplier);

	/// Every pool card that CAUSES a demand, strongest first. Empty for most demands.
	public IReadOnlyList<string> CausalSuppliersOf(int demandIndex) =>
		_causalSupply[demandIndex]
			.OrderByDescending(kv => kv.Value)
			.ThenBy(kv => kv.Key, StringComparer.Ordinal)
			.Select(kv => kv.Key)
			.ToList();

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
	public bool Informative(int demandIndex) =>
		_supply[demandIndex].Count > 0 || _landSupply[demandIndex];

	/// <summary>
	/// Every pool card that ASKS a demand — the archetype's payoff pool.
	///
	/// The mirror of <see cref="SuppliersOf"/>, and it did not exist because nothing needed the
	/// reverse direction until the engine report became a card POOL rather than a decklist.
	/// Deriving payoffs from a built deck instead reports only the ones the seeder happened to
	/// draw: Frogmite was listed as an affinity payoff while Myr Enforcer, Cranial Plating,
	/// Arcbound Ravager and Atog — which ask exactly the same thing — were not.
	/// </summary>
	public IReadOnlyList<string> AskersOf(int demandIndex) =>
		_demandsOf
			.Where(kv => kv.Value.Contains(demandIndex))
			.Select(kv => kv.Key)
			.OrderBy(n => n, StringComparer.Ordinal)
			.ToList();

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
	/// The WORST-answered of a card's demands, rather than the total across all of them.
	///
	/// **A card with two demands is only as good as its starving one, and summing hid exactly the
	/// case the dead-card rule is named after.** Dragonstorm asks two things — spells cast this
	/// turn, and a Dragon to find — so in a storm deck the first is richly answered, the sum comes
	/// out high, and `Satisfaction` reports a perfectly supported card that will resolve for
	/// nothing because the deck contains no Dragons. It was seeded into the storm engine on
	/// exactly that arithmetic.
	///
	/// <see cref="double.NaN"/> still means "asks nothing", as in <see cref="Satisfaction"/>.
	/// </summary>
	public double WeakestSatisfaction(string cardName, Decklist deck)
	{
		var demands = DemandsOf(cardName).Where(Informative).ToList();
		if (demands.Count == 0)
			return double.NaN;

		var weakest = double.MaxValue;
		foreach (var d in demands)
		{
			var total = 0.0;
			var supplyOfD = _supply[d];
			foreach (var (name, copies) in deck.Spells)
			{
				if (string.Equals(name, cardName, StringComparison.Ordinal))
					continue;
				if (supplyOfD.TryGetValue(name, out var per))
					total += per * copies;
			}
			if (_landSupply[d])
				total += deck.Lands;
			weakest = Math.Min(weakest, total);
		}
		return weakest;
	}

	/// <summary>
	/// Cards in the deck that ask for something and get nothing — the Dragonstorm-with-no-dragons
	/// set. Cards asking nothing are never listed.
	///
	/// Reads the WEAKEST demand, not the sum: a card starving on one of two questions is a blank
	/// holding a slot regardless of how well the other is answered.
	/// </summary>
	public IReadOnlyList<string> DeadCards(Decklist deck) =>
		deck
			.Spells.Keys.Where(n => WeakestSatisfaction(n, deck) == 0)
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

		// The caster's own battlefield copy, recorded by NAME rather than read back out of
		// `placements` by index. The list's order is a positional contract nothing else depends on,
		// and this project has already lost a session to `Slots[0]` meaning three different things.
		var myBattlefield = new Dictionary<string, int>(StringComparer.Ordinal);

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
				if (zone == ids.Player1BattlefieldId)
					myBattlefield[key] = placed.Id;
				spots.Add(placed.Id);
			}
			placements[key] = spots;
		}

		foreach (var card in pool)
			Place(card, card.Name);

		// **The control copy for the deck-scope question: the same card, on the OPPONENT's board.**
		//
		// Kept in its own map and deliberately NOT added to `placements`, because `placements` is
		// what `Matches` walks to compute supply — putting an opponent-controlled copy in there
		// would make every "creature an opponent controls" spec suddenly answered by the whole pool
		// and would move every supply number in the file. See IsControlScoped for what this is for.
		var theirBattlefield = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var card in pool)
		{
			if (theirBattlefield.ContainsKey(card.Name))
				continue;
			(state, var placed) = state.AddObject(
				card with
				{
					OwnerId = ids.Player2Id,
					ControllerId = ids.Player2Id,
				},
				parentId: ids.Player2BattlefieldId
			);
			theirBattlefield[card.Name] = placed.Id;
		}

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
		// The CAUSAL half of supply, kept alongside the merged total rather than replacing it. See
		// `CausalSupplyOf` for why one number could not answer the question.
		var causal = new Dictionary<string, int>[demands.Count];
		var landSupply = new bool[demands.Count];

		// One fixture per card, three answers. Shared because a demand is not known to need it
		// until the loop reaches it and the probe is far too expensive to run per demand.
		var profiles = ProbeCardProfiles(pool, failures);

		for (var d = 0; d < demands.Count; d++)
		{
			supply[d] = new Dictionary<string, int>(StringComparer.Ordinal);
			causal[d] = new Dictionary<string, int>(StringComparer.Ordinal);

			// There is nothing to evaluate a SpellsCastDemand against: "a spell was cast" is an
			// event in the turn, not a property of a card sitting in a zone. It is answered by the
			// two resources a storm turn actually runs on.
			//
			// **A storm enabler is a card that does not reduce your ability to keep casting**, and
			// that is two resources, not one. A ritual is mana-positive and card-negative; a
			// cantrip is card-neutral and mana-negative; both keep the chain going. A creature is
			// negative on both. Requiring EITHER is what admits Preordain alongside Seething Song
			// without needing an exchange rate between cards and mana — which would be a scoring
			// decision, and nothing in this file scores anything.
			//
			// Lands are left at false: a land is not a spell cast.
			if (demands[d] is SpellsCastDemand)
			{
				// **Weighted by how much of each resource it leaves you**, not a flat yes. Every
				// qualifying card scoring 1 made the supplier set correct and unsortable: a
				// Seething Song and a break-even cantrip read identically, so `CardDelta` broke
				// every tie and the storm deck came out as the format's best card-draw spells with
				// no rituals in it. Lotus Bloom now scores 4, Ancestral Recall 3, a cantrip 1.
				foreach (var (name, p) in profiles)
					if (p.Mana >= 0 || p.Cards >= 0)
						supply[d][name] = 1 + Math.Max(0, p.Mana) + Math.Max(0, p.Cards);
				continue;
			}

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

			// --- Movement supply: a card that PUTS things in the zone answers the zone ---
			//
			// **The demand model is declarative and this gap is causal.** "A creature card in your
			// graveyard" is answered, on the reading above, by every creature card — which is true
			// and useless, because nothing in a deck of creatures puts one in the graveyard.
			// Reanimator built as Reanimate plus fat creatures with no discard outlet and no mill,
			// and assembled 40% of games at depth 0.
			//
			// The other half of the same bug is that the enablers were filed as PAYOFFS. Entomb is
			// `SelectCardFromZoneAction { Filter = creature }` followed by a move to the graveyard,
			// and `Filter` is a demand property — so the card that FILLS your graveyard was
			// recorded as a card that WANTS a full graveyard. A filter naming what a card fetches
			// is not a precondition.
			//
			// Both are fixed by one test, and it needs no new vocabulary: if resolving a card moved
			// another card into this demand's zone, that card SUPPLIES the demand — and is removed
			// from its askers, because a card does not demand what it creates.
			//
			// No filter match is required on the moved card, deliberately. A discard outlet is a
			// graveyard enabler whatever it happened to pitch in one probe, because in a real game
			// you choose what to pitch.
			var zone = DemandZone(demands[d], context);
			if (zone is null)
				continue;

			// How much of the pool this demand's filter matches, read BEFORE the movement pass
			// writes to `supply[d]` — so it is the declarative count and nothing else.
			var density = supply[d].Count / (double)Math.Max(1, pool.Count);

			foreach (var (name, p) in profiles)
			{
				if (!p.MovesInto.TryGetValue(zone.Value, out var movedCount))
					continue;

				// **"No filter match required" is right for a zone you FILL and wrong for one you
				// DRAW from.** You choose what to pitch, so a discard outlet is a graveyard enabler
				// whatever it moved in one probe. You do not choose what you draw — and Goblin
				// Lackey asks for "a Goblin in your hand", so every draw spell in the pool was
				// credited and the enabler slot came out at 72 cards against 23 actual Goblins.
				//
				// Two ways to be credited, and a mover needs either:
				//
				//  - **DIRECTED** — the mover's own card data names this demand, which is exactly
				//    what Entomb's `SelectCardFromZoneAction.Filter` does. It chose a matching
				//    card, so the count stands whatever the pool looks like. Same test the askers
				//    strike below already makes, so a tutor keeps working.
				//  - **LIKELY** — more often than not at least one card it moved matches:
				//    `1 - (1 - density)^moved >= 0.5`. An outlet pitching two into a pool that is
				//    58% creatures clears it; a cantrip drawing one from a pool 1.4% Goblins does
				//    not.
				//
				// Understating is the safe direction, as everywhere else here: a card cut from the
				// causal channel still sits in the DECLARATIVE slot if it matches the filter
				// itself. The probe cannot answer this by inspection — its moved cards are a fixed
				// seven-card filler, never a Goblin — so the expectation is the honest form.
				//
				// ponytail: density is measured over the POOL where what matters is density in the
				// DECK — a built Zombie deck mills its own Zombies far more reliably than the pool
				// share implies. No deck exists at harvest time; revisit if a real archetype is
				// measured losing its enablers.
				var directed = demandsOf.TryGetValue(name, out var asks) && asks.Contains(d);
				if (!directed && 1.0 - Math.Pow(1.0 - density, movedCount) < 0.5)
					continue;

				supply[d][name] = Math.Max(supply[d].GetValueOrDefault(name), movedCount);
				// **Recorded separately as well as merged.** This is the only channel that means
				// "this card CAUSES the thing"; every other one means "this card IS the thing".
				causal[d][name] = Math.Max(causal[d].GetValueOrDefault(name), movedCount);
				if (directed)
					demandsOf[name] = [.. demandsOf[name].Where(x => x != d)];
			}
		}

		// --- 3b. Probe: which cards, when played, fire which trigger ---
		ProbeTriggers(pool, demands, supply, failures);

		// --- 3c. Probe: whose COST moves when a demand is supplied ---
		ProbeCostDemands(pool, demands, supply, demandsOf, failures);

		// --- 4. Drop demands the whole pool answers: they cannot separate two decks ---
		// Targeting specs are dropped here too unless they aim at the caster's own cards — see
		// TargetingProperty. Done in the same pass as the uninformative filter so there is one
		// remap rather than two.
		var ceiling = pool.Count * UninformativeShare;
		var keep = Enumerable
			.Range(0, demands.Count)
			.Where(d =>
				supply[d].Count < ceiling
				&& (
					!origins[d].EndsWith(TargetingProperty, StringComparison.Ordinal)
					|| IsDeckScoped(demands[d], context)
					|| IsControlScoped(demands[d], context, myBattlefield, theirBattlefield)
				)
			)
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
			keep.Select(d => causal[d]).ToArray(),
			keep.Select(d => landSupply[d]).ToArray(),
			failures.Distinct(StringComparer.Ordinal).ToList()
		)
		{
			PoolSize = pool.Count,
		};
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

				// **A third perturbation: something ELSE enters while the subject is in play.**
				//
				// Without it, "whenever ANOTHER creature you control enters" is structurally
				// unfireable — the subject arrives on an empty battlefield, so there is no other
				// creature and the trigger never gets a chance. Soul Warden therefore supplied no
				// life gain at all and the life-gain concept came back as a pile of creatures that
				// gain life on their OWN entry, with the actual engine piece missing.
				//
				// The companion's own events are stripped: it is scaffolding, and attributing its
				// arrival to the subject would make every card in the pool a supplier of
				// "a creature entered the battlefield".
				var (withCompanion, companionEvents) = probed
					.AddActions([new PutIntoBattlefieldAction { CardTemplate = Companion(ids) }])
					.ProcessAllActions();

				var companionId = withCompanion
					.GetCardsInZone(ids.Player1BattlefieldId)
					.LastOrDefault(c =>
						string.Equals(c.Name, CompanionName, StringComparison.Ordinal)
					)
					?.Id;

				probed = withCompanion;
				events = events.AddRange(companionEvents.Where(e => CardIdOf(e) != companionId));

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
			// A SpellsCastDemand is supplied by every cheap card in the pool, so its representative
			// is whichever of those happens to sort first — and if that is an artifact, four copies
			// on the battlefield move every affinity card's cost and this would report the entire
			// artifact archetype as demanding "cast spells first". The probe reads a BOARD; this
			// demand is about a turn, so it has nothing to say here.
			if (demands[d] is SpellsCastDemand)
				continue;

			// A supplier that can sit on a battlefield. Cost reducers count permanents, so a
			// representative that is only ever a spell tells us nothing.
			var representative = supply[d]
				.Keys.OrderBy(n => n, StringComparer.Ordinal)
				.Select(n => byName.GetValueOrDefault(n))
				.FirstOrDefault(c => c is not null && c.HasComponent<PermanentComponent>());

			if (representative is null)
				continue;

			// **A control that does NOT answer this demand, and without it the probe cannot tell
			// affinity from convoke.** Convoke discounts per creature of ANY kind, so four Zombies
			// on the battlefield move every convoke card's cost — and Stoke the Flames and
			// Devouring Light were therefore filed as payoffs of the Zombie concept, the Spirit
			// concept, the Elf concept and every other creature demand in the pool. In the ALL
			// report they appeared as payoffs of nearly every concept containing creatures.
			//
			// A card only demands THIS thing if its cost moves for the representative and does
			// NOT move for a permanent that answers something else. Affinity passes (the control
			// is not an artifact); convoke is correctly rejected as wanting creatures generally.
			var control = pool.FirstOrDefault(c =>
				c.HasComponent<PermanentComponent>() && !supply[d].ContainsKey(c.Name)
			);

			try
			{
				GameState WithFour(Card template)
				{
					var s = empty;
					for (var i = 0; i < Copies; i++)
						(s, _) = s.AddObject(
							template with
							{
								OwnerId = ids.Player1Id,
								ControllerId = ids.Player1Id,
							},
							parentId: ids.Player1BattlefieldId
						);
					return s;
				}

				var state = WithFour(representative);
				var controlState = control is null ? null : WithFour(control);

				foreach (var card in pool)
				{
					if (CostIn(state, card) == baseline[card.Name])
						continue;
					// Moved for both: whatever it wants, it is not specifically this.
					if (
						controlState is not null
						&& CostIn(controlState, card) != baseline[card.Name]
					)
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
	/// How much mana each card LEAVES you, measured: mana gained minus mana paid.
	///
	/// **This replaces a cheapness test that was pointed the wrong way.** Supplying a
	/// <see cref="SpellsCastDemand"/> from "costs 2 or less" produced a storm deck of Delver of
	/// Secrets, Llanowar Elves, Imposing Sovereign and Skyknight Vanguard — cheap CREATURES, which
	/// are the opposite of a storm enabler. A two-mana creature costs you two mana to add one to
	/// the count; the count is not the resource, the mana is. What storm wants is cards that leave
	/// you able to cast MORE than you could before: rituals, Moxen, Sol Ring, Lotus Bloom.
	///
	/// Measured rather than listed, so no action type is named here and a mana source written
	/// tomorrow is found the day it exists. A permanent is put onto the battlefield and a turn is
	/// started — which is what fires the upkeep triggers every mana rock and dork in this project
	/// produces from, so a probe that skipped the turn would score every one of them at zero. A
	/// non-permanent has its effects resolved directly, since it never sits on a battlefield.
	///
	/// **Cantrips come out negative and are excluded, and that is understated on purpose.**
	/// Preordain genuinely does advance a storm turn by replacing itself. Counting "draws a card"
	/// as mana needs a conversion rate between two resources, which is a scoring decision, and
	/// nothing in this file scores anything. Understating is the safe direction for a rule that
	/// decides what a deck gets built out of.
	/// </summary>
	private static Dictionary<string, CardProfile> ProbeCardProfiles(
		IReadOnlyList<Card> pool,
		List<string> failures
	)
	{
		var profiles = new Dictionary<string, CardProfile>(StringComparer.Ordinal);

		// Cards for the subject to act ON. A probe against empty zones measures nothing: a cantrip
		// draws from an empty library and reads as card-negative, and Entomb searches a library
		// with no creature in it and moves nothing. Taken from the pool in its own order so the
		// filler is deterministic and is the kind of card these effects expect to find.
		var filler = new List<Card>();
		filler.AddRange(pool.Where(c => c.HasComponent<CreatureComponent>()).Take(3));
		filler.AddRange(
			pool.Where(c =>
					!c.HasComponent<PermanentComponent>() && c.HasComponent<SpellComponent>()
				)
				.Take(3)
		);
		filler.Add(CardLibrary.Plains());

		foreach (var card in pool)
		{
			if (card.HasSubtype("Land"))
				continue;

			try
			{
				var (fixture, ids) = MtgGameFactory.CreateForTesting();

				var fillerIds = new List<int>();
				// The GRAVEYARD is stocked too, and leaving it out cost the probe Past In Flames.
				// A card that makes your graveyard castable adds nothing measurable against an empty
				// one — it flashes back nothing, the castable count does not move, and the best card
				// in a storm deck reads as pure loss. Same for every recursion card in the pool.
				foreach (
					var zone in new[]
					{
						ids.Player1LibraryId,
						ids.Player1HandId,
						ids.Player1GraveyardId,
					}
				)
				foreach (var f in filler)
				{
					(fixture, var placed) = fixture.AddObject(
						f with
						{
							OwnerId = ids.Player1Id,
							ControllerId = ids.Player1Id,
						},
						parentId: zone
					);
					fillerIds.Add(placed.Id);
				}

				// CurrentMana only, not MaxMana. The question is "can I cast more spells THIS
				// turn", and `CreateForTesting` starts both at 99 with CurrentMana already equal
				// to MaxMana — so `StartTurnAction`'s refill is a no-op here and any movement is
				// the card's doing.
				int ManaOf(GameState s) =>
					s.GetObject(ids.Player1Id) is MtgPlayer p ? p.CurrentMana : 0;
				// **Castable cards ANYWHERE, not cards in hand.** Past In Flames adds nothing to
				// your hand — it makes your GRAVEYARD castable — so a hand-size measure read the
				// best card in a storm deck as card-negative and excluded it outright. Impulse
				// draw has the same shape from exile. `IsInCastableZone` is the single "can you
				// play this from where it is" predicate all four play actions consult, so asking
				// it here cannot disagree with what the game will actually let you cast, and a
				// future mechanic that widens castability is counted the day it ships.
				// **Flashback is NOT in `IsInCastableZone`** — it is handled separately by
				// `MtgActionGenerator.AddGraveyardFlashbackActions`, so the predicate covers hand,
				// impulse-exile and library-top and stops. Asking it alone still missed Past In
				// Flames, whose entire function is making your graveyard castable. Checked here
				// rather than widened in the engine: that predicate gates cast LEGALITY and giving
				// it a new true case would change what the game allows, for a measurement.
				bool Castable(GameState s, int id) =>
					s.IsInCastableZone(id, ids.Player1Id)
					|| (
						s.GetCardZoneId(id) == ids.Player1GraveyardId
						&& s.GetObject(id) is Card c
						&& c.HasComponent<FlashbackComponent>()
					);

				int CastableOf(GameState s) =>
					new[] { ids.Player1HandId, ids.Player1GraveyardId, ids.Player1ExileId }
						.Where(z => z != 0)
						.SelectMany(s.GetChildrenIds)
						.Count(id => Castable(s, id));

				var manaBefore = ManaOf(fixture);
				var castableBefore = CastableOf(fixture);
				var zoneBefore = fillerIds.ToDictionary(id => id, id => ZoneOf(fixture, id));

				var subject = card with { OwnerId = ids.Player1Id, ControllerId = ids.Player1Id };

				GameState after;
				if (card.HasComponent<PermanentComponent>())
				{
					// The turn is what fires the upkeep trigger every mana rock and dork in this
					// project produces from. Without it they all read as zero and the probe would
					// find nothing but rituals.
					(after, _) = fixture
						.AddActions(
							[
								new PutIntoBattlefieldAction { CardTemplate = subject },
								new StartTurnAction
								{
									ActivePlayerId = ids.Player1Id,
									BattlefieldId = ids.Player1BattlefieldId,
									SkipDraw = true,
								},
							]
						)
						.ProcessAllActions();
				}
				else
				{
					var spell = subject.GetComponent<SpellComponent>();
					if (spell is null || spell.Effects.Count == 0)
						continue;

					(after, _) = fixture
						.AddActions(
							[
								new ResolveEffectAction
								{
									Effects = spell.Effects,
									CastingPlayerId = ids.Player1Id,
									SourceCardId = 0,
								},
							]
						)
						.ProcessAllActions();
				}

				// **An X card's ManaCost is 0 and that is not a discount.** X lives on the cast
				// action, never on the card, so Banefire and Hangarback Walker both read
				// `manaCost: 0` and came back as free spells that any storm deck should play.
				// A cost that cannot be known cannot be shown to be profitable.
				var cost = card.HasComponent<XCostComponent>() ? int.MaxValue / 2 : card.ManaCost;

				// Counted, not just recorded. A mill-4 fills a graveyard four times as fast as a
				// one-card tutor, and `SupplyOf` is the channel that difference travels down.
				var moved = fillerIds
					.Where(id => ZoneOf(after, id) != zoneBefore[id])
					.Select(id => ZoneOf(after, id))
					.Where(z => z is not null and not ZoneType.Battlefield)
					.GroupBy(z => z!.Value)
					.ToDictionary(g => g.Key, g => g.Count());

				profiles[card.Name] = new CardProfile(
					ManaOf(after) - manaBefore - cost,
					// Minus one for the card itself: it was spent to do this. A cantrip comes out
					// at exactly 0 — card-neutral, which is what makes it a storm enabler despite
					// making no mana.
					CastableOf(after)
						- castableBefore
						- 1,
					moved
				);
			}
			catch (Exception ex)
			{
				// Same rule as every other probe here: a card that cannot be deployed into a bare
				// fixture answers nothing rather than killing the build, and is surfaced.
				failures.Add($"card probe {card.Name} threw {ex.GetType().Name}");
			}
		}

		return profiles;
	}

	private const string CompanionName = "__companion";

	/// <summary>
	/// A deliberately featureless 1/1 for the trigger probe to play alongside the subject. Vanilla
	/// so it fires nothing of its own — the only thing it contributes is *existing*.
	/// </summary>
	private static Card Companion(MtgGameIds ids) =>
		new()
		{
			Name = CompanionName,
			OwnerId = ids.Player1Id,
			ControllerId = ids.Player1Id,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 1, Toughness = 1 }
			),
		};

	/// The card an event names, by property, or null. Same positional rule as the demand harvest.
	private static int? CardIdOf(GameEvent e) =>
		e.GetType().GetProperty("CardId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(e)
		as int?;

	private static ZoneType? ZoneOf(GameState state, int cardId) =>
		state.HasObject(cardId) ? state.GetCardZone(cardId)?.ZoneType : null;

	/// <summary>
	/// The single non-battlefield zone a demand's candidates live in, or null.
	///
	/// This is what makes movement supply addressable: "a creature card in your graveyard" is a
	/// question about the GRAVEYARD, so anything that puts cards there answers it. A demand whose
	/// candidates are scattered (a plain subtype filter matches cards in every zone) has no single
	/// zone and gets no movement supply — the concept of "putting a Goblin somewhere" is not what
	/// a Goblin lord is asking for.
	/// </summary>
	private static ZoneType? DemandZone(object demand, TargetingContext context)
	{
		if (demand is not TargetSpecification spec)
			return null;

		try
		{
			ZoneType? found = null;
			foreach (var id in spec.GetCandidateIds(context))
			{
				var zone = ZoneOf(context.GameState, id);
				if (zone is null or ZoneType.Battlefield)
					return null;
				if (found is not null && found != zone)
					return null;
				found = zone;
			}
			return found;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// What one card does to the two resources a deck runs on, and where it moves cards to.
	/// </summary>
	/// <param name="Mana">Mana left over after paying for it. A ritual is positive.</param>
	/// <param name="Cards">Cards in hand afterwards, counting itself as spent. A cantrip is 0.</param>
	/// <param name="MovesInto">
	/// How many OTHER cards this card put into each non-battlefield zone.
	/// </param>
	private readonly record struct CardProfile(
		int Mana,
		int Cards,
		IReadOnlyDictionary<ZoneType, int> MovesInto
	);

	/// <summary>
	/// Does this targeting spec aim at the caster's OWN cards rather than at the board?
	///
	/// Asked of the fixture, where every pool card sits in all four zones, so the answer comes
	/// from what the spec actually selects rather than from its type. A spec whose candidates
	/// include anything on a battlefield is aiming at the shared board — "destroy target
	/// permanent", "deal 3 damage to any target" — and says nothing about your deck. One whose
	/// candidates are confined to hand, graveyard or library is asking your deck a question.
	///
	/// **Empty means no**, deliberately. A spec that selects nothing in a fixture holding the
	/// whole pool cannot be shown to be about your deck, and the safe direction for a rule that
	/// CREATES demands is to add fewer of them.
	/// </summary>
	/// <summary>
	/// **Does this targeting spec ask about cards YOU CONTROL on the battlefield?**
	///
	/// The zone rule above uses "not on a battlefield" as its proxy for "about your deck", on the
	/// reasoning that the battlefield is the SHARED board. That is right for Lightning Bolt and
	/// wrong for anything reading *"target Illusionist you control"* — which is a statement about
	/// what you must BUILD, not about what the opponent happens to have. **Controlled-by-you is
	/// what un-shares the battlefield.**
	///
	/// It cost the mode its first real combo. CMB's copiers read "target Illusionist you control";
	/// measured, all four combo pieces harvested **zero** demands, so `DeckCore.For` returned null
	/// and a two-card loop that `LoopDetector` finds in two actions was invisible to the builder.
	/// Same class as the reanimator miss the zone rule was itself introduced to fix, one proxy down.
	///
	/// **Decided empirically, like every other rule in this file — no type list.** The same card is
	/// placed on both battlefields; a spec is control-scoped when it accepts YOUR copy and rejects
	/// the OPPONENT's. Nothing names `IsControlledByYouSpecification`, so a new way of expressing
	/// "you control" works the day it is written.
	///
	/// **This deliberately widens the demand set**, admitting every "target creature you control"
	/// pump spell. Those are answered by most of the creature pool, so `Informative` and
	/// `DeckCore.HasANarrowSlot` are what stop them becoming archetypes — measured rather than
	/// assumed, see `ComboProvingDiscoveryTests.DumpDemandAndCoreCounts`.
	/// </summary>
	private static bool IsControlScoped(
		object demand,
		TargetingContext context,
		Dictionary<string, int> mine,
		Dictionary<string, int> theirs
	)
	{
		if (demand is not TargetSpecification spec)
			return false;

		foreach (var (name, myId) in mine)
		{
			if (!theirs.TryGetValue(name, out var theirId))
				continue;

			try
			{
				// One card that the spec accepts as yours and refuses as theirs is proof; a spec
				// matching neither says nothing, which is why this asks for a witness rather than
				// checking every card agrees.
				if (spec.IsSatisfiedBy(myId, context) && !spec.IsSatisfiedBy(theirId, context))
					return true;
			}
			catch
			{
				// Same rule as the supply evaluation: a spec that cannot make sense of the fixture
				// answers "no" for this card, it does not kill the build.
			}
		}

		return false;
	}

	private static bool IsDeckScoped(object demand, TargetingContext context)
	{
		if (demand is not TargetSpecification spec)
			return false;

		try
		{
			var any = false;
			foreach (var id in spec.GetCandidateIds(context))
			{
				if (!context.GameState.HasObject(id))
					continue;
				if (context.GameState.GetCardZone(id).ZoneType == ZoneType.Battlefield)
					return false;
				any = true;
			}
			return any;
		}
		catch
		{
			// Same rule as the supply evaluation: a spec that cannot make sense of the fixture
			// answers "no", it does not kill the build.
			return false;
		}
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

		// The cast-restriction half of SpellsCastDemand. Caught at the node rather than in the
		// property switch below because what carries the meaning is the restriction's TYPE — its
		// only property is a bare int called `Minimum`, which is not a demand anywhere else.
		if (node is RequiresSpellsCastThisTurnRestriction restriction)
			demands.Add(
				(new SpellsCastDemand { Minimum = restriction.Minimum }, $"{owner}.CanCast")
			);

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
				//
				// `TargetingStrategy.Specification` is the third case and it is DECIDED LATER, not
				// here. "What am I aiming at" is usually not a demand — Lightning Bolt's "any
				// creature" is about the opponent's board — but Reanimate's "a creature card in
				// YOUR graveyard" is a question about your deck and nothing else. Which one a spec
				// is cannot be read off the card; it is read off the ZONE its candidates come
				// from, and that needs the fixture. Recorded with a marker origin and filtered in
				// `Build`.
				case TargetSpecification spec:
					if (DemandProperties.Contains(prop.Name))
						demands.Add((spec, $"{owner}.{prop.Name}"));
					else if (prop.Name == TargetingProperty)
						demands.Add((spec, $"{owner}.{TargetingProperty}"));
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

				// Storm. The count it multiplies by lives in `MtgGame`, so the card carries only
				// this flag — 2 rather than 1 because a storm spell already counts itself, so a
				// minimum of 1 would be satisfied by casting nothing at all.
				case bool flag when prop.Name == "HasStorm" && flag:
					demands.Add((new SpellsCastDemand { Minimum = 2 }, $"{owner}.HasStorm"));
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
