using System;
using System.Collections.Generic;
using System.Linq;
using MtgCore;

namespace MtgGame;

/// <summary>
/// Per-card colouring, on two independent channels: the FRAME carries the card's colour
/// (<see cref="Card.ColorPips"/>) and the NAME PLATE carries its tribe.
///
/// Frame-by-colour is how a Magic player actually reads a card, and it is the signal that decides
/// whether a card is castable in their deck — which since colours became a real constraint is the
/// first question a drafter asks of a pack. It replaced frame-by-card-TYPE, which was what this
/// file could do back when the engine had no colour field at all: type is already stated in words
/// on the type line, so spending the strongest visual channel on it was spending it twice.
///
/// Tribe keeps the name plate because it is the second question ("does it go in my deck?") and
/// the two never compete for the same pixels.
///
/// **Black cannot be black.** Values are multiplied against the frame art via SelfModulate, so
/// every one of them must stay light — a dark colour multiplies the frame into mud. Black is a
/// desaturated violet-grey rather than the colour it names, which is the same compromise every
/// real Magic frame makes for exactly the same reason.
/// </summary>
public static class MtgCardTheme
{
	/// <summary>
	/// Strings that live in <see cref="Card.Subtypes"/> but name a card type rather than a tribe.
	/// Filtered out of the type line's tribe list, since the type line already states the type.
	/// </summary>
	private static readonly string[] Supertypes =
	{
		"Land",
		"Basic",
		"Artifact",
		"Enchantment",
		// A card type that rides in Subtypes, like the others here. Without it every planeswalker
		// reads "Planeswalker — Planeswalker".
		"Planeswalker",
		"Equipment",
	};

	/// <summary>
	/// Tribe display and colour order. Cards are commonly multi-tribe (Chapel Longbowman is
	/// Human + Soldier), so this order has to be explicit rather than incidental: the race wins
	/// over the class, both for which colour the name plate takes and for how the type line reads.
	/// </summary>
	private static readonly string[] TribePriority =
	{
		// Races first, ordered by how much they identify a deck in the live sets. This list was
		// written for Hollowmere and was almost entirely its tribes; those cards are retired, so
		// the order now follows what CSC, CMB and Legacy actually print — Goblin and Elf are the
		// two real tribal decks in this pool and Human is the most common subtype by a distance.
		"Goblin",
		"Elf",
		"Dragon",
		"Angel",
		"Demon",
		"Zombie",
		"Vampire",
		"Spirit",
		"Horror",
		"Elemental",
		"Werewolf",
		"Cat",
		"Merfolk",
		"Giant",
		"Beast",
		"Insect",
		"Human",
		// Classes last: a Goblin Wizard is read as a Goblin.
		"Wizard",
		"Shaman",
		"Druid",
		"Knight",
		"Cleric",
		"Soldier",
		"Warrior",
		"Berserker",
		"Rogue",
		"Scout",
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
			["Goblin"] = new Color(0.90f, 0.58f, 0.42f),
			["Elf"] = new Color(0.66f, 0.86f, 0.62f),
			["Dragon"] = new Color(0.88f, 0.52f, 0.40f),
			["Elemental"] = new Color(0.74f, 0.84f, 0.86f),
			["Cat"] = new Color(0.92f, 0.80f, 0.52f),
			["Merfolk"] = new Color(0.60f, 0.84f, 0.86f),
			["Giant"] = new Color(0.76f, 0.72f, 0.62f),
			["Beast"] = new Color(0.80f, 0.74f, 0.52f),
			["Shaman"] = new Color(0.78f, 0.70f, 0.90f),
			["Druid"] = new Color(0.70f, 0.84f, 0.66f),
			["Knight"] = new Color(0.86f, 0.86f, 0.92f),
			["Warrior"] = new Color(0.88f, 0.74f, 0.66f),
			["Berserker"] = new Color(0.90f, 0.66f, 0.58f),
			["Scout"] = new Color(0.74f, 0.82f, 0.70f),
		};

	/// <summary>
	/// Frame tint by COLOUR. Light throughout — see the type remarks on SelfModulate.
	///
	/// Black is a violet-grey rather than black, and white is a cream rather than white, for the
	/// same reason: one would multiply the frame art into mud and the other would not read as a
	/// tint at all.
	/// </summary>
	private static readonly Color WhiteFrame = new(0.99f, 0.97f, 0.88f);
	private static readonly Color BlueFrame = new(0.66f, 0.81f, 0.96f);
	private static readonly Color BlackFrame = new(0.70f, 0.68f, 0.75f);
	private static readonly Color RedFrame = new(0.97f, 0.71f, 0.63f);
	private static readonly Color GreenFrame = new(0.71f, 0.88f, 0.70f);

	/// Two or more colours. One gold frame rather than a blend: a blend of blue and red is a
	/// muddy purple that reads as a third colour rather than as "this needs both".
	private static readonly Color GoldFrame = new(0.96f, 0.87f, 0.58f);

	private static readonly Color LandFrame = new(0.86f, 0.76f, 0.60f);
	private static readonly Color ColorlessFrame = new(0.84f, 0.88f, 0.92f);

	/// <summary>
	/// Frame tint by the card's colour. Every card matches exactly one branch.
	///
	/// Order matters. Lands are checked FIRST because a land's colour is what it produces, not
	/// what it costs — it has no pips at all, and letting it fall through to colourless would put
	/// every land in the artifact frame. Pips are then checked BEFORE the artifact subtype, so a
	/// coloured artifact (Ancestral Blade is {1}{W}) takes its colour rather than grey, which is
	/// what the real card does too.
	/// </summary>
	public static Color FrameColor(Card card)
	{
		if (card.HasSubtype("Land"))
			return LandFrame;

		var pips = card.ColorPips;
		if (pips.IsEmpty)
			return ColorlessFrame;

		var colors = ManaPool.Colors.Where(c => pips[c] > 0).ToList();
		if (colors.Count > 1)
			return GoldFrame;

		return colors[0] switch
		{
			ManaColor.White => WhiteFrame,
			ManaColor.Blue => BlueFrame,
			ManaColor.Black => BlackFrame,
			ManaColor.Red => RedFrame,
			ManaColor.Green => GreenFrame,
			_ => ColorlessFrame,
		};
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
