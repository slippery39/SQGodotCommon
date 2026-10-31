using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ImmutableGameObjects;
using MagicCore;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;

namespace MagicTable;

/// <summary>
/// Scryfall images by card name (tools/fetch_set_art.py): the art crop for a permanent on the
/// battlefield, the full card for the hand and the preview — its printed Oracle text is exact. A
/// printing's own art is in <c>Art/&lt;set&gt;/</c> (a reprint: TRK's basics), looked for first; every
/// other card's is in the shared folder (MC-5.5).
/// </summary>
public static class MagicArt
{
	/// <summary>
	/// The image key (<see cref="Key(string, string?)"/>) that shows <paramref name="c"/>. A prepared copy is named after its spell face,
	/// and faces are shared ("Seed Suture" by three cards) — so it shows the creature that prepared
	/// it, through its link. By the face name, 4 of 6 such copies showed another creature (Shayne's
	/// sealed game, 2026-10-23).
	/// </summary>
	public static string ImageName(GameState s, Card c) =>
		c.GetComponent<PreparedCopyOf>() is { } link && s.HasObject(link.CreatureId)
			? Key(s.GetCard(link.CreatureId))
			: Key(c);

	/// <summary>
	/// The image key every lookup here takes: the name, and the printing's set when it has one
	/// ("Forest|TRK") — one string, so a hover or zoom carries it as it carried the name.
	/// </summary>
	public static string Key(string name, string? set) => set is null ? name : $"{name}|{set}";

	public static string Key(Card c) => Key(c.Name, c.Set);

	/// <summary>The card name in an image key.</summary>
	public static string NameOf(string key) => key.Split('|')[0];

	// A copy with no link (a copy of a prepared copy: CopySpell drops it) is still named after its
	// face, and the file is a whole card's — the first card with that face, the best a name can do.
	private static readonly Dictionary<string, string> FaceToCard = CardRegistry
		.AllNames.Select(n => CardRegistry.Get(n).Data)
		.Where(c => c.CardFaces is not null)
		.SelectMany(c => c.CardFaces!.Select(f => (Face: f.Name, Card: c.Name)))
		.DistinctBy(x => x.Face)
		.ToDictionary(x => x.Face, x => x.Card);

	public static Texture2D? Crop(string key) => Load("Art", key);

	public static Texture2D? Full(string key) => Load("Cards", key);

	private static Texture2D? Load(string folder, string key)
	{
		var cardName = NameOf(key);
		var set = key.Length > cardName.Length ? key[(cardName.Length + 1)..] : null;
		var name = FaceToCard.GetValueOrDefault(cardName, cardName);
		// MagicCore names an emblem "Emblem — Ajani Resolute"; Scryfall's is "Ajani Resolute Emblem".
		if (name.StartsWith("Emblem — "))
			name = $"{name["Emblem — ".Length..]} Emblem";
		// An effect in the command zone, "Theorist's Proxy (this turn)", shows the card that made it.
		name = Regex.Replace(name, @" \((this turn|until your next turn)\)$", "");
		var slug = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
		var own = $"res://MagicTable/Assets/{folder}/{set?.ToLowerInvariant()}/{slug}.jpg";
		var shared = $"res://MagicTable/Assets/{folder}/{slug}.jpg";
		var path = set is not null && ResourceLoader.Exists(own) ? own : shared;
		return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
	}
}
