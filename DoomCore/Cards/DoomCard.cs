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
}
