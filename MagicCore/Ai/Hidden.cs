using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;

namespace MagicCore.Ai;

/// <summary>
/// What the search may know of the hidden zones (story AI-3). It decides from ONE sampled position,
/// taken at its root, so its tree and its rollouts see the same cards; the decision is then played in
/// the real game. Who knows each card is the engine's <see cref="Card.KnownTo"/>.
/// </summary>
public static class Hidden
{
	public enum Library
	{
		/// <summary>The libraries as they really are — the AI sees its own future (AI-2's first cut; `+realorder`).</summary>
		RealOrder,

		/// <summary>
		/// The default from AI-3.9 to AI-20.4 (`+shuffle`). Every card whose place the searching player doesn't know is shuffled among the unknown
		/// places; the known stay put — a surveil's keep, a mulligan's bottom, a card seen going on top.
		/// </summary>
		ShuffleUnknown,

		/// <summary>
		/// THE DEFAULT (AI-20.4, Shayne; `+landornot` names it too; Shayne: "what happens if i draw a land vs not"). Nothing is shuffled and no
		/// order is guessed: an unseen card on top of a library is an unknown card (<see cref="UnknownCard"/>), and the
		/// search plays every line's look-ahead BOTH ways - our unseen draws are lands, or they are not - averaged by
		/// the land odds of our own deck list (<see cref="LandOdds"/>). So the same position decides the same way.
		/// </summary>
		LandOrNot,
	}

	/// <summary>
	/// An unseen card as the look-ahead draws it under <see cref="Library.LandOrNot"/>: no type, no cost, never cast
	/// or played - held at the average spell's price (<see cref="HeldValues.Spell"/>). It takes the id of the card it
	/// stands for, as <see cref="GuessHand"/>'s guesses do.
	/// </summary>
	public const string UnknownCard = "Unknown card";

	/// <summary>
	/// How many unseen places on top of a library hold unknown cards; below them the library's real cards stay, so a
	/// search of it still finds them. ponytail: the 6th unseen draw of one look-ahead is a real card, the lowest id
	/// left (five covers two draw steps and three draws by effects); raise it if a draw engine is ever misjudged.
	/// </summary>
	public const int UnknownDraws = 5;

	/// <summary>
	/// The RNG seed every position arranged by <see cref="Unknown"/> carries - never the real game's, so a shuffle
	/// inside the look-ahead doesn't predict the real one, and the same for every position, so nothing in the
	/// look-ahead moves with the game's seed. Anything random (a shuffle) moves it: that is how the search and the
	/// rollout see a library needs arranging again.
	/// </summary>
	public const int Arranged = 20_203;

	public static bool IsUnknown(Card c) => c.Name == UnknownCard;

	// "You can't cast this": a condition that never holds (in your turn and not in your turn).
	static readonly CardDefinition Blank =
		new(
			new CardData(
				UnknownCard,
				"normal",
				null,
				0,
				"",
				null,
				null,
				null,
				null,
				null,
				[],
				null
			),
			[new CastOnlyIf(new Both(new DuringYourTurn(), new Not(new DuringYourTurn())))]
		);

	/// <summary>
	/// The position as <see cref="Library.LandOrNot"/> lets <paramref name="me"/> know it. The opponent's library: its
	/// unseen top cards are unknown cards. Mine, one of two ways - <paramref name="lands"/>: the unseen lands of my
	/// deck on top (each name spread by its share, so the commonest comes first) and every unknown card already in my
	/// hand a land too; or not: unknown cards on top. Places I know (a surveil's keep) stay, as in
	/// <see cref="ShuffleUnknown"/>; a card a pending choice offers is never made unknown.
	/// ponytail: one look-ahead's unseen draws ALL go one way, so two draws are two lands or none - the average
	/// number of lands is right, their spread too wide; and the land is the commonest name, never the colour we
	/// miss. Split further (AI-20's open question on the surveil) if a position shows it.
	/// </summary>
	public static GameState Unknown(GameState s, int me, bool lands)
	{
		s = Arrange(s, s.OpponentOf(me), Knowers.Opponent, lands: false);
		if (lands)
			foreach (
				var (drawn, land) in s.CardsIn(me, ZoneType.Hand)
					.Where(IsUnknown)
					.ToList()
					.Zip(Spread(Unseen(s, me).Where(c => c.Is(CardType.Land))))
			)
				s = s.MoveObject(land.Id, s.ZoneId(me, ZoneType.Hand))
					.MoveObject(drawn.Id, s.ZoneId(me, ZoneType.Library));
		s = Arrange(s, me, Knowers.Owner, lands);
		return s.RngSeed == Arranged ? s : s with { RngSeed = Arranged };
	}

