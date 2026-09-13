using System.Collections.Immutable;
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

		// Nothing per-turn to reset on a unit. A lane is chosen once, when the unit is played, and
		// held until it dies — there is no assignment to clear and no damage to wipe, since damage
		// persists for the whole battle.

		ImmutableList<GameEvent> events;
		(state, events) = DrawCards(state, HandSize);

		// Irradiated cards can kill you on the draw, so death is checked HERE as well as at end of
		// turn. Without this the player keeps playing at 0 life until the turn happens to end.
		if (state.GetPlayer().Life <= 0)
		{
			var dying = state.GetBattle();
			state = state.UpdateObject(dying.Id, dying with { IsOver = true, PlayerIsDead = true });
			return new ActionResult(state).WithEvents(events.Add(new PlayerDiedEvent()));
		}

		var battle = state.GetBattle();
		return new ActionResult(state).WithEvents(
			events.Add(
				new TurnStartedEvent
				{
					TurnNumber = battle.TurnNumber,
					CountdownRemaining = battle.CountdownRemaining,
				}
			)
		);
	}

	/// <summary>
	/// Draws up to <paramref name="count"/>, reshuffling Discard into Draw when it runs dry.
	/// Running out of cards entirely is survivable — it draws fewer, it does not lose the battle.
	/// Decking is an MTG rule and has no place here; the countdown already ends every battle.
	/// </summary>
	public static (GameState State, ImmutableList<GameEvent> Events) DrawCards(
		GameState state,
		int count
	)
	{
		var drawId = state.ZoneId(ZoneType.Draw);
		var handId = state.ZoneId(ZoneType.Hand);
		var discardId = state.ZoneId(ZoneType.Discard);
		var events = ImmutableList<GameEvent>.Empty;

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

			// Nuclear's price, paid on the draw rather than on the play — you cannot dodge it by
			// declining to cast the card.
			if (state.GetObject(top) is DoomCard card && card.HasTag(DoomTransforms.IrradiatedTag))
			{
				var player = state.GetPlayer();
				var life = player.Life - 1;
				state = state.UpdateObject(player.Id, player with { Life = life });
				events = events.Add(
					new IrradiatedDrawnEvent
					{
						CardId = top,
						CardName = card.Name,
						LifeRemaining = life,
					}
				);
			}
		}

		return (state, events);
	}
}
