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

	/// <summary>
	/// Whether drawing from an empty library loses the game. On for real play; off for
	/// `MtgGameFactory.CreateForTesting()`.
	///
	/// **This is a test convenience, not a game option — do not expose it to players.** Unit
	/// tests build boards by hand and leave libraries empty, because that used to be harmless.
	/// Once decking kills, an empty library makes the AI's lookahead correct in ways that have
	/// nothing to do with what the test is asking: it declines to cast a draw spell because
	/// drawing decks it, and it plays EndTurn because ending the turn decks the *opponent* and
	/// wins on the spot. Both are right, and both quietly invalidate a test about something else.
	///
	/// The alternative was padding every hand-built test with filler cards. That is dozens of
	/// edits, and each one silently re-encodes "libraries do not matter here" in a place where
	/// a future reader cannot see why the filler exists.
	///
	/// It lives on the game state rather than a static so it stays serializable and safe under
	/// the simulator's parallel batches.
	/// </summary>
	public bool DeckingLossEnabled { get; init; } = true;

	/// <summary>
	/// Cards currently playable from exile — impulse draw ("you may play it this turn"). Same
	/// index-the-few pattern as StaticSourceIds.
	///
	/// It exists because the alternative was scanning the whole exile zone inside
	/// `MtgActionGenerator.AddHandActions`, which runs on **every** legal-action generation and so
	/// sits on the hottest path in the engine. Exile is the one zone that only grows: every land
	/// played is moved there (`PlayLandAction`), so that scan cost climbs with the turn number for
	/// every deck in the game, whether or not it contains a single impulse-draw card.
	///
	/// Treated as a HINT, never as truth. Readers re-check that the card still exists, still
	/// carries `ExiledPlayableComponent`, and still sits in that player's exile — so a stale id is
	/// skipped rather than producing a phantom action. That keeps correctness independent of every
	/// future path that might move an exiled card.
	/// </summary>
	public ImmutableHashSet<int> PlayableExiledIds { get; init; } = ImmutableHashSet<int>.Empty;
}
