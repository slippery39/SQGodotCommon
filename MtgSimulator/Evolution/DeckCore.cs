using System.Collections.Immutable;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// One ROLE a deck must fill, and the interchangeable cards that can fill it.
/// </summary>
/// <param name="Role">Human-readable, for the report: "Rituals", "Goblins", "Untapper".</param>
/// <param name="Cards">
/// Every card that satisfies this role. **A set, not a card**, because redundancy is what makes
/// a combo deck a deck: Splinter Twin and Kiki-Jiki fill the same slot, so a list holding four of
/// either — or two of each — satisfies it identically.
/// </param>
/// <param name="MinCopies">
/// The identity FLOOR: how many copies, across the whole set, the deck must hold. Never cut below —
/// <see cref="DeckCore.ProtectedIn"/> locks at exactly this number, and this is what makes the deck
/// the archetype it claims to be.
/// </param>
/// <param name="TargetCopies">
/// The CAP the fill aims for, and the number an optimiser is free to move. Defaults to unbounded,
/// which means "this slot absorbs the deck".
///
/// **Two numbers, because they answer different questions, and collapsing them produced 12 Dragons
/// where a real list plays six.** The floor says what the deck must contain to BE this archetype;
/// the cap says how much of it is worth playing. With only a floor, the fill ran to 60 cards
/// best-first inside the archetype pool and a slot that qualified kept getting topped up.
///
/// Unbounded is right for a slot answering a COUNT — a storm deck wants as many rituals and
/// cantrips as it can hold, and there is no such thing as too many. It is wrong for a slot
/// answering for ONE OBJECT: you fetch a Dragon, so past "enough that one survives in the library"
/// every further copy is a card you did not want to draw.
/// </param>
/// <remarks>
/// <paramref name="Cards"/> is a concrete <see cref="ImmutableHashSet{T}"/> rather than
/// <c>IReadOnlySet</c> because a core is WRITTEN TO DISK and read back by the evolver:
/// System.Text.Json serializes an interface happily and then throws `NotSupportedException` on
/// load. `Program.cs` only compares engine counts after reloading, so the failure mode was a
/// report that looked fine and un-constrained every engine slot that consumed it.
/// </remarks>
public sealed record CoreSlot(
	string Role,
	ImmutableHashSet<string> Cards,
	int MinCopies,
	int TargetCopies = int.MaxValue
)
{
	public int CountIn(Decklist deck) => Cards.Sum(deck.CopiesOf);

	public bool SatisfiedBy(Decklist deck) => CountIn(deck) >= MinCopies;

	/// Whether this slot will take another copy — the cap, not the floor.
	public bool WantsMore(Decklist deck) => CountIn(deck) < Math.Max(MinCopies, TargetCopies);
}

