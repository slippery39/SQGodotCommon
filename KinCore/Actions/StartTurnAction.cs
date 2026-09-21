using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore;

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

		// Cleared HERE rather than at end of turn, so anything spawned by the turn that is ending
		// still reads that turn's dead. The spawn queue is FIFO and this action runs last.
		var turning = state.GetBattle();
		state = state.UpdateObject(
			turning.Id,
			turning with
			{
				// ROLLED, not dropped. A card that scales on what the enemy killed last turn has to
				// be able to read it while you are choosing plays, and this list is emptied before
				// you get a hand. See KinBattle.DiedLastTurnRunCardIds.
				DiedLastTurnRunCardIds = turning.DiedThisTurnRunCardIds,
				DiedThisTurnRunCardIds = [],
				CardsPlayedThisTurn = 0,
			}
		);

		// Nothing per-turn to reset on a unit. A lane is chosen once, when the unit is played, and
		// held until it dies — there is no assignment to clear and no damage to wipe, since damage
		// persists for the whole battle.

		// **Before the draw, so a card that comes back this turn can be drawn this turn.** Taken
		// cards are in no deck at all while they are gone — see `TakeCardsAction`.
		ImmutableList<GameEvent> events = ImmutableList<GameEvent>.Empty;
		(state, events) = TakeCardsAction.ReturnWhatIsDue(state, events);

		ImmutableList<GameEvent> drawn;
		(state, drawn) = DrawCards(state, HandSize);
		events = events.AddRange(drawn);

		// Drawing can cost life, so death is checked HERE as well as at end of turn. Without this
		// the player keeps playing at 0 life until the turn happens to end.
		if (state.GetPlayer().Life <= 0)
		{
			var dying = state.GetBattle();
			state = state.UpdateObject(dying.Id, dying with { IsOver = true, PlayerIsDead = true });
			return new ActionResult(state).WithEvents(events.Add(new PlayerDiedEvent()));
		}

		// Fired here so the enum does not lie. A trigger that is declared and never fires is the
		// same silent no-op as an inert card: nothing errors, and the effect simply never happens.
		state = FireTurnStart(state);

		var battle = state.GetBattle();
		return new ActionResult(state).WithEvents(
			events.Add(
				new TurnStartedEvent { TurnNumber = battle.TurnNumber }
			)
		);
	}

	/// <summary>
	/// Draws up to <paramref name="count"/>, reshuffling Discard into Draw when it runs dry.
	/// Running out of cards entirely is survivable — it draws fewer, it does not lose the battle.
	/// Decking is an MTG rule and has no place here; the countdown already ends every battle.
	/// </summary>
	/// <summary>
	/// OnTurnStart for everything on the board that carries effects. Runs AFTER the draw, so an
	/// effect that cares about your hand sees the hand you will actually play with.
	/// </summary>
	private static GameState FireTurnStart(GameState state)
	{
		foreach (var enemy in state.LivingEnemies().ToList())
			state = Fire(state, enemy.Id, enemy.Effects);

		foreach (var unit in state.Units().Where(u => !u.Unit().IsDead).ToList())
			state = Fire(state, unit.Id, unit.Effects);

		var opponent = state.GetOpponent();
		return Fire(state, opponent.Id, opponent.Effects);

		static GameState Fire(GameState s, int sourceId, ImmutableList<KinEffect> effects) =>
			effects.Any(e => e.Trigger == EffectTrigger.OnTurnStart)
				? s.SpawnAction(
					new ResolveEffectsAction
					{
						SourceId = sourceId,
						Trigger = EffectTrigger.OnTurnStart,
						Effects = effects,
					}
				)
				: s;
	}

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
				state = KinRng.ShuffleZone(state, drawId);

				top = state.GetChildrenIds(drawId).FirstOrDefault();
				if (top == 0)
					break;
			}

			state = state.MoveObject(top, handId);
		}

		return (state, events);
	}
}
