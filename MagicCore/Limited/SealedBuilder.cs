using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;

namespace MagicCore.Limited;

/// <summary>A built sealed deck: its colours, the 40, and the rest of the pool (the sideboard — "outside the game").</summary>
/// <param name="Colors">Its pair, then a splash colour if it has one.</param>
/// <param name="Kind">How greedy it is: <see cref="SealedBuilder.TwoColours"/>, <see cref="SealedBuilder.Splashing"/> or <see cref="SealedBuilder.Greedy"/> (LIM-3).</param>
/// <param name="Detail">What it splashes, for a reader ("Splash Black: Lich's Relic"); null for two colours.</param>
/// <param name="Splash">The colours it only splashes (not its mains or a real extra colour).</param>
public sealed record SealedDeck(
	IReadOnlyList<ManaColor> Colors,
	IReadOnlyList<string> Main,
	IReadOnlyList<string> Sideboard,
	string Kind = SealedBuilder.TwoColours,
	string? Detail = null,
	IReadOnlyList<ManaColor>? Splash = null
)
{
	public string Label => Detail ?? Kind;

	/// <summary>Its button: its pair, and how far past it — "Blue/Black", "Blue/Black + splash", "White/Black greedy" (Shayne, LIM-3.6).</summary>
	public string Button =>
		$"{Colors[0]}/{Colors[1]}"
		+ Kind switch
		{
			SealedBuilder.Splashing => " + splash",
			SealedBuilder.Greedy => " greedy",
			_ => "",
		};

	/// <summary>The colours it only splashes — their cards are on time by turn 6 (Castability).</summary>
	public IReadOnlyList<ManaColor> Splashes => Splash ?? [];
}

/// <summary>
/// The AI's sealed builds (stories LIM-1, LIM-3) — the suggested decks for a human, and the AI
/// opponent's own from its own pool. <see cref="Build"/> is the streamlined two colours: the pair whose
/// best <see cref="Spells"/> castable cards rate highest, by any <see cref="CardRatings"/> table; every
/// dual in the pool that makes exactly the pair's colours, then basics split by the spells' pips.
/// <see cref="Suggestions"/> adds a splash and a greedy splash when the mana allows; an opponent plays
/// one of them at random. Like MtgSimulator's draft builder, the manabase serves the cards the deck
/// plays, and nothing else.
/// </summary>
public static class SealedBuilder
{
	public const int DeckSize = 40;

	/// <summary>23 spells and 17 lands — the limited standard.</summary>
	public const int Spells = 23;

	static readonly ManaColor[] Wubrg =
	[
		ManaColor.White,
		ManaColor.Blue,
		ManaColor.Black,
		ManaColor.Red,
		ManaColor.Green,
	];

	/// <summary>The basic lands, in WUBRG order — free in limited, never from the pool.</summary>
	public static readonly IReadOnlyList<string> Basics =
	[
		"Plains",
		"Island",
		"Swamp",
		"Mountain",
		"Forest",
	];

	public const string TwoColours = "Two colours",
		Splashing = "Splash",
		Greedy = "Greedy";

	/// <summary>
	/// An AI sealed opponent: a pool opened from <paramref name="seed"/>, and one of its
	/// <see cref="Suggestions"/> at random — streamlined or greedy, as a person might (Shayne, LIM-3).
	/// </summary>
	public static SealedDeck Opponent(string set, int seed)
	{
		var builds = Suggestions(Boosters.SealedPool(set, seed), CardRatings.Of(set));
		return builds[new Random(seed).Next(builds.Count)];
	}

