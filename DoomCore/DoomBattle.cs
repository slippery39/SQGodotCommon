using System.Collections.Immutable;
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
	/// The board is five lanes, 0-4. One of your units and one enemy per lane; they fight
	/// automatically. Lane choice is the only positional decision in the game, so this number is
	/// load-bearing for both the UI and the doom scenarios that read the board.
	/// </summary>
	public const int LaneCount = 5;

	/// <summary>
	/// Turns until the next firing. Ticks down at the end of every turn, and RESETS to
	/// <see cref="CountdownTotal"/> when it fires — the doom is a metronome, not a wall.
	///
	/// **Nothing may prevent this reaching zero, and reaching zero does not end the battle.**
	/// A battle ends when the Opponent dies or the player does. Cards that BURN the countdown
	/// (spend it to cast something now) only ever make it smaller.
	/// </summary>
	public int CountdownRemaining { get; init; }

	/// <summary>What the countdown started at. Varies per scenario — Nuclear 2, Flood 5.</summary>
	public int CountdownTotal { get; init; }

	/// <summary>Which apocalypse resolves when the countdown hits zero.</summary>
	public DoomScenario Scenario { get; init; } = DoomScenario.None;

	public int TurnNumber { get; init; } = 1;

	/// <summary>Set when the battle is over, win or lose. The countdown never sets it.</summary>
	public bool IsOver { get; init; } = false;

	/// <summary>
	/// RunCardIds of units played to the Field this battle, and of units that DIED this battle.
	///
	/// These exist because a doom transform rewrites the RUN deck, and the run outlives this
	/// GameState. "Duplicate everything you summoned" cannot be asked of battle objects — their ids
	/// are thrown away when the battle ends — so the battle records the run identities as it goes.
	///
	/// Died is a LIST, not a set: a card can cycle back out of Discard and die twice in one battle,
	/// and Zombie pays per death rather than per card.
	/// </summary>
	public ImmutableHashSet<int> SummonedRunCardIds { get; init; } = ImmutableHashSet<int>.Empty;

	public ImmutableList<int> DiedRunCardIds { get; init; } = ImmutableList<int>.Empty;

	/// <summary>
	/// True when the player hit 0 life. Distinct from <see cref="IsOver"/>, which a win also sets —
	/// only this one ends the run.
	/// </summary>
	public bool PlayerIsDead { get; init; } = false;

	/// <summary>
	/// True when the Opponent was killed. **This is the only winning end of a battle.**
	/// Distinct from <see cref="IsOver"/>, which a loss also sets.
	/// </summary>
	public bool OpponentDefeated { get; init; } = false;

	/// <summary>
	/// How many times the doom has fired this battle.
	///
	/// The run reads it to decide whether a doom transform applies at all: killing the Opponent
	/// before the first firing means no apocalypse happened, and a transform that ran anyway would
	/// rewrite the deck for an event the player never saw.
	/// </summary>
	public int DoomsFired { get; init; } = 0;

	/// <summary>
	/// Every firing this battle, with what it read at that instant. The run replays these.
	///
	/// A LIST because the doom recurs: one entry per firing, in order. See <see cref="DoomFiring"/>
	/// for why a single end-of-battle read is not enough.
	/// </summary>
	public ImmutableList<DoomFiring> Firings { get; init; } = ImmutableList<DoomFiring>.Empty;
}