	/// <summary>
	/// The chance an unseen draw of <paramref name="me"/>'s is a land: the lands among the cards of my library whose
	/// place I don't know, over those cards - exact, since I know my deck list (Shayne, AI-20.1) - an unknown card
	/// already drawn in the look-ahead counted back in.
	/// After <paramref name="looks"/> looks at an unseen card on top (the Jace token's "[−1]: Surveil 1", AI-20.9): each
	/// card looked at is binned unless it is the draw we look for - a land (<paramref name="forLand"/>), or not a
	/// land - so the draw misses only if every card looked at and the one under them all miss. Decided before the
	/// look (Shayne, 2026-11-15: "I am looking for a specific card and thats it"): 13 lands in 27 are 48% a draw,
	/// 74% after one look for a land.
	/// These are the odds of the NEXT draw; the search spreads them over a look-ahead's draws (BeamSearchAi.RolloutScore).
	/// ponytail: "a land" is any land, never the colour we miss - split the lands by what they unlock (AI-20.10).
	/// </summary>
	public static double LandOdds(GameState s, int me, int looks = 0, bool forLand = true)
	{
		var unseen = Unseen(s, me).ToList();
		var all = unseen.Count + s.CardsIn(me, ZoneType.Hand).Count(IsUnknown);
		var lands = unseen.Count(c => c.Is(CardType.Land));
		if (all == 0 || looks == 0)
			return all == 0 ? 0 : lands / (double)all;
		var miss = 1.0; // every card looked at, then the draw, is not what we look for
		for (var i = 0; i <= looks; i++)
			miss *= Math.Max(0.0, (forLand ? all - lands : lands) - i) / Math.Max(1, all - i);
		return forLand ? 1 - miss : miss;
	}

	/// <summary>
	/// The unknown cards a look of <paramref name="me"/>'s is showing: a surveil's choice waiting over cards the
	/// look-ahead made up (<see cref="UnknownCard"/>). Nothing can be decided by them - they count as looks
	/// (<see cref="LandOdds"/>). A real card looked at (the real game's surveil; a land where the draws are lands) is none.
	/// </summary>
	public static int Looked(GameState s, int me) =>
		s.GetPendingChoice() is Actions.ChooseFromLibraryTopChoice top && top.PlayerId == me
			? top.Options.Count(o => IsUnknown(s.GetCard(o.Id)))
			: 0;

	/// <summary>The cards of <paramref name="me"/>'s library whose place I don't know.</summary>
	public static IEnumerable<Card> Unseen(GameState s, int me)
	{
		var ids = s.GetChildrenIds(s.ZoneId(me, ZoneType.Library)).ToArray();
		return Places(s, me, Knowers.Owner, ids).Select(i => s.GetCard(ids[i]));
	}

	/// <summary>Lands in the order they are drawn: the j-th of a name's n copies at (j + ½) / n, so 7 Islands and 6 Swamps alternate, an Island first.</summary>
	static List<Card> Spread(IEnumerable<Card> lands) =>
		[
			.. lands
				.GroupBy(c => c.Name)
				.SelectMany(g =>
				{
					var n = g.Count();
					return g.OrderBy(c => c.Id).Select((c, j) => (Card: c, At: (j + 0.5) / n));
				})
				.OrderBy(x => x.At)
				.ThenBy(x => x.Card.Name, StringComparer.Ordinal)
				.Select(x => x.Card),
		];

