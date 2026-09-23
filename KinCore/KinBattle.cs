using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore;

/// <summary>
/// The root object of a single battle. Owns the enemy zone; the player is also a child.
///
/// It is not the whole game — the run above it (life, deck and companion, carried between battles)
/// lives outside GameState entirely.
/// </summary>
public record KinBattle : GameObject
{
	/// <summary>
	/// The board is five lanes, 0-4. One of your units and one enemy per lane; they fight
	/// automatically. Lane choice is the only positional decision in the game, so this number is
	/// load-bearing for both the UI and anything that reads the board.
	/// </summary>
	public const int LaneCount = 5;

	public int TurnNumber { get; init; } = 1;

	/// <summary>Set when the battle is over, win or lose. **Only a death ends a battle.**</summary>
	public bool IsOver { get; init; } = false;

	/// <summary>
	/// Deaths THIS TURN only, cleared when the next turn starts.
	///
	/// Feeds <see cref="CountOf.DiedThisTurn"/> — what you spent THIS turn, which is what makes a
	/// sacrifice a combo rather than a setup. Rolled into
	/// <see cref="DiedLastTurnRunCardIds"/> when the next turn starts.
	/// </summary>
	public ImmutableList<int> DiedThisTurnRunCardIds { get; init; } = ImmutableList<int>.Empty;

	/// <summary>
	/// What died LAST turn, held so a card played this turn can still read it.
	///
	/// <see cref="DiedThisTurnRunCardIds"/> is cleared when a turn starts, so by the time you are
	/// choosing plays it is already empty — a card that scales on "what died last turn" would
	/// always read zero and look exactly like a card that worked. `StartTurnAction` rolls one into
	/// the other before clearing.
	///
	/// **Deaths only. A unit that withdrew at the end of the turn is not in here**, which is the
	/// whole point of the attrition axis: it counts what the enemy took from you, not your own
	/// board doing what it does every turn.
	/// </summary>
	public ImmutableList<int> DiedLastTurnRunCardIds { get; init; } = ImmutableList<int>.Empty;

	/// <summary>
	/// Cards played so far this turn, reset each turn. The volume axis — see <see cref="CountOf"/>.
	/// </summary>
	public int CardsPlayedThisTurn { get; init; }

	/// <summary>
	/// **Damage your units soaked in the lanes THIS turn** — the part of an attack, or of an
	/// enemy's Thorns, that a body took instead of your face. Cleared when a turn starts.
	///
	/// Lane combat only, deliberately: a trait that chips every unit is not something you CHOSE to
	/// stand in front of. Feeds <see cref="CountOf.AbsorbedThisTurn"/>, which is Bramble.
	/// </summary>
	public int AbsorbedThisTurn { get; init; }

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
}