/// <summary>
/// **What a deck must CONTAIN, as a checkable fact rather than a resemblance.**
///
/// The question this exists to pose is not "find the highest win-rate deck" — that reliably
/// answers "a pile of the format's best cards", which is correct and useless. It is *find the
/// highest win-rate deck THAT CONTAINS this*. Win rate stays the only judge; the constraint
/// carries the identity. Nothing here scores anything, so there is no synergy-density fitness to
/// go wrong — and going wrong is measured: maximum-density goblins scored **24.4%** against the
/// AI's half-built version at **55.0%**.
///
/// **This replaces a pool-share quota, which was the wrong predicate.** That version asked for
/// "60% of spells drawn from the archetype's card pool" — a resemblance score — and a real run
/// spent the other 40% on Steppe Lynx, Gravecrawler, Liliana of the Veil and Zombie Horde Leader
/// while remaining legal throughout. A budget for drift gets spent on drift. "At least 4 Tendrils
/// and at least 8 rituals" cannot be satisfied by a deck that is not a storm deck.
///
/// One structure covers every deck class the search cannot otherwise reach:
///
/// | class | slots |
/// |---|---|
/// | combo | `(Twin effect, {Splinter Twin, Kiki-Jiki}, 4)` + `(Untapper, {Exarch, Pestermite}, 4)` |
/// | threshold | `(Goblins, {every goblin}, X)` — one slot, and X is the thing to sweep |
/// | mana engine | `(Mana, {rituals, rocks}, 8)` + `(Draw, {cantrips}, 8)` + `(Payoff, {Tendrils}, 4)` |
///
/// The mana-engine and threshold cases are buildable from data `PoolFeatures` already computes:
/// `AskersOf` is a payoff set, `SuppliersOf` is an enabler set, and `ProbeCardProfiles` net mana
/// and net cards give the ritual and cantrip sets directly.
/// </summary>
public sealed record DeckCore(string Name, IReadOnlyList<CoreSlot> Slots)
{
	/// <summary>
	/// The turn a core is built to be working by, and how reliably. Both are **design choices about
	/// what an archetype is for**, not tuned parameters — a core that assembles on turn 8 is a
	/// different deck from one that assembles on turn 5, and neither is wrong.
	///
	/// Storm runs at 12 lands and Zoo at 14; `MinFor` reads `Decklist.MinLands`, so setting
	/// `MTG_MIN_LANDS` changes the counts these produce. That is correct — a 48-spell deck needs
	/// fewer copies to find one than a 40-spell deck does.
	/// </summary>
	public const int TargetTurn = 5;

	public const double TargetProbability = 0.90;

	/// <summary>
	/// An opening hand is 7 cards containing 3 lands by rule (`BeginGameAction.OpeningHandLandCount`),
	/// so it holds only FOUR spells. Counting seven is the easy mistake and it overstates every
	/// deck's consistency.
	/// </summary>
	private const int OpeningHandSize = 7;
	private const int OpeningHandSpells = 4;