	static GameState Arrange(GameState s, int owner, Knowers mine, bool lands)
	{
		var libraryId = s.ZoneId(owner, ZoneType.Library);
		var ids = s.GetChildrenIds(libraryId).ToArray();
		var places = Places(s, owner, mine, ids);
		if (places.Length == 0)
			return s;
		// a choice of objects only: a mode's or a number's option ids are no card ids
		var offered =
			s.GetPendingChoice() is { } choice && MagicRules.OptionsAreObjects(choice)
				? choice.Options.Select(o => o.Id).ToHashSet()
				: [];
		var unseen = places.Select(i => s.GetCard(ids[i])).ToList();
		var spread = Spread(unseen.Where(c => c.Is(CardType.Land)));
		// by id, never by where they really are: the unknown cards first, a card a choice offers last
		var rest = unseen
			.Where(c => !c.Is(CardType.Land))
			.OrderBy(c =>
				IsUnknown(c) ? 0
				: offered.Contains(c.Id) ? 2
				: 1
			)
			.ThenBy(c => c.Id)
			.ToList();
		List<Card> order = lands ? [.. spread, .. rest] : [.. rest, .. spread];
		foreach (
			var c in order
				.Take(UnknownDraws)
				.Where(c => !c.Is(CardType.Land) && !IsUnknown(c) && !offered.Contains(c.Id))
		)
			s = s.UpdateObject(c.Id, CardFactory.Create(Blank, owner));
		var arranged = (int[])ids.Clone();
		for (var k = 0; k < places.Length; k++)
			arranged[places[k]] = order[k].Id;
		if (arranged.AsSpan().SequenceEqual(ids))
			return s;
		// Out and back in, as ShuffleUnknown does: MoveObject appends, so this is the new order.
		foreach (var id in arranged)
			s = s.MoveObject(id, 0);
		foreach (var id in arranged)
			s = s.MoveObject(id, libraryId);
		return s;
	}

	/// <summary>The places of a library <paramref name="mine"/> doesn't know (Zefram Cochrane: "You may look at the top card of your library any time.").</summary>
	static int[] Places(GameState s, int owner, Knowers mine, int[] ids)
	{
		var topKnown = (mine & Knowers.Owner) != 0 && s.TopIsKnownToOwner(owner);
		return
		[
			.. Enumerable
				.Range(0, ids.Length)
				// an unknown card looked at (a surveil's keep) is still unknown: its place stays one to arrange, or the
				// lands half of the look-ahead would draw it as a card that is never a land
				.Where(i =>
					((s.GetCard(ids[i]).KnownTo & mine) == 0 || IsUnknown(s.GetCard(ids[i])))
					&& !(i == 0 && topKnown)
				),
		];
	}

	public enum Hand
	{
		/// <summary>The opponent's hand as it really is — a casting opponent casts what it really holds (AI-3.1).</summary>
		Real,

		/// <summary>
		/// The opponent's hand cards the searching player hasn't seen are drawn again from those and the
		/// cards of its library whose place is unknown; the seen ones stay. A card drawn from the library
		/// swaps places with the hand card it replaces, so the rest of the library keeps its order and the
		/// hand is measured apart from the library's mode (story AI-3.4, Shayne).
		/// </summary>
		ShuffleUnseen,

		/// <summary>
		/// The opponent's hand cards the searching player hasn't seen are GUESSED — never its real cards
		/// (honest even when its deck is unknown, as in limited): new cards of the colours it has shown,
		/// weighted as a booster weights rarity; a land with the chance of the lands left in a 40-card,
		/// 17-land deck, a basic of a shown colour. Its library is left real (story AI-3.5, Shayne).
		/// </summary>
		GuessedFromColors,
	}

	/// <summary>
	/// The position the search decides from. Under ShuffleUnknown both libraries are reshuffled where
	/// <paramref name="me"/> can't know them, and under a <see cref="Hand"/> mode the opponent's unseen
	/// hand is drawn again or guessed (before the libraries) — from a seed of the state's, never from its
	/// RNG, which the real game draws from next — and the sample gets a fresh RNG seed, so a shuffle
	/// inside a rollout doesn't predict the real game's (so either mode also hides that foresight). The real position when a pending choice would offer different
	/// ids in the sample (a search lists the first copy of each name in library order): the search's
	/// pick must be one the real choice accepts. Under <see cref="Library.LandOrNot"/> nothing is sampled: the
	/// libraries are arranged (<see cref="Unknown"/>), the same for every seed.
	/// </summary>
	public static GameState Sample(GameState s, int me, Library mode, Hand hand = Hand.Real)
	{
		if (mode == Library.RealOrder && hand == Hand.Real)
			return s;
		// ponytail: seeded by the game's seed, which only moves when the game uses randomness — so a
		// turn's decisions mostly share a seed, but the sample still changes as the library does (a
		// draw), and an unseeded game (seed 0) samples afresh at every decision. Seed by turn and
		// library size if plans ever need one library for the whole turn.
		var rng = s.RngSeed == 0 ? new Random() : new Random(unchecked(s.RngSeed * 31 + me));
		var sample = s;
		if (hand == Hand.ShuffleUnseen)
			sample = ShuffleUnseenHand(sample, s.OpponentOf(me), rng);
		if (hand == Hand.GuessedFromColors)
			sample = GuessHand(sample, s.OpponentOf(me), rng);
		if (mode == Library.ShuffleUnknown)
			foreach (var owner in new[] { s.Player1Id(), s.Player2Id() })
				sample = ShuffleUnknown(
					sample,
					owner,
					owner == me ? Knowers.Owner : Knowers.Opponent,
					rng
				);
		sample =
			mode == Library.LandOrNot
				? Unknown(sample, me, lands: false)
				: sample with
				{
					RngSeed = rng.Next(1, int.MaxValue),
				};
		return
			s.GetPendingChoice() is { } real
			&& !real
				.Options.Select(o => o.Id)
				.SequenceEqual(sample.GetPendingChoice()!.Options.Select(o => o.Id))
			? s
			: sample;
	}

