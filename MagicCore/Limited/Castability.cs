using System.Text.RegularExpressions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets.Fra;

namespace MagicCore.Limited;

/// <summary>
/// How often a sealed deck's spells are castable ON TIME, by simulated draws (story LIM-3.3): a card
/// of the main colours on curve (its mana value), a splash card by turn 6 (Shayne, 2026-10-25). Shown
/// beside the two-colour build's, never used to reject a build. A lightweight simulation, not the
/// engine: shuffle; London mulligan (keep 2–5 lands, as GreedyAi); on the play or the draw, half each;
/// a land a turn, the one that adds the colour least covered; lands that enter tapped (a dual "unless
/// two or more other lands" untapped from the third); a land that chooses its colour picks the one
/// least covered; a landcycler or land-searcher fetches the basic least covered once it can pay for
/// it; mana creatures, rocks and Heartwood makers cast once payable, a source from the next turn; a
/// surveil spell, cast once payable, bins what isn't needed (lands once six are in play and hand).
/// ponytail: no spell but those is ever cast, so mana is never spent on the curve itself — this
/// measures having the mana, as Karsten's tables do, not using it.
/// </summary>
public static class Castability
{
	public const int SplashTurn = 6;

	/// <summary>
	/// The share of a deck's spells castable on time, over <paramref name="games"/> simulated games —
	/// of <paramref name="only"/> those spells, if given. ~0.1 s at the default on one core.
	/// </summary>
	public static double OnTime(
		SealedDeck deck,
		int games = 1000,
		int seed = 1,
		IReadOnlyCollection<string>? only = null
	)
	{
		var cards = deck.Main.Select(Card.Of).ToArray();
		var spells = cards.Where(c => !c.Land && (only is null || only.Contains(c.Name))).ToArray();
		if (spells.Length == 0)
			return 1;
		var due = spells
			.Select(c => c.Needs(deck) ? Math.Max(c.Value, SplashTurn) : Math.Max(1, c.Value))
			.ToArray();
		var last = due.Max();
		var rng = new Random(seed);
		long onTime = 0;
		for (var g = 0; g < games; g++)
		{
			var mana = Play(cards, deck.Colors, last, onThePlay: g % 2 == 0, rng);
			for (var i = 0; i < spells.Length; i++)
				if (Pays(mana[due[i]], spells[i].Cost))
					onTime++;
		}
		return (double)onTime / (games * spells.Length);
	}

	/// <summary>One card as the simulation reads it — from its script and, for when a land enters untapped, its printed text.</summary>
	sealed record Card(
		string Name,
		bool Land,
		ManaCost Cost,
		int Value,
		IReadOnlyList<ManaColor> Makes,
		bool ChoosesColor,
		bool Fetches,
		ManaCost? CycleCost,
		bool EntersTapped,
		int UntappedWithLands,
		int Surveil
	)
	{
		static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Card> known =
			new();

		public static Card Of(string name) =>
			known.GetOrAdd(
				name,
				n =>
				{
					var def = FraCards.Get(n);
					var land = def.Data.TypeLine.Contains("Land");
					var tapped = def.Components.OfType<EntersTappedComponent>().FirstOrDefault();
					var text = def.Data.OracleText ?? "";
					var others = Regex.Match(
						text,
						@"unless you control (two|three) or more other lands"
					);
					var cost = land ? ManaCost.None : SealedBuilder.FrontCost(def.Data);
					return new Card(
						n,
						land,
						cost,
						cost.ManaValue,
						// a Treasure is spent once: not a source turn after turn (ponytail: not counted at all)
						SealedBuilder.ManaIsSpentOnce(n)
							? []
							: SealedBuilder.Fixes(n),
						land && def.Components.OfType<ChoosesColorAsItEnters>().Any(),
						SealedBuilder.FetchesABasic(n),
						// landcycling: an ability from the hand (null: the fetch comes with casting it)
						def.Components.OfType<ActivatedAbilityComponent>()
							.FirstOrDefault(a =>
								a.From == ZoneType.Hand && a.Effects.OfType<SearchLibrary>().Any()
							)
							?.Cost,
						tapped is not null,
						tapped?.Unless is null ? int.MaxValue
							: others.Success ? (others.Groups[1].Value == "two" ? 2 : 3)
							: int.MaxValue, // "unless you control a planeswalker": tapped, early on
						SealedBuilder.Surveils(n)
					);
				}
			);

		/// <summary>Can't be cast without a splash colour — judged by turn 6, not on curve. As the builder reads it.</summary>
		public bool Needs(SealedDeck deck) =>
			deck.Splashes.Any(x =>
				!SealedBuilder.Castable(Cost, deck.Colors.Where(c => c != x).ToArray())
			);
	}