	/// <summary>
	/// **The core of a deck, read off the payoff card's own demands.**
	///
	/// This is the answer to "how does the search DISCOVER that Dragonstorm needs dragons" — it
	/// does not have to. Dragonstorm's card data already says so: `HasStorm` produces a
	/// <c>SpellsCastDemand</c> and its `SelectCardFromLibraryAction.Subtype` produces a Dragon
	/// filter, and <see cref="PoolFeatures.DemandsOf"/> has returned both since the day the card
	/// was written. What was missing is the JOIN — <see cref="EngineDiscovery"/> iterates demands
	/// one at a time, so it probes "spells cast" and "Dragon" as two unrelated concepts and neither
	/// of them is Dragonstorm.
	///
	/// **A core is a conjunction of demands anchored on a payoff CARD, and that is the unit.** It
	/// is also why tribal decks are found easily and combo decks are not: a tribal deck is one
	/// demand with a large supplier set, which random mutation stumbles into, while a combo deck is
	/// a conjunction of narrow demands, which it never will. The conjunction has to be CONSTRUCTED.
	///
	/// The payoff slot holds every card whose informative demands are a **subset** of the anchor's,
	/// so redundancy falls out without naming anything: Tendrils belongs in a Dragonstorm core
	/// (it needs strictly less), and Dragonstorm does not belong in a Tendrils core (it needs a
	/// Dragon that core never promises).
	///
	/// Returns null when the card asks nothing answerable — that is a good-stuff card, not an
	/// archetype, and it is the majority of any pool.
	/// </summary>
	public static DeckCore? For(PoolFeatures features, string payoff, int payoffCopies = 4)
	{
		var demands = features.DemandsOf(payoff).Where(features.Informative).ToList();
		if (demands.Count == 0)
			return null;

		var wanted = demands.ToHashSet();

		var interchangeable = demands
			.SelectMany(features.AskersOf)
			.Distinct(StringComparer.Ordinal)
			.Where(n =>
			{
				var theirs = features.DemandsOf(n).Where(features.Informative).ToList();
				return theirs.Count > 0 && theirs.All(wanted.Contains);
			})
			.ToHashSet(StringComparer.Ordinal);

		var slots = new List<CoreSlot>
		{
			new("Payoff", interchangeable.ToImmutableHashSet(StringComparer.Ordinal), payoffCopies),
		};

		foreach (var d in demands)
		{
			// **A payoff never supplies its own slot.** Same rule as `Satisfaction` and
			// `EngineProbe.Read`, and here it does a second job: the slots are counted
			// independently, so a card in two of them would satisfy both off one copy. Stripping
			// also produces the right shape for tribal — 4 lords in the payoff slot and 8 OTHER
			// goblins in the demand slot, rather than one slot the lords satisfy by themselves.
			var support = features.SuppliersOf(d).ToHashSet(StringComparer.Ordinal);
			support.ExceptWith(interchangeable);
			if (support.Count == 0)
				continue;

			// **Split into what CAUSES the demand and what merely MATCHES it.**
			//
			// A reanimation demand is answered by 513 creatures — every one of them could be the
			// thing in your graveyard — and by ~118 cards that actually PUT one there. Emitted as
			// one slot, `Satisfy` fills it best-first and a reanimator core comes out as a pile of
			// fatties with no discard outlet, which is the "engine decks are built badly" failure.
			// Two slots state the deck's real requirement: things to reanimate, AND a way to get
			// them there.
			//
			// Disjoint, with a card that does both filed as causal. Slots are counted independently,
			// so an overlap would let one copy satisfy both — the same rule that strips payoffs from
			// their own support slot above — and the causal role is the scarcer one.
			var causal = features
				.CausalSuppliersOf(d)
				.Where(support.Contains)
				.ToImmutableHashSet(StringComparer.Ordinal);

			var declarative = support.Except(causal).ToImmutableHashSet(StringComparer.Ordinal);

			// **Clamped to what the pool can legally provide, rather than rejecting the core.** A
			// pool holding one Dragon supports "all four copies of the only Dragon there is", which
			// is a true statement about the archetype. Rejecting instead would let `MinFor` — a
			// placeholder constant — decide which archetypes exist, and a guess must not have that
			// power.
			var role = features.Describe(d);
			AddSlot(slots, declarative, role, features, d, demands, TargetTurn);
			// **Enablers are asked for one turn earlier, and that is the whole reason the two slots
			// do not get the same number.** An outlet has to have RESOLVED before the payoff is
			// worth casting — a discard outlet on the turn you cast Reanimate is too late — so it
			// needs the higher density that an earlier deadline implies.
			AddSlot(slots, causal, $"{role} [enablers]", features, d, demands, TargetTurn - 1);
		}

		// This guard is a fact about deck legality rather than a guess, so it does reject: slots
		// that cannot fit alongside the smallest legal mana base are not a deck anyone can build.
		if (slots.Sum(s => s.MinCopies) > Decklist.DeckSize - Decklist.MinLands)
			return null;

		return new DeckCore(payoff, slots);
	}