	/// <summary>The opponent's unseen hand drawn again from its unseen cards — <see cref="Hand.ShuffleUnseen"/>.</summary>
	static GameState ShuffleUnseenHand(GameState s, int owner, Random rng)
	{
		var (handId, libraryId) = (
			s.ZoneId(owner, ZoneType.Hand),
			s.ZoneId(owner, ZoneType.Library)
		);
		var (hand, library) = (
			s.GetChildrenIds(handId).ToArray(),
			s.GetChildrenIds(libraryId).ToArray()
		);
		// Unseen by me, its opponent: in the hand, a card I haven't seen; in the library, a place I don't know.
		bool Unseen(int id) => (s.GetCard(id).KnownTo & Knowers.Opponent) == 0;
		var handSlots = Enumerable.Range(0, hand.Length).Where(i => Unseen(hand[i])).ToArray();
		var librarySlots = Enumerable
			.Range(0, library.Length)
			.Where(i => Unseen(library[i]))
			.ToArray();
		int[] pool = [.. handSlots.Select(i => hand[i]), .. librarySlots.Select(i => library[i])];
		rng.Shuffle(pool);
		var drawn = pool.Take(handSlots.Length).ToHashSet();
		var leaving = handSlots.Where(i => !drawn.Contains(hand[i])).ToArray();
		var arriving = librarySlots.Where(i => drawn.Contains(library[i])).ToArray();
		if (leaving.Length == 0)
			return s;
		rng.Shuffle(leaving); // which arriving card's place each leaving card takes — not by hand order
		for (var k = 0; k < leaving.Length; k++)
			(hand[leaving[k]], library[arriving[k]]) = (library[arriving[k]], hand[leaving[k]]);
		// Out and back in, as ShuffleUnknown does: MoveObject appends, so this is the new order of both.
		foreach (var id in hand.Concat(library))
			s = s.MoveObject(id, 0);
		foreach (var id in hand)
			s = s.MoveObject(id, handId);
		foreach (var id in library)
			s = s.MoveObject(id, libraryId);
		return s;
	}

	/// <summary>The opponent's unseen hand guessed from the colours it has shown — <see cref="Hand.GuessedFromColors"/>.</summary>
	/// ponytail: the deck is taken to be 40 cards, 17 of them lands, and its cards FRA's — read the deck
	/// size from the game and the pool from the game's set if another format or set ever plays.
	static GameState GuessHand(GameState s, int owner, Random rng)
	{
		bool Seen(Card c) => (c.KnownTo & Knowers.Opponent) != 0;
		IEnumerable<Card> Its(IEnumerable<Card> cards) => cards.Where(c => c.OwnerId == owner);
		// What it has shown me: its cards in public zones (by owner — a card it took is still mine), and
		// the cards of its hand and library I know; not tokens or copies, which are not in its deck.
		List<Card> shown =
		[
			.. Its(s.CardsIn(s.Player1Id(), ZoneType.Battlefield))
				.Concat(Its(s.CardsIn(s.Player2Id(), ZoneType.Battlefield)))
				.Concat(Its(s.GetChildren(s.GetWellKnownId(MagicKeys.Stack)).OfType<Card>()))
				.Concat(s.CardsIn(owner, ZoneType.Graveyard))
				.Concat(s.CardsIn(owner, ZoneType.Exile))
				.Concat(s.CardsIn(owner, ZoneType.Hand).Where(Seen))
				.Concat(s.CardsIn(owner, ZoneType.Library).Where(Seen))
				.Where(c => !c.IsToken && !c.IsCopy),
		];
		var colors = shown
			.SelectMany(c => c.Is(CardType.Land) ? ManaAbilities.ColorsOf(s, c) : c.Colors)
			.Where(c => c != ManaColor.Colorless)
			.ToHashSet();
		if (colors.Count == 0)
			colors = [.. ManaAbilities.AnyColor.Where(c => c != ManaColor.Colorless)]; // nothing shown yet
		var spells = Spells.Where(x => x.Colors.All(colors.Contains)).ToArray();
		// Lands from what's left of the deck.
		var landsLeft = Math.Max(0, 17 - shown.Count(c => c.Is(CardType.Land)));
		var landChance = Math.Min(1.0, landsLeft / (double)Math.Max(1, 40 - shown.Count));
		var basics = colors.Order().Select(c => Basics[c]).ToArray();
		// Each guess takes the id and place of the card it replaces: new ids would move NextId, so every
		// object a searched line creates would have another id than in the real game and a committed
		// plan that names one could never be found again.
		foreach (var card in s.CardsIn(owner, ZoneType.Hand).Where(c => !Seen(c)).ToList())
		{
			var guess =
				rng.NextDouble() < landChance || spells.Length == 0
					? basics[rng.Next(basics.Length)]
					: Weighted(spells, rng);
			s = s.UpdateObject(card.Id, CardFactory.Create(CardRegistry.Get(guess), owner));
		}
		return s;
	}

