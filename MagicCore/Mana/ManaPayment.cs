using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using Tappable = (
	int Id,
	bool IsCreature,
	bool SameColor,
	System.Collections.Immutable.ImmutableArray<System.Collections.Immutable.ImmutableArray<MagicCore.ManaColor>> Units,
	bool Sacrificed
);

namespace MagicCore;

/// <summary>
/// Arena-style auto-payment: finds which floating mana and which untapped lands pay a cost.
/// The only place a cost is solved — casting validates and pays through the same call.
///
/// Backtracking, so it finds a payment whenever one exists (hybrid and {2/W} make greedy wrong).
/// Identical sources (two floating G, two Forests) are tried once per step, which keeps the
/// search tiny however many basics are out.
///
/// It solves UNITS of mana, not permanents: tapping a permanent makes its whole yield
/// (ManaAbilities.YieldOf) — Loot, the Nexus makes several of one colour, a Mountain under Molten
/// Tide an extra {R}. A permanent is tapped once, and what the payment doesn't use floats.
///
/// ponytail: spends floating mana first, then lands with the fewest colours (colourless counts as none). Arena's tapper also
/// keeps colours the rest of your hand needs. That trigger has fired — the FRA decks play duals
/// since batch 7a, so a {1}{W} can still tap the Island a {U}{U} in hand needed. Next to build.
/// </summary>
public static class ManaPayment
{
	/// <summary>
	/// One unit of mana: from a permanent tapped for it, or floating (LandId 0). Floats: made by a
	/// tapped permanent but not spent — it goes to the pool.
	/// </summary>
	/// <param name="Sacrificed">Its source is sacrificed as it makes the mana (Treasure) — decided by
	/// the search, so a Treasure creature tapped for convoke is only tapped.</param>
	/// <param name="Restriction">Floating restricted mana spent (Chandra's {U}) — Pay takes that one.</param>
	public record Payment(
		int LandId,
		ManaColor Color,
		bool Floats = false,
		bool Sacrificed = false,
		ManaRestriction Restriction = default
	);

	/// <summary>
	/// A permanent (or one floating mana) and what tapping it makes. Kind: what makes two sources the
	/// same choice — two Forests, two Mountains under Molten Tide — so the search tries one of them.
	/// </summary>
	record Source(
		int LandId,
		bool IsCreature,
		bool SameColor,
		ImmutableArray<Unit> Units,
		bool Sacrificed = false,
		ManaRestriction Restriction = default
	)
	{
		public string Kind { get; } =
			$"{LandId == 0}|{IsCreature}|{SameColor}|{Sacrificed}|{(Restriction == default ? "" : Restriction.ToString())}|{string.Join(";", Units.Select(u => string.Join(",", u.Colors)))}";
	}

	/// <summary>One mana a source makes, and the colours it may be.</summary>
	record Unit(int Source, ImmutableArray<ManaColor> Colors);

	/// <summary>Allowed colours for one mana; TwoBridColor set means "this, or two generic instead".</summary>
	record Need(ImmutableArray<ManaColor> Allowed, ManaColor? TwoBridColor = null)
	{
		/// <summary>Starfleet Academy's own {5}: never paid by the mana it makes.</summary>
		public int NotFrom { get; init; }
	}

	static readonly ImmutableArray<ManaColor> Any = [.. Enum.GetValues<ManaColor>()];

