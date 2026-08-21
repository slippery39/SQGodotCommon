using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Represents a player in the game.
/// A player owns their personal zones (Hand, Library, Graveyard, Battlefield, Exile)
/// as children in the GameState hierarchy.
///
/// CurrentMana is how much mana the player can spend this turn.
/// MaxMana is permanent mana from lands — increases each time a land is played.
/// CurrentMana is refilled to MaxMana at the start of each turn.
/// LandsPlayedThisTurn resets each turn; limits land plays to 1 (or more with Exploration).
/// LandsPlayedTotal never resets; used by Terravore's dynamic P/T.
/// LifeGainedThisTurn resets each turn; read by LifeGainedThisTurnCondition.
/// LifeLostThisTurn is its mirror — resets each turn, accumulates in LoseLifeAction,
/// DrainLifeAction and the player-damage path of DealDamageAction, so it counts damage and
/// drain alike. Read by LifeLostThisTurnCondition ("if a player lost 4 or more life this turn")
/// and by WasDealtDamageThisTurnCondition, which is how bloodthirst is expressed.
/// StartingLife is the life total the player began the game with — needed by abilities gated
/// on "N more life than your starting life total", which cannot assume 20.
/// </summary>
public record MtgPlayer : GameObject
{
	public int Life { get; init; } = 20;
	public int StartingLife { get; init; } = 20;
	public int LifeGainedThisTurn { get; init; } = 0;
	public int LifeLostThisTurn { get; init; } = 0;
	public bool HasLost { get; init; } = false;

	/// <summary>
	/// Set when this player was asked to draw from an empty library. Turned into a loss by
	/// `CheckStateBasedEffectsAction`, never by the draw itself — the state-based check owns
	/// `HasLost`, `PlayerLostEvent` and winner determination, and there must be exactly one
	/// way to lose.
	///
	/// It is a flag rather than a "library is empty" test because the rule is about the
	/// *attempt to draw*, not about the count: a player at zero cards has not lost until
	/// something asks them to draw. Milling out an empty library is not a loss either, which
	/// is why only `DrawCardsAction` sets it.
	/// </summary>
	public bool AttemptedDrawFromEmptyLibrary { get; init; } = false;
	public int CurrentMana { get; init; } = 0;
	public int MaxMana { get; init; } = 0;
	public int LandsPlayedThisTurn { get; init; } = 0;
	public int LandsPlayedTotal { get; init; } = 0;
	public ImmutableList<Emblem> Emblems { get; init; } = ImmutableList<Emblem>.Empty;
}
