using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace DoomCore;

/// <summary>What a term on a card or the board means, in one sentence.</summary>
public sealed record Keyword
{
	public string Name { get; init; } = "";

	/// <summary>The reminder text. One sentence, present tense, no rules lawyering.</summary>
	public string Text { get; init; } = "";

	/// <summary>
	/// Other spellings that mean this keyword. Card text is authored English —
	/// `"when the doom fires: 6 to every enemy"` — rather than tagged, so a keyword is recognised by
	/// the phrases that actually appear on cards.
	/// </summary>
	public ImmutableArray<string> Aliases { get; init; } = [];
}

/// <summary>
/// The terms this game expects a player to know, and what each one means.
///
/// **In DoomCore, not in the front end.** The console and the board must explain a word the same
/// way; two glossaries is how a game ends up telling a player two different things about Irradiated.
/// This is also why it is not simply a dictionary in `DoomBoard`.
///
/// **Data only — no delegates, ever.** The serialization rule in the root `CLAUDE.md` applies: these
/// are strings and nothing else, so a keyword can be serialised, printed, or shipped to a UI without
/// carrying behaviour with it. A keyword never DOES anything; the effect it describes is implemented
/// where every other effect is.
///
/// Reminder text is not rules text. It exists so a player does not have to leave the board to find
/// out what a word means — see DoomUI.md, "Hover and explanation".
/// </summary>
public static class KeywordLibrary
{
	public static readonly ImmutableArray<Keyword> All =
	[
		new Keyword
		{
			Name = "Irradiated",
			Text = "Drawing this card costs 1 life, for the rest of the run.",
			Aliases = ["irradiated"],
		},
		new Keyword
		{
			Name = "Companion",
			Text = "Free, and starts in the centre lane every battle. No doom can touch it.",
			Aliases = ["companion"],
		},
		new Keyword
		{
			Name = "Rite",
			Text = "Resolves when you play it. It never holds a lane.",
			Aliases = ["rite"],
		},
		new Keyword
		{
			Name = "Doom",
			Text =
				"The apocalypse on the clock above. It fires at zero, then reloads and fires "
				+ "again.",
			Aliases = ["doom", "when the doom fires", "the doom"],
		},
		new Keyword
		{
			Name = "Exhaust",
			Text = "Played once, then out of this battle. You get it back in the next fight.",
			Aliases = ["exhaust", "exhausts", "exhausted"],
		},
		new Keyword
		{
			Name = "Loss",
			Text =
				"A unit of yours the enemy KILLED last turn. A unit that left the board at the end "
				+ "of the turn is not a Loss.",
			Aliases = ["loss", "per loss", "losses"],
		},
		new Keyword
		{
			Name = "Adjacent",
			Text = "The lanes immediately either side. An edge lane has only one.",
			Aliases = ["adjacent", "either side"],
		},
		new Keyword
		{
			Name = "Power",
			Text =
				"The number beside the sword: what this body deals each turn. An open lane "
				+ "sends it straight at the Opponent.",
			Aliases = ["power"],
		},
		new Keyword
		{
			Name = "Toughness",
			Text =
				"The number in the red disc: damage it absorbs before dying. Damage beyond it "
				+ "hits the face behind.",
			Aliases = ["toughness"],
		},
		new Keyword
		{
			Name = "Intent",
			Text =
				"What an enemy will do on its turn, shown a turn ahead. A greyed sword "
				+ "means it is waiting.",
			Aliases = ["intent"],
		},
		new Keyword
		{
			Name = "Incoming",
			Text = "The Opponent has announced a body for this lane next turn.",
			Aliases = ["incoming", "telegraph"],
		},
		new Keyword
		{
			Name = "Energy",
			Text =
				"The gold pips. Spent to play cards, refilled in full every turn, never "
				+ "carried over.",
			Aliases = ["energy"],
		},
	];

	private static readonly ImmutableDictionary<string, Keyword> ByName = All.ToImmutableDictionary(
		k => k.Name,
		StringComparer.OrdinalIgnoreCase
	);

	public static bool TryFind(string name, out Keyword keyword)
	{
		keyword = null!;
		return name is not null && ByName.TryGetValue(name, out keyword!);
	}

	/// <summary>
	/// Every keyword mentioned in a piece of text, in the order this library declares them.
	///
	/// **Whole words only.** A naive substring match reports Power for "Powerful" and, worse, Rite
	/// for "favourite" — reminder text that appears for no reason is worse than none, because the
	/// player learns to stop reading the panel.
	/// </summary>
	public static ImmutableArray<Keyword> In(params string[] texts)
	{
		var haystack = string.Join(" ", texts.Where(t => !string.IsNullOrWhiteSpace(t)));
		if (haystack.Length == 0)
			return [];

		var found = new List<Keyword>();

		foreach (var keyword in All)
			if (keyword.Aliases.Any(alias => ContainsWord(haystack, alias)))
				found.Add(keyword);

		return [.. found];
	}

	private static bool ContainsWord(string haystack, string needle)
	{
		var at = haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase);

		while (at >= 0)
		{
			var beforeOk = at == 0 || !char.IsLetter(haystack[at - 1]);
			var after = at + needle.Length;
			var afterOk = after >= haystack.Length || !char.IsLetter(haystack[after]);

			if (beforeOk && afterOk)
				return true;

			at = haystack.IndexOf(needle, at + 1, StringComparison.OrdinalIgnoreCase);
		}

		return false;
	}
}
