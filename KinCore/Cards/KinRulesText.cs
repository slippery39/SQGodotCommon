using System.Collections.Immutable;

namespace KinCore;

/// <summary>
/// What a card SAYS, assembled in one place.
///
/// **Built because a keyword that is a flag rather than an effect would otherwise be invisible.**
/// Every rules-text renderer in the game read `Effects.Select(e => e.Text)` — so Devour, which is a
/// bool on the card and not an effect, would have rendered on no surface at all: a card that
/// silently eats your unit and never says so. `KinCardFace` and `KinConsole` each had their own
/// copy of that one line, which is precisely how the two would come to disagree.
///
/// Lines rather than a finished string, because the two surfaces join them differently — a card
/// face stacks them, the console dump runs them together on one row.
/// </summary>
public static class KinRulesText
{
	/// <summary>
	/// **Thorns and Strikes are NUMBERS on the unit, not effects, so they hit the same trap Devour
	/// did** — every surface built its text out of `Effects`, and a card whose whole rule is a
	/// field would have rendered blank. They are stated here, once, ahead of the effect lines.
	/// </summary>
	public static IEnumerable<string> Lines(
		bool devours,
		int thorns,
		int strikes,
		ImmutableList<KinEffect> effects
	)
	{
		if (devours)
			yield return "Devour";

		if (thorns > 0)
			yield return $"Thorns {thorns}";

		// "Strikes 1" is what every ordinary unit does and saying so would be noise on every card.
		if (strikes > 1)
			yield return strikes == 2 ? "Strikes twice" : $"Strikes {strikes} times";

		foreach (var effect in effects)
		{
			if (!string.IsNullOrEmpty(effect.Text))
				yield return effect.Text;
		}
	}

	/// <summary>
	/// A battle card keeps its stats on <see cref="UnitComponent"/>, and a RITE has no component at
	/// all — so the numbers are read off it defensively rather than assumed.
	/// </summary>
	public static IEnumerable<string> Lines(KinCard card)
	{
		var unit = card.GetComponent<UnitComponent>();
		return Lines(card.Devours, unit?.Thorns ?? 0, unit?.Strikes ?? 1, card.Effects);
	}

	public static IEnumerable<string> Lines(RunCard card) =>
		Lines(card.Devours, card.Thorns, card.Strikes, card.Effects);
}