	/// <summary>One game: the mana sources usable on each turn 0..<paramref name="last"/>, each a set of colours.</summary>
	static List<ManaColor[]>[] Play(
		Card[] deck,
		IReadOnlyList<ManaColor> colors,
		int last,
		bool onThePlay,
		Random rng
	)
	{
		var library = deck.ToList();
		Shuffle(library, rng);
		var hand = Mulligan(library, rng);
		var inPlay = new List<ManaColor[]>(); // sources, usable from the turn after they arrive
		var lands = 0;
		var usable = new List<ManaColor[]>[last + 1];
		usable[0] = [];
		for (var turn = 1; turn <= last; turn++)
		{
			if (turn > 1 || !onThePlay)
				Draw(library, hand);
			// the land a turn: the one that adds a colour least covered; untapped first. A land that
			// enters tapped is a source from the next turn, as is everything cast below
			var land = hand.Where(c => c.Land)
				.OrderByDescending(c => Gain(c, inPlay, colors))
				.ThenBy(c => Tapped(c, lands))
				.FirstOrDefault();
			ManaColor[]? tappedLand = null;
			if (land is not null)
			{
				hand.Remove(land);
				var source = Source(land, inPlay, colors);
				if (Tapped(land, lands))
					tappedLand = source;
				else
					inPlay.Add(source);
				lands++;
			}
			usable[turn] = [.. inPlay];
			if (tappedLand is not null)
				inPlay.Add(tappedLand);

			// after the measure: what the deck casts for its mana — a fetcher when it has no land to
			// play next, a mana source, a surveil spell — each once it can pay
			var mana = usable[turn].ToList();
			foreach (var card in hand.Where(c => !c.Land).ToList())
			{
				var fetch = card.Fetches && !hand.Any(c => c.Land);
				if (!fetch && card.Makes.Count == 0 && card.Surveil == 0)
					continue;
				var cycles = fetch && card.CycleCost is not null; // discarded, not cast
				var cost = cycles ? card.CycleCost! : card.Cost;
				if (!Pays(mana, cost))
					continue;
				Spend(mana, cost);
				hand.Remove(card);
				if (fetch && Fetch(library, inPlay, colors) is { } basic)
					hand.Add(basic);
				if (card.Makes.Count > 0 && !cycles)
					inPlay.Add([.. card.Makes]);
				// surveil N: of the top N, what isn't wanted goes to the graveyard — lands until six
				// are in play and hand, spells after
				var top = library.Take(card.Surveil).ToList();
				var wantLands = lands + hand.Count(c => c.Land) < 6;
				library.RemoveRange(0, top.Count);
				library.InsertRange(0, top.Where(c => c.Land == wantLands));
			}
		}
		return usable;
	}

	static void Shuffle(List<Card> cards, Random rng)
	{
		for (var i = cards.Count - 1; i > 0; i--)
		{
			var j = rng.Next(i + 1);
			(cards[i], cards[j]) = (cards[j], cards[i]);
		}
	}

	static void Draw(List<Card> library, List<Card> hand)
	{
		if (library.Count == 0)
			return;
		hand.Add(library[0]);
		library.RemoveAt(0);
	}

	/// <summary>London: seven each time, kept with 2–5 lands (anything after two), one to the bottom per mulligan.</summary>
	static List<Card> Mulligan(List<Card> library, Random rng)
	{
		for (var taken = 0; ; taken++)
		{
			var hand = library.Take(7).ToList();
			var lands = hand.Count(c => c.Land);
			if (taken >= 2 || lands is >= 2 and <= 5)
			{
				library.RemoveRange(0, 7);
				// to the bottom: a land if more than four, else the most expensive spell
				for (var i = 0; i < taken; i++)
				{
					var bottom =
						hand.Count(c => c.Land) > 4
							? hand.First(c => c.Land)
							: hand.Where(c => !c.Land)
								.OrderByDescending(c => c.Value)
								.FirstOrDefault() ?? hand[0];
					hand.Remove(bottom);
					library.Add(bottom);
				}
				return hand;
			}
			Shuffle(library, rng);
		}
	}

	static bool Tapped(Card land, int landsInPlay) =>
		land.EntersTapped && landsInPlay < land.UntappedWithLands;