	/// <summary>
	/// The builds worth suggesting from a pool (story LIM-3) — never "the optimal deck": the
	/// streamlined two-colour <see cref="Build"/>; a SPLASH of its pair's single best off-colour card;
	/// and a GREEDY build — any two main colours and one or two more, each a real colour or a splash —
	/// when it rates higher than the splash. Shayne's rules (2026-10-24): every colour meets its
	/// sources — <see cref="MainSources"/> for the mains, <see cref="RealSources"/> for a real third or
	/// fourth colour (any card), <see cref="SplashSources"/> for a splash (one more per extra card;
	/// only the set's top 10% by rating, one pip) — counting every fixer as 1 (<see cref="Fixes"/>).
	/// ponytail: the mana is the rule of thumb — LIM-3.3's measured bar (simulated draws, surveil
	/// included) replaces it.
	/// </summary>
	public static IReadOnlyList<SealedDeck> Suggestions(
		IReadOnlyList<string> pool,
		IReadOnlyDictionary<string, double> ratings
	) => Builds(pool, ratings, Build(pool, ratings), Pairs);

	/// <summary>
	/// A close pair's own builds (LIM-3.6, Shayne): its two colours, and its best splash or greedy
	/// build on that pair when the mana allows.
	/// </summary>
	public static IReadOnlyList<SealedDeck> ForPair(
		IReadOnlyList<string> pool,
		IReadOnlyDictionary<string, double> ratings,
		IReadOnlyList<ManaColor> pair
	)
	{
		var builds = Builds(
			pool,
			ratings,
			Build(pool, ratings, pair),
			[
				[.. pair],
			]
		);
		// Builds adds the greedy only when it beats the splash: the last past two colours is the best
		return [builds[0], .. builds.Skip(1).TakeLast(1)];
	}

	/// <summary>Two builds of the same 40, whatever order their cards are listed in.</summary>
	public static bool SameDeck(SealedDeck a, SealedDeck b) =>
		a.Main.Order(StringComparer.Ordinal).SequenceEqual(b.Main.Order(StringComparer.Ordinal));

	/// <summary>
	/// Everything the Sealed screen offers for a pool (LIM-3.4, Shayne): the <see cref="Suggestions"/>,
	/// then every close pair's builds (<see cref="ForPair"/>) — each 40 once.
	/// </summary>
	public static IReadOnlyList<SealedDeck> AllBuilds(
		IReadOnlyList<string> pool,
		IReadOnlyDictionary<string, double> ratings
	)
	{
		var all = Suggestions(pool, ratings).ToList();
		foreach (var close in ClosePairs(pool, ratings))
			all.AddRange(
				ForPair(pool, ratings, close.Pair).Where(d => !all.Any(x => SameDeck(x, d)))
			);
		return all;
	}

	/// <summary>A pair as the builder rates it: its best <see cref="Spells"/> castable spells' mean rating, and how many of them cost 1–3.</summary>
	public sealed record PairRating(IReadOnlyList<ManaColor> Pair, double Rating, int Cheap);

	/// <summary>Within this much of the best pair's rating a pair is offered too — 1.5 points (Shayne, LIM-3.6).</summary>
	public const double ClosePairPoints = 0.015;

	/// <summary>At most this many close pairs, the closest — within 1.5 points most pools have 7–9 of the 9 (Shayne, LIM-3.4).</summary>
	public const int MaxClosePairs = 4;

	/// <summary>Every pair, best first — the first is the one <see cref="Build"/> picks.</summary>
	public static IReadOnlyList<PairRating> RatePairs(
		IReadOnlyList<string> pool,
		IReadOnlyDictionary<string, double> ratings
	)
	{
		var data = pool.Distinct().ToDictionary(n => n, n => CardRegistry.Get(n).Data);
		return
		[
			.. Pairs
				.Select(pair => (pair, Cards: BestSpells(pool, data, ratings, pair)))
				.Select(x => new PairRating(
					x.pair,
					x.Cards.Sum(n => ratings[n]) / Spells,
					CheapSpells(x.Cards)
				))
				.OrderByDescending(p => p.Rating), // stable: the first pair wins a tie, as in Build
		];
	}

	/// <summary>The pairs besides the best within <see cref="ClosePairPoints"/> of it, the <see cref="MaxClosePairs"/> closest — Shayne picks among them.</summary>
	public static IReadOnlyList<PairRating> ClosePairs(
		IReadOnlyList<string> pool,
		IReadOnlyDictionary<string, double> ratings
	)
	{
		var rated = RatePairs(pool, ratings);
		return
		[
			.. rated
				.Skip(1)
				.Where(p => p.Rating >= rated[0].Rating - ClosePairPoints - 1e-9)
				.Take(MaxClosePairs),
		];
	}

