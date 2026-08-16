using System;
using System.Collections.Generic;
using System.Linq;
using MtgCore;

namespace MtgGame;

/// <summary>
/// Per-card colouring. The engine has no colour, faction or rarity field — Hollowmere
/// deliberately replaced colours with themes — so the only categorical signals available are
/// component presence (card type) and <see cref="Card.Subtypes"/> (tribe). Those drive a frame
/// tint and a name-plate tint respectively, which is what keeps a Zombie from looking exactly
/// like a Spirit at battlefield size.
///
/// Values are multiplied against the frame art via SelfModulate, so they must stay light —
/// a dark colour multiplies the frame into mud.
/// </summary>
public static class MtgCardTheme
{
	/// <summary>
	/// Strings that live in <see cref="Card.Subtypes"/> but name a card type rather than a tribe.
	/// Filtered out of the type line's tribe list, since the type line already states the type.
	/// </summary>
	private static readonly string[] Supertypes = { "Land", "Basic", "Artifact", "Enchantment" };

	/// <summary>
	/// Tribe display and colour order. Cards are commonly multi-tribe (Chapel Longbowman is
	/// Human + Soldier), so this order has to be explicit rather than incidental: the race wins
	/// over the class, both for which colour the name plate takes and for how the type line reads.
	/// </summary>
	private static readonly string[] TribePriority =
	{
		"Zombie",
		"Vampire",
		"Spirit",
		"Werewolf",
		"Angel",
		"Demon",
		"Horror",
		"Insect",
		"Human",
		"Wizard",
		"Cleric",
		"Soldier",
		"Rogue",
	};

	private static readonly Dictionary<string, Color> TribeColors =
		new(StringComparer.OrdinalIgnoreCase)
		{
			["Zombie"] = new Color(0.62f, 0.80f, 0.48f),
			["Vampire"] = new Color(0.85f, 0.38f, 0.44f),
			["Spirit"] = new Color(0.68f, 0.86f, 0.95f),
			["Werewolf"] = new Color(0.78f, 0.60f, 0.38f),
			["Angel"] = new Color(1.00f, 0.93f, 0.62f),
			["Demon"] = new Color(0.72f, 0.30f, 0.28f),
			["Horror"] = new Color(0.70f, 0.55f, 0.88f),
			["Insect"] = new Color(0.74f, 0.78f, 0.42f),
			["Human"] = new Color(0.95f, 0.86f, 0.70f),
			["Wizard"] = new Color(0.62f, 0.72f, 0.95f),
			["Cleric"] = new Color(0.96f, 0.96f, 0.90f),
			["Soldier"] = new Color(0.78f, 0.80f, 0.84f),
			["Rogue"] = new Color(0.60f, 0.62f, 0.68f),
		};

	private static readonly Color CreatureFrame = new(1.00f, 0.94f, 0.82f);
	private static readonly Color SpellFrame = new(0.78f, 0.86f, 1.00f);
	private static readonly Color LandFrame = new(0.86f, 0.76f, 0.60f);
	private static readonly Color ArtifactFrame = new(0.84f, 0.88f, 0.92f);

	/// <summary>Frame tint by card type. Every card matches exactly one branch.</summary>
	public static Color FrameColor(Card card)
	{
		if (card.HasSubtype("Land"))
			return LandFrame;
		if (card.HasSubtype("Artifact"))
			return ArtifactFrame;
		if (card.HasComponent<CreatureComponent>())
			return CreatureFrame;
		return SpellFrame;
	}

	/// <summary>
	/// Name-plate tint by tribe. White for anything without a recognised tribe — including every
	/// spell, since <c>SpellCardBuilder</c> never sets subtypes.
	/// </summary>
	public static Color NamePlateColor(Card card)
	{
		var tribe = OrderedTribes(card).FirstOrDefault();
		return tribe != null && TribeColors.TryGetValue(tribe, out var color)
			? color
			: Colors.White;
	}

	/// <summary>
	/// The card's tribes, most distinctive first. Unrecognised subtypes sort last alphabetically
	/// so a set that adds a tribe without updating this list still renders it.
	/// </summary>
	public static IEnumerable<string> OrderedTribes(Card card) =>
		card
			.Subtypes.Where(s => !Supertypes.Contains(s, StringComparer.OrdinalIgnoreCase))
			.OrderBy(s => Rank(s))
			.ThenBy(s => s, StringComparer.OrdinalIgnoreCase);

	private static int Rank(string subtype)
	{
		var index = Array.FindIndex(
			TribePriority,
			t => string.Equals(t, subtype, StringComparison.OrdinalIgnoreCase)
		);
		return index < 0 ? TribePriority.Length : index;
	}
}
