using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Limited;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Limited;

/// <summary>
/// The sealed suggestions (story LIM-3): the streamlined build, a splash, a greedy splash — each by
/// Shayne's rules of 2026-10-24, read from <see cref="SealedBuilder"/>'s own constants.
/// </summary>
public class SealedSuggestionTests
{
	static List<string> Pool(int seed) => [.. Boosters.SealedPool(seed)];

	static CardData Data(string name) => FraCards.Get(name).Data;

	static bool IsLand(string name) => Data(name).TypeLine.Contains("Land");

	// Every fixer counts 1 (Shayne): what makes the colour, and every fetcher once the deck has a basic
	// of it — two landcyclers and a Swamp are 3 black sources, two and no Swamp are 0. Fixes and
	// FetchesABasic are themselves checked against the printed text below.
	static bool Makes(string card, ManaColor color) => SealedBuilder.Fixes(card).Contains(color);

	static int Sources(SealedDeck deck, ManaColor color) =>
		deck.Main.Count(n => Makes(n, color))
		+ (
			deck.Main.Contains(SealedBuilder.Basics[(int)color])
				? deck.Main.Count(SealedBuilder.FetchesABasic)
				: 0
		);

	// Fixes and FetchesABasic read SCRIPTS; this reads the PRINTED text, so a fixer the script hides
	// shows here. By what the text says, never a card's name (a name would list it as tested in
	// FraCardSheet.md).
	[Test]
	public void Fixes_MatchesWhatEachCardSays()
	{
		var seen = new Dictionary<string, int>();
		foreach (var card in CardDataSet.Load("FRA").Where(c => FraCards.IsImplemented(c.Name)))
		{
			// a two-faced card (prepare: creature // spell) prints its text on its faces
			var text =
				card.OracleText
				?? string.Join('\n', (card.CardFaces ?? []).Select(f => f.OracleText));
			var (fixes, fetches) = (
				SealedBuilder.Fixes(card.Name),
				SealedBuilder.FetchesABasic(card.Name)
			);
			void Kind(string kind, bool holds)
			{
				Assert.That(
					holds,
					$"{card.Name}: {kind} — makes {string.Join(",", fixes)}, fetches {fetches}"
				);
				seen[kind] = seen.GetValueOrDefault(kind) + 1;
			}
			var basicSearch = System.Text.RegularExpressions.Regex.IsMatch(
				text,
				@"landcycling|[Ss]earch your library for (a|up to \w+) basic land"
			);
			Kind("fetches a basic only if its text says so", fetches == basicSearch);
			if (basicSearch)
				Kind(
					"a fetcher makes no colour itself",
					!card.TypeLine.Contains("Land") || fixes.Count < 5
				);
			else if (
				System.Text.RegularExpressions.Regex.IsMatch(
					text,
					@"[Ss]earch your library for a land card.*graveyard"
				)
			)
				Kind("a land searched into the graveyard: no source", !fetches && fixes.Count == 0);
			else if (text.Contains("Heartwood token"))
				Kind(
					"makes a Heartwood: red and green",
					fixes.Contains(ManaColor.Red) && fixes.Contains(ManaColor.Green)
				);
			else if (text.Contains("They create a Treasure"))
				Kind("a Treasure for the opponent: nothing", fixes.Count < 5);
			else if (
				card.TypeLine.Contains("Land")
				&& System.Text.RegularExpressions.Regex.Match(
					text,
					@"\{T\}: Add \{(.)\} or \{(.)\}"
				)
					is { Success: true } dual
			)
				Kind(
					"a dual: the two colours it prints",
					fixes
						.Select(c => "WUBRG"[(int)c])
						.Order()
						.SequenceEqual(
							new[] { dual.Groups[1].Value[0], dual.Groups[2].Value[0] }.Order()
						)
				);
			else if (!card.TypeLine.Contains("Land") && text.Contains("Spend this mana only"))
				Kind("restricted mana: not a source", fixes.Count == 0);
		}
		TestContext.Out.WriteLine(string.Join("; ", seen.Select(kv => $"{kv.Key} {kv.Value}")));
		Assert.That(seen.Keys, Has.Count.EqualTo(7), "every kind seen at least once");
	}

