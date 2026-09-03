using System.Collections.Immutable;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Builds and mutates constructed decklists.
///
/// Seeding and mutation live in one file because mutation reuses the seeder's weighted card
/// sampler; splitting them would duplicate it or export it just to be shared.
///
/// **A seed is not 36 random cards.** A human building a constructed deck starts from a
/// concept — a payoff card, a package that supports it, a curve — and fills around it. The
/// same shape is available here without hand-labelling a single card, because the trained
/// model already carries both halves: per-card win rates say what is good, and the measured
/// pair table says what wants to be together. Both were found unsupervised.
///
/// A seed is therefore: an ANCHOR (sampled by card value), a KERNEL of its best-evidenced
/// synergy partners, a CURVE TARGET, then a weighted fill. Archetypes fall out of the curve
/// and the kernel rather than being enumerated anywhere.
/// </summary>
public static class DeckBuilder
{
	/// <summary>
	/// Softmax temperature for card sampling, in percentage points of win rate — the same
	/// units DraftPickers uses. High, because seeding wants spread across the pool rather than
	/// the argmax: eight decks that all open on the best card in the format are one deck.
	/// </summary>
	public const double SeedTemperature = 4.0;

	/// Tighter than seeding: a mutation is a considered swap, not exploration.
	public const double MutateTemperature = 2.0;

	/// How many synergy partners form the kernel around the anchor.
	public const int KernelSize = 4;

	/// Points of score lost per point of mana cost away from the deck's curve target.
	public const double CurvePenalty = 1.5;

	/// <summary>
	/// Score added for a card with no constructed evidence yet, scaled by how unmeasured it is.
	///
	/// Without it the mode is purely exploitative: a card gets played because it has good data
	/// and has good data because it was played. Measured over 100 generations, only 343 of 785
	/// cards were ever tried, and cards in final decks had a median 11 310 games against 998
	/// for the rest.
	///
	/// Sized to roughly one point of win rate — enough to get an unknown card looked at, far
	/// too small to keep it in a deck it loses with. The presimulation is the bigger half of
	/// this fix; the bonus is what keeps late generations still sampling.
	/// </summary>
	public const double ExplorationBonus = 3.0;

	/// <summary>
	/// Points subtracted from a card whose demands this deck answers with NOTHING — Dragonstorm
	/// with no dragons, Thoughtcast with no artifacts.
	///
	/// **This ranks the cut; it never deletes a card, and that limit is deliberate.**
	/// `Satisfaction == 0` cannot tell a card that does literally nothing from a perfectly good
	/// body carrying an irrelevant rider: a 7-mana Dragonstorm with no dragons and a 2/2 for 2
	/// that would gain 1 life if a Goblin entered both read exactly 0. A second false positive is
	/// structural — a "whenever a creature dies" trigger with no controller restriction fires on
	/// the OPPONENT's creatures, so a creatureless control deck supplies zero while the card is
	/// live all game.
	///
	/// Sized in the same percentage points as <see cref="ConstructedValues.CardDelta"/>, and small
	/// on purpose: a card winning by 10pp survives it easily, while a card that is merely average
	/// gets cut first. So the measured win rate still decides and this only breaks ties —
	/// Dragonstorm leaves in generation 2 instead of generation 30, and the 2/2 stays as long as
	/// it keeps winning.
	/// </summary>
	public const double DeadCardPenalty = 5.0;

	/// <summary>
	/// Points added for a card whose demands the deck already answers — "I have goblins, so try
	/// the lord" and "I have the lord, so try goblins", which is the direction single-card hill
	/// climbing cannot travel on its own.
	///
	/// Because `Fill` scores against the PARTIALLY built deck, this compounds as a deck fills:
	/// the first goblin makes the second slightly more attractive. That is the clustering the
	/// pair table was supposed to provide and could never evidence.
	/// </summary>
	public const double SupportBonus = 4.0;

	/// Satisfaction at which <see cref="SupportBonus"/> is fully paid. Bounded rather than linear
	/// for the same reason `DeckFit` averages: an unbounded term makes card quality a rounding
	/// error, which this file has already been burned by once.
	private const double FullSupport = 12.0;

	/// <summary>
	/// What KIND of deck a slot is told to build, expressed as the only lever the engine actually
	/// has: where on the curve it sits.
	///
	/// **Deliberately three bands of one existing number, not a new mechanism.** There are no
	/// colours, and `curveTarget` already drives land count through `LandsForCurve`, so an aggro
	/// deck is a low curve with fewer lands and a control deck is the reverse. Anything more
	/// ("play removal", "play card draw") would be the hand-labelling this project rejected.
	///
	/// <see cref="DeckProfile.Any"/> is the pre-existing behaviour — draw uniformly across the
	/// whole range — and stays the default so nothing changes for callers that do not ask.
	///
	/// **Synergy slots deliberately take no profile.** Affinity and reanimator have high printed
	/// curves and low real ones, so a band drawn from anywhere but the concept itself fights the
	/// deck; `SeedConcept` uses the concept's own average cost instead.
	/// </summary>
	public enum DeckProfile
	{
		Any,
		Aggro,
		Midrange,
		Control,
	}

	/// <summary>
	/// The profiles a non-concept slot cycles through. Order matters only in that it spreads the
	/// bands across adjacent slots; nothing downstream reads it.
	/// </summary>
	private static readonly DeckProfile[] Profiles =
	[
		DeckProfile.Aggro,
		DeckProfile.Midrange,
		DeckProfile.Control,
	];

	/// <summary>
	/// Which profile a field slot gets. **Public because the evolver has to ENFORCE the same band the
	/// seeder assigned**, and two copies of this cycling would drift into disagreeing about what
	/// "Aggro-G" means.
	/// </summary>
	public static DeckProfile ProfileForSlot(int index, int conceptSlots, bool wildcard) =>
		wildcard || index < conceptSlots
			? DeckProfile.Any
			: Profiles[(index - conceptSlots) % Profiles.Length];

	/// Curve band per profile. Bands overlap, because the boundary between aggro and midrange is
	/// a spectrum and a hard edge would just be a different arbitrary number.
	internal static (double Min, double Max) BandFor(DeckProfile profile) =>
		profile switch
		{
			DeckProfile.Aggro => (MinCurveTarget, 2.7),
			DeckProfile.Midrange => (2.4, 3.6),
			DeckProfile.Control => (3.3, MaxCurveTarget),
			_ => (MinCurveTarget, MaxCurveTarget),
		};

	private const double MinCurveTarget = 2.0;
	private const double MaxCurveTarget = 4.5;

