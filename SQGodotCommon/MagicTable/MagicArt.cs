using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ImmutableGameObjects;
using MagicCore;
using MagicCore.Cards;
using MagicCore.Effects;

namespace MagicTable;

/// <summary>
/// Scryfall images by card name (tools/fetch_set_art.py): the art crop for a permanent on the
/// battlefield, the full card for the hand and the preview — its printed Oracle text is exact.
/// </summary>
public static class MagicArt
{
	/// <summary>
	/// The name whose image shows <paramref name="c"/>. A prepared copy is named after its spell face,
	/// and faces are shared ("Seed Suture" by three cards) — so it shows the creature that prepared
	/// it, through its link. By the face name, 4 of 6 such copies showed another creature (Shayne's
	/// sealed game, 2026-10-23).
	/// </summary>
	public static string ImageName(GameState s, Card c) =>
		c.GetComponent<PreparedCopyOf>() is { } link && s.HasObject(link.CreatureId)
			? s.GetCard(link.CreatureId).Name
			: c.Name;

	// A copy with no link (a copy of a prepared copy: CopySpell drops it) is still named after its
	// face, and the file is a whole card's — the first card with that face, the best a name can do.
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
		// An effect in the command zone, "Theorist's Proxy (this turn)", shows the card that made it.
		name = Regex.Replace(name, @" \((this turn|until your next turn)\)$", "");
		var slug = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
		var path = $"res://MagicTable/Assets/{folder}/{slug}.jpg";
		return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
	}
}
