using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// Resolves the turn: every lane trades, then deaths, then the countdown.
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

		(state, events) = ResolveLanes(state, events);
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

		// Checked AFTER the player's death, so a turn that kills both is still a loss — the run
		// ending outranks winning the battle. Checked BEFORE the doom, so killing the Opponent on
		// the same turn the countdown expires means you got out first and no apocalypse lands.
		if (state.GetOpponent().IsDead)
		{
			battle = state.GetBattle();
			state = state.UpdateObject(
				battle.Id,
				battle with
				{
					IsOver = true,
					OpponentDefeated = true,
				}
			);
			return new ActionResult(state).WithEvents(events.Add(new OpponentDefeatedEvent()));
		}

		// The doom fires and the battle CARRIES ON. Reaching zero resets the clock; it never ends a
		// battle. Only the Opponent's death or yours does.
		battle = state.GetBattle();
		if (remaining <= 0)
			state = state.SpawnAction(new ResolveDoomAction { TurnNumber = battle.TurnNumber });

		state = state
			.UpdateObject(battle.Id, battle with { TurnNumber = battle.TurnNumber + 1 })
			.SpawnAction(new StartTurnAction());

		return new ActionResult(state).WithEvents(events);
	}

	/// <summary>
	/// Every lane resolves on its own, automatically. No assignment, no targeting — a unit fights
	/// whatever shares its lane, and both sides hit at once.
	///
	/// **A unit absorbs up to its remaining toughness and the excess hits your face**, so a body in
	/// a lane is worth exactly its toughness in life. That is the same rule blocking used to
	/// enforce, and it is what keeps toughness and life the same currency — every doom scenario
	/// trades on that axis, so it must stay exact. An empty lane absorbs nothing: the enemy's whole
	/// attack lands on you.
	///
	/// Both sides deal damage. The old "blockers deal no damage" rule existed to keep attack-vs-block
	/// a clean either/or; with no such choice left there is nothing for it to protect.
	///
	/// Damage is dealt from the state as it was at the START of the exchange, so a unit and an enemy
	/// that kill each other both die. Resolving one lane before the other must never decide who
	/// swings first.
	/// </summary>
	private static (GameState, ImmutableList<GameEvent>) ResolveLanes(
		GameState state,
		ImmutableList<GameEvent> events
	)
	{
		for (var lane = 0; lane < DoomBattle.LaneCount; lane++)
		{
			var enemy = state.EnemyInLane(lane);
			var card = state.UnitInLane(lane);

			// A lane you hold with nothing opposing it lands on the Opponent. This is the only way
			// to win a battle, which is what makes holding a lane offence as well as defence.
			if (enemy is null)
			{
				if (card is { } unopposed && unopposed.Unit().Power > 0)
					(state, events) = DamageOpponent(state, events, unopposed.Unit().Power);
				continue;
			}

			var attack = enemy.Intent == IntentKind.Attack ? enemy.IntentAmount : 0;

			if (card is null)
			{
				if (attack > 0)
					(state, events) = DamagePlayer(state, events, attack, absorbed: 0);
				continue;
			}

			var unit = card.Unit();

			// Read both sides first: the unit's power must not depend on damage it is taking in
			// this same exchange, or whoever resolves second is silently weaker.
			state = state.UpdateObject(enemy.Id, enemy with { Health = enemy.Health - unit.Power });

			if (attack <= 0)
				continue;

			var soak = Math.Min(attack, unit.RemainingToughness);
			state = state.UpdateObject(
				card.Id,
				card.WithComponentReplaced(unit with { Damage = unit.Damage + soak })
			);

			var excess = attack - soak;
			if (excess > 0)
				(state, events) = DamagePlayer(state, events, excess, absorbed: soak);
		}

		return (state, events);
	}

	private static (GameState, ImmutableList<GameEvent>) DamageOpponent(
		GameState state,
		ImmutableList<GameEvent> events,
		int amount
	)
	{
		var opponent = state.GetOpponent();
		var health = opponent.Health - amount;
		state = state.UpdateObject(opponent.Id, opponent with { Health = health });

		return (
			state,
			events.Add(new OpponentDamagedEvent { Amount = amount, HealthRemaining = health })
		);
	}

	private static (GameState, ImmutableList<GameEvent>) DamagePlayer(
		GameState state,
		ImmutableList<GameEvent> events,
		int amount,
		int absorbed
	)
	{
		var player = state.GetPlayer();
		var life = player.Life - amount;
		state = state.UpdateObject(player.Id, player with { Life = life });

		return (
			state,
			events.Add(
				new PlayerDamagedEvent
				{
					Amount = amount,
					Absorbed = absorbed,
					LifeRemaining = life,
				}
			)
		);
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

			// The companion is not a card. It leaves the battle outright rather than going to
			// Discard (it would be drawable), and its death is NOT a deck event — letting it feed
			// Zombie would mint a free card every time it chump-blocked. It returns next battle.
			if (card.HasComponent<CompanionComponent>())
			{
				state = state.RemoveObject(card.Id);
				continue;
			}

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
