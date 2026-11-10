using System.Collections.Generic;
using System.Linq;
using MagicCore.Cards;
using MagicCore.Sets;

namespace MagicTable;

/// <summary>How the deck view lays the deck out (LIM-5.4).</summary>
public enum DeckSort
{
	ManaValue,
	CreaturesApart,
	Colour,
}

/// <summary>One column of the deck view: its key ("3", "Lands", "W"), and its cards, copies counted.</summary>
public sealed record DeckColumn(string Key, IReadOnlyList<(string Card, int Copies)> Cards)
{
	public int Count => Cards.Sum(c => c.Copies);
}

/// <summary>
/// Arena's visual deck (LIM-5.3): the deck as rows of columns — by mana value (0–6, 7+, then Lands), creatures
/// and the rest as two rows each by mana value, or by colour. A card dragged to another column stays there
/// (<paramref name="moved"/>: a name → its column's key) until the sort is picked again. Plain C#, tested.
/// </summary>
public static class DeckColumns
{
	public const string Lands = "Lands";

	public static IReadOnlyList<IReadOnlyList<DeckColumn>> Rows(
		IEnumerable<string> deck,
		DeckSort sort,
		IReadOnlyDictionary<string, string>? moved = null
	)
	{
		var cards = deck.ToList();
		IEnumerable<IGrouping<bool, string>> rows =
			sort == DeckSort.CreaturesApart
				? cards.GroupBy(c => !Data(c).TypeLine.Contains("Creature")).OrderBy(g => g.Key) // creatures first
				: cards.GroupBy(_ => false);
		var order = Order(sort);
		return rows.Select(row =>
				(IReadOnlyList<DeckColumn>)
					row.GroupBy(c => moved?.GetValueOrDefault(c) ?? KeyOf(c, sort))
						.OrderBy(g => order.IndexOf(g.Key) is var i and >= 0 ? i : order.Count)
						.Select(g => new DeckColumn(
							g.Key,
							g.GroupBy(c => c)
								.OrderBy(c => Data(c.Key).Cmc)
								.ThenBy(c => c.Key)
								.Select(c => (c.Key, c.Count()))
								.ToList()
						))
						.ToList()
			)
			.ToList();
	}

	/// <summary>The column a card sorts into.</summary>
	public static string KeyOf(string card, DeckSort sort)
	{
		var data = Data(card);
		if (data.TypeLine.Contains("Land"))
			return Lands;
		if (sort != DeckSort.Colour)
			return data.Cmc >= 7 ? "7+" : ((int)data.Cmc).ToString();
		var colors = data.Colors ?? [];
		return colors.Length switch
		{
			0 => "Colourless",
			1 => colors[0],
			_ => "Multi",
		};
	}

	static List<string> Order(DeckSort sort) =>
		sort == DeckSort.Colour
			? ["W", "U", "B", "R", "G", "Multi", "Colourless", Lands]
			: ["0", "1", "2", "3", "4", "5", "6", "7+", Lands];

	static CardData Data(string card) => CardRegistry.Get(card).Data;
}
