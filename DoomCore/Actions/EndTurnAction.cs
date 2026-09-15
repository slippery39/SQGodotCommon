using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// Resolves the turn: every lane trades, the dead clear, the Opponent reinforces, the clock ticks.
///
/// **The clock ticks here unconditionally, and reaching zero does not end anything** — it fires the
/// apocalypse and resets. A battle ends only when the Opponent dies or the player does.
///
/// The ordering in `Execute` is load-bearing and is not arbitrary:
/// player death, then the Opponent's, then the doom. The player's loss outranks winning the battle,
/// and the Opponent's death is checked BEFORE the doom, which is what makes an apocalypse
/// DODGEABLE — kill them on the turn the countdown expires and nothing lands. See DoomJam.md;
/// inevitability is per-battle content now, not a rule this action enforces.
/// </summary>
public record EndTurnAction : GameAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		(state, events) = ResolveLanes(state, events);
		(state, events) = ClearTheDead(state, events);
		(state, events) = RefreshTheOpponentsLine(state, events);

		// Spawned BEFORE the doom is spawned below, so end-of-turn effects resolve before the
		// apocalypse does. The spawn queue is FIFO, so the order things are queued here is the
		// order they run.
		state = FireTriggers(state, EffectTrigger.OnTurnEnd);

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
		{
			state = state.SpawnAction(new ResolveDoomAction { TurnNumber = battle.TurnNumber });

			// AFTER the apocalypse, so anything reacting to it sees the board it left behind.
			state = FireTriggers(state, EffectTrigger.OnDoomFires);
		}

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

	/// <summary>
	/// The Opponent puts a body back in the line, and announces the next one.
	///
	/// **Runs AFTER the dead are cleared**, so it can see the lane you just opened — but what it
	/// places was telegraphed a turn ago, and what it announces now lands a turn from now. You
	/// always get one full turn to shoot through a hole you made. Without that delay a lane closes
	/// the instant it opens and the Opponent is unreachable.
	///
	/// The lane is the lowest free one, deliberately: predictable, and certainty is permission to
	/// show the player everything. Randomising it would buy surprise in a game whose whole tension
	/// is inevitability.
	/// </summary>
	private static (GameState, ImmutableList<GameEvent>) RefreshTheOpponentsLine(
		GameState state,
		ImmutableList<GameEvent> events
	)
	{
		var opponent = state.GetOpponent();
		if (opponent.IsDead)
			return (state, events);

		// What it announced last turn arrives now.
		if (opponent.NextSummon is { } due && state.EnemyInLane(due.Lane) is null)
		{
			(state, _) = state.AddObject(due.ToEnemy(), state.ZoneId(ZoneType.Enemies));
			events = events.Add(
				new EnemySummonedEvent
				{
					EnemyName = due.Name,
					Lane = due.Lane,
					Attack = due.Attack,
				}
			);

			opponent = state.GetOpponent() with { NextSummon = null };
			state = state.UpdateObject(opponent.Id, opponent);
		}

		var turns = opponent.TurnsUntilSummon - 1;

		// Nothing to announce while its line is full — the rate limit is on summoning, not waiting.
		var lane = Enumerable
			.Range(0, DoomBattle.LaneCount)
			.Where(l => state.EnemyInLane(l) is null)
			.Select(l => (int?)l)
			.FirstOrDefault();

		if (turns > 0 || opponent.NextSummon is not null || lane is null)
			return (
				state.UpdateObject(
					opponent.Id,
					opponent with
					{
						TurnsUntilSummon = Math.Max(turns, 0),
					}
				),
				events
			);

		var next = StarterContent.SummonFor(state.GetBattle().TurnNumber, lane.Value);
		state = state.UpdateObject(
			opponent.Id,
			opponent with
			{
				NextSummon = next,
				TurnsUntilSummon = opponent.SummonInterval,
			}
		);

		return (
			state,
			events.Add(new EnemyTelegraphedEvent { EnemyName = next.Name, Lane = next.Lane })
		);
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

			state = FireEffects(state, card.Id, EffectTrigger.OnDeath, card.Effects);
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

				// Safe because a dead enemy is MARKED, not removed — it is still in the zone when
				// the spawned effect resolves, so "self" and "my lane" still mean something.
				state = FireEffects(state, enemy.Id, EffectTrigger.OnDeath, enemy.Effects);
			}
		}

		return (state, events);
	}

	/// <summary>
	/// Fires one trigger for everything on the board that carries effects — enemies, your units and
	/// the Opponent.
	///
	/// **Deliberately not card-only.** A `DoomEffect` does not know what holds it, so the same
	/// `DealDamageAction` serves a rite, a dying enemy and an Opponent that bleeds you every turn.
	/// </summary>
	private static GameState FireTriggers(GameState state, EffectTrigger trigger)
	{
		foreach (var enemy in state.LivingEnemies().ToList())
			state = FireEffects(state, enemy.Id, trigger, enemy.Effects);

		foreach (var unit in state.Units().Where(u => !u.Unit().IsDead).ToList())
			state = FireEffects(state, unit.Id, trigger, unit.Effects);

		var opponent = state.GetOpponent();
		return FireEffects(state, opponent.Id, trigger, opponent.Effects);
	}

	/// <summary>
	/// Queues one holder's effects for a trigger, and queues NOTHING when it has none for it — an
	/// empty ResolveEffectsAction per object per turn would be noise in every action log.
	/// </summary>
	private static GameState FireEffects(
		GameState state,
		int sourceId,
		EffectTrigger trigger,
		ImmutableList<DoomEffect> effects
	)
	{
		if (!effects.Any(e => e.Trigger == trigger))
			return state;

		return state.SpawnAction(
			new ResolveEffectsAction
			{
				SourceId = sourceId,
				Trigger = trigger,
				Effects = effects,
			}
		);
	}

	private static GameState DiscardHand(GameState state)
	{
		var discardId = state.ZoneId(ZoneType.Discard);
		foreach (var id in state.GetChildrenIds(state.ZoneId(ZoneType.Hand)).ToList())
			state = state.MoveObject(id, discardId);

		return state;
	}
}
