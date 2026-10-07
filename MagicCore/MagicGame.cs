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
	public int StartingPlayerId { get; init; }
	public int ActivePlayerId { get; init; }

	/// <summary>Every turn counts, both players' — the starting player's first turn is 1.</summary>
	public int TurnNumber { get; init; }

	public Step Step { get; init; } = Step.PreGame;

	/// <summary>0 while nobody has priority: pregame, untap, cleanup, a pending choice, game over.</summary>
	public int PriorityPlayerId { get; init; }

	/// <summary>
	/// Passes since the last thing anyone did (CR 117.4). Two in a row with an empty stack ends
	/// the step; any action — a land, a mana ability — resets it.
	/// </summary>
	public int PassesInSuccession { get; init; }
}

public static class MagicKeys
{
	public const string Game = "Game";
	public const string Stack = "Stack";
	public const string Player1 = "Player1";
	public const string Player2 = "Player2";

	public static string Zone(int playerId, ZoneType type) => $"{playerId}.{type}";
}
