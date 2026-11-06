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

	/// <summary>First Contact: "If this spell is the first spell you've cast this game" — never reset.</summary>
	public int SpellsCastThisGame { get; init; }

	public int MulligansTaken { get; init; }
	public bool HasKeptHand { get; init; }

	/// <summary>
	/// The draw ATTEMPT is the loss, not the empty library (CR 704.5b) — milling to zero is fine.
	/// Turned into <see cref="HasLost"/> by the state-based check — the one way to lose, besides an
	/// opponent's "you win the game" (WinTheGame), which ends the game at once.
	/// </summary>
	public bool AttemptedDrawFromEmptyLibrary { get; init; }

	public bool HasLost { get; init; }
}

/// <summary>
/// "This turn" counts for one player. One record, so a new count cannot miss the reset: the turn
/// start replaces the whole thing (<see cref="Turns.TurnStructure"/>). Counts only: one card's
/// "this turn" effect is an object in the command zone (<see cref="Effects.ThisTurnEffect"/>).
/// </summary>
public record TurnTally
{
	public int LandsPlayed { get; init; }

	/// <summary>Way of the Paradox: "You may play an additional land this turn."</summary>
	public int ExtraLandDrops { get; init; }
	public int SpellsCast { get; init; }
	public int NoncreatureSpellsCast { get; init; }
	public int CardsDrawn { get; init; }

	/// <summary>Kiora of Salt and Sand: "if you've activated a loyalty ability this turn".</summary>
	public int LoyaltyAbilitiesActivated { get; init; }

	/// <summary>Jiang Yanggu: "for each card you've discarded this turn" — counted by MagicState.Discard.</summary>
	public int CardsDiscarded { get; init; }
	public int LifeGained { get; init; }

	/// <summary>Gul Dukat, Colonial Prefect: "the life that player lost this turn" — counted by Rules.Life.Lose, damage included.</summary>
	public int LifeLost { get; init; }

	/// <summary>"If you've scried or surveilled this turn" — FRA has no scry, so surveils.</summary>
	public int Surveils { get; init; }

	/// <summary>Cruel Calculations: "cards that were put into [this player's] graveyard from their library this turn" — MagicState.MoveCard.</summary>
	public int CardsFromLibraryToGraveyard { get; init; }

	/// <summary>Miles O'Brien: "a permanent was returned to your hand from the battlefield this turn" — its owner's, MagicState.MoveCard.</summary>
	public int PermanentsReturnedToHand { get; init; }

	/// <summary>Noncombat damage dealt TO this player (Command the Stage).</summary>
	public int NoncombatDamageTaken { get; init; }

	/// <summary>Sulu: "if an Equipment or Spacecraft entered the battlefield under your control this turn" — the subtypes of every permanent that entered under this player's control (MagicState.EnterBattlefield), kept though it left again.</summary>
	public System.Collections.Immutable.ImmutableArray<string> SubtypesEntered { get; init; } = [];
}
