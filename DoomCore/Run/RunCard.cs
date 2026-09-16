using System.Collections.Immutable;

namespace DoomCore;

/// <summary>
/// A card as it exists in the RUN deck — the definition, not an instance in a battle.
///
/// Deliberately NOT a GameObject. The run lives outside GameState entirely, so a run card carries
/// no engine identity; <see cref="RunCardId"/> is its own, stable across every battle of the run.
/// That stability is the whole point: a doom transform names deck entries, and battle ids are
/// thrown away when the battle ends.
/// </summary>
public record RunCard
{
	public int RunCardId { get; init; }
	public string Name { get; init; } = "";
	public string Description { get; init; } = "";
	public int Cost { get; init; }

	public bool IsUnit { get; init; }
	public int Power { get; init; }
	public int Toughness { get; init; }

	/// <summary>
	/// Marks left by apocalypses — "Zombie", "Irradiated". Free-form so a new scenario needs no
	/// engine change, and readable by reward weighting so the game can offer you answers to the
	/// doom you actually took.
	/// </summary>
	public ImmutableHashSet<string> Tags { get; init; } =
		ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// What the card DOES, beyond being a body. Empty for a plain unit.
	///
	/// Lives on the run card because it is part of the card's definition, and it is copied onto the
	/// battle card each battle — the same way Power and Toughness are.
	/// </summary>
	public ImmutableList<DoomEffect> Effects { get; init; } = ImmutableList<DoomEffect>.Empty;

	/// <summary>
	/// Which act this card belongs to, or null for the shared core every act draws from.
	///
	/// **Not a <see cref="Tags"/> entry, deliberately.** Tags are marks an apocalypse LEAVES, and a
	/// doom transform writes to them — a card could gain a theme by being irradiated. Identity and
	/// damage do not belong in the same field.
	/// </summary>
	public DoomTheme? Theme { get; init; }

	public bool HasTag(string tag) => Tags.Contains(tag);
}
