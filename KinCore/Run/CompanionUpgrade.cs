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
/// **It is a choice at a screen rather than a trickle, and here is the evidence for that — not a
/// rule.** Companion MARKS did this job automatically, stamping +N/+N for every apocalypse
/// survived, and were cut on the playtest note *"I never liked this mechanic"*: a stat trickle
/// nobody chose. See <see cref="Companion"/>.
///
/// That is ONE playtest note about ONE implementation, and the game is still exploratory — see the
/// root `CLAUDE.md`. If a future pass wants growth that is not a pick, the thing to weigh is why
/// marks felt bad, not that a doc once said no.
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

	// Echo ("everything it does, it does twice") lived here as `EchoesAbility` and was cut on
	// 2026-09-22. Its one scar is worth keeping for anything that copies effects later: Ash's
	// ability is a SYMMETRIC PAIR — a buff at turn start and the same buff negated at turn end —
	// so a copy of only the first half makes him gain power every turn and never give it back,
	// with nothing reporting an error. Copy the whole list or none of it.
}