	[Test]
	public void EverySuggestion_IsLegal_AndEverySplashKeepsTheRules()
	{
		var bar = SealedBuilder.TopDecile(CardRatings.Fra);
		var (splashes, greedy) = (0, 0);
		for (var seed = 1; seed <= 60; seed++)
		{
			var pool = Pool(seed);
			var builds = SealedBuilder.Suggestions(pool, CardRatings.Fra);
			var streamlined = builds[0];
			Assert.That(
				streamlined,
				Is.EqualTo(SealedBuilder.Build(pool, CardRatings.Fra)).Using<SealedDeck>(Same)
			);
			foreach (var deck in builds)
			{
				CheckRules(
					deck,
					pool,
					streamlined,
					CardRatings.Fra,
					bar,
					$"seed {seed}, {deck.Label}"
				);
				splashes += deck.Kind == SealedBuilder.Splashing ? 1 : 0;
				greedy += deck.Kind == SealedBuilder.Greedy ? 1 : 0;
			}
		}
		TestContext.Out.WriteLine($"60 pools: {splashes} splash builds, {greedy} greedy");
		Assert.That(splashes, Is.GreaterThan(0), "the splash rule fires on some pool");
		Assert.That(greedy, Is.GreaterThan(0), "the greedy rule fires on some pool");
	}

	[Test]
	public void AnOpponent_PlaysOneOfItsPoolsSuggestions_TheSameOneEachTime()
	{
		for (var seed = 1; seed <= 20; seed++)
		{
			var opponent = SealedBuilder.Opponent(seed);
			Assert.That(
				SealedBuilder.Suggestions(Pool(seed), CardRatings.Fra),
				Has.Some.Matches<SealedDeck>(d => Same(d, opponent)),
				$"seed {seed}"
			);
			Assert.That(Same(SealedBuilder.Opponent(seed), opponent), $"seed {seed}");
		}
	}

	static bool Same(SealedDeck a, SealedDeck b) =>
		a.Label == b.Label
		&& a.Colors.SequenceEqual(b.Colors)
		&& a.Main.SequenceEqual(b.Main)
		&& a.Sideboard.SequenceEqual(b.Sideboard);

	// Two bombs of one off colour, each a single pip, and two duals of that colour: the mana carries
	// both, so the greedy build splashes both and the splash build the better one. Its own ratings —
	// the pair's cards good, the rest worthless, the bombs best — so the pair stays put (two real FRA
	// bombs pull it onto their colour in every pool tried) and the test is about the rules, not FRA's
	// numbers.
	[Test]
	public void TwoBombsOfOneColour_AndItsDuals_MakeAGreedyBuild()
	{
		// the first pool whose pair the bombs don't pull onto their colour (a pool heavy in one of the
		// pair's colours can: that colour plus two bombs outscores the pair)
		var found = (
			from seed in Enumerable.Range(1, 30)
			let basePool = Pool(seed)
			let pair = SealedBuilder.Build(basePool, CardRatings.Fra).Colors
			from color in Colours.Where(c => !pair.Contains(c))
			let bombs = Bombs(color, basePool)
			where bombs.Count == 2
			let duals = DualsOf(color, pair)
			where duals.Count > 0
			let pool = basePool.Concat(bombs).Concat(duals).ToList()
			let ratings = Ratings(pool, pair, bombs)
			where SealedBuilder.Build(pool, ratings).Colors.SequenceEqual(pair)
			select (pool, pair, bombs, ratings)
		).FirstOrDefault();
		Assert.That(found.pool, Is.Not.Null, "a pool whose pair the bombs don't move");
		var (withBombs, _, added, rated) = found;
		var bar = SealedBuilder.TopDecile(rated);

		var builds = SealedBuilder.Suggestions(withBombs, rated);

		Assert.That(
			builds.Select(b => b.Colors.Count).ToArray(),
			Is.EqualTo(new[] { 2, 3, 3 }),
			string.Join(" | ", builds.Select(b => b.Label))
		);
		Assert.That(
			CheckRules(builds[1], withBombs, builds[0], rated, bar, builds[1].Label),
			Is.EqualTo(1)
		);
		Assert.That(builds[1].Main, Does.Contain(added[0]), "the splash takes the better bomb");
		Assert.That(
			CheckRules(builds[2], withBombs, builds[0], rated, bar, builds[2].Label),
			Is.EqualTo(2)
		);
		Assert.That(builds[2].Main, Is.SupersetOf(added));
	}

