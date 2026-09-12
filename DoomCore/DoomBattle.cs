using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// The root object of a single battle. Owns the enemy zone; the player is also a child.
///
/// A battle is ONE doomsday scenario on a countdown. It is not the whole game — the run above it
/// (life and deck carried between battles) lives outside GameState entirely. The run layer above it is not built yet.
/// </summary>
public record DoomBattle : GameObject
{
	/// <summary>
	/// Turns left before the doom resolves. Ticks down at the end of every turn.
	///
	/// **Nothing may prevent this reaching zero.** If clearing the enemies could end a battle early
	/// the player has beaten the apocalypse, and it is an obstacle rather than doom. Cards that
	/// BURN the countdown (spend it to cast something now) only ever make it smaller.
	/// </summary>
	public int CountdownRemaining { get; init; }

	/// <summary>What the countdown started at. Varies per scenario — Nuclear 2, Flood 5.</summary>
	public int CountdownTotal { get; init; }

	/// <summary>Which apocalypse resolves when the countdown hits zero.</summary>
	public DoomScenario Scenario { get; init; } = DoomScenario.None;

	public int TurnNumber { get; init; } = 1;

	/// <summary>Set once the doom has resolved. The battle is over at that point, win or lose.</summary>
	public bool IsOver { get; init; } = false;

	/// <summary>
	/// True when the player hit 0 life. Distinct from <see cref="IsOver"/> because surviving to the
	/// doom is the NORMAL end of a battle — only this one ends the run.
	/// </summary>
	public bool PlayerIsDead { get; init; } = false;
}
