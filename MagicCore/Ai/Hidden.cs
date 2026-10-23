using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Sets.Fra;

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
		/// THE DEFAULT (AI-3.9). Every card whose place the searching player doesn't know is shuffled among the unknown
		/// places; the known stay put — a surveil's keep, a mulligan's bottom, a card seen going on top.
		/// </summary>
		ShuffleUnknown,
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
	/// pick must be one the real choice accepts.
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
		sample = sample with { RngSeed = rng.Next(1, int.MaxValue) };
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
			s = s.UpdateObject(card.Id, CardFactory.Create(FraCards.Get(guess), owner));
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
		var all = FraCards
			.AllNames.Except(Limited.Boosters.Guests)
			.Select(n => FraCards.Get(n).Data)
			.ToList();
		var slots = new Dictionary<string, double>
		{
			["common"] = 10,
			["uncommon"] = 3,
			["rare"] = 7 / 8.0,
			["mythic"] = 1 / 8.0,
		};
		var nonland = all.Where(d => !d.TypeLine.Contains("Land")).ToList(); // lands are guessed apart
		var inSet = nonland.GroupBy(d => d.Rarity).ToDictionary(g => g.Key, g => g.Count());
		return
		[
			.. nonland
				.Where(d => FraCards.IsImplemented(d.Name))
				.OrderBy(d => d.Name, StringComparer.Ordinal)
				.Select(d =>
					(
						d.Name,
						(d.Colors ?? []).Select(ManaCost.ColorOf).ToArray(),
						slots.GetValueOrDefault(d.Rarity) / inSet[d.Rarity]
					)
				),
		];
	}

	static GameState ShuffleUnknown(GameState s, int owner, Knowers mine, Random rng)
	{
		var libraryId = s.ZoneId(owner, ZoneType.Library);
		var ids = s.GetChildrenIds(libraryId).ToArray();
		var places = Enumerable
			.Range(0, ids.Length)
			.Where(i => (s.GetCard(ids[i]).KnownTo & mine) == 0)
			.ToArray();
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