	/// <summary>
	/// How much this deck supports one card, in `CardDelta` units. Zero when features are absent
	/// (the production default today) or when the card asks nothing of the deck.
	///
	/// **NaN and 0 mean opposite things and both arrive here.** NaN is "asks nothing" — a burn
	/// spell, a vanilla creature — which is neither rewarded nor punished. 0 is "asks and gets
	/// nothing", which is the penalty case.
	///
	/// **Reads the WEAKEST demand, not the sum across them.** Dragonstorm asks for spells cast
	/// this turn AND for a Dragon to find; summed, a storm deck answers the first so richly that
	/// the total looks healthy and the card is seeded into a deck containing no Dragons, where it
	/// resolves for nothing. A card is only as good as its starving demand.
	/// </summary>
	private static double SupportScore(string name, Decklist deck, PoolFeatures? features)
	{
		if (features is null)
			return 0;

		var satisfaction = features.WeakestSatisfaction(name, deck);
		if (double.IsNaN(satisfaction))
			return 0;

		return satisfaction <= 0
			? -DeadCardPenalty
			: SupportBonus * Math.Min(satisfaction / FullSupport, 1.0);
	}

	/// <summary>
	/// Fewest suppliers a demand needs before it can be the concept of a whole deck. A demand two
	/// cards answer is a nice interaction, not an archetype.
	/// </summary>
	public const int MinConceptSuppliers = 6;

	/// <summary>
	/// `CardDelta` points per unit of supply, inside concept seeding only.
	///
	/// Sized against `SupportBonus` (4.0) rather than tuned: one unit of supply should be worth
	/// about as much as being well-supported, so a Lotus Bloom at supply 4 carries ~12 points and
	/// comfortably outbids the ~5-15 point `CardDelta` spread between a good card and a mediocre
	/// one — which is exactly the margin that was burying the rituals.
	///
	/// **This belongs to GENERATION and must never reach judgement.** It decides what gets
	/// proposed; the measured win rate still decides what survives. Max-density goblins scored
	/// 24.4% against the AI's half-built goblin deck at 55.0%, so a fitness that rewards synergy
	/// density directly would rebuild that result on purpose.
	/// </summary>
	private const double ConceptSupplyWeight = 3.0;

	/// <summary>
	/// A deck built by COMMITTING to one concept and jamming it, rather than by hill climbing
	/// toward it one card at a time.
	///
	/// **This is the operator the mode was missing, and no scoring change substitutes for it.**
	/// Real deckbuilding picks a concept, plays every card in the pool that serves it, measures,
	/// and then prunes *within* the concept — abandoning it only once the pool is out of options.
	/// Nobody arrives at Goblins by adding eight Goblins to a midrange pile one at a time; that
	/// tanks the win rate at every intermediate step, which is exactly what `Mutate`'s
	/// accept-if-better rule then rejects. `Package` was the first attempt and is too small: an
	/// anchor plus two partners, where a concept needs its whole critical mass at once.
	///
	/// The candidate set is both halves of the concept — every card that ANSWERS the demand and
	/// every card that ASKS it. Without the payoffs you build 24 artifacts and no Atog; without
	/// the suppliers you build Atog and nothing to eat.
	///
	/// **No curve target is imposed.** Affinity and reanimator have high printed curves and low
	/// real ones, so a target drawn from anywhere but the concept itself would fight the deck. The
	/// concept's own average cost is used instead, and land count is then tuned by `AdjustLands`
	/// like any other deck's.
	///
	/// Returns null when the pool has no demand with <see cref="MinConceptSuppliers"/> suppliers —
	/// the caller falls back to an ordinary seed rather than failing.
	/// </summary>
	/// <param name="demandIndex">
	/// Which concept to build. Null picks one at random among the viable demands; the caller
	/// passes distinct indices when seeding several concept slots, since three slots that all
	/// discover Goblins are one deck and the diversity floor would reject two of them anyway.
	/// </param>
	public static Decklist? SeedConcept(
		string name,
		IReadOnlyList<Card> pool,
		ConstructedValues values,
		PoolFeatures features,
		Random rng,
		int? demandIndex = null
	)
	{
		var spells = pool.Where(c => !c.HasSubtype("Land")).ToList();
		if (spells.Count == 0)
			return null;

		var present = spells.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);

		var chosen = demandIndex ?? PickConcept(features, present, rng);
		if (chosen is null)
			return null;

		// Both halves: what answers the demand, and what asks it.
		var wanted = features
			.SuppliersOf(chosen.Value)
			.Where(present.Contains)
			.ToHashSet(StringComparer.Ordinal);
		foreach (var card in spells)
			if (features.DemandsOf(card.Name).Contains(chosen.Value))
				wanted.Add(card.Name);

		var candidates = spells.Where(c => wanted.Contains(c.Name)).ToList();
		if (candidates.Count == 0)
			return null;

		var curve = candidates.Average(c => c.ManaCost);
		var lands = LandsForCurve(Math.Clamp(curve, MinCurveTarget, MaxCurveTarget), rng);
		var deck = Decklist.Empty(name) with { Lands = lands };

		// **The payoffs go in FIRST, and this is not a preference — without it the method breaks
		// its own contract.** The jam loop below samples the whole candidate set, so when a demand
		// has 400 suppliers and 4 askers the askers are essentially never drawn: a storm concept
		// comes back as 36 cheap spells and no Tendrils, which is exactly the "24 artifacts and no
		// Atog" failure ruled out three paragraphs above. Measured on the ALL pool, **17 of 39
		// viable concepts produced a deck with no payoff in it at all**, and the broad demands —
		// the ones whose payoff is rarest and therefore most worth finding — were systematically
		// the ones that failed.
		//
		// Capped at a third of the spell slots so a concept with many askers still leaves room for
		// the support that makes them work. Same softmax as the main loop, so two slots on one
		// concept still differ.
		var askers = candidates
			.Where(c => features.DemandsOf(c.Name).Contains(chosen.Value))
			.ToList();
		var payoffRoom = (Decklist.DeckSize - lands) / 3;
		while (deck.SpellCount < payoffRoom && askers.Count > 0)
		{
			// SupportScore, not raw value — a payoff with a SECOND demand this deck cannot answer
			// is a blank. Dragonstorm asks for spells cast this turn and for a Dragon to find, and
			// seeding payoffs on card value alone jammed it into a storm deck holding no Dragons,
			// where the dead-card rule then correctly flagged it after the fact.
			var askerScores = askers
				.Select(c => values.CardDelta(c.Name) + SupportScore(c.Name, deck, features))
				.ToArray();
			var payoff = askers[DraftPickers.SampleSoftmax(askerScores, SeedTemperature, rng)];
			deck = deck.WithCopies(
				payoff.Name,
				Math.Min(Decklist.MaxCopies, payoffRoom - deck.SpellCount)
			);
			askers.Remove(payoff);
		}

