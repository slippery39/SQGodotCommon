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

	/// <summary>"Ash, the Last Dog — Gravemarked, Glowing" — what the run did to it, at a glance.</summary>
	public string FullName =>
		Marks.IsEmpty ? Name : $"{Name} — {string.Join(", ", Marks.Select(m => m.Name))}";
}

/// <summary>
/// Marks the battlefield card that IS the companion. Its presence means "no transform may touch
/// this, and its death is not a deck event" — a companion dying must not feed Zombie.
/// </summary>
public record CompanionComponent : GameComponent;
