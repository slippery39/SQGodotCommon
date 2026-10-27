using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Limited;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Limited;

/// <summary>
/// The simulated draws behind "castable on time" (story LIM-3.3) — each rule it simulates pinned by
/// a deck where only that rule differs. Cards are found by their scripts, never by name (a name here
/// would list a card as tested in FraCardSheet.md).
/// </summary>
public class CastabilityTests
{
	static readonly IEnumerable<CardData> Fra = CardDataSet
		.Load("FRA")
		.Where(c => FraCards.IsImplemented(c.Name));

	static string Basic(ManaColor c) => SealedBuilder.Basics[(int)c];

	// Spells of one colour, one pip of it, nothing else that makes or fetches mana.
	static List<string> Spells(ManaColor color, int count) =>
		[
			.. Fra.Where(c =>
					!c.TypeLine.Contains("Land")
					&& SealedBuilder.FrontCost(c).Symbols is var s
					&& s.Count(x => x.Kind == SymbolKind.Colored) == 1
					&& s.Any(x => x.Kind == SymbolKind.Colored && x.Color == color)
					&& s.All(x => x.Kind is SymbolKind.Colored or SymbolKind.Generic)
					&& SealedBuilder.Fixes(c.Name).Count == 0
					&& !SealedBuilder.FetchesABasic(c.Name)
					&& SealedBuilder.Surveils(c.Name) == 0
				)
				.Select(c => c.Name)
				.Distinct()
				.Order(StringComparer.Ordinal)
				.Take(count),
		];

	static SealedDeck Deck(
		IEnumerable<string> main,
		ManaColor[] colors,
		ManaColor[]? splash = null
	) => new(colors, [.. main], [], Splash: splash);

	static readonly ManaColor W = ManaColor.White,
		U = ManaColor.Blue,
		B = ManaColor.Black;

	[Test]
	public void MoreSourcesOfAColour_MakeItsCardsCastableMoreOften()
	{
		var (white, blue) = (Spells(W, 12), Spells(U, 11));
		double OnTime(int plains) =>
			Castability.OnTime(
				Deck(
					[
						.. white,
						.. blue,
						.. Enumerable.Repeat(Basic(W), plains),
						.. Enumerable.Repeat(Basic(U), 17 - plains),
					],
					[W, U]
				),
				only: white
			);

		Assert.That(OnTime(9), Is.GreaterThan(OnTime(6)));
		Assert.That(OnTime(12), Is.GreaterThan(OnTime(9)));
		Assert.That(
			OnTime(17),
			Is.GreaterThan(OnTime(12)),
			"all Plains: only land screw stops a white card"
		);
	}

	[Test]
	public void ALandThatEntersTapped_IsLateOnCurve()
	{
		// a common dual: "enters tapped unless you control a planeswalker" — tapped in these games
		var dual = Fra.First(c =>
			c.Rarity == "common"
			&& FraCards.Get(c.Name).Components.OfType<EntersTappedComponent>().Any()
			&& SealedBuilder.Fixes(c.Name) is [var a, var b]
			&& new[] { a, b }.Order().SequenceEqual(new[] { W, U }.Order())
		).Name;
		var spells = Spells(W, 12).Concat(Spells(U, 11)).ToList();
		var basics = Deck(
			[.. spells, .. Enumerable.Repeat(Basic(W), 9), .. Enumerable.Repeat(Basic(U), 8)],
			[W, U]
		);
		var duals = Deck([.. spells, .. Enumerable.Repeat(dual, 17)], [W, U]);

		Assert.That(Castability.OnTime(duals), Is.LessThan(Castability.OnTime(basics)));
	}

	[Test]
	public void ALandcycler_FindsAColour_OnlyIfTheDeckHasABasicOfIt()
	{
		// Shayne: two landcyclers and a Swamp are 3 black sources; two and no Swamp are 0
		var cyclers = Landcyclers().Take(2).ToList();
		Assert.That(cyclers, Has.Count.EqualTo(2), "two landcyclers");
		var black = Spells(B, 1);
		var rest = Spells(W, 20 - cyclers.Count + 2);
		double OnTime(int swamps, bool withCyclers) =>
			Castability.OnTime(
				Deck(
					[
						.. black,
						.. withCyclers ? cyclers : rest.Take(cyclers.Count),
						.. rest.Skip(cyclers.Count).Take(20),
						.. Enumerable.Repeat(Basic(B), swamps),
						.. Enumerable.Repeat(Basic(W), 17 - swamps),
					],
					[W, B],
					splash: [B]
				),
				only: black
			);

		Assert.That(OnTime(0, withCyclers: true), Is.Zero, "no Swamp: nothing makes black");
		Assert.That(OnTime(1, withCyclers: true), Is.GreaterThan(OnTime(1, withCyclers: false)));
	}

	static List<string> Landcyclers() =>
		[
			.. Fra.Where(c =>
					SealedBuilder.FetchesABasic(c.Name)
					&& FraCards
						.Get(c.Name)
						.Components.OfType<ActivatedAbilityComponent>()
						.Any(a =>
							a.From == ZoneType.Hand && a.Effects.OfType<SearchLibrary>().Any()
						)
				)
				.Select(c => c.Name)
				.Distinct()
				.Order(StringComparer.Ordinal),
		];

