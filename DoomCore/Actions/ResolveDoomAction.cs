using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// The apocalypse lands — and the battle CARRIES ON.
///
/// **The doom is a metronome, not a wall.** It fires on its interval, the clock resets, and the
/// fight continues until the Opponent dies or you do. Nothing here may end a battle, and nothing
/// that ends one should ever be added: an apocalypse you can outlast is a wall, and the model this
/// replaced proved that a battle which ends on a counter turns a cleared board into dead air.
///
/// What each firing DOES is deliberately not here. It records a <see cref="DoomFiring"/> — a
/// snapshot of what the scenario read at this instant, in RUN ids — and the run layer replays those
/// against the deck once the battle is over. Keeping the two apart is what lets every scenario be
/// data rather than code, and it is the only way a transform can rewrite a deck that outlives this
/// GameState.
/// </summary>
public record ResolveDoomAction : GameAction
{
	/// <summary>
	/// The turn this firing landed on, passed in rather than read off the battle: EndTurnAction
	/// increments the turn before this spawned action runs, so reading it here would log turn+1.
	/// </summary>
	public int TurnNumber { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		var battle = gameState.GetBattle();

		var firing = DoomFiring.Capture(gameState, TurnNumber);

		// A BATTLE-scope apocalypse happens right here, to this GameState. A PERMANENT one is only
		// recorded; the run replays it once the battle is over, because it rewrites a deck that
		// outlives this state. The firing is captured BEFORE either, so both read the same board.
		var (gs, firingEvents) =
			StarterContent.ScopeOf(battle.Scenario) == DoomScope.Battle
				? DoomBattleEffects.Apply(gameState, battle.Scenario)
				: (gameState, ImmutableList<GameEvent>.Empty);

		var state = gs.UpdateObject(
			battle.Id,
			battle with
			{
				Firings = battle.Firings.Add(firing),
				DoomsFired = battle.DoomsFired + 1,

				// Deaths are consumed by the firing that read them. Leaving them would pay Zombie
				// for the same death again on every later firing.
				DiedRunCardIds = [],

				// The clock resets rather than stopping. This line is the whole change.
				CountdownRemaining = battle.CountdownTotal,
			}
		);

		// The firing's own events travel with it — what it took, what it damaged — so the board can
		// animate an apocalypse rather than showing numbers that changed for no stated reason.
		return new ActionResult(state)
			.WithEvent(
				new DoomResolvedEvent
				{
					Scenario = battle.Scenario,
					FiringNumber = battle.DoomsFired + 1,
				}
			)
			.WithEvents(firingEvents);
	}
}
