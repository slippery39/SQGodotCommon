using System.Collections.Immutable;

namespace DoomCore;

/// <summary>
/// What a card SAYS, assembled in one place.
///
/// **Built because a keyword that is a flag rather than an effect would otherwise be invisible.**
/// Every rules-text renderer in the game read `Effects.Select(e => e.Text)` — so Devour, which is a
/// bool on the card and not an effect, would have rendered on no surface at all: a card that
/// silently eats your unit and never says so. `DoomCardFace` and `DoomConsole` each had their own
/// copy of that one line, which is precisely how the two would come to disagree.
///
/// Lines rather than a finished string, because the two surfaces join them differently — a card
/// face stacks them, the console dump runs them together on one row.
/// </summary>
public static class DoomRulesText
{
	public static IEnumerable<string> Lines(bool devours, ImmutableList<DoomEffect> effects)
	{
		if (devours)
			yield return "Devour";

		foreach (var effect in effects)
		{
			if (!string.IsNullOrEmpty(effect.Text))
				yield return effect.Text;
		}
	}

	public static IEnumerable<string> Lines(DoomCard card) => Lines(card.Devours, card.Effects);

	public static IEnumerable<string> Lines(RunCard card) => Lines(card.Devours, card.Effects);
}