	/// <summary>
	/// **A core anchored on a DEMAND rather than on a payoff card** — the shape a THEME request
	/// needs: "build me a graveyard deck", where no card is named.
	///
	/// This is the unit `EngineCandidate` used to be keyed on, and reviving it is not a step
	/// backwards. That unit is one level too low for *"build a Dragonstorm deck"*, because
	/// Dragonstorm is a conjunction of two demands and anchoring on either half builds something
	/// that is not Dragonstorm. It is exactly right for *"build a graveyard deck"*, because there
	/// the demand IS the request. **Two units, two questions; neither replaces the other.**
	///
	/// The payoff slot is every card that ASKS the demand, so "an artifact deck" gets Atog, Frogmite,
	/// Myr Enforcer and Thoughtcast as interchangeable payoffs and `Satisfy` picks by value — Atog
	/// appears because it is good, not because it was named.
	/// </summary>
	public static DeckCore? ForDemand(PoolFeatures features, int demandIndex, int payoffCopies = 4)
	{
		if (!features.Informative(demandIndex))
			return null;

		var askers = features.AskersOf(demandIndex).ToImmutableHashSet(StringComparer.Ordinal);
		if (askers.IsEmpty)
			return null;

		var slots = new List<CoreSlot> { new("Payoff", askers, payoffCopies) };

		var support = features.SuppliersOf(demandIndex).ToHashSet(StringComparer.Ordinal);
		support.ExceptWith(askers);

		if (support.Count > 0)
		{
			var causal = features
				.CausalSuppliersOf(demandIndex)
				.Where(support.Contains)
				.ToImmutableHashSet(StringComparer.Ordinal);

			var role = features.Describe(demandIndex);
			int[] self = [demandIndex];

			AddSlot(
				slots,
				support.Except(causal).ToImmutableHashSet(StringComparer.Ordinal),
				role,
				features,
				demandIndex,
				self,
				TargetTurn
			);
			AddSlot(
				slots,
				causal,
				$"{role} [enablers]",
				features,
				demandIndex,
				self,
				TargetTurn - 1
			);
		}

		if (slots.Sum(s => s.MinCopies) > Decklist.DeckSize - Decklist.MinLands)
			return null;

		return new DeckCore(features.Describe(demandIndex), slots);
	}

	private static void AddSlot(
		List<CoreSlot> slots,
		ImmutableHashSet<string> cards,
		string role,
		PoolFeatures features,
		int demandIndex,
		IReadOnlyList<int> payoffDemands,
		int byTurn
	)
	{
		if (cards.IsEmpty)
			return;

		var min = Math.Min(
			MinFor(features, demandIndex, payoffDemands, byTurn),
			cards.Count * Decklist.MaxCopies
		);

		// **A FETCHED slot is capped at its floor; everything else absorbs.** You search a Dragon out
		// of the library, so once enough survive to be found, every further copy is a card you did
		// not want to draw — measured, the uncapped version played 12 Dragons where a real list plays
		// six. A slot answering a COUNT is the opposite: a storm deck wants every ritual and cantrip
		// it can hold, and capping it would evict the cards the archetype is made of.
		//
		// The floor is where the cap STARTS, not where it belongs. It is the number an optimiser
		// should move, and it is derived rather than guessed so there is something honest to move
		// away from.
		var target = IsFetched(features, demandIndex) ? min : int.MaxValue;

		slots.Add(new CoreSlot(role, cards, min, target));
	}

	/// <summary>
	/// **How many copies a slot needs, DERIVED rather than picked.** Replaces a flat
	/// <see cref="DefaultSlotCopies"/> of 8, which was a guess applied identically to a Dragon slot,
	/// a ritual slot and a discard-outlet slot.
	///
	/// Two rules, in order:
	///
	/// 1. **A demand carrying its own number uses it.** `SpellsCastDemand.Minimum` is harvested
	///    straight off `HasStorm` and `RequiresSpellsCastThisTurnRestriction`, so "storm 2" is a
	///    fact in the card data and not something to search for. It asks for m spells cast in ONE
	///    turn including the payoff, so m-1 others must be in hand at the same time — a density
	///    question, not a draw-at-least-one question.
	/// 2. **Everything else is consistency**: the fewest copies that put at least one in your hand
	///    by <paramref name="byTurn"/> with probability <see cref="TargetProbability"/>.
	/// </summary>
	private static int MinFor(
		PoolFeatures features,
		int demandIndex,
		IReadOnlyList<int> payoffDemands,
		int byTurn
	)
	{
		var spells = Decklist.DeckSize - Decklist.MinLands;
		var seen = SpellsSeenBy(byTurn, spells);

		if (
			features.Demands[demandIndex] is PoolFeatures.SpellsCastDemand storm
			&& storm.Minimum > 1
		)
			return (int)Math.Ceiling((storm.Minimum - 1) * spells / Math.Max(1.0, seen));

		// **A card you FETCH is never drawn, so the consistency rule asks a question the deck does
		// not have to answer.** What it needs is enough copies still SITTING IN THE LIBRARY when the
		// payoff resolves — and how many that is comes from the payoff's OTHER demand, because storm
		// resolves the spell `Math.Max(SpellsCastThisTurn, 1)` times and Dragonstorm fetches one
		// Dragon per copy. Two demands on one card that are not independent; this is the only place
		// the model expresses that.
		if (IsFetched(features, demandIndex))
		{
			var fetches = payoffDemands
				.Select(d => features.Demands[d])
				.OfType<PoolFeatures.SpellsCastDemand>()
				.Select(s => s.Minimum)
				.DefaultIfEmpty(1)
				.Max();
			return CopiesThatSurvive(fetches, seen, spells);
		}

		for (var copies = 1; copies <= spells; copies++)
			if (1.0 - MissChance(copies, seen, spells) >= TargetProbability)
				return copies;

		return spells;
	}

