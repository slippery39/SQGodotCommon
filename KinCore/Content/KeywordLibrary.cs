using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace KinCore;

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
/// **In KinCore, not in the front end.** The console and the board must explain a word the same
/// way; two glossaries is how a game ends up telling a player two different things about a rule.
/// This is also why it is not simply a dictionary in `KinBoard`.
///
/// **Data only — no delegates, ever.** The serialization rule in the root `CLAUDE.md` applies: these
/// are strings and nothing else, so a keyword can be serialised, printed, or shipped to a UI without
/// carrying behaviour with it. A keyword never DOES anything; the effect it describes is implemented
/// where every other effect is.
///
/// Reminder text is not rules text. It exists so a player does not have to leave the board to find
/// out what a word means — see KinUI.md, "Hover and explanation".
/// </summary>
public static class KeywordLibrary
{
	public static readonly ImmutableArray<Keyword> All =
	[
		new Keyword
		{
			Name = "Companion",
			Text =
				"Free, and starts in the centre lane every battle. Nothing can take it from you.",
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
			Name = "Exhaust",
			Text = "Played once, then out of this battle. You get it back in the next fight.",
			Aliases = ["exhaust", "exhausts", "exhausted"],
		},
		new Keyword
		{
			Name = "Loss",
			Text =
				"A unit of yours that DIED — killed, or sacrificed. Leaving at end of turn is "
				+ "not a Loss.",
			Aliases = ["loss", "per loss", "losses"],
		},
		new Keyword
		{
			Name = "Taken",
			Text =
				"Out of every deck — not drawable, not shuffled back. It returns to your discard "
				+ "when the turns are up.",
			Aliases = ["taken", "takes", "is taken"],
		},
		new Keyword
		{
			Name = "Sacrifice",
			Text =
				"Your own unit dies on the spot. It is a Loss, and everything that reads Losses "
				+ "pays.",
			Aliases = ["sacrifice", "sacrificed"],
		},
		new Keyword
		{
			Name = "Devour",
			Text = "The unit this replaces dies instead of leaving, so it counts as a Loss.",
			Aliases = ["devour", "devours"],
		},
		new Keyword
		{
			Name = "Thorns",
			Text =
				"When this is attacked, the attacker takes this much. It adds to the attack, so "
				+ "the excess reaches you.",
			Aliases = ["thorns"],
		},
		new Keyword
		{
			Name = "Strikes",
			Text =
				"This deals its damage that many times. Each hit is separate, so Thorns answers "
				+ "every one.",
			Aliases = ["strikes twice", "strikes", "strike"],
		},
		new Keyword
		{
			Name = "Breakthrough",
			Text = "Power past the enemy's health carries on to the Opponent.",
			Aliases = ["breakthrough"],
		},
		new Keyword
		{
			Name = "Flier",
			Text = "Its attack goes over the unit in its lane and lands on you.",
			Aliases = ["flier", "flies", "flying"],
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