	// One bomb and no fixing at all: 3 basics of the splash leave 14, and only a 7/7 split meets the
	// main colours' 7 — the basics must be ALLOCATED to the rule, not split by pips and then checked.
	[Test]
	public void OneBomb_OnBasicsAlone_IsSplashed_WithSevenSourcesForEachMainColour()
	{
		var found = (
			from seed in Enumerable.Range(1, 30)
			let basePool = Pool(seed)
				.Where(n => !IsLand(n) || SealedBuilder.Basics.Contains(n)) // no fixing
				.ToList()
			let pair = SealedBuilder.Build(basePool, CardRatings.Fra).Colors
			from color in Colours.Where(c => !pair.Contains(c))
			let bombs = Bombs(color, basePool).Take(1).ToList()
			where bombs.Count == 1
			let pool = basePool.Concat(bombs).ToList()
			let ratings = Ratings(pool, pair, bombs)
			let streamlined = SealedBuilder.Build(pool, ratings)
			where streamlined.Colors.SequenceEqual(pair)
			// lopsided pips: split by pips, the weaker colour would get under 7 of the 14 basics
			where pair.Min(c => PipShare(streamlined, c)) < 0.45
			select (pool, ratings, color)
		).FirstOrDefault();
		Assert.That(found.pool, Is.Not.Null, "a pool whose pair the bomb doesn't move");
		var (withBomb, rated, splash) = found;

		var builds = SealedBuilder.Suggestions(withBomb, rated);

		Assert.That(builds, Has.Count.EqualTo(2), string.Join(" | ", builds.Select(b => b.Label)));
		Assert.That(
			CheckRules(
				builds[1],
				withBomb,
				builds[0],
				rated,
				SealedBuilder.TopDecile(rated),
				builds[1].Label
			),
			Is.EqualTo(1)
		);
		Assert.That(builds[1].Colors, Does.Contain(splash));
	}

	// A colour's share of the coloured pips among a deck's spells.
	static double PipShare(SealedDeck deck, ManaColor color)
	{
		var pips = deck
			.Main.Where(n => !IsLand(n))
			.SelectMany(n => SealedBuilder.FrontCost(Data(n)).Symbols)
			.Where(p => p.Kind == SymbolKind.Colored && deck.Colors.Contains(p.Color))
			.ToList();
		return pips.Count == 0 ? 0.5 : (double)pips.Count(p => p.Color == color) / pips.Count;
	}