	/// <summary>Its spells that cost 1–3 — the early curve (Shayne's qualifier: 16 against 13).</summary>
	public static int CheapSpells(IEnumerable<string> cards) =>
		cards.Count(n =>
			CardRegistry.Get(n).Data is var d
			&& !IsLand(d)
			&& FrontCost(d).ManaValue is >= 1 and <= 3
		);

	static IReadOnlyList<SealedDeck> Builds(
		IReadOnlyList<string> pool,
		IReadOnlyDictionary<string, double> ratings,
		SealedDeck streamlined,
		IEnumerable<ManaColor[]> mainsFrom
	)
	{
		var bar = TopDecile(ratings);
		var builds = new List<SealedDeck> { streamlined };
		var data = pool.Distinct().ToDictionary(n => n, n => CardRegistry.Get(n).Data);

		var splash = Wubrg
			.Except(streamlined.Colors)
			.Select(c =>
				Multicolour(
					pool,
					data,
					ratings,
					[.. streamlined.Colors],
					[(c, Splash: true)],
					bar,
					1
				)
			)
			.OfType<Built>()
			.MaxBy(b => b.Score);
		if (splash is not null)
			builds.Add(splash.Deck);

		var greedy = (
			from mains in mainsFrom
			from extras in Extras(mains)
			select Multicolour(pool, data, ratings, mains, extras, bar, int.MaxValue)
		)
			.OfType<Built>()
			.ToList();
		var streamlinedScore = streamlined
			.Main.Where(n => !IsLand(CardRegistry.Get(n).Data))
			.Sum(n => ratings[n]); // its spells, as every build scores
		var three = greedy.Where(b => b.Deck.Colors.Count == 3).MaxBy(b => b.Score);
		var safer = three ?? splash ?? new Built(streamlined, streamlinedScore);
		// three colours preferred; four only for a top-10% card the safer build can't play (Shayne)
		var four = greedy
			.Where(b =>
				b.Deck.Colors.Count == 4
				&& b.Deck.Main.Any(n =>
					!IsLand(CardRegistry.Get(n).Data)
					&& ratings.GetValueOrDefault(n) >= bar
					&& !safer.Deck.Main.Contains(n)
				)
			)
			.MaxBy(b => b.Score);
		var pick =
			four is not null && four.Score > (three?.Score ?? double.MinValue) ? four : three;
		if (pick is not null && pick.Score > (splash?.Score ?? streamlinedScore))
			builds.Add(pick.Deck);
		return builds;
	}

	/// <summary>A rating in the table's top tenth — what a splash is for.</summary>
	public static double TopDecile(IReadOnlyDictionary<string, double> ratings)
	{
		var sorted = ratings.Values.OrderByDescending(r => r).ToList();
		return sorted[(int)(0.1 * (sorted.Count - 1))];
	}

	/// <summary>Sources a colour needs: a main colour, a real third or fourth, a splash for one card (one more per extra card).</summary>
	public const int MainSources = 7,
		RealSources = 6,
		SplashSources = 3;

	sealed record Built(SealedDeck Deck, double Score);

	static readonly ManaColor[][] Pairs =
	[
		.. from i in Enumerable.Range(0, 5)
		from j in Enumerable.Range(i + 1, 4 - i)
		select new[] { Wubrg[i], Wubrg[j] },
	];

	/// <summary>One or two more colours beside <paramref name="mains"/>, each real or a splash.</summary>
	static IEnumerable<(ManaColor Color, bool Splash)[]> Extras(ManaColor[] mains)
	{
		var others = Wubrg.Except(mains).ToArray();
		foreach (var a in others)
		foreach (var splashA in new[] { true, false })
		{
			yield return [(a, splashA)];
			foreach (var b in others.Where(b => b > a))
			foreach (var splashB in new[] { true, false })
				yield return [(a, splashA), (b, splashB)];
		}
	}

