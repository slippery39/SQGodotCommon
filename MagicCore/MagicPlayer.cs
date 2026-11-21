using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MagicCore;

public record MagicPlayer : GameObject
{
	/// <summary>Cecil, Dark Knight: "half your starting life total" — every game starts at 20 (Bo1, two players).</summary>
	public const int StartingLife = 20;

	public int Life { get; init; } = StartingLife;

	/// <summary>Empties at the end of every step and phase (CR 500.4).</summary>
	public ManaPool ManaPool { get; init; } = ManaPool.Empty;

	/// <summary>What this player did this turn — any turn, theirs or yours. Replaced whole as each turn begins.</summary>
	public TurnTally ThisTurn { get; init; } = new();

	/// <summary>The turn before this one — "if an opponent was dealt noncombat damage last turn".</summary>
	public TurnTally LastTurn { get; init; } = new();

	/// <summary>First Contact: "If this spell is the first spell you've cast this game" — never reset.</summary>
	public int SpellsCastThisGame { get; init; }

	/// <summary>
	/// Caves of Chaos Adventurer: "if you've completed a dungeon" — counted as one is removed on its last room
	/// (Undercity.Complete, CR 309.7), never reset.
	/// </summary>
	public int DungeonsCompleted { get; init; }

	/// <summary>H.M.S. Bounty: "haven't begun an extra turn this game" — set as one begins.</summary>
	public bool BegunExtraTurn { get; init; }

	/// <summary>The TurnNumber this player's most recent turn began — summoning sickness (CR 302.6); 0 before their first.</summary>
	public int TurnBeganOn { get; init; }

	public int MulligansTaken { get; init; }
	public bool HasKeptHand { get; init; }

	/// <summary>
	/// The draw ATTEMPT is the loss, not the empty library (CR 704.5b) — milling to zero is fine.
	/// Turned into <see cref="HasLost"/> by the state-based check — the one way to lose, besides an
	/// opponent's "you win the game" (WinTheGame), which ends the game at once.
	/// </summary>
	public bool AttemptedDrawFromEmptyLibrary { get; init; }

	public bool HasLost { get; init; }

	/// <summary>
	/// Counters on a player (CR 122.1) — Galvanic Discharge's energy, Etali's poison — put and removed through
	/// MagicState.PutCounters alone, as a permanent's.
	/// </summary>
	public ImmutableDictionary<string, int> Counters { get; init; } =
		ImmutableDictionary<string, int>.Empty;

	public int CountersOf(string kind) => Counters.GetValueOrDefault(kind);

	/// <summary>MagicState.PutCounters' own write for a player — never called elsewhere (CounterWritesTests).</summary>
	public MagicPlayer WithCounters(string kind, int n) =>
		this with
		{
			Counters = n == 0 ? Counters.Remove(kind) : Counters.SetItem(kind, n),
		};
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

	/// <summary>Revolt (Fatal Push): "if a permanent left the battlefield under your control this turn" — its controller's, MagicState.MoveCard.</summary>
	public int PermanentsLeftTheBattlefield { get; init; }

	/// <summary>Captain Benjamin Sisko: "for each dilemma you've faced this turn" — FacedDilemmaEvent.Face.</summary>
	public int DilemmasFaced { get; init; }

	/// <summary>Noncombat damage dealt TO this player (Command the Stage).</summary>
	public int NoncombatDamageTaken { get; init; }

	/// <summary>Sulu: "if an Equipment or Spacecraft entered the battlefield under your control this turn" — the subtypes of every permanent that entered under this player's control (MagicState.EnterBattlefield), kept though it left again.</summary>
	public System.Collections.Immutable.ImmutableArray<string> SubtypesEntered { get; init; } = [];

	/// <summary>Robber of the Rich: "any turn you attacked with a Rogue" — the subtypes of the creatures this player attacked with (FinishAttackersAction).</summary>
	public System.Collections.Immutable.ImmutableArray<string> SubtypesAttacked { get; init; } = [];
}
