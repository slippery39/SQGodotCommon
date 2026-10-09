using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MagicCore.Cards;

namespace MagicTable;

/// <summary>
/// Scryfall images by card name (tools/fetch_set_art.py): the art crop for a permanent on the
/// battlefield, the full card for the hand and the preview — its printed Oracle text is exact.
/// </summary>
public static class MagicArt
{
	// A prepared copy on the stack is named after its spell face; the file is the whole card's.
	// ponytail: faces are shared ("Seed Suture" by three cards, "Omit Variables" by three), so a
	// prepared copy shows the FIRST card's image — 4 of 6 such copies show another creature. Resolve
	// through the copy's PreparedCopyOf link when the board shows the stack.
	private static readonly Dictionary<string, string> FaceToCard = CardDataSet
		.Load("FRA")
		.Where(c => c.CardFaces is not null)
		.SelectMany(c => c.CardFaces!.Select(f => (Face: f.Name, Card: c.Name)))
		.DistinctBy(x => x.Face)
		.ToDictionary(x => x.Face, x => x.Card);

	public static Texture2D? Crop(string cardName) => Load("Art", cardName);

	public static Texture2D? Full(string cardName) => Load("Cards", cardName);

	private static Texture2D? Load(string folder, string cardName)
	{
		var name = FaceToCard.GetValueOrDefault(cardName, cardName);
		// MagicCore names an emblem "Emblem — Ajani Resolute"; Scryfall's is "Ajani Resolute Emblem".
		if (name.StartsWith("Emblem — "))
			name = $"{name["Emblem — ".Length..]} Emblem";
		var slug = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
		var path = $"res://MagicTable/Assets/{folder}/{slug}.jpg";
		return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
	}
}