	/// <summary>
	/// Sunburst (Engineered Explosives): the payment that spends the most different colours — the player
	/// picks X, the payment does the rest (Shayne, 2026-11-05). k of the generic as k different coloured
	/// pips, the largest k that pays; else Find's own. Never by sacrificing a Treasure or using Starfleet
	/// Academy that Find's own wouldn't: a colour isn't worth a resource the player didn't choose to spend.
	/// Converge (Prismatic Ending, {X}{W}): the k colours are ones the cost's own pips don't already spend, so the
	/// first k-subset that pays adds k colours to theirs. ponytail: a hybrid pip's colour isn't known until paid.
	/// </summary>
	static IReadOnlyList<Payment>? FindMostColors(
		GameState s,
		int playerId,
		ManaCost cost,
		int x,
		int excludeId = 0,
		Card? forSpell = null,
		IReadOnlyCollection<int>? alsoExclude = null,
		Card? forAbilityOf = null
	)
	{
		var plain = Find(s, playerId, cost, x, excludeId, forSpell, alsoExclude, forAbilityOf);
		if (plain is null)
			return null;
		var generic = cost.WithGeneric(x)
			.Symbols.Where(m => m.Kind == SymbolKind.Generic)
			.Sum(m => m.Amount);
		ManaColor[] colors =
		[
			.. new[]
			{
				ManaColor.White,
				ManaColor.Blue,
				ManaColor.Black,
				ManaColor.Red,
				ManaColor.Green,
			}.Where(c => !cost.Symbols.Any(m => m.Kind == SymbolKind.Colored && m.Color == c)),
		];
		for (var k = Math.Min(generic, colors.Length); k > 0; k--)
			foreach (var set in Subsets(colors, k))
				if (
					Find(
						s,
						playerId,
						cost.WithGeneric(x - k)
							.Plus(
								new ManaCost(
									[.. set.Select(c => new ManaSymbol(SymbolKind.Colored, c))]
								)
							),
						0,
						excludeId,
						forSpell,
						alsoExclude,
						forAbilityOf
					)
						is { } found
					&& found.Count(p => p.Sacrificed) <= plain.Count(p => p.Sacrificed)
					&& found.Count(p => p.Floats) <= plain.Count(p => p.Floats)
					&& FromHand(s, playerId, found) <= FromHand(s, playerId, plain) // nor a Spirit Guide
				)
					return found;
		return plain;
	}

	static int FromHand(GameState s, int playerId, IEnumerable<Payment> payment) =>
		payment.Count(p => p.LandId != 0 && s.IsIn(p.LandId, playerId, ZoneType.Hand));

	/// <summary>Sunburst's count: the different colours a payment spent — not {C}, not mana that only floats.</summary>
	public static int ColorsSpent(IEnumerable<Payment> payment) =>
		payment
			.Where(p => !p.Floats && p.Color != ManaColor.Colorless)
			.Select(p => p.Color)
			.Distinct()
			.Count();

	static IEnumerable<ManaColor[]> Subsets(ManaColor[] colors, int k) =>
		k == 0
			?
			[
				[],
			]
		: colors.Length < k ? []
		: Subsets(colors[1..], k - 1)
			.Select(rest => (ManaColor[])[colors[0], .. rest])
			.Concat(Subsets(colors[1..], k));

	/// <summary>
	/// Convoke (CR 702.51) and improvise (CR 702.126): whether this spell's creatures and its artifacts may tap to help
	/// pay — read once per payment, then asked of each permanent by <see cref="Helps"/>.
	/// </summary>
	public static (bool Creatures, bool Artifacts) Helpers(GameState s, Card? spell) =>
		spell is null ? default : (s.Has(spell, "Convoke"), s.Has(spell, "Improvise"));

	/// <summary>
	/// What tapping this permanent pays toward a spell its <see cref="Helpers"/> let it help, or null: a creature, for
	/// convoke, {1} or one mana of its colour (Colorless is how it pays generic only); an artifact, for improvise
	/// (Kappa Cannoneer: "Each artifact you tap after you're done activating mana abilities pays for {1}"), {1} only.
	/// Not a {T} cost, so summoning sickness doesn't matter.
	/// </summary>
	public static ImmutableArray<ManaColor>? Helps(
		(bool Creatures, bool Artifacts) helpers,
		Card permanent
	)
	{
		if (permanent.IsTapped)
			return null;
		ImmutableArray<ManaColor>? pays = null;
		if (helpers.Creatures && permanent.Is(CardType.Creature))
			pays = [.. permanent.Colors, ManaColor.Colorless];
		if (helpers.Artifacts && permanent.Is(CardType.Artifact))
			pays ??= [ManaColor.Colorless]; // a convoking creature already pays {1}
		return pays;
	}