	/// <summary>How many of the deck's colours a land adds that the sources in play make fewest of.</summary>
	static int Gain(Card land, List<ManaColor[]> inPlay, IReadOnlyList<ManaColor> colors) =>
		land.ChoosesColor
			? 1
			: land.Makes.Count(c => colors.Contains(c) && !inPlay.Any(s => s.Contains(c)));

	static ManaColor LeastCovered(List<ManaColor[]> inPlay, IReadOnlyList<ManaColor> colors) =>
		colors.OrderBy(c => inPlay.Count(s => s.Contains(c))).First();

	static ManaColor[] Source(
		Card land,
		List<ManaColor[]> inPlay,
		IReadOnlyList<ManaColor> colors
	) => land.ChoosesColor ? [LeastCovered(inPlay, colors)] : [.. land.Makes];

	/// <summary>A basic of the colour least covered, from the library — none if the deck has none of it left.</summary>
	static Card? Fetch(
		List<Card> library,
		List<ManaColor[]> inPlay,
		IReadOnlyList<ManaColor> colors
	)
	{
		var basic = colors
			.OrderBy(c => inPlay.Count(s => s.Contains(c)))
			.Select(c => library.FirstOrDefault(x => x.Name == SealedBuilder.Basics[(int)c]))
			.FirstOrDefault(x => x is not null);
		if (basic is not null)
			library.Remove(basic);
		return basic;
	}

	/// <summary>Can these sources pay the cost: each coloured pip a different source of its colour, the rest generic. X is 0.</summary>
	public static bool Pays(IReadOnlyList<ManaColor[]> sources, ManaCost cost) =>
		Assign(sources, cost) is not null;

	static void Spend(List<ManaColor[]> sources, ManaCost cost)
	{
		var used = Assign(sources, cost)!;
		foreach (var i in used.OrderDescending())
			sources.RemoveAt(i);
	}

	/// <summary>The sources a payment uses (indices), or null — a matching of pips to sources, then generic from the rest.</summary>
	static List<int>? Assign(IReadOnlyList<ManaColor[]> sources, ManaCost cost)
	{
		var pips = new List<Func<ManaColor[], bool>>();
		var generic = 0;
		var twoBrids = new List<ManaColor>();
		foreach (var s in cost.Symbols)
			switch (s.Kind)
			{
				case SymbolKind.Generic:
					generic += s.Amount;
					break;
				case SymbolKind.Colored:
					var c = s.Color;
					pips.Add(src => src.Contains(c));
					break;
				case SymbolKind.TwoBrid:
					twoBrids.Add(s.Color);
					break;
				case SymbolKind.Hybrid:
					var (a, b) = (s.Color, s.Color2);
					pips.Add(src => src.Contains(a) || src.Contains(b));
					break;
			}
		// {2/W}: its colour, or two generic — each way tried, the most paid by colour first
		foreach (
			var byColor in Enumerable
				.Range(0, 1 << twoBrids.Count)
				.OrderByDescending(m => System.Numerics.BitOperations.PopCount((uint)m))
		)
		{
			var all = pips.ToList();
			var extra = 0;
			for (var t = 0; t < twoBrids.Count; t++)
				if ((byColor & 1 << t) != 0)
				{
					var c = twoBrids[t];
					all.Add(src => src.Contains(c));
				}
				else
					extra += 2;
			if (Match(sources, all, generic + extra) is { } used)
				return used;
		}
		return null;
	}

	static List<int>? Match(
		IReadOnlyList<ManaColor[]> sources,
		List<Func<ManaColor[], bool>> pips,
		int generic
	)
	{
		var owner = new int[sources.Count];
		Array.Fill(owner, -1);
		bool Augment(int pip, bool[] seen)
		{
			for (var i = 0; i < sources.Count; i++)
				if (!seen[i] && pips[pip](sources[i]))
				{
					seen[i] = true;
					if (owner[i] < 0 || Augment(owner[i], seen))
					{
						owner[i] = pip;
						return true;
					}
				}
			return false;
		}
		for (var p = 0; p < pips.Count; p++)
			if (!Augment(p, new bool[sources.Count]))
				return null;
		var used = Enumerable.Range(0, sources.Count).Where(i => owner[i] >= 0).ToList();
		var free = Enumerable
			.Range(0, sources.Count)
			.Where(i => owner[i] < 0)
			.Take(generic)
			.ToList();
		return free.Count < generic ? null : [.. used, .. free];
	}
}
