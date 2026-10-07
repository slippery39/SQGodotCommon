using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Mana;

public class ManaCostTests
{
	[Test]
	public void Parse_EverySymbolKind()
	{
		var cost = ManaCost.Parse("{2}{W/U}{2/G}{X}{R}");

		Assert.That(
			cost.Symbols,
			Is.EqualTo(
				new[]
				{
					new ManaSymbol(SymbolKind.Generic, Amount: 2),
					new ManaSymbol(SymbolKind.Hybrid, ManaColor.White, ManaColor.Blue),
					new ManaSymbol(SymbolKind.TwoBrid, ManaColor.Green),
					new ManaSymbol(SymbolKind.X),
					new ManaSymbol(SymbolKind.Colored, ManaColor.Red),
				}
			)
		);
		Assert.That(cost.ManaValue, Is.EqualTo(2 + 1 + 2 + 0 + 1));
	}

	// Thalia's tax once rendered as {1}{B}{1}.
	[Test]
	public void ATax_MergesIntoTheGenericSymbol() =>
		Assert.That(
			ManaCost.Parse("{1}{B}").WithGeneric(1).Symbols,
			Is.EqualTo(ManaCost.Parse("{2}{B}").Symbols)
		);

	// Every printed cost and type line in the set builds — a new symbol or type word fails here,
	// not mid-game. Scryfall's own mana value is the oracle for the parse.
	[Test]
	public void EveryFraCard_BuildsAndItsManaValueMatchesScryfall()
	{
		foreach (var data in CardDataSet.Load("FRA").Where(d => d.CardFaces is null))
		{
			var card = CardFactory.Create(CardDefinition.Vanilla(data), ownerId: 1);
			Assert.That(card.ManaCost.ManaValue, Is.EqualTo((int)data.Cmc), data.Name);
			// Printed numbers, negatives included (Loot, the Anomaly is -2/4).
			if (int.TryParse(data.Power, out var power))
				Assert.That(card.OwnPower, Is.EqualTo(power), data.Name);
			if (int.TryParse(data.Toughness, out var toughness))
				Assert.That(card.OwnToughness, Is.EqualTo(toughness), data.Name);
		}
	}
}

public class ManaPaymentTests
{
	static (GameState S, int Me) Board(params string[] lands)
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		foreach (var land in lands)
			(s, _) = TestGames.Put(s, me, TestGames.Card(land), ZoneType.Battlefield);
		return (s, me);
	}

	static bool CanPay(string cost, params string[] lands)
	{
		var (s, me) = Board(lands);
		return ManaPayment.Find(s, me, ManaCost.Parse(cost), 0) is not null;
	}

	[TestCase("{1}{W}", new[] { "Forest", "Plains" }, true)]
	[TestCase("{G}{G}", new[] { "Forest", "Plains" }, false)]
	[TestCase("{W/U}", new[] { "Island" }, true)]
	[TestCase("{W/U}", new[] { "Forest" }, false)]
	[TestCase("{2/W}", new[] { "Plains" }, true)]
	[TestCase("{2/W}", new[] { "Forest", "Forest" }, true)]
	[TestCase("{2/W}", new[] { "Forest" }, false)]
	// Greedy gives the Island (first source) to {W/U} and strands {U/B}; backtracking must not.
	[TestCase("{W/U}{U/B}", new[] { "Island", "Plains" }, true)]
	public void Find(string cost, string[] lands, bool payable) =>
		Assert.That(CanPay(cost, lands), Is.EqualTo(payable));

	// A mana creature tapped for a spell can no longer attack — lands go first.
	[Test]
	public void LandsAreTappedBeforeAManaCreature()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		int propagator,
			spell;
		(s, propagator) = TestGames.Put(
			s,
			me,
			FraCards.Get("Greenhouse Propagator"),
			ZoneType.Battlefield
		);
		(s, _) = TestGames.Put(s, me, TestGames.Card("Forest"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, me, TestGames.Card("Forest"), ZoneType.Battlefield);
		(s, spell) = TestGames.Put(
			s,
			me,
			TestGames.CreatureCosting("{1}{G}", "Bear", "2/2"),
			ZoneType.Hand
		);

		s = s.Act(new CastSpellAction { PlayerId = me, CardId = spell });

		Assert.That(s.GetCard(propagator).IsTapped, Is.False);
	}

	[Test]
	public void FloatingManaIsSpentBeforeAnyLandIsTapped()
	{
		var (s, me) = Board("Forest", "Forest");
		var lands = s.CardsIn(me, ZoneType.Battlefield).Select(c => c.Id).ToList();
		s = s.Act(
			new TapForManaAction
			{
				PlayerId = me,
				CardId = lands[0],
				Color = ManaColor.Green,
			}
		);
		int spell;
		(s, spell) = TestGames.Put(
			s,
			me,
			TestGames.CreatureCosting("{G}", "Bear", "2/2"),
			ZoneType.Hand
		);

		s = s.Act(new CastSpellAction { PlayerId = me, CardId = spell });

		Assert.That(s.GetPlayer(me).ManaPool, Is.EqualTo(ManaPool.Empty));
		Assert.That(s.GetCard(lands[1]).IsTapped, Is.False);
	}

	// Mana value counts {2/W} as 2, but one W pays it — an X bound built on mana value lost X=2 here.
	[Test]
	public void XSpell_WithTwoBrid_OffersTheXThatOneWPays()
	{
		var (s, me) = Board("Plains", "Mountain", "Mountain");
		int spell;
		(s, spell) = TestGames.Put(
			s,
			me,
			TestGames.Inline("Flare", "{X}{2/W}", "Sorcery"),
			ZoneType.Hand
		);

		var xs = s.LegalActions()
			.OfType<CastSpellAction>()
			.Where(a => a.CardId == spell)
			.Select(a => a.X);

		Assert.That(xs.Max(), Is.EqualTo(2));
	}

	[Test]
	public void XSpell_OffersEveryAffordableX()
	{
		var (s, me) = Board("Mountain", "Mountain", "Mountain", "Mountain");
		int spell;
		(s, spell) = TestGames.Put(
			s,
			me,
			TestGames.Inline("Blaze", "{X}{R}", "Sorcery"),
			ZoneType.Hand
		);

		var xs = s.LegalActions()
			.OfType<CastSpellAction>()
			.Where(a => a.CardId == spell)
			.Select(a => a.X);

		Assert.That(xs, Is.EqualTo(new[] { 0, 1, 2, 3 }));
	}
}