	/// <param name="alsoExclude">Permanents the rest of the cost uses — sacrificed, tapped.</param>
	/// <param name="forSpell">The spell being cast (in its zone, before it moves) — restricted mana asks what it is, and
	/// its convoke or improvise lets permanents help (Helps); null for an ability.</param>
	/// <param name="forAbilityOf">The source of the activated ability being paid for — Isolinear Processor's mana asks.</param>
	public static IReadOnlyList<Payment>? Find(
		GameState s,
		int playerId,
		ManaCost cost,
		int x,
		int excludeId = 0,
		Card? forSpell = null,
		IReadOnlyCollection<int>? alsoExclude = null,
		Card? forAbilityOf = null,
		bool mostColors = false
	)
	{
		if (mostColors)
			return FindMostColors(
				s,
				playerId,
				cost,
				x,
				excludeId,
				forSpell,
				alsoExclude,
				forAbilityOf
			);
		var player = s.GetPlayer(playerId);
		var helpers = Helpers(s, forSpell);
		var tappable = new List<Tappable>();
		// Sen Triplets: floating mana and convoke only; Clarion Conqueror: no artifact's or creature's mana
		var locks = Turns.Combat.Locks(s, playerId);
		// Urza: "Tap an untapped artifact you control: Add {U}" — what tapping each one for him pays (a lock on him stops it)
		var tappingAnother = ManaAbilities.TappingAnother(s, playerId, (forSpell, forAbilityOf));
		// his ability doesn't tap him, so another cost tapping or sacrificing him leaves it (CR 601.2g: activated first)
		tappingAnother.RemoveAll(r => Turns.Combat.Stopped(locks, s, r.Holder, manaAbility: true));
		foreach (var c in s.CardsIn(playerId, ZoneType.Battlefield))
		{
			// Its own {T} cost, or a permanent another cost sacrifices or taps: not mana for this too.
			if (c.Id == excludeId || alsoExclude?.Contains(c.Id) == true)
				continue;
			var yield =
				!Turns.Combat.Stopped(locks, s, c, manaAbility: true) && ManaAbilities.CanTap(s, c)
					? ManaAbilities.YieldOf(s, c, (forSpell, forAbilityOf))
					: null;
			if (yield is { Units.IsEmpty: true })
				yield = null; // its mana can't pay (YieldOf left out what can't) — but it may still convoke
			// Convoke and improvise (Helps): an untapped creature or artifact you control may pay {1} (and a
			// creature one mana of its colour). A one-mana creature may pay either way.
			var convokes = Helps(helpers, c);
			if (yield is { Sacrificed: true } && convokes is not null)
				yield = null; // ponytail: convoking only taps it, so a Sculpture Treasure convokes
			// rather than being sacrificed for a colour; offer both if that ever matters
			// Urza's artifacts join them: tapped for another's cost, each pays that ability's colour — after the line
			// above, so a Lotus or a Treasure keeps its own sacrifice (ponytail: and is sacrificed for his {U} too, DesignNotes).
			if (ManaAbilities.TappedFor(s, c, tappingAnother) is { } forAnother)
				convokes = [.. (convokes ?? []).Union(forAnother)];
			if (yield is not null && convokes is { } own && yield.Units.Length == 1)
				tappable.Add(
					(
						c.Id,
						true,
						false,
						[
							[.. yield.Units[0].Union(own)],
						],
						yield.Sacrificed
					)
				);
			else if (yield is not null)
				tappable.Add(
					(c.Id, c.Is(CardType.Creature), yield.SameColor, yield.Units, yield.Sacrificed)
				);
			else if (convokes is { } only)
				tappable.Add((c.Id, true, false, [only], false));
		}
		return Solve(s, player, tappable, Needs(cost, x), forSpell, forAbilityOf)
			?? (
				Turns.Combat.Stopped(locks, s, null, manaAbility: true)
					? null
					: FromHand(s, player, cost, x, tappable, forSpell, forAbilityOf, alsoExclude)
			)
			?? WithCostlyMana(
				s,
				player,
				tappable,
				cost,
				x,
				excludeId,
				alsoExclude,
				forSpell,
				forAbilityOf,
				locks
			);
	}

