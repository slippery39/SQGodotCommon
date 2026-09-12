using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// Refills energy, clears last turn's assignments, and draws a fresh hand.
///
/// Does NOT tick the countdown — <see cref="EndTurnAction"/> owns that, so the countdown is spent
/// by ending turns rather than beginning them and a battle of N turns ticks exactly N times.
/// </summary>
public record StartTurnAction : GameAction
{
	public int HandSize { get; init; } = 5;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var player = state.GetPlayer();

		state = state.UpdateObject(player.Id, player with { Energy = player.MaxEnergy });

		// Assignments are per-turn. A unit that blocked last turn is free to attack this one.
		foreach (var unit in state.Units().ToList())
		{
			var u = unit.Unit();
			if (u.Assignment == Assignment.None)
				continue;

			state = state.UpdateObject(
				unit.Id,
				unit.WithComponentReplaced(
					u with
					{
						Assignment = Assignment.None,
						AssignedEnemyId = 0,
					}
				)
			);
		}

		state = DrawCards(state, HandSize);

		var battle = state.GetBattle();
		return new ActionResult(state).WithEvent(
			new TurnStartedEvent
			{
				TurnNumber = battle.TurnNumber,
				CountdownRemaining = battle.CountdownRemaining,
			}
		);
	}

	/// <summary>
	/// Draws up to <paramref name="count"/>, reshuffling Discard into Draw when it runs dry.
	/// Running out of cards entirely is survivable — it draws fewer, it does not lose the battle.
	/// Decking is an MTG rule and has no place here; the countdown already ends every battle.
	/// </summary>
	public static GameState DrawCards(GameState state, int count)
	{
		var drawId = state.ZoneId(ZoneType.Draw);
		var handId = state.ZoneId(ZoneType.Hand);
		var discardId = state.ZoneId(ZoneType.Discard);

		for (var i = 0; i < count; i++)
		{
			var top = state.GetChildrenIds(drawId).FirstOrDefault();

			if (top == 0)
			{
				var discarded = state.GetChildrenIds(discardId).ToList();
				if (discarded.Count == 0)
					break;

				foreach (var id in discarded)
					state = state.MoveObject(id, drawId);
				state = DoomRng.ShuffleZone(state, drawId);

				top = state.GetChildrenIds(drawId).FirstOrDefault();
				if (top == 0)
					break;
			}

			state = state.MoveObject(top, handId);
		}

		return state;
	}
}