	/// <summary>
	/// The best 23 the colours allow — a splash colour's cards only top-decile and one pip, at most
	/// <paramref name="perSplash"/> of them — then lands that bring every colour to its sources:
	/// fixing lands while they cover a colour still short, then basics. Null when the mana can't, or an
	/// extra colour plays nothing.
	/// </summary>
	static Built? Multicolour(
		IReadOnlyList<string> pool,
		IReadOnlyDictionary<string, CardData> data,
		IReadOnlyDictionary<string, double> ratings,
		ManaColor[] mains,
		(ManaColor Color, bool Splash)[] extras,
		double bar,
		int perSplash
	)
	{
		var colors = mains.Concat(extras.Select(e => e.Color)).ToArray();
		var splashes = extras.Where(e => e.Splash).Select(e => e.Color).ToArray();
		var needs = new Dictionary<string, ManaColor[]>();
		// the extra colours a card can't be cast without — a hybrid of two extras needs both
		ManaColor[] Needs(string n)
		{
			if (needs.TryGetValue(n, out var known))
				return known;
			var cost = FrontCost(data[n]);
			var each = extras.Select(e => e.Color).ToArray();
			var hard = each.Where(x => !Castable(cost, colors.Where(c => c != x).ToArray()))
				.ToArray();
			return needs[n] =
				Castable(cost, mains) ? []
				: hard.Length > 0 ? hard
				: [.. each.Where(x => ColorsOf(cost).Contains(x))];
		}
		bool Eligible(string n) =>
			Castable(FrontCost(data[n]), colors)
			&& Needs(n)
				.Where(splashes.Contains)
				.All(x =>
					ratings[n] >= bar
					&& Pips(FrontCost(data[n]), x, colors.Where(c => c != x).ToArray()) <= 1
				);

		var chosen = new List<string>();
		var perColor = splashes.ToDictionary(c => c, _ => 0);
		foreach (
			var n in pool.Where(n =>
					!IsLand(data[n]) && CardRegistry.IsImplemented(n) && Eligible(n)
				)
				.OrderByDescending(n => ratings[n])
				.ThenBy(n => n, StringComparer.Ordinal)
		)
		{
			if (chosen.Count == Spells)
				break;
			var splashed = Needs(n).Where(splashes.Contains).ToList();
			if (splashed.Any(x => perColor[x] >= perSplash))
				continue;
			chosen.Add(n);
			foreach (var x in splashed)
				perColor[x]++;
		}
		if (extras.Any(e => !chosen.Any(n => Needs(n).Contains(e.Color))))
			return null; // a colour nothing needs isn't a splash, it's a weaker manabase
		int CardsOf(ManaColor c) => chosen.Count(n => ColorsOf(FrontCost(data[n])).Contains(c));
		if (extras.Any(e => mains.Any(m => CardsOf(m) < CardsOf(e.Color))))
			return null; // the colours with the most cards are the mains — the swap is tried too

		// what each colour still needs once the spells that make it are counted; fetchers come last
		var needed = colors.ToDictionary(
			c => c,
			c =>
				(
					mains.Contains(c) ? MainSources
					: splashes.Contains(c) ? SplashSources + perColor[c] - 1
					: RealSources
				) - chosen.Count(n => Fixes(n).Contains(c))
		);
		var fetchers = chosen.Count(FetchesABasic);
		var lands = DeckSize - chosen.Count;
		var fixing = new List<string>();
		// a land that makes only one of the deck's colours is a basic that enters tapped: never fixing
		// (Shayne, his qualifier pool: a White/Black dual in a Blue/Black + Green deck)
		var available = pool.Where(n =>
				IsLand(data[n]) && !Basics.Contains(n) && Fixes(n).Count(colors.Contains) >= 2
			)
			.ToList();
		while (fixing.Count < lands)
		{
			var land = available
				.OrderByDescending(n => Fixes(n).Count(c => needed.GetValueOrDefault(c) > 0))
				.ThenByDescending(n => Fixes(n).Count(colors.Contains))
				.ThenBy(n => n, StringComparer.Ordinal)
				.FirstOrDefault();
			if (land is null || !Fixes(land).Any(c => needed.GetValueOrDefault(c) > 0))
				break;
			fixing.Add(land);
			available.Remove(land);
			foreach (var c in Fixes(land).Where(needed.ContainsKey))
				needed[c]--;
		}
		// a fetcher finds a basic: with one basic of a colour in the deck, every fetcher is a source of
		// it; with none, no fetcher is (Shayne: two landcyclers and a Swamp are 3 black sources, two and
		// no Swamp are 0)
		var least = colors.ToDictionary(
			c => c,
			c =>
				needed[c] <= 0 ? 0
				: fetchers > 0 ? Math.Max(1, needed[c] - fetchers)
				: needed[c]
		);
		var free = lands - fixing.Count - least.Values.Sum();
		if (free < 0)
			return null; // the mana can't carry these colours
		var basics = colors
			.SelectMany(c => Enumerable.Repeat(Basics[(int)c], least[c]))
			.Concat(BasicsFor(chosen.Select(n => data[n]), colors, free));

		var main = chosen.Concat(fixing).Concat(basics).ToList();
		var sideboard = pool.ToList();
		foreach (var card in main.Where(n => !Basics.Contains(n)))
			sideboard.Remove(card);
		var splashCards = chosen.Where(n => Needs(n).Any(splashes.Contains)).ToList();
		var kind = perSplash == 1 ? Splashing : Greedy;
		var detail =
			kind == Splashing
				? $"{Splashing} {splashes[0]}: {string.Join(", ", splashCards.Distinct())}"
				: $"{Greedy} {string.Join("/", mains)}"
					+ string.Concat(
						extras.Select(e =>
							e.Splash
								? $" + {e.Color} splash ({string.Join(", ", chosen.Where(n => Needs(n).Contains(e.Color)).Distinct())})"
								: $" + {e.Color}"
						)
					);
		return new Built(
			new SealedDeck(colors, main, sideboard, kind, detail, splashes),
			chosen.Sum(n => ratings[n])
		);
	}