	// White 2-drops and black 3-drops with one Swamp; the landcyclers, or white cards in their place.
	static SealedDeck Fixing(bool withCyclers, bool splashBlack)
	{
		var cyclers = Landcyclers().Take(4).ToList();
		Assert.That(cyclers, Has.Count.EqualTo(4), "four landcyclers");
		var white = Spells(W, 60).Where(n => Cost(n).ManaValue == 2).ToList();
		var black = Spells(B, 60).Where(n => Cost(n).ManaValue == 3).Take(4).ToList();
		return Deck(
			[
				.. black,
				.. withCyclers ? cyclers : white.Skip(15).Take(4),
				.. white.Take(15),
				.. Enumerable.Repeat(Basic(B), 1),
				.. Enumerable.Repeat(Basic(W), 16),
			],
			[W, B],
			splash: splashBlack ? [B] : null
		);
	}

	static ManaCost Cost(string name) => SealedBuilder.FrontCost(FraCards.Get(name).Data);

	[Test]
	public void ALandcycle_SpendsTheTurnsMana_BeforeItIsMeasured()
	{
		// Shayne's qualifier: he cycled for black on turn 2, so the 2/1 never came down to block
		static Castability.Result Measure(bool withCyclers) =>
			Castability.Measure(
				Fixing(withCyclers, splashBlack: false),
				only: [.. Spells(W, 60).Where(n => Cost(n).ManaValue == 2)]
			);

		Assert.That(Measure(true).OnTime, Is.LessThan(Measure(false).OnTime), "the 2-drops");
		Assert.That(Measure(true).CyclesEarly, Is.GreaterThan(0.2));
		Assert.That(Measure(false).CyclesEarly, Is.Zero);
	}

	[Test]
	public void ALandcycle_ForAColour_WaitsUntilACardNeedsIt()
	{
		// patient (Shayne): a black card due on turn 3 is fixed for on turn 2; a splash, due on 6, on 5
		double CyclesEarly(bool splash) =>
			Castability.Measure(Fixing(withCyclers: true, splashBlack: splash)).CyclesEarly;

		Assert.That(CyclesEarly(splash: true), Is.LessThan(CyclesEarly(splash: false) / 2));
	}

	[Test]
	public void ASplashCard_IsOnTimeByTurnSix_NotOnCurve()
	{
		var black = Spells(B, 50)
			.Where(n => SealedBuilder.FrontCost(FraCards.Get(n).Data).ManaValue < 4)
			.Take(1)
			.ToList();
		Assert.That(
			black,
			Has.Count.EqualTo(1),
			"a cheap black card, so turn 6 is later than its curve"
		);
		var main =
			(IEnumerable<string>)
				[
					.. black,
					.. Spells(W, 22),
					.. Enumerable.Repeat(Basic(B), 3),
					.. Enumerable.Repeat(Basic(W), 14),
				];

		Assert.That(
			Castability.OnTime(Deck(main, [W, B], splash: [B]), only: black),
			Is.GreaterThan(Castability.OnTime(Deck(main, [W, B]), only: black))
		);
	}

	// {2/W}: its colour or two generic — Karn's five of them are payable by any ten mana
	[TestCase("{2/U}", "W,W", true)]
	[TestCase("{2/U}", "W", false)]
	[TestCase("{2/U}", "U", true)]
	[TestCase("{2/W}{2/U}{2/B}{2/R}{2/G}", "W,W,W,W,W,W,W,W,W,W", true)]
	[TestCase("{1}{W}", "U,U", false)]
	[TestCase("{W/B}{W/B}", "B,W", true)]
	public void APayment_UsesEachSourceOnce_AndTwoBridsEitherWay(
		string cost,
		string sources,
		bool pays
	) =>
		Assert.That(
			Castability.Pays(
				[.. sources.Split(',').Select(c => new[] { ManaCost.ColorOf(c) })],
				ManaCost.Parse(cost)
			),
			Is.EqualTo(pays)
		);

	// By what the text says: a Treasure ("sacrifice this") is spent once; a surveil only an ability does
	// ("{cost}: Surveil N", a loyalty ability) isn't one the card does as it's cast or enters.
	[Test]
	public void ATreasureIsSpentOnce_AndAnAbilitysSurveilIsntCasts()
	{
		var seen = new Dictionary<string, int>();
		foreach (var card in Fra)
		{
			var text =
				card.OracleText
				?? string.Join('\n', (card.CardFaces ?? []).Select(f => f.OracleText));
			if (
				text.Contains("Sacrifice this token: Add one mana") && !text.Contains("They create")
			)
			{
				Assert.That(SealedBuilder.ManaIsSpentOnce(card.Name), card.Name);
				seen["Treasure"] = seen.GetValueOrDefault("Treasure") + 1;
			}
			var surveils = System.Text.RegularExpressions.Regex.Matches(
				text,
				@"^(.*)[Ss]urveil \d",
				System.Text.RegularExpressions.RegexOptions.Multiline
			);
			if (
				surveils.Count > 0
				&& surveils.All(m =>
					System.Text.RegularExpressions.Regex.IsMatch(
						m.Groups[1].Value,
						@"^[^""]*(\}|[+−-]\d+|0): "
					)
				)
			)
			{
				Assert.That(SealedBuilder.Surveils(card.Name), Is.Zero, card.Name);
				seen["an ability's surveil"] = seen.GetValueOrDefault("an ability's surveil") + 1;
			}
		}
		TestContext.Out.WriteLine(string.Join("; ", seen.Select(kv => $"{kv.Key} {kv.Value}")));
		Assert.That(seen.Keys, Has.Count.EqualTo(2), "both seen");
	}

	[Test]
	public void TheSameDeck_MeasuresTheSame_AndOnlyItsSpellsCount()
	{
		var deck = SealedBuilder.Suggestions(Boosters.SealedPool(3), CardRatings.Fra)[0];
		Assert.That(Castability.OnTime(deck), Is.EqualTo(Castability.OnTime(deck)));
		Assert.That(Castability.OnTime(deck), Is.InRange(0.0, 1.0));
	}
}