	/// <summary>
	/// Was this demand harvested from an action that SEARCHES for the card, rather than from one
	/// that requires it in hand or in play?
	///
	/// ponytail: matched on the recorded origin string, which is `{ownerType}.{property}` and
	/// already survives the demand remap. Recording a bool at harvest time would be sturdier;
	/// it is the same threading `CausalSupplyOf` needed and worth doing if this list grows.
	///
	/// `DemandZone` cannot answer this — a bare subtype spec matches cards in every zone of the
	/// probe fixture, so it returns null for exactly the demand in question.
	/// </summary>
	private static bool IsFetched(PoolFeatures features, int demandIndex)
	{
		var origin = features.OriginOf(demandIndex);
		return origin.Contains("SelectCard", StringComparison.Ordinal)
			|| origin.Contains("SearchLibrary", StringComparison.Ordinal);
	}

	/// <summary>
	/// Fewest copies such that at least <paramref name="need"/> are still UNDRAWN with probability
	/// <see cref="TargetProbability"/> — the complement of the consistency question.
	///
	/// Each copy is treated as independently drawn with probability `seen / spells`. Sound while the
	/// cards seen are a small share of the deck (~7 of 40-48 here); it slightly understates the
	/// spread, and understating a FLOOR is the safe direction.
	///
	/// **This produces a floor, not the list a human would build.** At `Minimum = 2` a Dragonstorm
	/// core asks for 3 Dragons; the historical Standard deck ran 6, because it wanted a storm count
	/// of ~4 converted into lethal, and "enough to kill" is not in the card data. That gap is
	/// correct — a core is a floor with slack, `ProtectedIn` only locks a slot AT its minimum, and
	/// tuning is free to find 6. A floor of 11 would have forced a deck nobody would build.
	/// </summary>
	private static int CopiesThatSurvive(int need, double seen, int spells)
	{
		var drawn = Math.Clamp(seen / Math.Max(1, spells), 0.0, 1.0);

		for (var copies = need; copies <= spells; copies++)
		{
			var enough = 0.0;
			for (var gone = 0; gone <= copies - need; gone++)
				enough +=
					Choose(copies, gone)
					* Math.Pow(drawn, gone)
					* Math.Pow(1 - drawn, copies - gone);

			if (enough >= TargetProbability)
				return copies;
		}

		return spells;
	}

	private static double Choose(int n, int k)
	{
		var result = 1.0;
		for (var i = 0; i < k; i++)
			result = result * (n - i) / (i + 1);
		return result;
	}

	/// <summary>
	/// Non-land cards seen by a given turn.
	///
	/// **An opening hand is 7 cards of which 3 are lands by rule** (`BeginGameAction`), so it
	/// contains only FOUR spells — treating it as seven would overstate every deck's consistency and
	/// understate every count here. After that each draw is one card from the 53 that remain, of
	/// which the spell share is what is left of the spell population.
	/// </summary>
	private static double SpellsSeenBy(int turn, int spells)
	{
		var remaining = Decklist.DeckSize - OpeningHandSize;
		var spellShare = Math.Max(0, spells - OpeningHandSpells) / (double)remaining;
		return OpeningHandSpells + Math.Max(0, turn) * spellShare;
	}

