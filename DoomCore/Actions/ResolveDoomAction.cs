using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// The apocalypse lands. Ends the battle unconditionally — surviving to this point is the NORMAL
/// way a battle ends, not a loss.
///
/// The deck transform each scenario applies is deliberately NOT here: it rewrites the run deck,
/// which outlives this GameState. This action ends the battle and reports the scenario; the run
/// layer reads the final board and applies the transform. Keeping the two apart is what lets every
/// scenario be data rather than code.
/// </summary>
public record ResolveDoomAction : GameAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var battle = gameState.GetBattle();
		var state = gameState.UpdateObject(battle.Id, battle with { IsOver = true });

		return new ActionResult(state).WithEvent(
			new DoomResolvedEvent { Scenario = battle.Scenario }
		);
	}
}