	/// <summary>
	/// Elvish Spirit Guide: "Exile this card from your hand: Add {G}." — tried only when the battlefield can't pay: the
	/// hand's mana joins the sources after every permanent (a card spent is dearer than a tap). Pay exiles it.
	/// ponytail: not with Starfleet Academy's costly mana (WithCostlyMana) — no deck mixes them yet.
	/// </summary>
	static IReadOnlyList<Payment>? FromHand(
		GameState s,
		MagicPlayer player,
		ManaCost cost,
		int x,
		List<Tappable> tappable,
		Card? forSpell,
		Card? forAbilityOf,
		IReadOnlyCollection<int>? alsoExclude
	)
	{
		List<Tappable> hand =
		[
			.. ManaAbilities
				.FromHand(s, player.Id, forSpell, forAbilityOf)
				.Where(h => alsoExclude?.Contains(h.Card.Id) != true) // Force of Will's exiled card
				.Select(h =>
					(
						h.Card.Id,
						false,
						h.Ability.AnyOneColor,
						(ImmutableArray<ImmutableArray<ManaColor>>)
							[
								.. Enumerable.Repeat<ImmutableArray<ManaColor>>(
									h.Ability.AnyOneColor
										? ManaAbilities.AnyColor
										: [h.Ability.Color],
									Math.Max(0, h.Ability.Amount.Value(s, Conditions.Of(h.Card)))
								),
							],
						false
					)
				),
		];
		return hand.Count == 0
			? null
			: Solve(s, player, tappable, Needs(cost, x), forSpell, forAbilityOf, last: hand);
	}

	/// <summary>
	/// Starfleet Academy: "{5}, {T}: Choose a color. Add an amount of mana of that color equal to …" —
	/// a mana ability that costs mana, tried only when nothing else pays: its N units of one colour
	/// join the sources in place of its plain {C}, and its own cost is owed too, never paid by the mana
	/// it makes. What it makes beyond the cost floats, as any tapped source's does.
	/// ponytail: its cost may be paid by mana restricted to the spell being cast (Chandra's {U}), or by a
	/// creature convoking that spell — check the restriction per need if a deck ever mixes them.
	/// </summary>
	static IReadOnlyList<Payment>? WithCostlyMana(
		GameState s,
		MagicPlayer player,
		List<Tappable> tappable,
		ManaCost cost,
		int x,
		int excludeId,
		IReadOnlyCollection<int>? alsoExclude,
		Card? forSpell,
		Card? forAbilityOf,
		IReadOnlyList<(CantCastOrActivate Rule, Card Holder)> locks
	)
	{
		foreach (var c in s.CardsIn(player.Id, ZoneType.Battlefield))
		{
			if (
				c.Id == excludeId
				|| alsoExclude?.Contains(c.Id) == true
				|| !ManaAbilities.CanPayTapCost(s, c)
				|| Turns.Combat.Stopped(locks, s, c, manaAbility: true) // Sen Triplets, Clarion Conqueror
			)
				continue;
			foreach (
				var costly in ManaAbilities
					.AbilitiesOf(s, c)
					.Where(m => m.Cost is not null && !m.ChoosesCost)
			)
			{
				var n = costly.Amount.Value(s, Conditions.Of(c));
				if (n <= 0)
					continue;
				var plan = Solve(
					s,
					player,
					[
						.. tappable.Where(t => t.Id != c.Id),
						(
							c.Id,
							c.Is(CardType.Creature),
							true,
							[
								.. Enumerable.Repeat<ImmutableArray<ManaColor>>(
									costly.AnyOneColor ? ManaAbilities.AnyColor : [costly.Color],
									n
								),
							],
							costly.SacrificesItself // Chromatic Star: "{1}, {T}, Sacrifice this artifact" — Pay sacrifices it
						),
					],
					// its own cost first: the most constrained (never its own mana)
					[
						.. Needs(costly.Cost!, 0).Select(need => need with { NotFrom = c.Id }),
						.. Needs(cost, x),
					],
					forSpell,
					forAbilityOf
				);
				if (plan is not null)
					return plan;
			}
		}
		return null;
	}