		// Jam it. Four copies at a time, best-first with enough noise that two slots on the same
		// concept are not the same 36 cards — this is "shove in as many as will fit and find out
		// which ones pull their weight", which is what the refinement loop is for.
		//
		// Cards already placed above are excluded rather than left in: `WithCopies` SETS a count,
		// so re-picking a payoff would overwrite its four copies with however much room is left.
		var remaining = candidates.Where(c => deck.CopiesOf(c.Name) == 0).ToList();
		while (deck.SpellCount < Decklist.DeckSize - lands && remaining.Count > 0)
		{
			// **How much this card supplies THE CONCEPT, not just whether it qualifies.** Without
			// this term the candidate set is correct and unsortable — every supplier scores the
			// same through `SupportScore`, so `CardDelta` decides, and a 513-card reanimator pool
			// gets sampled for its best creatures rather than its dozen discard outlets. Storm
			// came out as the format's best draw spells with no rituals in it.
			//
			// `SupplyOf` carries the magnitude: net mana and cards for a storm demand, cards moved
			// for a graveyard one, bodies produced for a token maker, and a flat 1 for a plain
			// filter match — a Goblin is a Goblin.
			var scores = remaining
				.Select(c =>
					values.CardDelta(c.Name)
					+ SupportScore(c.Name, deck, features)
					+ ConceptSupplyWeight * features.SupplyOf(chosen.Value, c.Name)
				)
				.ToArray();
			var pick = remaining[DraftPickers.SampleSoftmax(scores, SeedTemperature, rng)];
			var room = Math.Min(Decklist.MaxCopies, Decklist.DeckSize - lands - deck.SpellCount);
			deck = deck.WithCopies(pick.Name, room);
			remaining.Remove(pick);
		}