	/// <summary>The pips only <paramref name="color"/> pays beside the others — a hybrid with a half among them isn't one.</summary>
	static int Pips(ManaCost cost, ManaColor color, IReadOnlyCollection<ManaColor> others) =>
		cost.Symbols.Count(s =>
			s.Kind switch
			{
				SymbolKind.Colored or SymbolKind.TwoBrid => s.Color == color,
				SymbolKind.Hybrid => (s.Color == color || s.Color2 == color)
					&& !others.Contains(s.Color)
					&& !others.Contains(s.Color2),
				_ => false,
			}
		);

	static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ManaColor[]> fixes =
		new();

	/// <summary>
	/// The colours a card MAKES, from its SCRIPT — every fixer counts 1 (Shayne, LIM-3): a basic its
	/// colour; a mana ability (a land, a mana creature, a rock — not one restricted to some spells); a
	/// token it makes for you with one (Heartwood) or a basic land type (Forest Tentacle); every colour
	/// for a land that chooses its colour or mana of any one colour (Treasure). A card that FETCHES a
	/// basic is <see cref="FetchesABasic"/>: it makes nothing itself.
	/// </summary>
	public static IReadOnlyList<ManaColor> Fixes(string card) =>
		fixes.GetOrAdd(
			card,
			n =>
			{
				if (Basics.Contains(n))
					return [Wubrg.First(c => Basics[(int)c] == n)];
				var parts = Parts(CardRegistry.Get(n).Components, 0).ToList();
				var land = IsLand(CardRegistry.Get(n).Data);
				if (
					parts.Any(p =>
						p is ManaOfOneColorComponent || land && p is ChoosesColorAsItEnters
					)
				)
					return Wubrg;
				return
				[
					.. parts
						.OfType<ManaAbilityComponent>()
						.Where(m => m.Restriction == ManaRestriction.None)
						.Select(m => m.Color)
						.Concat(
							parts
								.OfType<CreateTokens>()
								.SelectMany(t =>
									Wubrg.Where(c => t.Token.TypeLine.Contains(Basics[(int)c]))
								)
						)
						.Where(Wubrg.Contains)
						.Distinct(),
				];
			}
		);

