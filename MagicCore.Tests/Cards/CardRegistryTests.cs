using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Limited;
using MagicCore.Sets;

namespace MagicCore.Tests.Cards;

/// <summary>
/// One registry across every set (MC-5.5): a card's script written once, its rules text the same in
/// every set that prints it, its printing — rarity, collector number, art — per set.
/// </summary>
public class CardRegistryTests
{
	static CardData Card(string name, string text) =>
		new(name, "normal", "{1}", 1, "Sorcery", text, null, null, null, [], [], null);

	static CardPrinting Printing(string set, CardData card) => new(set, card, "common", "1");

	static readonly ImmutableArray<GameComponent> Script = [new SpellEffectsComponent([])];

	[Test]
	public void ANameScriptedInTwoSets_IsRefused()
	{
		var reprint = Card("Reprint", "Draw a card.");
		var printed = new Dictionary<string, IReadOnlyList<CardPrinting>>
		{
			["AAA"] = [Printing("AAA", reprint)],
			["BBB"] = [Printing("BBB", reprint)],
		};

		Assert.That(
			() =>
				CardRegistry.Merge(
					printed,
					(
						"AAA",
						new Dictionary<string, ImmutableArray<GameComponent>>
						{
							["Reprint"] = Script,
						}
					),
					(
						"BBB",
						new Dictionary<string, ImmutableArray<GameComponent>>
						{
							["Reprint"] = Script,
						}
					)
				),
			Throws.InvalidOperationException.With.Message.Contains("scripted in both AAA and BBB")
		);
	}

	[Test]
	public void AScriptForACardItsSetDoesntPrint_IsRefused()
	{
		var printed = new Dictionary<string, IReadOnlyList<CardPrinting>>
		{
			["AAA"] = [Printing("AAA", Card("Real", "Draw a card."))],
		};

		Assert.That(
			() =>
				CardRegistry.Merge(
					printed,
					(
						"AAA",
						new Dictionary<string, ImmutableArray<GameComponent>> { ["Typo"] = Script }
					)
				),
			Throws.InvalidOperationException.With.Message.Contains("doesn't print it")
		);
	}

	[Test]
	public void AReprintWhoseRulesTextDiffers_IsRefused()
	{
		Assert.That(
			() =>
				CardRegistry.SameInEverySet(
					[
						Printing("AAA", Card("Reprint", "Draw a card.")),
						Printing("BBB", Card("Reprint", "Draw two cards.")),
					]
				),
			Throws.InvalidOperationException.With.Message.Contains(
				"reads differently in AAA and BBB"
			)
		);
	}

	[Test]
	public void AReprintThatReadsTheSame_IsOneCard_WithAPrintingPerSet()
	{
		var data = CardRegistry.SameInEverySet(
			[
				Printing("AAA", Card("Reprint", "Draw a card.")),
				new("BBB", Card("Reprint", "Draw a card."), "uncommon", "99"),
			]
		);

		Assert.That(data.Keys, Is.EqualTo(new[] { "Reprint" }));
	}

	// The real sets: the basics are in FRA and TRK — one card, two printings.
	[Test]
	public void ABasicInTwoSets_ShowsThePrintingItWasAskedFor()
	{
		Assert.That(CardRegistry.Get("Forest").Set, Is.EqualTo("FRA"), "the first set by default");
		Assert.That(CardRegistry.Get("Forest", "TRK").Set, Is.EqualTo("TRK"));
		Assert.That(
			CardRegistry.Get("Forest", "TRK").Data,
			Is.SameAs(CardRegistry.Get("Forest", "FRA").Data),
			"one rules text"
		);
		Assert.That(
			CardRegistry.Printings("TRK").Single(p => p.Card.Name == "Forest").CollectorNumber,
			Is.Not.EqualTo(
				CardRegistry.Printings("FRA").Single(p => p.Card.Name == "Forest").CollectorNumber
			)
		);
		Assert.That(() => CardRegistry.Get("Mugato", "FRA"), Throws.TypeOf<KeyNotFoundException>());
	}

	// A card keeps the printing it was made from across zone changes (Fresh resets status, not printing).
	[Test]
	public void ACard_CarriesItsPrintingsSet_ThroughAZoneChange()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		int forest;
		(s, forest) = TestGames.Put(s, me, CardRegistry.Get("Forest", "TRK"), ZoneType.Hand);

		s = s.MoveCard(forest, s.ZoneId(me, ZoneType.Graveyard));

		Assert.That(s.GetCard(forest).Set, Is.EqualTo("TRK"));
	}

	[Test]
	public void ASetWithNoBoostersOrRatings_SaysWhichImportToRun()
	{
		Assert.That(
			() => Boosters.Pack("TRK", new Random(1)),
			Throws.InvalidOperationException.With.Message.Contains("import_mtgjson_boosters.py trk")
		);
		Assert.That(
			() => CardRatings.Of("TRK"),
			Throws.InvalidOperationException.With.Message.Contains("import_17lands.py trk")
		);
	}
}