	static string Weighted((string Name, ManaColor[] Colors, double Weight)[] spells, Random rng)
	{
		var pick = rng.NextDouble() * spells.Sum(x => x.Weight);
		foreach (var (name, _, weight) in spells)
			if ((pick -= weight) < 0)
				return name;
		return spells[^1].Name;
	}

	static readonly Dictionary<ManaColor, string> Basics =
		new()
		{
			[ManaColor.White] = "Plains",
			[ManaColor.Blue] = "Island",
			[ManaColor.Black] = "Swamp",
			[ManaColor.Red] = "Mountain",
			[ManaColor.Green] = "Forest",
		};

	/// <summary>
	/// Every implemented FRA nonland card, weighted as a booster deals it: 10 commons, 3 uncommons and
	/// a rare or (one time in eight) a mythic, each slot shared among that rarity's nonland cards.
	/// Not the Special Guests (MC-3): one pack in 55 holds one — too rare to guess at. ponytail: the old draft-booster slots; LIM-1.9 weights by FRA.boosters.json's sheets.
	/// </summary>
	static readonly (string Name, ManaColor[] Colors, double Weight)[] Spells = BoosterWeighted();

	static (string, ManaColor[], double)[] BoosterWeighted()
	{
		// ponytail: FRA's printings, whatever the game's set — guess by the set being played once a
		// game knows it (MC-5.4, TRK sealed and draft).
		var guests = Limited.Boosters.Guests("FRA").ToHashSet();
		var all = CardRegistry.Printings("FRA").Where(p => !guests.Contains(p.Card.Name)).ToList();
		var slots = new Dictionary<string, double>
		{
			["common"] = 10,
			["uncommon"] = 3,
			["rare"] = 7 / 8.0,
			["mythic"] = 1 / 8.0,
		};
		var nonland = all.Where(p => !p.Card.TypeLine.Contains("Land")).ToList(); // lands are guessed apart
		var inSet = nonland.GroupBy(d => d.Rarity).ToDictionary(g => g.Key, g => g.Count());
		return
		[
			.. nonland
				.Where(d => CardRegistry.IsImplemented(d.Card.Name))
				.OrderBy(d => d.Card.Name, StringComparer.Ordinal)
				.Select(d =>
					(
						d.Card.Name,
						(d.Card.Colors ?? []).Select(ManaCost.ColorOf).ToArray(),
						slots.GetValueOrDefault(d.Rarity) / inSet[d.Rarity]
					)
				),
		];
	}

	static GameState ShuffleUnknown(GameState s, int owner, Knowers mine, Random rng)
	{
		var libraryId = s.ZoneId(owner, ZoneType.Library);
		var ids = s.GetChildrenIds(libraryId).ToArray();
		var places = Places(s, owner, mine, ids);
		var unknown = places.Select(i => ids[i]).ToArray();
		rng.Shuffle(unknown);
		for (var k = 0; k < places.Length; k++)
			ids[places[k]] = unknown[k];
		// Out and back in, as MagicState.Shuffle does: MoveObject appends, so this is the new order.
		foreach (var id in ids)
			s = s.MoveObject(id, 0);
		foreach (var id in ids)
			s = s.MoveObject(id, libraryId);
		return s;
	}
}