	// Four colours only for a top-10% card three colours can't play (Shayne, round 4). Fixing for
	// anything — six lands that choose their colour — so the mana never decides; the ratings do: the
	// pair's cards clearly best (so neither is dropped), two cards of a fourth colour a little better
	// than them but no bomb, and bombs (the top 10%, set by ten unplayed ratings above everything).
	// A bomb in the third colour only: four colours would add only playables, so they aren't
	// suggested. A bomb in the fourth too: three colours can't play both, so four are.
	[Test]
	public void AFourthColour_IsSuggested_OnlyForABombThreeColoursCantPlay()
	{
		var chooser = CardDataSet
			.Load("FRA")
			.First(c =>
				c.TypeLine.Contains("Land")
				&& FraCards.Get(c.Name).Components.OfType<ChoosesColorAsItEnters>().Any()
			)
			.Name;
		var basePool = Pool(1).Concat(Enumerable.Repeat(chooser, 6)).ToList();
		bool Mono(string n, ManaColor c) =>
			!IsLand(n)
			&& SealedBuilder
				.FrontCost(Data(n))
				.Symbols.Where(p => p.Kind == SymbolKind.Colored)
				.Select(p => p.Color)
				.Distinct()
				.SequenceEqual([c]);
		List<string> MonoOf(ManaColor c) =>
			[.. basePool.Distinct().Where(n => Mono(n, c)).Order(StringComparer.Ordinal)];
		// the pair: the two colours with the most cards, so neither extra colour can displace one
		var byCount = Colours.OrderByDescending(c => MonoOf(c).Count).ToList();
		var pair = byCount.Take(2).ToArray();
		var (third, fourth) = (byCount[2], byCount[3]);
		Assert.That(
			MonoOf(fourth),
			Has.Count.GreaterThanOrEqualTo(5),
			"a fourth colour with cards to rate"
		);
		Assert.That(
			pair.Select(c => MonoOf(c).Count),
			Is.All.GreaterThanOrEqualTo(9),
			"nine of each pair colour"
		);
		Dictionary<string, double> Rated(params string[] bombs)
		{
			var rated = basePool
				.Distinct()
				.ToDictionary(
					n => n,
					n =>
						bombs.Contains(n) ? 0.99
						: MonoOf(fourth).Take(2).Contains(n) ? 0.75 // better playables, not bombs
						// nine of each: neither pair colour fills a deck alone, so neither is dropped
						: pair.Any(c => MonoOf(c).Take(9).Contains(n)) ? 0.7
						: 0.0
				);
			for (var i = 0; i < 10; i++)
				rated[$"unplayed {i}"] = 0.99; // the top decile: only the bombs reach it
			return rated;
		}
		var (bomb3, bomb4) = (MonoOf(third)[4], MonoOf(fourth)[4]);

		Assert.That(
			SealedBuilder.Build(basePool, Rated(bomb3, bomb4)).Colors,
			Is.EquivalentTo(pair)
		);
		var one = SealedBuilder.Suggestions(basePool, Rated(bomb3));
		Assert.That(
			one.Select(b => b.Colors.Count),
			Has.None.EqualTo(4),
			string.Join(" | ", one.Select(b => b.Label))
		);
		Assert.That(
			one.Where(b => b.Main.Contains(bomb3)),
			Is.Not.Empty,
			"three colours play the bomb"
		);

		var two = SealedBuilder.Suggestions(basePool, Rated(bomb3, bomb4));
		Assert.That(
			two.Where(b => b.Colors.Count == 4 && b.Main.Contains(bomb3) && b.Main.Contains(bomb4)),
			Is.Not.Empty,
			string.Join(" | ", two.Select(b => b.Label))
		);
	}

	static readonly ManaColor[] Colours =
	[
		ManaColor.White,
		ManaColor.Blue,
		ManaColor.Black,
		ManaColor.Red,
		ManaColor.Green,
	];

	// The test's own ratings: the bombs best, the pair's coloured cards good, the rest worthless.
	static Dictionary<string, double> Ratings(
		List<string> pool,
		IReadOnlyList<ManaColor> pair,
		List<string> bombs
	) =>
		pool.Distinct()
			.ToDictionary(
				n => n,
				n =>
					bombs.Contains(n) ? (bombs.IndexOf(n) == 0 ? 0.95 : 0.9)
					: SealedBuilder.Castable(SealedBuilder.FrontCost(Data(n)), pair)
					&& SealedBuilder
						.FrontCost(Data(n))
						.Symbols.Any(p =>
							p.Kind is SymbolKind.Colored or SymbolKind.Hybrid or SymbolKind.TwoBrid
							&& p.Color != ManaColor.Colorless
						)
						? 0.6
					: 0.0
			);

	// Two cards of one colour with a single coloured pip that the pool doesn't hold already (a copy
	// there would be a third bomb), alphabetically — any will do as bombs here.
	static List<string> Bombs(ManaColor color, List<string> pool) =>
		[
			.. CardDataSet
				.Load("FRA")
				.Select(c => c.Name)
				.Where(n =>
					FraCards.IsImplemented(n)
					&& !IsLand(n)
					&& !pool.Contains(n)
					&& SealedBuilder.FrontCost(Data(n)).Symbols is var pips
					&& pips.Count(p => p.Kind == SymbolKind.Colored) == 1
					&& pips.Any(p => p.Kind == SymbolKind.Colored && p.Color == color)
				)
				.Distinct()
				.OrderBy(n => n, StringComparer.Ordinal)
				.Take(2),
		];

	// Common duals of the splash colour and one of the pair's — found by what they make, not named (a
	// name here would list them as tested in FraCardSheet.md).
	static List<string> DualsOf(ManaColor color, IReadOnlyList<ManaColor> pair) =>
		[
			.. CardDataSet
				.Load("FRA")
				.Where(c =>
					c.Rarity == "common"
					&& FraCards.Get(c.Name).Components.OfType<ManaAbilityComponent>().Count() == 2
					&& Makes(c.Name, color)
					&& pair.Any(m => Makes(c.Name, m))
				)
				.Select(c => c.Name),
		];

