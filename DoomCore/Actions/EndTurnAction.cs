using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// Resolves the turn: your attacks, then theirs, then deaths, then the countdown.
///
/// **The countdown ticks here unconditionally.** Nothing in this action can stop it, and nothing
/// should ever be added that can. Clearing the enemies does not end a battle early — if it did,
/// the player would have beaten the apocalypse and it would be an obstacle rather than doom.
/// </summary>
public record EndTurnAction : GameAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		state = ResolvePlayerAttacks(state);
		(state, events) = ResolveEnemyAttacks(state, events);
		(state, events) = ClearTheDead(state, events);

		state = DiscardHand(state);

		var battle = state.GetBattle();
		var remaining = battle.CountdownRemaining - 1;
		state = state.UpdateObject(battle.Id, battle with { CountdownRemaining = remaining });
		events = events.Add(new CountdownTickedEvent { Remaining = remaining });

		// Death is checked after the tick so a player killed on the same turn the doom lands still
		// loses — the run ending outranks the battle ending.
		if (state.GetPlayer().Life <= 0)
		{
			battle = state.GetBattle();
			state = state.UpdateObject(
				battle.Id,
				battle with
				{
					IsOver = true,
					PlayerIsDead = true,
				}
			);
			return new ActionResult(state).WithEvents(events.Add(new PlayerDiedEvent()));
		}

		if (remaining <= 0)
			state = state.SpawnAction(new ResolveDoomAction());
		else
		{
			battle = state.GetBattle();
			state = state
				.UpdateObject(battle.Id, battle with { TurnNumber = battle.TurnNumber + 1 })
				.SpawnAction(new StartTurnAction());
		}

		return new ActionResult(state).WithEvents(events);
	}

	private static GameState ResolvePlayerAttacks(GameState state)
	{
		foreach (var card in state.Units().ToList())
		{
			var unit = card.Unit();
			if (unit.Assignment != Assignment.Attack || unit.IsDead)
				continue;

			if (state.GetObject(unit.AssignedEnemyId) is not Enemy enemy || enemy.IsDead)
				continue;

			state = state.UpdateObject(enemy.Id, enemy with { Health = enemy.Health - unit.Power });
		}

		return state;
	}

	/// <summary>
	/// **Blocking reduces damage, it never prevents it.** Blockers absorb up to their remaining
	/// toughness and the excess hits the player, so a body in front of an attack is worth exactly
	/// its toughness in life. That makes stalling impossible by construction and needs no keyword.
	///
	/// **Blockers deal no damage** — pure absorption. It keeps attack-vs-block a clean either/or.
	/// </summary>
	private static (GameState, ImmutableList<GameEvent>) ResolveEnemyAttacks(
		GameState state,
		ImmutableList<GameEvent> events
	)
	{
		foreach (var enemy in state.LivingEnemies().ToList())
		{
			if (enemy.Intent != IntentKind.Attack)
				continue;

			var incoming = enemy.IntentAmount;
			var absorbed = 0;

			foreach (var card in state.Units().ToList())
			{
				if (incoming <= 0)
					break;

				var unit = card.Unit();
				if (
					unit.Assignment != Assignment.Block
					|| unit.AssignedEnemyId != enemy.Id
					|| unit.IsDead
				)
					continue;

				var soak = Math.Min(incoming, unit.RemainingToughness);
				incoming -= soak;
				absorbed += soak;

				state = state.UpdateObject(
					card.Id,
					card.WithComponentReplaced(unit with { Damage = unit.Damage + soak })
				);
			}

			if (incoming <= 0)
				continue;

			var player = state.GetPlayer();
			var life = player.Life - incoming;
			state = state.UpdateObject(player.Id, player with { Life = life });
			events = events.Add(
				new PlayerDamagedEvent
				{
					Amount = incoming,
					Absorbed = absorbed,
					LifeRemaining = life,
				}
			);
		}

		return (state, events);
	}

	/// <summary>
	/// Dead units go to Discard so they are still in the run deck — a unit dying in a battle does
	/// not remove it from your deck. Only a doom transform can do that.
	/// </summary>
	private static (GameState, ImmutableList<GameEvent>) ClearTheDead(
		GameState state,
		ImmutableList<GameEvent> events
	)
	{
		foreach (var card in state.Units().ToList())
		{
			if (!card.Unit().IsDead)
				continue;

			events = events.Add(
				new UnitDiedEvent
				{
					CardId = card.Id,
					RunCardId = card.RunCardId,
					CardName = card.Name,
				}
			);
			state = state.MoveObject(card.Id, state.ZoneId(ZoneType.Discard));

			var battle = state.GetBattle();
			state = state.UpdateObject(
				battle.Id,
				battle with
				{
					DiedRunCardIds = battle.DiedRunCardIds.Add(card.RunCardId),
				}
			);
		}

		foreach (
			var enemy in state.GetChildren(state.ZoneId(ZoneType.Enemies)).OfType<Enemy>().ToList()
		)
		{
			if (enemy.IsDead && !enemy.GetMeta("Mourned", false))
			{
				events = events.Add(
					new EnemyDiedEvent { EnemyId = enemy.Id, EnemyName = enemy.Name }
				);
				state = state.UpdateObject(enemy.Id, (Enemy)enemy.WithMeta("Mourned", true));
			}
		}

		return (state, events);
	}

	private static GameState DiscardHand(GameState state)
	{
		var discardId = state.ZoneId(ZoneType.Discard);
		foreach (var id in state.GetChildrenIds(state.ZoneId(ZoneType.Hand)).ToList())
			state = state.MoveObject(id, discardId);

		return state;
	}
}
