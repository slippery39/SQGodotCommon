using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore;

/// <summary>
/// TAG ALONG. The one thing on the board that is not a card and does not leave at end of turn.
///
/// Not a card and never in the deck, so no transform can zombify, irradiate or drown it. It is on
/// the field free at the start of every battle, which also fixes the real problem that a bad draw
/// used to leave you with nothing on the board at all.
///
/// **Its ABILITY is what it is for**, and it is the one thing in the game a deck can be built
/// around — see <see cref="Effects"/>. A companion's ability decides which cards in a reward screen
/// are good, which is the deckbuilding depth the pool could not supply on its own.
///
/// > **MARKS ARE GONE (2026-09-18), cut on playtest feedback: "I never liked this mechanic."**
/// >
/// > Every apocalypse survived used to stamp a permanent +N/+N on the companion — Gravemarked,
/// > Glowing, Barnacled — and the name grew into "Ash — Hardened x3, Rewritten x4". It was sold as
/// > "the record of your run", and it played as a stat trickle nobody chose and a name that had to
/// > be collapsed to stop it leaving the screen.
/// >
/// > Do not reintroduce it as stats. If a companion should grow across a run, that is a design with
/// > a decision in it, not an automatic bonus for having been present.
/// </summary>
public record Companion
{
	public string Name { get; init; } = "";
	public string Description { get; init; } = "";

	public int BasePower { get; init; }
	public int BaseToughness { get; init; }

	public int Power => BasePower;
	public int Toughness => BaseToughness;

	/// <summary>
	/// **What this companion DOES, and the reason a companion is the spine of a deck.**
	///
	/// Design it to change how you PLACE, not what you draft. "A bonus when you play Scavengers" is
	/// a checklist you satisfy once at a reward screen and then forget. "+2/+0 for each unit that
	/// died last turn" makes you feed losing lanes on purpose, every turn, with the cheap bodies you
	/// would otherwise bin. Placement is the only decision this game has.
	///
	/// A roster is cheap: one record with a list of effects. `KinEffect` does not know what holds
	/// it, so nothing else has to change to add another companion.
	/// </summary>
	public ImmutableList<KinEffect> Effects { get; init; } = ImmutableList<KinEffect>.Empty;

	/// <summary>
	/// **The three cards of this companion's archetype that a run STARTS with.** Empty means the
	/// generic three — see `StarterContent.NewRun`.
	///
	/// Ten vanilla starters meant the first several floors held no decision at all, and a
	/// companion whose plan you cannot act on until floor 6 has not declared a plan. With these,
	/// floor 1 already has one.
	/// </summary>
	public ImmutableList<RunCard> Starter { get; init; } = ImmutableList<RunCard>.Empty;

	/// <summary>
	/// Applies an upgrade and returns the companion it becomes. The whole of progression.
	///
	/// **`EchoesAbility` is read off the CURRENT effects, so order matters between upgrades** — an
	/// echo taken after another upgrade's effect copies that one too. That is intended: it is what
	/// makes a late echo worth more than an early one, and it is the only interaction between
	/// upgrades in the system.
	/// </summary>
	public Companion With(CompanionUpgrade upgrade)
	{
		var effects = Effects.AddRange(upgrade.Effects);

		if (upgrade.EchoesAbility)
			effects = effects.AddRange(effects);

		return this with
		{
			BasePower = BasePower + upgrade.Power,
			BaseToughness = BaseToughness + upgrade.Toughness,
			Effects = effects,
		};
	}
}

/// <summary>
/// Marks the battlefield card that IS the companion. Its presence means "no transform may touch
/// this, and its death is not a deck event" — a companion dying must not feed Zombie.
/// </summary>
public record CompanionComponent : GameComponent;
