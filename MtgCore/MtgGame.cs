using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// The root object of the game. Owns the shared zones: Stack, Battlefield, and Exile.
/// Players are also children of the game root.
///
/// Turn state lives here because it is global to the game — not owned by either player.
/// </summary>
public record MtgGame : GameObject
{
	/// <summary>
	/// The player whose turn it currently is.
	/// Set to the first player's ID by MtgGameFactory after both players are created.
	/// </summary>
	public int ActivePlayerId { get; init; }

	/// <summary>
	/// Increments by 1 each time a full round completes (i.e. both players have taken a turn).
	/// Starts at 1.
	/// </summary>
	public int TurnNumber { get; init; } = 1;

	/// <summary>
	/// The current phase of the active player's turn.
	/// </summary>
	public TurnPhase Phase { get; init; } = TurnPhase.Main;

	/// <summary>
	/// Number of spells (creatures + non-creatures) cast this turn by any player.
	/// Incremented by CastSpellAction and CastCreatureAction; reset to 0 by StartTurnAction.
	/// Used by the Storm mechanic to determine the number of copies.
	/// </summary>
	public int SpellsCastThisTurn { get; init; } = 0;

	/// <summary>
	/// Number of spells cast during the previous turn by any player. StartTurnAction rolls
	/// SpellsCastThisTurn into this field before zeroing it, so "last turn" means the
	/// immediately preceding half-turn (a turn is one player's turn in this engine).
	/// Used by werewolf transform conditions.
	/// </summary>
	public int SpellsCastLastTurn { get; init; } = 0;

	/// <summary>
	/// IDs of all permanents currently on the battlefield that have at least one
	/// StaticAbilityComponent. Maintained by StaticAbilityEngine via CheckStateBasedEffectsAction.
	/// Enables O(k) source lookup when applying statics to new permanents or cleaning up.
	/// </summary>
	public ImmutableHashSet<int> StaticSourceIds { get; init; } = ImmutableHashSet<int>.Empty;

	/// <summary>
	/// Extra turns owed to the active player. EndTurnAction spends one instead of passing the
	/// turn, so the same player starts another.
	///
	/// Capped by TakeExtraTurnAction rather than here: an AI that overvalues extra turns could
	/// otherwise chain them indefinitely and blow past the simulator's per-turn action cutoff.
	/// </summary>
	public int ExtraTurnsQueued { get; init; } = 0;
}