	/// <summary>Most constrained first; generic last so it takes whatever colours are left.</summary>
	static ImmutableList<Need> Needs(ManaCost cost, int x) =>
		cost
			.Symbols.SelectMany(sym =>
				sym.Kind switch
				{
					SymbolKind.Colored => [new Need([sym.Color])],
					SymbolKind.Hybrid => [new Need([sym.Color, sym.Color2])],
					SymbolKind.TwoBrid => [new Need([sym.Color], sym.Color)],
					SymbolKind.Generic => Enumerable.Repeat(new Need(Any), sym.Amount),
					// ponytail: only an activation chooses life or mana (Drivnod); a spell with {B/P} waits for its card.
					SymbolKind.Phyrexian => throw new InvalidOperationException(
						"A Phyrexian symbol reached the solver: pay it with ManaCost.PayingLifeFor first"
					),
					_ => Enumerable.Repeat(new Need(Any), x),
				}
			)
			.OrderBy(n => n.Allowed.Length + (n.TwoBridColor is null ? 0 : 1))
			.ToImmutableList();

	static IReadOnlyList<Payment>? Solve(
		GameState s,
		MagicPlayer player,
		List<Tappable> tappable,
		ImmutableList<Need> needs,
		Card? forSpell,
		Card? forAbilityOf,
		List<Tappable>? last = null
	)
	{
		var sources = new List<Source>();
		// Restricted floating mana first: it can pay for less, so spend it where it can.
		foreach (
			var r in player.ManaPool.Restricted.Where(r =>
				ManaAbilities.Allows(r.Restriction, s, forSpell, forAbilityOf)
			)
		)
			sources.Add(
				new(0, false, false, [new(sources.Count, [r.Color])], Restriction: r.Restriction)
			);
		foreach (var color in Any)
			for (var i = 0; i < player.ManaPool[color]; i++)
				sources.Add(new(0, false, false, [new(sources.Count, [color])]));
		// Lands before creatures (a creature tapped for mana can no longer attack or block), and
		// a Treasure last of all: spending it spends the permanent.
		foreach (
			var (id, isCreature, sameColor, units, sacrificed) in tappable
				.OrderBy(p => p.Sacrificed)
				.ThenBy(p => p.IsCreature)
				// {C} is no colour: a colourless land goes before a basic, whatever their order on the table
				.ThenBy(p => p.Units.Sum(u => u.Count(c => c != ManaColor.Colorless)))
				.Concat(last ?? []) // the hand's (Spirit Guides): after everything
		)
			sources.Add(
				new(
					id,
					isCreature,
					sameColor,
					[.. units.Select(u => new Unit(sources.Count, u))],
					sacrificed
				)
			);
		var all = sources.SelectMany(src => src.Units).ToList();

		var search = new Search(sources, all);
		if (!search.Solve(needs))
			return null;
		// What a tapped permanent made and the payment didn't use floats (it empties with the step).
		var plan = search.Plan.ToList();
		for (var u = 0; u < all.Count; u++)
		{
			var src = sources[all[u].Source];
			if (!search.Used[u] && src.LandId != 0 && search.Tapped(all[u].Source))
				plan.Add(
					new Payment(
						src.LandId,
						// a unit of one colour floats as that colour, whatever the source's chosen one (ExtraMana)
						all[u].Colors.Length == 1
							? all[u].Colors[0]
							: search.Locked[all[u].Source] ?? all[u].Colors[0],
						Floats: true
					)
				);
		}
		return plan;
	}

