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
	/// **Thorns, Strikes, Breakthrough and Flier are fields, not effects, so they hit the same
	/// trap Devour did** — every surface built its text out of `Effects`, and a card whose whole
	/// rule is a field would have rendered blank. They are stated here, once, ahead of the effect
	/// lines.
	/// </summary>
	public static IEnumerable<string> Lines(
		ImmutableList<KinEffect> effects,
		bool devours = false,
		int thorns = 0,
		int strikes = 1,
		bool breakthrough = false,
		bool flies = false
	)
	{
		foreach (var trait in Traits(devours, thorns, strikes, breakthrough, flies))
			yield return trait;

		foreach (var effect in effects)
		{
			if (!string.IsNullOrEmpty(effect.Text))
				yield return effect.Text;
		}
	}

	/// <summary>
	/// **Only the keyword fields, without the effect text** — what a body in a LANE shows.
	///
	/// A lane has room for a word, not a sentence, and these are the facts that decide which unit
	/// to put against which enemy: a Flier goes over your wall, Thorns bleeds your striker. KinUI.md
	/// is explicit that a fact needed to choose a lane is on the board, never only on hover.
	/// </summary>
	public static IEnumerable<string> Traits(
		bool devours = false,
		int thorns = 0,
		int strikes = 1,
		bool breakthrough = false,
		bool flies = false
	)
	{
		if (devours)
			yield return "Devour";

		if (flies)
			yield return "Flier";

		if (thorns > 0)
			yield return $"Thorns {thorns}";

		// "Strikes 1" is what every ordinary unit does and saying so would be noise on every card.
		if (strikes > 1)
			yield return strikes == 2 ? "Strikes twice" : $"Strikes {strikes} times";

		if (breakthrough)
			yield return "Breakthrough";
	}

	/// <summary>A unit in a lane. Devour is left out: it happened when the card was played.</summary>
	public static IEnumerable<string> Traits(KinCard card) =>
		card.GetComponent<UnitComponent>() is { } unit
			? Traits(false, unit.Thorns, unit.Strikes, unit.Breakthrough)
			: [];

	public static IEnumerable<string> Traits(Enemy enemy) =>
		Traits(thorns: enemy.Thorns, strikes: enemy.Strikes, flies: enemy.Flies);

	/// <summary>
	/// A battle card keeps its stats on <see cref="UnitComponent"/>, and a RITE has no component at
	/// all — so the numbers are read off it defensively rather than assumed.
	/// </summary>
	public static IEnumerable<string> Lines(KinCard card)
	{
		var unit = card.GetComponent<UnitComponent>();
		return Lines(
			card.Effects,
			card.Devours,
			unit?.Thorns ?? 0,
			unit?.Strikes ?? 1,
			unit?.Breakthrough ?? false
		);
	}

	public static IEnumerable<string> Lines(RunCard card) =>
		Lines(card.Effects, card.Devours, card.Thorns, card.Strikes, card.Breakthrough);

	/// <summary>
	/// **What an enemy does, in the same words a card would use.** Enemies had no text surface at
	/// all — the lane showed name, intent and health — so a counter to a behaviour the player
	/// cannot see was no counter. Shared by the board and the console so they cannot disagree.
	/// </summary>
	public static IEnumerable<string> Lines(Enemy enemy) =>
		Lines(enemy.Effects, thorns: enemy.Thorns, strikes: enemy.Strikes, flies: enemy.Flies);

	public static IEnumerable<string> Lines(EnemyDefinition enemy) =>
		Lines(enemy.Effects, thorns: enemy.Thorns, strikes: enemy.Strikes, flies: enemy.Flies);
}
