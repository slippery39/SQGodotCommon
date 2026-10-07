using ImmutableGameObjects;

namespace MagicCore;

public record MagicPlayer : GameObject
{
	public int Life { get; init; } = 20;

	/// <summary>Empties at the end of every step and phase (CR 500.4).</summary>
	public ManaPool ManaPool { get; init; } = ManaPool.Empty;

	public int LandsPlayedThisTurn { get; init; }

	/// <summary>"If you didn't cast a spell this turn" — any turn, theirs or yours; reset as each turn begins.</summary>
	public int SpellsCastThisTurn { get; init; }
	public int NoncreatureSpellsCastThisTurn { get; init; }
	public int MulligansTaken { get; init; }
	public bool HasKeptHand { get; init; }

	/// <summary>
	/// The draw ATTEMPT is the loss, not the empty library (CR 704.5b) — milling to zero is fine.
	/// Turned into <see cref="HasLost"/> by the state-based check, the one way to lose.
	/// </summary>
	public bool AttemptedDrawFromEmptyLibrary { get; init; }

	public bool HasLost { get; init; }
}