	sealed class Search(List<Source> sources, List<Unit> units)
	{
		public readonly bool[] Used = new bool[units.Count];
		public readonly ManaColor?[] Locked = new ManaColor?[sources.Count];
		public readonly List<Payment> Plan = [];
		readonly int[] spent = new int[sources.Count];
		int unused = units.Count;

		public bool Tapped(int source) => spent[source] > 0;

		public bool Solve(ImmutableList<Need> needs)
		{
			if (needs.IsEmpty)
				return true;
			// Fewer units left than mana still owed: fail now. Without this, an unaffordable X
			// backtracked through every order of land kinds for its generic — 5^X paths — and
			// LegalActions asks for every X up to the land count (20 lands hung a test). Checked
			// here, not once up front, because {2/W} grows the need list mid-search.
			if (needs.Count > unused)
				return false;
			var need = needs[0];
			var rest = needs.RemoveAt(0);
			var tried = new HashSet<(string, int, ManaColor?, string, ManaColor)>();

			// A permanent already tapped first: its other mana is free, a new one costs a tap.
			foreach (var alreadyTapped in new[] { true, false })
				for (var i = 0; i < units.Count; i++)
				{
					var source = units[i].Source;
					if (Used[i] || Tapped(source) != alreadyTapped)
						continue;
					var src = sources[source];
					if (need.NotFrom != 0 && src.LandId == need.NotFrom)
						continue;
					// "one colour chosen" binds the units that have a choice: Badgermole Cub's additional {G} beside
					// Birds of Paradise's mana is {G} whatever the Birds made
					var chooses = units[i].Colors.Length > 1;
					var colors =
						chooses && Locked[source] is { } locked ? [locked] : units[i].Colors;
					foreach (var color in colors.Where(need.Allowed.Contains))
					{
						// The same kind of source in the same state is the same choice.
						var key = (
							src.Kind,
							spent[source],
							Locked[source],
							string.Join(",", units[i].Colors),
							color
						);
						if (!tried.Add(key))
							continue;
						var lockedBefore = Locked[source];
						if (src.SameColor && chooses)
							Locked[source] = color;
						Used[i] = true;
						unused--;
						spent[source]++;
						Plan.Add(
							new Payment(
								src.LandId,
								color,
								Sacrificed: src.Sacrificed,
								Restriction: src.Restriction
							)
						);
						if (Solve(rest))
							return true;
						Used[i] = false;
						unused++;
						spent[source]--;
						Locked[source] = lockedBefore;
						Plan.RemoveAt(Plan.Count - 1);
					}
				}

			// {2/W} that could not be paid with W: pay two generic instead.
			return need.TwoBridColor is not null
				&& Solve(rest.AddRange([new Need(Any), new Need(Any)]));
		}
	}

