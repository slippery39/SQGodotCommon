using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// A scar left by an apocalypse the companion walked through. Permanent, and it stacks.
/// </summary>
public record CompanionMark
{
	public DoomScenario From { get; init; }
	public string Name { get; init; } = "";
	public int Power { get; init; }
	public int Toughness { get; init; }
}

/// <summary>
/// TAG ALONG. The one thing the doom cannot take — and the one thing that remembers it.
///
/// Not a card and never in the deck, so no transform can zombify, irradiate or drown it. It is on
/// the field free at the start of every battle, which also fixes the real problem that a bad draw
/// used to leave you with nothing on the board at all.
///
/// **Every doom it survives leaves a mark, and marks are permanent and cumulative.** By the last
/// floor it is a patchwork of every ending you lived through — the record of your run, and the
/// thing you carried out of it.
/// </summary>
public record Companion
{
	public string Name { get; init; } = "";
	public string Description { get; init; } = "";

	public int BasePower { get; init; }
	public int BaseToughness { get; init; }

	public ImmutableList<CompanionMark> Marks { get; init; } = ImmutableList<CompanionMark>.Empty;

	/// <summary>
	/// **What this companion DOES, and the reason a companion is now the spine of a deck.**
	///
	/// Until combat v3 the companion was a body with stats and nothing else, and the pool had no
	/// synergies at all — every card was an isolated stat line. The companion is the one permanent
	/// thing on the board, so it is the only place a deck can build TOWARD. Its ability is the
	/// build declaration: it decides which cards in a reward screen are good, which is the
	/// deckbuilding depth the game was missing.
	///
	/// **Design it to change how you PLACE, not what you draft.** "A bonus when you play
	/// Scavengers" is a checklist you satisfy once at a reward screen and then forget. "+2/+0 for
	/// each unit that died last turn" makes you feed losing lanes on purpose, every turn, with the
	/// cheap bodies you would otherwise bin. Placement is the only decision this game has.
	///
	/// A roster is cheap: one record with a list of effects. `DoomEffect` does not know what holds
	/// it, so nothing else has to change to add another companion.
	/// </summary>
	public ImmutableList<DoomEffect> Effects { get; init; } = ImmutableList<DoomEffect>.Empty;

	public int Power => BasePower + Marks.Sum(m => m.Power);
	public int Toughness => BaseToughness + Marks.Sum(m => m.Toughness);

	/// <summary>
	/// The mark an apocalypse leaves. Each is small; surviving a whole act is what makes it large,
	/// so a late-run companion is visibly the sum of what it went through.
	/// </summary>
	public static CompanionMark MarkFor(DoomScenario scenario) =>
		ScenarioLibrary.Of(scenario).Mark with
		{
			From = scenario,
		};

	public Companion Marked(DoomScenario scenario) =>
		this with
		{
			Marks = Marks.Add(MarkFor(scenario)),
		};

	/// <summary>
	/// "Ash — Hardened x3, Rewritten x4" — what the run did to it, at a glance.
	///
	/// **Repeats are COLLAPSED with a count.** Marks are cumulative and an act fires the same
	/// apocalypse for six floors at a time, so by floor 19 this was
	/// "Ash — Hardened, Hardened, Hardened, Rewritten, Rewritten, Rewritten, Rewritten,
	/// Replicated, Replicated, Replicated, Replicated, Replicated" — a name that grew without
	/// limit and pushed the intermission panel off the side of the screen.
	///
	/// This is PRESENTATION ONLY. Every mark is still in <see cref="Marks"/> and still counts
	/// toward <see cref="Power"/> and <see cref="Toughness"/>; only the rendering groups them. The
	/// length is now bounded by the number of distinct apocalypses rather than by the length of the
	/// run — still long, and the companion's naming remains a design question of its own.
	/// </summary>
	public string FullName
	{
		get
		{
			if (Marks.IsEmpty)
				return Name;

			// Grouped in the order each mark was FIRST taken, so the name still reads as a history
			// rather than being re-sorted into something alphabetical.
			var counted = Marks
				.Select(m => m.Name)
				.Distinct()
				.Select(name => (Name: name, Count: Marks.Count(m => m.Name == name)))
				.Select(g => g.Count > 1 ? $"{g.Name} x{g.Count}" : g.Name);

			return $"{Name} — {string.Join(", ", counted)}";
		}
	}
}

/// <summary>
/// Marks the battlefield card that IS the companion. Its presence means "no transform may touch
/// this, and its death is not a deck event" — a companion dying must not feed Zombie.
/// </summary>
public record CompanionComponent : GameComponent;
