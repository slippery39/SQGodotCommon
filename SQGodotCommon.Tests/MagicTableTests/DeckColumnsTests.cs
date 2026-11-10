using System.Collections.Generic;
using System.Linq;
using MagicCore.Cards;
using MagicCore.Sets;
using MagicTable;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

/// <summary>Arena's visual deck: the deck as columns (story LIM-5.3, 5.4, 5.5).</summary>
public class DeckColumnsTests
{
	// Real cards, picked by what they are; every number below is read from their data.
	static readonly CardData Creature = Pick(d =>
		d.TypeLine.Contains("Creature") && d.Cmc is >= 1 and < 7 && d.Colors?.Length == 1
	);
	static readonly CardData Instant = Pick(d =>
		d.TypeLine.Contains("Instant")
		&& d.Cmc >= 1
		&& d.Cmc != Creature.Cmc
		&& d.Colors?.Length == 1
	);

	static CardData Pick(System.Func<CardData, bool> what) =>
		CardDataSet.Load("FRA").Select(p => p.Card).First(d => what(d) && d.CardFaces is null);

	static readonly List<string> Deck =
	[
		Creature.Name,
		Creature.Name,
		Instant.Name,
		"Swamp",
		"Swamp",
		"Swamp",
	];

	[Test]
	public void ByManaValue_OneColumnPerCost_LandsLast_CopiesCounted()
	{
		var rows = DeckColumns.Rows(Deck, DeckSort.ManaValue);

		Assert.That(rows, Has.Count.EqualTo(1));
		var keys = new[] { ((int)Creature.Cmc).ToString(), ((int)Instant.Cmc).ToString() }
			.OrderBy(k => k)
			.Append(DeckColumns.Lands);
		Assert.That(rows[0].Select(c => c.Key), Is.EqualTo(keys));
		Assert.That(
			rows[0].Single(c => c.Key == ((int)Creature.Cmc).ToString()).Cards,
			Is.EqualTo(new[] { (Creature.Name, 2) })
		);
		Assert.That(rows[0][^1].Count, Is.EqualTo(3), "three Swamps, one entry");
	}

	[Test]
	public void CreaturesApart_IsTwoRows_CreaturesFirst()
	{
		var rows = DeckColumns.Rows(Deck, DeckSort.CreaturesApart);

		Assert.That(rows, Has.Count.EqualTo(2));
		Assert.That(
			rows[0].SelectMany(c => c.Cards).Select(c => c.Card),
			Is.EqualTo(new[] { Creature.Name })
		);
		Assert.That(
			rows[1].SelectMany(c => c.Cards).Select(c => c.Card),
			Does.Not.Contain(Creature.Name)
		);
	}

	[Test]
	public void ByColour_ReadsTheCardsColour()
	{
		var rows = DeckColumns.Rows(Deck, DeckSort.Colour);

		Assert.That(
			rows[0].Single(c => c.Cards.Any(x => x.Card == Instant.Name)).Key,
			Is.EqualTo(Instant.Colors![0])
		);
	}

	[Test]
	public void AMovedCard_StaysInTheColumnItWasDraggedTo()
	{
		var to = ((int)Creature.Cmc).ToString();

		var rows = DeckColumns.Rows(
			Deck,
			DeckSort.ManaValue,
			new Dictionary<string, string> { [Instant.Name] = to }
		);

		Assert.That(rows[0].Single(c => c.Key == to).Count, Is.EqualTo(3));
		Assert.That(rows[0].Select(c => c.Key), Does.Not.Contain(((int)Instant.Cmc).ToString()));
	}
}