	/// <summary>
	/// Spends a payment. A permanent's mana is produced and spent in the same breath, so tapping it
	/// is the cost; floating mana leaves the pool; what a tapped permanent made beyond the cost floats.
	/// </summary>
	public static GameState Pay(GameState s, int playerId, IEnumerable<Payment> payments)
	{
		var sacrificed = new HashSet<int>();
		var exiled = new HashSet<int>();
		foreach (var p in payments)
		{
			if (p.Sacrificed)
				sacrificed.Add(p.LandId); // Treasure: "{T}, Sacrifice this"
			// A Spirit Guide: exiled from the hand (its ExileSelfCost) once, never tapped; what it made beyond floats.
			if (exiled.Contains(p.LandId))
			{
				if (p.Floats)
					s = s.WithPlayer(
						s.GetPlayer(playerId) with
						{
							ManaPool = s.GetPlayer(playerId).ManaPool.Add(p.Color, 1),
						}
					);
				continue;
			}
			if (p.LandId != 0 && s.IsIn(p.LandId, playerId, ZoneType.Hand))
			{
				exiled.Add(p.LandId);
				// Kai Winn: activating it is activating an ability (CR 605), as a land's or creature's
				s = Turns.Triggers.Raise(
					s,
					new Turns.AbilityActivatedEvent(playerId, s.GetCard(p.LandId))
				);
				s = ManaAbilities.PayAsUsed(
					s,
					playerId,
					s.GetCard(p.LandId),
					p.Color,
					ZoneType.Hand
				);
				continue;
			}
			var firstTap = p.LandId != 0 && !s.GetCard(p.LandId).IsTapped;
			if (firstTap)
				s = RaiseActivated(s, playerId, p.LandId);
			if (p.LandId != 0)
				s = s.Tap(p.LandId);
			if (firstTap) // Talismans' damage, Mana Confluence's life: once, for the colour it made
				s = ManaAbilities.PayAsUsed(s, playerId, s.GetCard(p.LandId), p.Color);
			if (p.Restriction != ManaRestriction.None)
			{
				var pool = s.GetPlayer(playerId).ManaPool;
				var spent = pool.Restricted.First(r =>
					r.Color == p.Color && r.Restriction == p.Restriction
				);
				s = s.WithPlayer(
					s.GetPlayer(playerId) with
					{
						ManaPool = pool with { Restricted = pool.Restricted.Remove(spent) },
					}
				);
			}
			else if (p.LandId == 0 || p.Floats)
			{
				var player = s.GetPlayer(playerId);
				s = s.WithPlayer(
					player with
					{
						ManaPool = player.ManaPool.Add(p.Color, p.Floats ? 1 : -1),
					}
				);
			}
		}
		return sacrificed.Aggregate(s, (st, id) => st.Sacrifice(id));
	}

	/// <summary>
	/// Delighted Halfling: "Spend this mana only to cast a legendary spell, and that spell can't be countered." — did this
	/// payment spend such mana (floating, or made by the ability that paid)? Asked before Pay, while the sources are there.
	/// </summary>
	public static bool MakesUncounterable(
		GameState s,
		int playerId,
		IEnumerable<Payment> payment
	) =>
		payment.Any(p =>
			!p.Floats
			&& (
				p.LandId == 0 ? p.Restriction
				: s.IsIn(p.LandId, playerId, ZoneType.Hand)
					? ManaAbilities
						.Used(s, s.GetCard(p.LandId), p.Color, ZoneType.Hand)
						?.Restriction
				: ManaAbilities.Used(s, s.GetCard(p.LandId), p.Color)?.Restriction
			)
				is { CantBeCountered: true }
		);

	/// <summary>
	/// Kai Winn: a mana ability is an activated ability (CR 605) — "activates an ability of a source with the
	/// chosen name" sees one tapped for mana. ponytail: a nonland source only, and a creature tapped for
	/// convoke reads as one too (Payment doesn't say which) — no card asks about lands or convoke yet.
	/// </summary>
	public static GameState RaiseActivated(GameState s, int playerId, int sourceId) =>
		s.GetCard(sourceId) is var source
		&& !source.Is(CardType.Land)
		&& (
			ManaAbilities.CanTap(s, source)
			// Chromatic Star has no plain tap: its one mana ability costs {1} — cashed in, it was activated
			|| ManaAbilities.AbilitiesOf(s, source).Any(m => m.CashedIn)
		)
			? Turns.Triggers.Raise(s, new Turns.AbilityActivatedEvent(playerId, source))
			: s;
}
