using System.Collections.Concurrent;
using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MagicCore;

/// <summary>
/// Steps in turn order (CR 500.1). <see cref="PreGame"/> is not a step — it is the mulligan
/// window before the first untap.
/// </summary>
public enum Step
{
	PreGame,
	Untap,
	Upkeep,
	Draw,
	PreCombatMain,
	BeginCombat,
	DeclareAttackers,
	DeclareBlockers,
	FirstStrikeDamage, // only when a combatant has first or double strike (CR 510.4)
	CombatDamage,
	EndCombat,
	PostCombatMain,
	End,
	Cleanup,
}

/// <summary>Root object. Owns the shared Stack zone and both players.</summary>
public record MagicGame : GameObject
{
	/// <summary>
	/// A token or a copy went to a hand or a library since the last state-based check, which scans those two zones
	/// only while it is up (AI-22: that scan ran at every check). Raised by MoveCard, the only way in.
	/// </summary>
	public bool TokenOrCopyInHandOrLibrary { get; init; }

	public int StartingPlayerId { get; init; }
	public int ActivePlayerId { get; init; }

	/// <summary>Every turn counts, both players' — the starting player's first turn is 1. An extra turn counts too.</summary>
	public int TurnNumber { get; init; }

	/// <summary>
	/// H.M.S. Bounty: "take an extra turn after this one" — whose, in the order created; the LAST is taken
	/// first (CR 500.7), by TurnStructure.FinishCleanup.
	/// </summary>
	public ImmutableList<int> ExtraTurns { get; init; } = [];

	public Step Step { get; init; } = Step.PreGame;

	/// <summary>0 while nobody has priority: pregame, untap, cleanup, a pending choice, game over.</summary>
	public int PriorityPlayerId { get; init; }

	/// <summary>
	/// Passes since the last thing anyone did (CR 117.4). Two in a row with an empty stack ends
	/// the step; any action — a land, a mana ability — resets it.
	/// </summary>
	public int PassesInSuccession { get; init; }

	/// <summary>"If two or more creatures died this turn" — anyone's. Reset as each turn begins.</summary>
	public int CreaturesDiedThisTurn { get; init; }
}

public static class MagicKeys
{
	public const string Game = "Game";
	public const string Stack = "Stack";
	public const string Player1 = "Player1";
	public const string Player2 = "Player2";

	/// <summary>
	/// Built once per player and zone, then reused: every zone lookup asks for it (story AI-7.3d — a
	/// new string each time, with its enum formatted, ran on every CardsIn under the search).
	/// </summary>
	public static string Zone(int playerId, ZoneType type) =>
		ZoneKeys.GetOrAdd((playerId, type), k => $"{k.Item1}.{k.Item2}");

	static readonly ConcurrentDictionary<(int, ZoneType), string> ZoneKeys = new();
}