	/// <summary>
	/// A suggestion's legality, and every colour's rule (Shayne, 2026-10-24): the first two are its mains
	/// (7 sources); each colour after them is REAL (6+) or a splash — its cards top-decile, one pip, and
	/// 3 sources plus one per extra card. Returns how many cards need a colour beyond the mains.
	/// </summary>
	static int CheckRules(
		SealedDeck deck,
		List<string> pool,
		SealedDeck streamlined,
		IReadOnlyDictionary<string, double> ratings,
		double bar,
		string why
	)
	{
		Assert.That(deck.Main, Has.Count.EqualTo(SealedBuilder.DeckSize), why);
		var spells = deck.Main.Where(n => !IsLand(n)).ToList();
		Assert.That(spells, Has.Count.LessThanOrEqualTo(SealedBuilder.Spells), why);
		foreach (var spell in spells)
			Assert.That(
				SealedBuilder.Castable(SealedBuilder.FrontCost(Data(spell)), deck.Colors),
				$"{why}: {spell}"
			);
		Assert.That(
			deck.Main.Concat(deck.Sideboard).Where(n => !SealedBuilder.Basics.Contains(n)),
			Is.EquivalentTo(pool.Where(n => !SealedBuilder.Basics.Contains(n))),
			why
		);
		// a land that makes only one of the deck's colours is a tapped basic, never fixing
		foreach (var land in deck.Main.Where(n => IsLand(n) && !SealedBuilder.Basics.Contains(n)))
			Assert.That(
				SealedBuilder.Fixes(land).Count(deck.Colors.Contains),
				Is.GreaterThanOrEqualTo(2),
				$"{why}: {land} makes only one of its colours"
			);
		if (deck.Colors.Count == 2)
			return 0; // the streamlined build is LIM-1's: basics by pips, no source bar
		foreach (var main in deck.Colors.Take(2))
			Assert.That(
				Sources(deck, main),
				Is.GreaterThanOrEqualTo(SealedBuilder.MainSources),
				$"{why}: {main}"
			);
		var beyond = 0;
		foreach (var extra in deck.Colors.Skip(2))
		{
			var others = deck.Colors.Where(c => c != extra).ToArray();
			var mains = deck.Colors.Take(2).ToArray();
			// as the builder reads it: can't be cast without this colour — or, off the mains, a hybrid
			// of extras that names it (an {R/G} card with R and G both extras needs both)
			bool Hard(string n, ManaColor x) =>
				!SealedBuilder.Castable(
					SealedBuilder.FrontCost(Data(n)),
					deck.Colors.Where(c => c != x).ToArray()
				);
			var needing = spells
				.Where(n =>
					Hard(n, extra)
					|| !SealedBuilder.Castable(SealedBuilder.FrontCost(Data(n)), mains)
						&& !deck.Colors.Skip(2).Any(x => Hard(n, x))
						&& SealedBuilder
							.FrontCost(Data(n))
							.Symbols.Any(sy =>
								sy.Kind == SymbolKind.Hybrid
								&& (sy.Color == extra || sy.Color2 == extra)
							)
				)
				.ToList();
			Assert.That(needing, Is.Not.Empty, $"{why}: {extra} plays nothing");
			beyond += needing.Count;
			if (Sources(deck, extra) >= SealedBuilder.RealSources)
				continue; // a real colour: any card
			Assert.That(
				Sources(deck, extra),
				Is.GreaterThanOrEqualTo(SealedBuilder.SplashSources + needing.Count - 1),
				$"{why}: {extra}"
			);
			foreach (var card in needing)
			{
				Assert.That(ratings[card], Is.GreaterThanOrEqualTo(bar), $"{why}: {card}");
				Assert.That(
					SealedBuilder
						.FrontCost(Data(card))
						// a hybrid counts when no other colour of the deck pays it
						.Symbols.Count(s =>
							s.Kind is SymbolKind.Colored or SymbolKind.TwoBrid && s.Color == extra
							|| s.Kind == SymbolKind.Hybrid
								&& (s.Color == extra || s.Color2 == extra)
								&& !others.Contains(s.Color)
								&& !others.Contains(s.Color2)
						),
					Is.LessThanOrEqualTo(1),
					$"{why}: {card}"
				);
			}
		}
		return beyond;
	}
}
