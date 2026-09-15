using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// A card. Behaviour comes from components — <see cref="UnitComponent"/> makes it a unit that can
/// be played to the Field and fight; without one it is a Rite that resolves and goes to Discard.
/// </summary>
public record DoomCard : GameObject
{
	/// <summary>Energy to play. There is no mana, no lands and no colours — see DoomJam.md.</summary>
	public int Cost { get; init; }

	/// <summary>
	/// Identity of this card in the RUN deck, stable across battles.
	///
	/// This is the bridge a doom transform needs. Battle objects get fresh GameState ids every
	/// battle, so "duplicate every unit summoned this battle" cannot be expressed against them —
	/// it has to name entries in the run deck, which outlives the GameState. Flood is the case
	/// that forces this: it reads the battle and rewrites the run.
	/// </summary>
	public int RunCardId { get; init; }

	/// <summary>
	/// Marks left by apocalypses, copied from the run card. Read at battle time — "Irradiated"
	/// costs a life when drawn. See <see cref="RunCard.Tags"/>.
	/// </summary>
	public ImmutableHashSet<string> Tags { get; init; } =
		ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// What this card does when it is played, dies, or a doom fires. Copied from the run card.
	///
	/// A card with no <see cref="UnitComponent"/> and no effects does NOTHING — it costs energy and
	/// goes to Discard. That is an authoring mistake, and `PlayCardAction` refuses it rather than
	/// letting it look like a card that worked.
	/// </summary>
	public ImmutableList<DoomEffect> Effects { get; init; } = ImmutableList<DoomEffect>.Empty;

	public bool HasTag(string tag) => Tags.Contains(tag);
}
