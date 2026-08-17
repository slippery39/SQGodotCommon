using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// A restriction on when a card may be cast at all — "this creature can't be cast during the
/// first three turns of the game" (Serra Avenger).
///
/// Distinct from AdditionalCost: a cost is something you pay, a restriction is something you
/// cannot pay your way past. Checked in ValidateAdd on all three cast actions, so a restricted
/// card is simply never offered rather than being offered and then failing.
/// </summary>
public abstract record CastRestrictionComponent : GameComponent
{
	public abstract bool CanCast(GameState state, int playerId);

	/// <summary>
	/// Player-facing reason. Abstract so a new restriction cannot ship without one.
	/// </summary>
	public abstract string Describe();
}

/// <summary>
/// "Can't be cast before round N."
///
/// IMPORTANT — round vs turn: MtgGame.TurnNumber counts ROUNDS, incrementing once both players
/// have taken a turn, and starts at 1. Real MTG counts each player's turn separately, so its
/// "first, second, or third turns of the game" spans rounds 1 and 2. MinimumRound = 2 is the
/// closest honest mapping and is the value Serra Avenger uses; the knob is here to retune after
/// playtesting rather than hidden in card code.
/// </summary>
public record MinimumRoundCastRestriction : CastRestrictionComponent
{
	public int MinimumRound { get; init; } = 2;

	public override bool CanCast(GameState state, int playerId) =>
		(state.TryGetGame()?.TurnNumber ?? int.MaxValue) >= MinimumRound;

	public override string Describe() => $"Can't be cast before round {MinimumRound}";
}
