using System;
using System.Collections.Generic;
using System.Linq;
using MtgCore;

namespace MtgGame;

/// <summary>
/// Per-card colouring: the FRAME carries the card's colour (<see cref="Card.ColorPips"/>), and
/// the tribe is read from the type line (<see cref="OrderedTribes"/>).
///
/// Frame-by-colour is how a Magic player actually reads a card, and it is the signal that decides
/// whether a card is castable in their deck — which since colours became a real constraint is the
/// first question a drafter asks of a pack. It replaced frame-by-card-TYPE, which was what this
/// file could do back when the engine had no colour field at all: type is already stated in words
/// on the type line, so spending the strongest visual channel on it was spending it twice.
///
/// The name plate USED to carry the tribe, but the shader dropped every tint, so nobody ever saw
/// it. Once tints rendered, a green plate on a black card read as a second colour claim; plates
/// are plain cream now (2026-10-06).
///
/// Values are multiplied against the frame art via SelfModulate. MTG supplies a near-white frame
/// (<see cref="MtgCardSkin"/>), so each value IS the colour the player sees.
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

	/// <summary>
	/// Frame colour by COLOUR — the Dark Arcane palette, sampled from the mockups
	/// (MtgVisualDesign.md §6). MTG's frame texture is near-white (<see cref="MtgCardSkin"/>), so
	/// the multiply lands on these values as they are: black can be a real dark grey now. On the
	/// old dark brown frame every one of these came out the same brown.
	/// </summary>
	private static readonly Color WhiteFrame = new("d8c296");
	private static readonly Color BlueFrame = new("4998c8");
	private static readonly Color BlackFrame = new("3c3735");
	private static readonly Color RedFrame = new("b8452f");
	private static readonly Color GreenFrame = new("4f8a5c");

	/// Two or more colours. One gold frame rather than a blend: a blend of blue and red is a
	/// muddy purple that reads as a third colour rather than as "this needs both".
	private static readonly Color GoldFrame = new("c9a24a");

	private static readonly Color LandFrame = new("8a6a48");
	private static readonly Color ColorlessFrame = new("9aa2aa");

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