	/// <summary>
	/// Landcycling, Simulacrum Shaper, Fblthp: it searches for a BASIC land — a source of every colour
	/// the deck has at least one basic of, and of none other (Shayne). Enroot's "a land card" isn't one.
	/// </summary>
	public static bool FetchesABasic(string card) =>
		Parts(CardRegistry.Get(card).Components, 0)
			.Any(p => p is SearchLibrary { Type: CardType.Land, Supertype: Supertype.Basic });

	/// <summary>
	/// Its only mana is spent as it's used — a Treasure ("sacrifice this: add one mana of any color").
	/// Loot, the Nexus makes mana of one colour without being sacrificed: a lasting source.
	/// </summary>
	public static bool ManaIsSpentOnce(string card)
	{
		var parts = Parts(CardRegistry.Get(card).Components, 0).ToList();
		return parts.OfType<ManaOfOneColorComponent>().Any(m => m.SacrificeSelf)
			&& !parts.OfType<ManaOfOneColorComponent>().Any(m => !m.SacrificeSelf)
			&& !parts.OfType<ManaAbilityComponent>().Any();
	}

	/// <summary>
	/// The most cards one of its surveils looks at, as it is cast or enters — not an activated or a
	/// loyalty ability's, which come later and cost more (Castability, LIM-3.3; from the script).
	/// </summary>
	public static int Surveils(string card) =>
		Parts(
				CardRegistry
					.Get(card)
					.Components.Where(c =>
						c is not (ActivatedAbilityComponent or LoyaltyAbilityComponent)
					),
				0
			)
			.OfType<Surveil>()
			.Select(x => x.N)
			.DefaultIfEmpty(0)
			.Max();

	/// <summary>The colours a cost's symbols name — a hybrid both halves; a generic symbol none.</summary>
	static IEnumerable<ManaColor> ColorsOf(ManaCost cost) =>
		cost.Symbols.SelectMany(x =>
			x.Kind switch
			{
				SymbolKind.Colored or SymbolKind.TwoBrid => [x.Color],
				SymbolKind.Hybrid => new[] { x.Color, x.Color2 },
				_ => [], // a generic symbol's colours are unset (White by default)
			}
		);

	/// <summary>Everything a script holds, nested — skipping tokens made for another player (Vraska's Treasure is theirs).</summary>
	static IEnumerable<object> Parts(object? o, int depth) =>
		o switch
		{
			null or string or CardData => [],
			CreateTokens { ControlledBy: not null } => [],
			System.Collections.IEnumerable list when depth < 12 => list.Cast<object>()
				.SelectMany(x => Parts(x, depth + 1)),
			_ when depth < 12 && o.GetType().Namespace?.StartsWith("MagicCore") == true =>
				o.GetType()
					.GetProperties(
						System.Reflection.BindingFlags.Public
							| System.Reflection.BindingFlags.Instance
					)
					.Where(p => p.GetIndexParameters().Length == 0 && p.Name != "EqualityContract")
					.SelectMany(p => Parts(p.GetValue(o), depth + 1))
					.Prepend(o),
			_ => [],
		};

	/// <param name="pair">The pair to build on; null: the best (LIM-3.6 builds a close one).</param>
	public static SealedDeck Build(
		IReadOnlyList<string> pool,
		IReadOnlyDictionary<string, double> ratings,
		IReadOnlyList<ManaColor>? pair = null
	)
	{
		var data = pool.Distinct().ToDictionary(n => n, n => CardRegistry.Get(n).Data);
		var (colors, chosen) = (
			pair is null
				? Pairs
				:
				[
					[.. pair],
				]
		).Select(p => (Pair: p, Cards: BestSpells(pool, data, ratings, p))).MaxBy(p => p.Cards.Sum(n => ratings[n])); // the first pair wins a tie: deterministic

		var duals = pool.Where(n =>
				IsLand(data[n]) && DualColors(n) is { Length: 2 } c && c.All(colors.Contains)
			)
			.Take(DeckSize - chosen.Count)
			.ToList();
		var main = chosen
			.Concat(duals)
			.Concat(
				BasicsFor(
					chosen.Select(n => data[n]),
					colors,
					DeckSize - chosen.Count - duals.Count
				)
			)
			.ToList();

		var sideboard = pool.ToList();
		foreach (var card in chosen.Concat(duals))
			sideboard.Remove(card);
		return new SealedDeck(colors, main, sideboard);
	}

