using ImmutableGameObjects;

namespace MagicCore;

public record MagicPlayer : GameObject
{
	public int Life { get; init; } = 20;

	/// <summary>Empties at the end of every step and phase (CR 500.4).</summary>
	public ManaPool ManaPool { get; init; } = ManaPool.Empty;

	/// <summary>What this player did this turn — any turn, theirs or yours. Replaced whole as each turn begins.</summary>
	public TurnTally ThisTurn { get; init; } = new();

	/// <summary>The turn before this one — "if an opponent was dealt noncombat damage last turn".</summary>
	public TurnTally LastTurn { get; init; } = new();
	public int MulligansTaken { get; init; }
	public bool HasKeptHand { get; init; }

	/// <summary>
	/// The draw ATTEMPT is the loss, not the empty library (CR 704.5b) — milling to zero is fine.
	/// Turned into <see cref="HasLost"/> by the state-based check, the one way to lose.
	/// </summary>
	public bool AttemptedDrawFromEmptyLibrary { get; init; }

	public bool HasLost { get; init; }
}

/// <summary>
/// "This turn" counts for one player. One record, so a new count cannot miss the reset: the turn
/// start replaces the whole thing (<see cref="Turns.TurnStructure"/>).
/// </summary>
public record TurnTally
{
	public int LandsPlayed { get; init; }

	/// <summary>Way of the Paradox: "You may play an additional land this turn."</summary>
	public int ExtraLandDrops { get; init; }
	public int SpellsCast { get; init; }
	public int NoncreatureSpellsCast { get; init; }
	public int CardsDrawn { get; init; }
	public int LifeGained { get; init; }

	/// <summary>"If you've scried or surveilled this turn" — FRA has no scry, so surveils.</summary>
	public int Surveils { get; init; }

	/// <summary>Noncombat damage dealt TO this player (Command the Stage).</summary>
	public int NoncombatDamageTaken { get; init; }
}