	/// Hypergeometric: the chance that NONE of <paramref name="copies"/> appears in the cards seen.
	private static double MissChance(int copies, double seen, int spells)
	{
		var miss = 1.0;
		for (var i = 0; i < copies; i++)
		{
			var left = spells - seen - i;
			if (left <= 0)
				return 0;
			miss *= left / (spells - i);
		}
		return miss;
	}

	public bool Holds(Decklist deck) => Slots.All(s => s.SatisfiedBy(deck));

	/// Slots this deck fails, with what it has against what it needs. For the report, and for
	/// telling "the constraint is binding" apart from "the constraint is broken".
	public IReadOnlyList<string> Missing(Decklist deck) =>
		Slots
			.Where(s => !s.SatisfiedBy(deck))
			.Select(s => $"{s.Role} {s.CountIn(deck)}/{s.MinCopies}")
			.ToList();

	/// <summary>
	/// Cards that cannot be cut without breaking the core.
	///
	/// **This is the enforcement, and checking the result afterwards is only the backstop.** A
	/// constraint applied by rejecting finished mutants throws away a mutation slot every time it
	/// fires, and — worse — a deck that somehow starts below the constraint can never propose a
	/// legal mutant again and freezes solid, silently. Protecting the cut instead makes proposals
	/// legal by construction.
	///
	/// **Slack is what makes this precise rather than a blanket freeze.** A slot holding more than
	/// its minimum protects nothing: evolution may still discover that a storm deck wants fewer
	/// rituals, and cut them one at a time until the floor. Only at the floor does the slot lock,
	/// and only the cards actually filling it. That is the difference between a constraint and a
	/// frozen decklist — and freezing decklists is what produced the 24.4% goblin result.
	/// </summary>
	public IReadOnlySet<string> ProtectedIn(Decklist deck)
	{
		var locked = new HashSet<string>(StringComparer.Ordinal);
		foreach (var slot in Slots)
		{
			if (slot.CountIn(deck) > slot.MinCopies)
				continue;
			foreach (var name in slot.Cards)
				if (deck.CopiesOf(name) > 0)
					locked.Add(name);
		}
		return locked;
	}

	/// <summary>
	/// **Satisfy the core, then fill the rest FROM THE ARCHETYPE'S OWN POOL.**
	///
	/// A core is a floor, so satisfying it leaves most of the deck free — and filling those slots
	/// from the whole format by standalone card value is how the good-stuff failure survives being
	/// evicted from the core. Measured: a Dragonstorm core produced 16 core cards and **26 flex**,
	/// and the flex was Atog, Frogmite, Kird Ape and Myr Enforcer — pieces of three unrelated decks.
	///
	/// **Nothing here needs to know it is building a "mana engine deck".** That is a human label for
	/// a relation the demand model already computes: storm's suppliers are the cards `ProbeManaProfit`
	/// found to be mana- or card-positive, which on ALL is Lotus Bloom, Ancestral Recall, Rain of
	/// Revelation and 82 others — rituals, cantrips, draw and tutors, derived rather than named. The
	/// archetype's card pool IS the answer to "what else should this deck play"; it was simply not
	/// being asked.
	///
	/// Same rule as `DeckBuilder.EngineIdentity`, which narrows what `Mutate` may draw from, and for
	/// the reason recorded there: a POOL LOCK beats a quota, because a budget for drift gets spent
	/// on drift. The format-wide pass runs only as a top-up, since a narrow archetype may not hold
	/// 60 cards.
	/// </summary>
	public Decklist Complete(
		Decklist deck,
		IReadOnlyList<Card> spells,
		ConstructedValues values,
		PoolFeatures? features = null
	)
	{
		// **The core's own slot cards, with NO transitive expansion.** Widening this by "also
		// everything answering a demand the core's cards ask" was tried and is vacuous: storm's 85
		// suppliers each ask demands answered by most of the pool, so one step of closure returns
		// the whole format and archetype-first fill becomes format-first fill. Both arms came out
		// byte-identical, which is what a no-op looks like when it is dressed as a feature.
		//
		// `DeckBuilder.EngineIdentity` locks mutation to exactly `Payoffs ∪ Enablers` for the same
		// reason.
		var mine = Slots.SelectMany(s => s.Cards).ToHashSet(StringComparer.Ordinal);

		deck = Satisfy(deck, values);
		deck = FillFrom(deck, spells.Where(c => mine.Contains(c.Name)).ToList(), values, this);
		return FillFrom(deck, spells, values, this);
	}