	/// <summary>A pair's best <see cref="Spells"/> castable spells. An unscripted card would play silently vanilla (MagicCore/CLAUDE.md), so it is never played.</summary>
	static List<string> BestSpells(
		IReadOnlyList<string> pool,
		IReadOnlyDictionary<string, CardData> data,
		IReadOnlyDictionary<string, double> ratings,
		ManaColor[] pair
	) =>
		[
			.. pool.Where(n =>
					!IsLand(data[n])
					&& CardRegistry.IsImplemented(n)
					&& Castable(FrontCost(data[n]), pair)
				)
				.OrderByDescending(n => ratings[n])
				.ThenBy(n => n, StringComparer.Ordinal)
				.Take(Spells),
		];

	/// <summary>
	/// Can these colours' lands pay every pip: a coloured pip needs its colour, a hybrid either half,
	/// {2/W} anything. {C} needs colourless mana, which basics never make.
	/// </summary>
	public static bool Castable(ManaCost cost, IReadOnlyCollection<ManaColor> colors) =>
		cost.Symbols.All(s =>
			s.Kind switch
			{
				SymbolKind.Colored => colors.Contains(s.Color),
				SymbolKind.Hybrid => colors.Contains(s.Color) || colors.Contains(s.Color2),
				_ => true,
			}
		);

	/// <summary>The cost a card is cast for from the hand — a prepare card's creature half.</summary>
	public static ManaCost FrontCost(CardData card) =>
		ManaCost.Parse(card.CardFaces?[0].ManaCost ?? card.ManaCost);

	static bool IsLand(CardData card) => card.TypeLine.Contains("Land");

	/// <summary>
	/// "{T}: Add {W} or {U}." — the colours its SCRIPT taps for (the mana abilities the engine plays),
	/// so an unscripted land makes none and is never counted as a source.
	/// </summary>
	static ManaColor[] DualColors(string land) =>
		[
			.. CardRegistry
				.Get(land)
				.Components.OfType<ManaAbilityComponent>()
				.Select(m => m.Color)
				.Distinct(),
		];

	/// <summary>
	/// The basics, split by the spells' pips (a hybrid's pip half to each colour it can be paid with),
	/// by largest remainder so they add up. ponytail: proportional to pips, not to WHEN they are needed
	/// — weight early pips more if builds come out colour-screwed.
	/// </summary>
	static IEnumerable<string> BasicsFor(
		IEnumerable<CardData> spells,
		ManaColor[] colors,
		int count
	)
	{
		var pips = colors.ToDictionary(c => c, _ => 0.0);
		foreach (var s in spells.SelectMany(c => FrontCost(c).Symbols))
			if (s.Kind is SymbolKind.Colored or SymbolKind.TwoBrid && pips.ContainsKey(s.Color))
				pips[s.Color] += 1;
			else if (s.Kind == SymbolKind.Hybrid)
			{
				var halves = new[] { s.Color, s.Color2 }.Where(pips.ContainsKey).ToList();
				foreach (var c in halves)
					pips[c] += 1.0 / halves.Count;
			}
		var total = pips.Values.Sum();
		var share = colors.ToDictionary(
			c => c,
			c => total == 0 ? (double)count / colors.Length : count * pips[c] / total
		);
		var whole = share.ToDictionary(kv => kv.Key, kv => (int)Math.Floor(kv.Value));
		foreach (
			var c in colors
				.OrderByDescending(c => share[c] - whole[c])
				.Take(count - whole.Values.Sum())
		)
			whole[c]++;
		return colors.SelectMany(c => Enumerable.Repeat(Basics[(int)c], whole[c]));
	}
}
