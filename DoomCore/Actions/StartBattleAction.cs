using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// Shuffles the draw pile and begins turn 1. Cards and enemies must already be placed.
/// </summary>
public record StartBattleAction : GameAction
{
	/// <summary>Cards drawn at the start of every turn — STS-style, the hand is discarded each turn.</summary>
	public int OpeningHandSize { get; init; } = 5;

	public override ActionResult Execute(GameState gameState)
	{
		var battle = gameState.GetBattle();
		var state = DoomRng.ShuffleZone(gameState, gameState.ZoneId(ZoneType.Draw));

		state = state.SpawnAction(new StartTurnAction { HandSize = OpeningHandSize });

		return new ActionResult(state).WithEvent(
			new BattleStartedEvent
			{
				Scenario = battle.Scenario,
				Countdown = battle.CountdownRemaining,
			}
		);
	}
}

/// <summary>
/// Seeded shuffling. Kept in one place so every shuffle advances the same seed and a battle
/// replays identically from its RngSeed — the simulator and any future AI depend on that.
/// </summary>
public static class DoomRng
{
	public static GameState ShuffleZone(GameState state, int zoneId)
	{
		var ids = state.GetChildrenIds(zoneId).ToList();
		if (ids.Count <= 1)
			return state;

		var rng = new Random(state.RngSeed);
		for (var i = ids.Count - 1; i > 0; i--)
		{
			var j = rng.Next(i + 1);
			(ids[i], ids[j]) = (ids[j], ids[i]);
		}

		// Re-parent in the shuffled order. MoveObject appends, so moving every card back in
		// sequence rewrites the child list to match.
		foreach (var id in ids)
			state = state.MoveObject(id, zoneId);

		return state with
		{
			RngSeed = rng.Next(),
		};
	}
}
