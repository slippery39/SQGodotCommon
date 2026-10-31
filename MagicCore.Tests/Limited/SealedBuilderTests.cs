using MagicCore.Cards;
using MagicCore.Limited;
using MagicCore.Sets;

namespace MagicCore.Tests.Limited;

/// <summary>The AI's sealed build (story LIM-1): a legal 40 in two colours, chosen by the ratings it is given.</summary>
public class SealedBuilderTests
{
	static readonly IReadOnlyList<string> BasicNames = SealedBuilder.Basics;

	static List<string> Pool(int seed) =>
		[.. Boosters.Sealed("FRA", seed).SelectMany(p => p).Select(c => c.Card)];

	static CardData Data(string name) => CardRegistry.Get(name).Data;

	static bool IsLand(string name) => Data(name).TypeLine.Contains("Land");

	[Test]
	public void EveryPool_Builds40_InTwoColours_EverySpellCastable_NothingLostFromThePool()
	{
		for (var seed = 1; seed <= 30; seed++)
		{
			var pool = Pool(seed);
			var deck = SealedBuilder.Build(pool, CardRatings.Of("FRA"));

			Assert.That(deck.Main, Has.Count.EqualTo(SealedBuilder.DeckSize), $"seed {seed}");
			Assert.That(deck.Colors, Has.Count.EqualTo(2));
			var spells = deck.Main.Where(n => !IsLand(n)).ToList();
			Assert.That(spells, Has.Count.EqualTo(SealedBuilder.Spells), $"seed {seed}");
			foreach (var spell in spells)
				Assert.That(
					SealedBuilder.Castable(SealedBuilder.FrontCost(Data(spell)), deck.Colors),
					spell
				);
			// basics are free: the pool's own go to the sideboard, the deck's are added
			Assert.That(
				deck.Main.Concat(deck.Sideboard).Where(n => !BasicNames.Contains(n)),
				Is.EquivalentTo(pool.Where(n => !BasicNames.Contains(n))),
				$"seed {seed}"
			);
		}
	}

	[Test]
	public void TheRatingsPickTheColours_AndItsDualsArePlayed()
	{
		string[] wanted = ["U", "B"];
		// found by what they make, not named: a name here would list them as tested in FraCardSheet.md
		static string CommonDual(string a, string b) =>
			CardDataSet
				.Load("FRA")
				.Single(c =>
					c.Rarity == "common"
					&& (c.Card.OracleText ?? "").Contains($"Add {{{a}}} or {{{b}}}")
				)
				.Card.Name;
		var (onColour, offColour) = (CommonDual("U", "B"), CommonDual("R", "G"));
		var pool = Pool(3).Append(onColour).Append(offColour).ToList();
		var ratings = pool.Distinct()
			.ToDictionary(n => n, n => Data(n).Colors is [var c] && wanted.Contains(c) ? 1.0 : 0.0);

		var deck = SealedBuilder.Build(pool, ratings);

		Assert.That(deck.Colors, Is.EquivalentTo(new[] { ManaColor.Blue, ManaColor.Black }));
		Assert.That(deck.Main, Does.Contain(onColour));
		Assert.That(deck.Main, Does.Not.Contain(offColour));
		Assert.That(deck.Sideboard, Does.Contain(offColour));
	}

	[Test]
	public void TheBasics_FollowThePips()
	{
		var deck = SealedBuilder.Build(Pool(5), CardRatings.Of("FRA"));
		var (a, b) = (deck.Colors[0], deck.Colors[1]);
		int Pips(ManaColor c) =>
			deck
				.Main.Where(n => !IsLand(n))
				.Sum(n =>
					SealedBuilder
						.FrontCost(Data(n))
						.Symbols.Count(s => s.Kind == SymbolKind.Colored && s.Color == c)
				);
		int Basics(ManaColor c) => deck.Main.Count(n => n == BasicNames[(int)c]);

		// more pips, never fewer basics (equal pips may split an odd count either way)
		Assert.That(
			Math.Sign(Pips(a) - Pips(b)) * (Basics(a) - Basics(b)),
			Is.GreaterThanOrEqualTo(0),
			$"{a} {Pips(a)}/{Basics(a)}, {b} {Pips(b)}/{Basics(b)}"
		);
	}

	[Test]
	public void AHybridPip_IsPaidByEitherHalf()
	{
		Assert.That(
			SealedBuilder.Castable(ManaCost.Parse("{W/B}"), [ManaColor.White, ManaColor.Blue]),
			Is.True
		);
		Assert.That(
			SealedBuilder.Castable(ManaCost.Parse("{2}{R/W}"), [ManaColor.Blue, ManaColor.Green]),
			Is.False
		);
		Assert.That(
			SealedBuilder.Castable(ManaCost.Parse("{1}{U}{U}"), [ManaColor.Blue, ManaColor.Red]),
			Is.True
		);
		Assert.That(
			SealedBuilder.Castable(ManaCost.Parse("{C}"), [ManaColor.Blue, ManaColor.Red]),
			Is.False
		);
	}

	[Test]
	public void EveryCardIsRated()
	{
		Assert.That(
			CardDataSet
				.Load("FRA")
				.Where(c => !CardRatings.Of("FRA").ContainsKey(c.Card.Name))
				.Select(c => c.Card.Name),
			Is.Empty
		);
	}

	[Test]
	public void ARating_IsSealedsRate_ElsePremiersOnSealedsScale_ElsePooledAndPulledToTheMedian()
	{
		const int enough = CardRatings.MinGames;
		static CardRatings.Gih G(double wr, int games) => new(wr, games);
		var ratings = CardRatings.From17Lands(
			new Dictionary<string, CardRatings.Formats>
			{
				// the one card rated in both: Sealed reads 0.02 above Premier — the gap
				["sealed"] = new(G(0.60, enough), G(0.58, enough)),
				["premier"] = new(G(0.90, enough - 1), G(0.55, enough)),
				["pooled"] = new(G(0.30, enough / 2), G(0.40, enough / 2)),
				["rare"] = new(G(0.10, 1), G(1.00, 1)),
			}
		);

		Assert.That(ratings["sealed"], Is.EqualTo(0.60));
		Assert.That(ratings["premier"], Is.EqualTo(0.55 + 0.02).Within(1e-12));
		Assert.That(ratings["pooled"], Is.EqualTo((0.30 + 0.40 + 0.02) / 2).Within(1e-12));
		// the rated two (0.60, 0.57) have median 0.585; two games barely move it
		var wins = 0.10 + 1.00 + 0.02;
		Assert.That(
			ratings["rare"],
			Is.EqualTo((wins + 0.585 * (enough - 2)) / enough).Within(1e-12)
		);
	}

	[Test]
	public void ASetNobodyHasPlayed_RatesEveryCardEven()
	{
		var none = new CardRatings.Gih(null, 0);
		var ratings = CardRatings.From17Lands(
			new Dictionary<string, CardRatings.Formats> { ["new"] = new(none, none) }
		);
		Assert.That(ratings["new"], Is.EqualTo(0.5));
	}
}