	/// <summary>
	/// Adds copies best-first, **stopping at each slot's cap**.
	///
	/// The cap is checked per card rather than per slot because slots are disjoint, so a card
	/// belongs to at most one and the lookup is unambiguous. A card in no slot is off-theme filler
	/// and is uncapped — the archetype pass never reaches it anyway.
	/// </summary>
	private static Decklist FillFrom(
		Decklist deck,
		IReadOnlyList<Card> candidates,
		ConstructedValues values,
		DeckCore core
	)
	{
		var slotOf = new Dictionary<string, CoreSlot>(StringComparer.Ordinal);
		foreach (var slot in core.Slots)
		foreach (var name in slot.Cards)
			slotOf.TryAdd(name, slot);

		foreach (
			var card in candidates
				.OrderByDescending(c => values.CardDelta(c.Name))
				.ThenBy(c => c.Name, StringComparer.Ordinal)
		)
		{
			var room = Decklist.DeckSize - deck.Lands - deck.SpellCount;
			if (room <= 0)
				break;

			var have = deck.CopiesOf(card.Name);
			if (have >= Decklist.MaxCopies)
				continue;

			if (slotOf.TryGetValue(card.Name, out var slot))
			{
				var headroom = Math.Max(slot.MinCopies, slot.TargetCopies) - slot.CountIn(deck);
				if (headroom <= 0)
					continue;
				room = Math.Min(room, headroom);
			}

			deck = deck.WithCopies(card.Name, Math.Min(Decklist.MaxCopies, have + room));
		}

		return deck;
	}

	/// <summary>
	/// Fills a deck up to every slot's minimum, best-valued card first within each role.
	///
	/// Deterministic on purpose. This is the *setup* for an experiment about how much of a theme
	/// a deck wants; sampling here would mix that question with a question about the sampler.
	/// </summary>
	public Decklist Satisfy(Decklist deck, ConstructedValues values)
	{
		foreach (var slot in Slots)
		{
			foreach (
				var name in slot
					// **The ANCHOR first, and this was a real bug.** The payoff slot holds every card
					// whose demands are a SUBSET of the anchor's, so a card that asks strictly less
					// belongs there — but it must not REPLACE the anchor, because the rest of the core
					// exists to serve the anchor's demands. Filling best-first by card value built a
					// Dragonstorm deck holding Tendrils of Agony and four Dragons that nothing
					// fetches: the mirror of the dead-card failure this whole structure prevents,
					// and `Holds` returned true throughout.
					.Cards.OrderByDescending(n => string.Equals(n, Name, StringComparison.Ordinal))
					.ThenByDescending(values.CardDelta)
					.ThenBy(n => n, StringComparer.Ordinal)
			)
			{
				if (slot.CountIn(deck) >= slot.MinCopies)
					break;
				var room = Math.Min(
					Decklist.MaxCopies,
					Decklist.DeckSize - deck.Lands - deck.SpellCount + deck.CopiesOf(name)
				);
				if (room > deck.CopiesOf(name))
					deck = deck.WithCopies(name, room);
			}
		}
		return deck;
	}
}