		// A concept too small to fill 36 slots is topped up from the whole pool rather than
		// abandoned — a 20-card artifact package plus good cards is still an artifact deck.
		return deck.SpellCount < Decklist.DeckSize - lands
			? Fill(deck, spells, values, rng, curve, 1.0, SeedTemperature, features)
			: deck;
	}

	/// <summary>
	/// A concept worth building, sampled among demands with enough suppliers to fill a deck and
	/// weighted toward the DISTINCTIVE ones.
	///
	/// **This weighted by supplier count and that was backwards.** Measured on CSC over 8 decks x
	/// 25 generations: it drew the broadest demands — "creatures you control" (237 of 408),
	/// "creature card in your graveyard" (237), "creature mana value <= 3" (182) — which are
	/// nearly the same card set and none of which is an archetype. Three "distinct" concepts
	/// produced three similar decks, so the field STARTED at 59% diversity against the control's
	/// 69% and collapsed to 48% within one generation. Goblin (18 suppliers) was almost never
	/// drawn.
	///
	/// Inverse frequency instead: a concept 58% of the pool answers cannot produce a deck that
	/// differs from any other deck, because a deck built at random already satisfies it. On CSC
	/// this makes Goblin ~14x likelier than "creatures you control" rather than 13x less likely.
	///
	/// **This is not the rarity cutoff rejected in the extractor.** That rejection was about
	/// SCORING whether a card is supported, where breadth is irrelevant — anthem-plus-cheap-tokens
	/// is a real deck and "creatures you control" is exactly the right demand for it. Choosing
	/// what a whole deck is ABOUT is the opposite question. Two rules, deliberately not shared.
	///
	/// No ceiling constant: a broad demand stays reachable, just rare, so the arm still explores.
	/// </summary>
	private static int? PickConcept(
		PoolFeatures features,
		HashSet<string> present,
		Random rng,
		HashSet<int>? exclude = null
	)
	{
		var viable = new List<(int Demand, int Suppliers)>();
		for (var d = 0; d < features.Demands.Count; d++)
		{
			if (exclude is not null && exclude.Contains(d))
				continue;
			var n = features.SuppliersOf(d).Count(present.Contains);
			if (n >= MinConceptSuppliers)
				viable.Add((d, n));
		}

		if (viable.Count == 0)
			return null;

		var weights = viable.Select(v => 1.0 / v.Suppliers).ToList();
		var roll = rng.NextDouble() * weights.Sum();
		for (var i = 0; i < viable.Count; i++)
		{
			roll -= weights[i];
			if (roll <= 0)
				return viable[i].Demand;
		}
		return viable[^1].Demand;
	}

	/// <summary>
	/// A fresh decklist built around a randomly chosen concept.
	/// </summary>
	/// <param name="wildcard">
	/// The exploration arm. Samples its anchor UNIFORMLY, ignoring card value entirely, and
	/// leans harder on synergy. It will often be bad and get culled — that is the mechanism,
	/// not a failure of it. Without a slot that ignores what the model already believes, the
	/// field can only ever refine the cards the prior already liked, and a combo deck built
	/// from individually-mediocre pieces is unreachable.
	/// </param>
	public static Decklist Seed(
		string name,
		IReadOnlyList<Card> pool,
		ConstructedValues values,
		Random rng,
		bool wildcard = false,
		PoolFeatures? features = null,
		DeckProfile profile = DeckProfile.Any
	)
	{
		var spells = pool.Where(c => !c.HasSubtype("Land")).ToList();
		if (spells.Count == 0)
			throw new ArgumentException("Card pool has no non-land cards.", nameof(pool));

		var (bandMin, bandMax) = BandFor(profile);
		var curveTarget = bandMin + rng.NextDouble() * (bandMax - bandMin);
		var lands = LandsForCurve(curveTarget, rng);

		var deck = Decklist.Empty(name) with { Lands = lands };

		// 1. Anchor — the concept the deck is about.
		var anchor = wildcard
			? spells[rng.Next(spells.Count)]
			: spells[
				DraftPickers.SampleSoftmax(
					spells.Select(c => values.CardDelta(c.Name)).ToArray(),
					SeedTemperature,
					rng
				)
			];
		deck = deck.WithCopies(anchor.Name, Decklist.MaxCopies);

		// 2. Kernel — what measurably wants to be alongside it.
		var byName = spells.ToDictionary(c => c.Name, StringComparer.Ordinal);
		foreach (var (partner, _) in values.TopPartners(anchor.Name, spells, KernelSize))
		{
			if (!byName.ContainsKey(partner))
				continue;
			deck = deck.WithCopies(partner, 3 + rng.Next(2));
			if (deck.SpellCount >= Decklist.DeckSize - lands)
				break;
		}

		// 3. Fill, in chunks — constructed decks play multiples, not 39 singletons.
		var synergyWeight = wildcard ? 2.0 : 1.0;
		return Fill(
			deck,
			spells,
			values,
			rng,
			curveTarget,
			synergyWeight,
			SeedTemperature,
			features
		);
	}

	/// <summary>
	/// One mutated copy of a deck. Returns null when the operator could not produce a valid
	/// distinct deck (an empty pool of candidates, a land move that would break the range),
	/// which the caller treats as "no proposal this round" rather than an error.
	///
	/// Operators are deliberately small. A mutation has to be measurable against its parent
	/// over a few dozen games, and a large rewrite is indistinguishable from a fresh seed —
	/// it destroys the hill being climbed.
	/// </summary>
	/// <param name="history">
	/// What this deck slot has learned about its own cards. When supplied, cutting is
	/// synergy-aware: a card whose pairs underperform inside this deck is likelier to be cut,
	/// and a card carrying several winning pairs is protected. Null falls back to overall card
	/// quality alone, which is all that is available in generation 1.
	/// </param>
	public static Decklist? Mutate(
		Decklist deck,
		IReadOnlyList<Card> pool,
		ConstructedValues values,
		Random rng,
		DeckHistory? history = null,
		PoolFeatures? features = null,
		DeckCore? core = null,
		DeckProfile profile = DeckProfile.Any,
		IReadOnlyDictionary<string, double>? contextValue = null,
		bool exploring = false
	)
	{
		var spells = pool.Where(c => !c.HasSubtype("Land")).ToList();

		// **A core narrows what mutation may DRAW FROM, not just what it may cut.**
		//
		// `ProtectedIn` below stops the archetype being cut away; on its own that keeps the floors
		// and lets every free slot drift back to the format's best cards — which is the good-stuff
		// failure re-entering through the one door the core does not watch. Measured on the build
		// side: filling flex from the format gave a Dragonstorm deck 61% off-theme cards (Atog,
		// Frogmite, Kird Ape); filling from the archetype pool gave 100% on-theme.
		//
		// A POOL LOCK beats a share quota, and that is not a preference — a real run allowed 40%
		// drift and the storm slot spent all of it on Steppe Lynx, Gravecrawler and Liliana while
		// staying legal throughout. A budget for drift gets spent on drift.
		//
		// It converges INWARD: cutting stays unconstrained, so a deck that starts impure cleans
		// itself up and nothing outside can return. Evolution may still discover a storm deck wants
		// fewer rituals; it cannot discover that it wants Steppe Lynx.
		if (core is not null)
		{
			var identity = core.Slots.SelectMany(s => s.Cards).ToHashSet(StringComparer.Ordinal);
			spells = spells.Where(c => identity.Contains(c.Name)).ToList();
			if (spells.Count == 0)
				return null;
		}

		var costs = spells.ToDictionary(c => c.Name, StringComparer.Ordinal);

		// **A profile is a CONSTRAINT, not a starting label, and it used to be the latter.** The
		// curve target was read off the deck's own current average, so after seeding nothing held a
		// slot to its band and "Aggro-G" was a generation-0 name. A real run drifted into a Past in
		// Flames card-advantage pile holding Wrath of God and Aetherspouts — and MORE generations
		// makes that worse, not better, because the target follows wherever the deck went.
		//
		// Clamped rather than pinned to the band's midpoint: a deck already inside its band is free
		// to sit anywhere in it, and the pull only appears once it leaves.
		var curveTarget = deck.AverageCost(costs);
		if (profile != DeckProfile.Any)
		{
			var (lo, hi) = BandFor(profile);
			curveTarget = Math.Clamp(curveTarget, lo, hi);
		}

		// Weighted: swapping cards is the operator that actually explores the card pool, so it
		// gets most of the budget. Land moves are one integer and converge quickly.
		// Package size is rolled independently so the mutator is not ALWAYS hunting synergy:
		// 0 partners is "just put a good card in this slot", 1 is a pair, 2 is a triple. A
		// mutator that only ever proposes packages narrows the field to whatever the pair table
		// already believes, and pair evidence is the thinnest thing in the model.
		// Cards holding a core slot at its floor, computed once. Every operator cuts through
		// `PickWeakest`, so handing it down is the whole enforcement.
		var locked = core?.ProtectedIn(deck);

		// **Two operators exist only under a core, and the roll is unchanged without one.** Mode 6's
		// unconstrained slots must behave exactly as they did, or every existing measurement in this
		// file becomes incomparable for a reason that has nothing to do with what was changed.
		// **Exploration moves in PLAYSETS and never by one copy.**
		//
		// 59% of all proposals in a measured run moved exactly one copy, and those were the least
		// accepted (22% against 50% for two-copy moves). The reason is not preference: a candidate
		// plays ~66 games, so one standard error is ~6pp, while a one-copy change is ~2% of a deck
		// and its true effect is a fraction of a point. Every such accept/reject is a coin flip —
		// which is what the oscillation guard was really patching over, and why the same pair could
		// swap back and forth scoring +6.1pp in BOTH directions.
		//
		// So `Recount` (±1 by construction) and `AdjustLands` (±1 land) are off during exploration.
		// Trimming is the OTHER phase's job, and it is the phase that needs the deeper evaluation.
		var roll = rng.Next(10);
		if (exploring)
			roll =
				core is not null
					// Swap or SwapWithinSlot. Rebalance moves ONE copy between roles and Recount moves
					// one copy of one card, so both belong to the other phase.
					? rng.Next(6)
				// Swap (0-4) or Package (7-8), skipping Recount at 5-6 and AdjustLands at 9. Written
				// as an explicit mapping rather than a narrowed range, because narrowing the range
				// silently kept Recount in — the roll table is not ordered by step size.
				: rng.Next(2) == 0 ? rng.Next(5)
				: 7 + rng.Next(2);

		var mutated =
			core is not null
				? roll switch
				{
					< 4 => Swap(
						deck,
						spells,
						values,
						rng,
						curveTarget,
						history,
						features,
						locked,
						contextValue
					),
					< 6 => SwapWithinSlot(deck, core, values, rng, history, features),
					< 8 => Rebalance(deck, core, values, rng, history, features),
					< 9 => Recount(
						deck,
						spells,
						values,
						rng,
						curveTarget,
						history,
						features,
						locked
					),
					_ => AdjustLands(
						deck,
						spells,
						values,
						rng,
						curveTarget,
						history,
						features,
						locked
					),
				}
			: roll < 5
				? Swap(
					deck,
					spells,
					values,
					rng,
					curveTarget,
					history,
					features,
					locked,
					contextValue
				)
			: roll < 7
				? Recount(
					deck,
					spells,
					values,
					rng,
					curveTarget,
					history,
					features,
					locked,
					contextValue
				)
			: roll < 9
				? Package(
					deck,
					spells,
					values,
					rng,
					history,
					rng.Next(3),
					features,
					locked,
					contextValue
				)
			: AdjustLands(
				deck,
				spells,
				values,
				rng,
				curveTarget,
				history,
				features,
				locked,
				contextValue
			);

		if (mutated is null || mutated.Validate() is not null)
			return null;

		// **Backstop, and it should almost never fire.** Protection covers cutting; this covers
		// the paths it cannot reach — `Recount` lowering copies of an unprotected card that was
		// nonetheless carrying a slot's surplus. Cheap, and it makes the invariant true rather
		// than merely likely.
		if (core is not null && !core.Holds(mutated))
			return null;

		// **Enforced as "never move FURTHER out", not as "must be inside".**
		//
		// Seeding only TARGETS a band — the fill is weighted toward `curveTarget` and cheap cards
		// score well on card value regardless, so an Aggro seed measures 1.65 against a band of
		// 2.0-2.7. A membership test would therefore reject every mutant from generation 0 and
		// freeze the slot solid, silently: exactly the failure the old 60% pool quota had, where a
		// deck starting below the line could never propose a legal mutant again.
		//
		// Monotone instead, so it converges INWARD like the pool lock does. A deck inside its band
		// may move anywhere inside it; a deck outside may only move toward it.
		if (profile != DeckProfile.Any)
		{
			var (lo, hi) = BandFor(profile);
			static double Outside(double cost, double lo, double hi) =>
				Math.Max(0, Math.Max(lo - cost, cost - hi));

			if (
				Outside(mutated.AverageCost(costs), lo, hi)
				> Outside(deck.AverageCost(costs), lo, hi)
			)
				return null;
		}

		return Decklist.Difference(deck, mutated) > 0 ? mutated : null;
	}

	/// <summary>
	/// How much a card is worth KEEPING in this deck — card quality, fit with the rest of the list,
	/// what this deck slot has learned about it, and how well its own demands are answered.
	///
	/// Extracted from `PickWeakest` so the slot operators cut by the same rule the general mutator
	/// does. A second copy of this expression would drift, and the two would disagree about which
	/// card is weakest while both looking correct.
	/// </summary>
	private static double CutScore(
		string name,
		Decklist deck,
		ConstructedValues values,
		DeckHistory? history,
		PoolFeatures? features
	) =>
		values.CardDelta(name)
		+ values.DeckFit(name, deck)
		+ (history?.KeepScore(name, deck) ?? 0)
		+ SupportScore(name, deck, features);

	/// <summary>
	/// **Try a different card for the SAME ROLE, at the same count.** "Bogardan Hellkite instead of
	/// Hunted Dragon", not "a dragon instead of a ritual".
	///
	/// The slot is already the right unit for this — `CoreSlot.Cards` is by definition the set of
	/// interchangeable cards that fill one role, so quality exploration is a swap inside that set
	/// and needs no new vocabulary. `Satisfy` fills a slot best-first by card value and one playset
	/// usually covers the floor, so a freshly built deck plays ONE of a role's options; this is what
	/// lets the rest of the set be tried.
	///
	/// Slot composition is deliberately untouched — the count question belongs to
	/// <see cref="Rebalance"/>, and an operator that moved both at once could not have either effect
	/// attributed to it.
	/// </summary>
	private static Decklist? SwapWithinSlot(
		Decklist deck,
		DeckCore core,
		ConstructedValues values,
		Random rng,
		DeckHistory? history,
		PoolFeatures? features
	)
	{
		var usable = core.Slots.Where(s => s.Cards.Count > 1 && s.CountIn(deck) > 0).ToList();
		if (usable.Count == 0)
			return null;

		var slot = usable[rng.Next(usable.Count)];

		var held = slot.Cards.Where(n => deck.CopiesOf(n) > 0).ToList();
		var absent = slot.Cards.Where(n => deck.CopiesOf(n) == 0).ToList();
		if (held.Count == 0 || absent.Count == 0)
			return null;

		// Weakest out, sampled in: cutting the worst is a judgement the value table can make, while
		// choosing the replacement at random is what makes this exploration rather than a second
		// reading of the same ranking.
		var outgoing = held.OrderBy(n => CutScore(n, deck, values, history, features)).First();
		var incoming = absent[rng.Next(absent.Count)];

		var copies = Math.Min(deck.CopiesOf(outgoing), Decklist.MaxCopies);
		return deck.WithCopies(outgoing, 0).WithCopies(incoming, copies);
	}

	/// <summary>
	/// **Move one copy from one role to another** — the "is four Dragons better than six" operator.
	///
	/// Deck size is held constant, so this asks purely about COMPOSITION: more rituals against more
	/// dragons, with everything else equal. Cutting only from a slot above its floor means the
	/// identity cannot be eroded by it — `ProtectedIn` is not even consulted, because the operator
	/// cannot propose an illegal move in the first place.
	///
	/// **`CoreSlot.TargetCopies` is not consulted here, and that is deliberate.** The cap is an
	/// opening position for the FILL; once a deck exists, what it should hold is a question for
	/// measurement, and a cap that also bound mutation would answer it by assumption.
	/// </summary>
	private static Decklist? Rebalance(
		Decklist deck,
		DeckCore core,
		ConstructedValues values,
		Random rng,
		DeckHistory? history,
		PoolFeatures? features
	)
	{
		var donors = core.Slots.Where(s => s.CountIn(deck) > s.MinCopies).ToList();
		var receivers = core
			.Slots.Where(s => s.Cards.Any(n => deck.CopiesOf(n) < Decklist.MaxCopies))
			.ToList();
		if (donors.Count == 0 || receivers.Count == 0)
			return null;

		var from = donors[rng.Next(donors.Count)];
		var to = receivers[rng.Next(receivers.Count)];
		if (ReferenceEquals(from, to))
			return null;

		var outgoing = from
			.Cards.Where(n => deck.CopiesOf(n) > 0)
			.OrderBy(n => CutScore(n, deck, values, history, features))
			.FirstOrDefault();
		if (outgoing is null)
			return null;

		var incoming = to
			.Cards.Where(n => deck.CopiesOf(n) < Decklist.MaxCopies)
			.OrderByDescending(n => values.CardDelta(n))
			.ThenBy(n => n, StringComparer.Ordinal)
			.FirstOrDefault();
		if (incoming is null)
			return null;

		return deck.WithCopies(outgoing, deck.CopiesOf(outgoing) - 1)
			.WithCopies(incoming, deck.CopiesOf(incoming) + 1);
	}

	/// Remove k copies of one card, add k copies of another.
	private static Decklist? Swap(
		Decklist deck,
		IReadOnlyList<Card> spells,
		ConstructedValues values,
		Random rng,
		double curveTarget,
		DeckHistory? history,
		PoolFeatures? features,
		IReadOnlySet<string>? protectedNames = null,
		IReadOnlyDictionary<string, double>? contextValue = null,
		bool exploring = false
	)
	{
		var outgoing = PickWeakest(
			deck,
			values,
			rng,
			history,
			features: features,
			protectedNames: protectedNames
		);
		if (outgoing is null)
			return null;

		// Exploring cuts the card OUTRIGHT rather than shaving copies: the question in this phase is
		// "does this card belong at all", and a deck holding 1 of something answers it with noise.
		var k = exploring
			? deck.CopiesOf(outgoing)
			: Math.Min(deck.CopiesOf(outgoing), 1 + rng.Next(Decklist.MaxCopies));
		var trimmed = deck.WithCopies(outgoing, deck.CopiesOf(outgoing) - k);
		return Fill(
			trimmed,
			spells,
			values,
			rng,
			curveTarget,
			1.0,
			MutateTemperature,
			features,
			contextValue
		);
	}

	/// Shift one card's copy count by 1, compensating with another card.
	private static Decklist? Recount(
		Decklist deck,
		IReadOnlyList<Card> spells,
		ConstructedValues values,
		Random rng,
		double curveTarget,
		DeckHistory? history,
		PoolFeatures? features,
		IReadOnlySet<string>? protectedNames = null,
		IReadOnlyDictionary<string, double>? contextValue = null
	)
	{
		if (deck.DistinctSpells < 2)
			return null;

		var names = deck.Spells.Keys.ToList();
		var target = names[rng.Next(names.Count)];
		var up = rng.Next(2) == 0;

		if (up && deck.CopiesOf(target) >= Decklist.MaxCopies)
			return null;

		var adjusted = deck.WithCopies(target, deck.CopiesOf(target) + (up ? 1 : -1));

		// Adding a copy has to take a slot from somewhere; removing one frees a slot to fill.
		if (!up)
			return Fill(
				adjusted,
				spells,
				values,
				rng,
				curveTarget,
				1.0,
				MutateTemperature,
				features,
				contextValue
			);

		var donor = PickWeakest(
			adjusted,
			values,
			rng,
			history,
			exclude: target,
			features: features,
			protectedNames: protectedNames
		);
		return donor is null ? null : adjusted.WithCopies(donor, adjusted.CopiesOf(donor) - 1);
	}

	/// <summary>
	/// Bring in a card together with its best measured synergy partners, in one move.
	///
	/// **This exists because single-card hill climbing cannot cross a synergy valley.** Atog
	/// alone is a bad card; artifacts without Atog are unremarkable. Every one-card step from a
	/// normal deck toward the Atog combo makes the deck WORSE, so it is rejected, and the combo
	/// is unreachable no matter how good the scoring function is. "Consistent piles of
	/// individually-strong cards" is precisely the set of decks reachable by one-card steps —
	/// which is exactly what the first four runs produced.
	///
	/// So this is a search-operator fix, not a scoring fix. The pair table is used to GENERATE
	/// the proposal rather than to score it, which is a much lighter demand on thin pair data:
	/// a wrong package is simply rejected by the win rate a generation later, whereas a wrong
	/// score silently biases every decision.
	/// </summary>
	/// <param name="partners">
	/// How many synergy partners come in with the anchor. **0 means "just add a good card"** —
	/// the anchor is chosen on card value alone and no pair evidence is consulted. That arm
	/// exists so the mutator is not permanently hunting synergy: pair data is the thinnest part
	/// of the model, and a mutator that only proposes packages can never fill a slot with a
	/// plainly strong card that happens to have no measured partners.
	/// </param>
	private static Decklist? Package(
		Decklist deck,
		IReadOnlyList<Card> spells,
		ConstructedValues values,
		Random rng,
		DeckHistory? history,
		int partners,
		PoolFeatures? features,
		IReadOnlySet<string>? protectedNames = null,
		IReadOnlyDictionary<string, double>? contextValue = null
	)
	{
		var outside = spells.Where(c => deck.CopiesOf(c.Name) == 0).ToList();
		if (outside.Count == 0)
			return null;

		var anchor = outside[
			DraftPickers.SampleSoftmax(
				outside.Select(c => values.CardDelta(c.Name)).ToArray(),
				MutateTemperature,
				rng
			)
		];

		// Partners must be outside the deck too — a "package" whose halves are already present
		// is just an expensive Recount. Ranked on absolute joint performance, so a partner has
		// to actually WIN alongside the anchor rather than merely beat a pessimistic baseline.
		var chosen =
			partners == 0
				? []
				: values
					.TopPartners(anchor.Name, spells, KernelSize)
					.Select(p => p.Name)
					.Where(n => deck.CopiesOf(n) == 0)
					.Take(partners)
					.ToList();

		var incoming = chosen.Prepend(anchor.Name).ToList();
		var copies = incoming.ToDictionary(n => n, _ => 2 + rng.Next(2), StringComparer.Ordinal);
		var needed = copies.Values.Sum();

		// Free the slots first, so the package lands as one atomic change.
		var trimmed = deck;
		var guard = 0;
		while (trimmed.SpellCount > Decklist.DeckSize - trimmed.Lands - needed)
		{
			if (guard++ > Decklist.DeckSize)
				return null;
			var cut = PickWeakest(
				trimmed,
				values,
				rng,
				history,
				features: features,
				protectedNames: protectedNames
			);
			if (cut is null)
				return null;
			trimmed = trimmed.WithCopies(cut, trimmed.CopiesOf(cut) - 1);
		}

		foreach (var (name, count) in copies)
			trimmed = trimmed.WithCopies(name, count);

		// Trimming works one copy at a time and can overshoot, so top back up.
		return trimmed.SpellCount < Decklist.DeckSize - trimmed.Lands
			? Fill(
				trimmed,
				spells,
				values,
				rng,
				trimmed.AverageCost(spells.ToDictionary(c => c.Name, StringComparer.Ordinal)),
				1.0,
				MutateTemperature,
				features,
				contextValue
			)
			: trimmed;
	}

	/// Move the mana base by one, compensating with a spell.
	private static Decklist? AdjustLands(
		Decklist deck,
		IReadOnlyList<Card> spells,
		ConstructedValues values,
		Random rng,
		double curveTarget,
		DeckHistory? history,
		PoolFeatures? features,
		IReadOnlySet<string>? protectedNames = null,
		IReadOnlyDictionary<string, double>? contextValue = null
	)
	{
		var up = rng.Next(2) == 0;
		var lands = deck.Lands + (up ? 1 : -1);
		if (lands < Decklist.MinLands || lands > Decklist.MaxLands)
			return null;

		var adjusted = deck with { Lands = lands };
		if (!up)
			return Fill(
				adjusted,
				spells,
				values,
				rng,
				curveTarget,
				1.0,
				MutateTemperature,
				features,
				contextValue
			);

		var donor = PickWeakest(
			adjusted,
			values,
			rng,
			history,
			features: features,
			protectedNames: protectedNames
		);
		return donor is null ? null : adjusted.WithCopies(donor, adjusted.CopiesOf(donor) - 1);
	}

	/// <summary>
	/// Adds cards until the deck is legal, sampling by value + synergy with what is already
	/// there − distance from the curve target.
	///
	/// Chunks of 2-4 rather than one at a time: constructed decks play multiples, and a fill
	/// that adds 39 singletons produces a pile that draws its own cards a fifth as often.
	/// </summary>
	private static Decklist? Fill(
		Decklist deck,
		IReadOnlyList<Card> spells,
		ConstructedValues values,
		Random rng,
		double curveTarget,
		double synergyWeight,
		double temperature,
		PoolFeatures? features,
		IReadOnlyDictionary<string, double>? contextValue = null,
		bool exploring = false
	)
	{
		var guard = 0;
		while (deck.SpellCount < Decklist.DeckSize - deck.Lands)
		{
			if (guard++ > Decklist.DeckSize * 2)
				return null; // pool too small to fill legally

			var need = Decklist.DeckSize - deck.Lands - deck.SpellCount;

			var candidates = spells.Where(c => deck.CopiesOf(c.Name) < Decklist.MaxCopies).ToList();
			if (candidates.Count == 0)
				return null;

			var scores = new double[candidates.Count];
			for (var i = 0; i < candidates.Count; i++)
			{
				var card = candidates[i];
				scores[i] =
					values.CardDelta(card.Name)
					+ synergyWeight * values.DeckFit(card.Name, deck)
					- CurvePenalty * Math.Abs(card.ManaCost - curveTarget)
					+ ExplorationBonus * values.Unmeasured(card.Name)
					+ SupportScore(card.Name, deck, features)
					// **What this card is worth IN THIS DECK, where `CardDelta` is what it is worth
					// in a random one.** Measured by `OutputProbe`, already scaled into CardDelta's
					// percentage-point units by the caller. Added rather than replacing: the
					// isolation rate is a real signal about a card and this is a real signal about a
					// fit, and the project's rule is that features PROPOSE while win rate judges.
					+ (contextValue?.GetValueOrDefault(card.Name) ?? 0.0);
			}

			var chosen = candidates[DraftPickers.SampleSoftmax(scores, temperature, rng)];
			var room = Decklist.MaxCopies - deck.CopiesOf(chosen.Name);
			var add = Math.Min(
				Math.Min(exploring ? Decklist.MaxCopies : 2 + rng.Next(3), room),
				need
			);
			deck = deck.WithCopies(chosen.Name, deck.CopiesOf(chosen.Name) + add);
		}

		return deck;
	}

	/// <summary>
	/// A card to cut, sampled toward the deck's weakest contributors. Sampled rather than
	/// argmin so repeated mutation of one deck does not propose the identical cut every time.
	/// </summary>
	private static string? PickWeakest(
		Decklist deck,
		ConstructedValues values,
		Random rng,
		DeckHistory? history = null,
		string? exclude = null,
		PoolFeatures? features = null,
		IReadOnlySet<string>? protectedNames = null,
		IReadOnlyDictionary<string, double>? contextValue = null
	)
	{
		// Cards holding a `DeckCore` slot at its floor are not candidates. Filtering here rather
		// than rejecting the finished mutant is what makes proposals legal by construction — see
		// DeckCore.ProtectedIn for why the rejecting version freezes a deck solid.
		var names = deck
			.Spells.Keys.Where(n => !string.Equals(n, exclude, StringComparison.Ordinal))
			.Where(n => protectedNames is null || !protectedNames.Contains(n))
			.ToList();
		if (names.Count == 0)
			return null;

		// **Never cut a card that is measurably carrying this deck.** Softmax alone still cuts
		// a strong card occasionally, and over 100 generations "occasionally" is constantly —
		// which is how Ancestral Recall left decks it was winning in. Restrict the candidates
		// to below-average performers and only fall back to the full list when every card is
		// pulling its weight (in which case the deck has no obvious weak link and any cut is a
		// guess anyway).
		// The filter and the ranking below MUST score identically. Scoring the filter on local
		// history alone made a card's global value unreachable: inside its own deck a card's
		// win rate sits near that deck's own rate, so its local delta is ~0 and — with the
		// imputed pair term — it lands at or above the local mean and is excluded from the cut
		// candidates entirely. Dragonstorm, the WORST card in a 780-card pool at -22.86pp,
		// survived 2 096 games in a deck that way, and Thoughtcast held slots in three decks
		// with no artifacts. Ancestral Recall meanwhile never got in, because the slots were
		// locked by junk that could not be cut.
		double Score(string n) => CutScore(n, deck, values, history, features);

		if (names.Count > 2)
		{
			var scored = names.Select(n => (Name: n, Score: Score(n))).ToList();
			var mean = scored.Average(e => e.Score);
			var below = scored.Where(e => e.Score < mean).Select(e => e.Name).ToList();
			if (below.Count > 0)
				names = below;
		}

		// Negated, so the softmax favours the LOW scorers.
		//
		// `history` is what makes cutting synergy-aware, and it is the per-deck table rather
		// than the global one on purpose: it asks "is this combination pulling its weight in
		// THIS deck", which is dense enough to answer (a 4-of is drawn most games) where the
		// global table is spread over 83 000 pairs at a median of 53 games. A card carrying
		// several winning pairs survives even on an unremarkable solo rate — which is exactly
		// what a synergy piece looks like, and what pure card quality cuts first.
		var scores = names.Select(n => -Score(n)).ToArray();
		return names[DraftPickers.SampleSoftmax(scores, MutateTemperature, rng)];
	}

	/// <summary>
	/// Land count for a curve, jittered.
	///
	/// The mana base is a scalar here — one land in the engine, no colours — but it is NOT an
	/// inert knob. The fixed three-land opening hand does not neutralise it: what decides
	/// whether you keep hitting drops is the density of the REMAINDER of the library, which is
	/// 17-in-53 (32%) at 20 lands and 23-in-53 (43%) at 26. See Draft.DefaultMaxSpells for the
	/// same argument measured in limited.
	/// </summary>
	/// <summary>
	/// Land count for a deck defined by a concept rather than a curve band — the concept's own
	/// average cost, clamped into the target range. Exactly what <see cref="SeedConcept"/> does,
	/// factored out so <see cref="EngineDiscovery"/> cannot drift into a second land rule.
	/// </summary>
	/// Land count for a request that named a curve band instead of a concept.
	internal static int LandsForProfile(DeckProfile profile, Random rng)
	{
		var (lo, hi) = BandFor(profile);
		return LandsForCurve(lo + rng.NextDouble() * (hi - lo), rng);
	}

	internal static int LandsForConcept(IEnumerable<Card> cards, Random rng)
	{
		var list = cards.ToList();
		var curve = list.Count == 0 ? MinCurveTarget : list.Average(c => c.ManaCost);
		return LandsForCurve(Math.Clamp(curve, MinCurveTarget, MaxCurveTarget), rng);
	}

	private static int LandsForCurve(double curveTarget, Random rng)
	{
		var scaled =
			Decklist.MinLands
			+ (curveTarget - MinCurveTarget)
				/ (MaxCurveTarget - MinCurveTarget)
				* (Decklist.MaxLands - Decklist.MinLands);
		return Math.Clamp(
			(int)Math.Round(scaled) + rng.Next(-1, 2),
			Decklist.MinLands,
			Decklist.MaxLands
		);
	}

	/// <summary>
	/// Seeds a whole field, rejecting any deck too similar to one already placed.
	///
	/// The diversity constraint is enforced HERE and again at mutation acceptance, which is
	/// what stops the field converging. It is a hard constraint rather than a fitness penalty
	/// because the requirement is categorical ("no two decks share more than X") and a penalty
	/// would let a strong deck buy its way past it.
	///
	/// The threshold relaxes if a slot cannot be filled, rather than looping forever — on a
	/// small pool, eight genuinely distinct 60-card decks may not exist, and reporting the
	/// achieved diversity is more useful than hanging.
	/// </summary>
	/// <param name="conceptSlots">
	/// How many slots are seeded by <see cref="SeedConcept"/> rather than by anchor-and-kernel.
	/// Each gets a DISTINCT demand — three slots that all discover Goblins are one deck, and the
	/// diversity floor would reject two of them anyway, so the slot would be wasted rather than
	/// exploring. Ignored when <paramref name="features"/> is null.
	/// </param>
	public static IReadOnlyList<Decklist> SeedField(
		int count,
		IReadOnlyList<Card> pool,
		ConstructedValues values,
		Random rng,
		double minDifference,
		bool includeWildcard = true,
		PoolFeatures? features = null,
		int conceptSlots = 0
	)
	{
		var field = new List<Decklist>(count);
		var usedConcepts = new HashSet<int>();

		for (var i = 0; i < count; i++)
		{
			var wildcard = includeWildcard && i == count - 1;

			// Concept slots come first so they get first pick of the pool, before the diversity
			// constraint has been narrowed by anything else. A synergy deck is the hardest shape
			// to fit past that constraint, since its cards are the ones it cannot substitute.
			if (!wildcard && features is not null && i < conceptSlots)
			{
				var built = SeedConceptDistinct(
					pool,
					values,
					features,
					rng,
					field,
					minDifference,
					usedConcepts,
					$"Synergy-{i + 1}"
				);
				if (built is not null)
				{
					field.Add(built);
					continue;
				}
				// No viable concept left — fall through to an ordinary seed rather than a gap.
			}

			// **Every non-concept, non-wildcard slot gets a real profile, cycled.** The label has
			// to mean something: these slots were previously named "Midrange-A" while drawing a
			// curve target uniformly from 2.0-4.5, so a "midrange" deck could come out as an aggro
			// or control curve and the report said otherwise.
			//
			// Cycling rather than a fixed split, because deck count is a parameter — 8 decks with
			// 3 concept slots leaves 4, and hardcoding "2 aggro, 1 midrange, 1 control" breaks the
			// moment either number changes. Cycling also guarantees the bands are spread across
			// the field rather than left to the RNG, which is the point of having them.
			var profile = wildcard
				? DeckProfile.Any
				: (Profiles.Length, i - conceptSlots) switch
				{
					(_, < 0) => DeckProfile.Any,
					var (n, k) => Profiles[k % n],
				};

			var name =
				wildcard ? "Wildcard"
				: profile == DeckProfile.Any ? $"Deck {(char)('A' + i)}"
				: $"{profile}-{(char)('A' + i)}";

			field.Add(
				SeedDistinct(
					name,
					pool,
					values,
					rng,
					field,
					minDifference,
					wildcard,
					features: features,
					profile: profile
				)
			);
		}
		return field;
	}

	/// <summary>
	/// One concept deck on a demand no other slot has taken, distinct from the field.
	///
	/// Unlike <see cref="SeedDistinct"/> this retries across CONCEPTS as well as across samples:
	/// a concept whose cards are already in the field cannot produce a distinct deck no matter
	/// how many times it is resampled.
	/// </summary>
	private static Decklist? SeedConceptDistinct(
		IReadOnlyList<Card> pool,
		ConstructedValues values,
		PoolFeatures features,
		Random rng,
		IReadOnlyList<Decklist> others,
		double minDifference,
		HashSet<int> used,
		string name,
		int attempts = 12
	)
	{
		var present = pool.Where(c => !c.HasSubtype("Land"))
			.Select(c => c.Name)
			.ToHashSet(StringComparer.Ordinal);

		Decklist? best = null;
		var bestGap = -1.0;
		int? bestConcept = null;

		for (var i = 0; i < attempts; i++)
		{
			var concept = PickConcept(features, present, rng, used);
			if (concept is null)
				break; // every viable concept is already taken by another slot

			var candidate = SeedConcept(name, pool, values, features, rng, concept);
			if (candidate is null || candidate.Validate() is not null)
				continue;

			var gap = MinDifference(candidate, others);
			if (gap > bestGap)
				(best, bestGap, bestConcept) = (candidate, gap, concept);
			if (gap >= minDifference)
				break;
		}

		if (bestConcept is not null)
			used.Add(bestConcept.Value);

		return best;
	}

	/// <summary>
	/// One deck that differs from every deck in <paramref name="others"/> by at least
	/// <paramref name="minDifference"/>. Falls back to the most distinct attempt seen.
	/// </summary>
	public static Decklist SeedDistinct(
		string name,
		IReadOnlyList<Card> pool,
		ConstructedValues values,
		Random rng,
		IReadOnlyList<Decklist> others,
		double minDifference,
		bool wildcard = false,
		int attempts = 30,
		PoolFeatures? features = null,
		DeckProfile profile = DeckProfile.Any
	)
	{
		Decklist? best = null;
		var bestGap = -1.0;

		for (var i = 0; i < attempts; i++)
		{
			var candidate = Seed(name, pool, values, rng, wildcard, features, profile);
			if (candidate.Validate() is not null)
				continue;

			var gap = MinDifference(candidate, others);
			if (gap >= minDifference)
				return candidate;
			if (gap > bestGap)
				(best, bestGap) = (candidate, gap);
		}

		return best ?? Seed(name, pool, values, rng, wildcard, features, profile);
	}

	/// Smallest difference between a deck and any of a field. 1.0 against an empty field.
	public static double MinDifference(Decklist deck, IReadOnlyList<Decklist> others) =>
		others.Count == 0
			? 1.0
			: others
				.Where(o => !ReferenceEquals(o, deck))
				.Select(o => Decklist.Difference(deck, o))
				.DefaultIfEmpty(1.0)
				.Min();
}
