using System.Collections.Immutable;

namespace KinCore;

/// <summary>
/// One thing you may make your companion into, offered three at a time between floors.
///
/// **THIS IS THE RUN'S POWER CURVE**, and it exists because deleting the dooms deleted the old one.
/// `KinJam.md` said it outright — "the apocalypses ARE the power curve, there is no separate
/// progression system" — and the measurement after they went was 0.0% act completion. This is the
/// separate progression system, arriving late and on purpose.
///
/// **IT IS A CHOICE AT A SCREEN, NEVER A TRICKLE, and that is not a style preference.** Companion
/// MARKS did exactly this job by stamping an automatic +N/+N for every apocalypse survived, and
/// they were cut on the playtest note *"I never liked this mechanic"* — a stat trickle nobody chose,
/// attached to a name that grew until it left the screen. See <see cref="Companion"/>. Anything
/// added here must be picked over two rejected alternatives, or it is the same mistake again.
///
/// **Pure data, no delegates** — see the serialization rule in CLAUDE.md. An upgrade is stat deltas
/// plus effects to append, and <see cref="Companion.With"/> is the whole of applying one.
/// </summary>
public record CompanionUpgrade
{
	public string Name { get; init; } = "";

	/// <summary>What it does, in the player's words. Shown on the offer.</summary>
	public string Text { get; init; } = "";

	public KinRarity Rarity { get; init; } = KinRarity.Common;

	public int Power { get; init; }
	public int Toughness { get; init; }

	/// <summary>Effects appended to the companion's own. Empty for a pure stat upgrade.</summary>
	public ImmutableList<KinEffect> Effects { get; init; } = ImmutableList<KinEffect>.Empty;

	/// <summary>
	/// Append a second copy of **every** effect the companion already has.
	///
	/// **Every, not the first — and the difference is a silent bug.** Ash's ability is a SYMMETRIC
	/// PAIR: a buff on turn start and the same buff negated on turn end, which is the entire
	/// duration system (see <see cref="StarterContent.StarterCompanion"/>). Duplicating only the
	/// first effect would double the buff and leave the unwind single, so Ash would gain power
	/// every turn and never give it back — a companion that quietly runs away with the game, with
	/// nothing anywhere reporting an error.
	///
	/// Copying the whole list keeps any such pair balanced, whatever a future companion declares.
	/// </summary>
	public bool EchoesAbility { get; init; }
}
