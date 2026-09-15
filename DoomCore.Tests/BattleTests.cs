using System.Collections.Immutable;
using DoomCore;
using ImmutableGameObjects;

namespace DoomCore.Tests;

/// <summary>
/// Cards are defined INLINE here, never loaded from a library — balance changes to real cards must
/// not break tests about mechanics.
/// </summary>
public class BattleTests
{
	private static (GameState State, ImmutableList<GameEvent> Events) Do(
		GameState state,
		GameAction action
	) => state.AddAction(action).ProcessAllActions();

	private static (GameState, int) AddUnit(
		GameState state,
		string name,
		int power,
		int toughness,
		int cost = 1,
		ZoneType zone = ZoneType.Hand,
		int lane = 0
	)
	{
		var card = new DoomCard
		{
			Name = name,
			Cost = cost,
			RunCardId = 0,
		};
		card = (DoomCard)
			card.WithComponent(
				new UnitComponent
				{
					Power = power,
					Toughness = toughness,
					Lane = lane,
				}
			);
		var (s, added) = state.AddObject(card, state.ZoneId(zone));
		return (s, added.Id);
	}

	private static (GameState, int) AddEnemy(
		GameState state,
		string name,
		int health,
		int attack,
		int lane = 0
	)
	{
		var (s, e) = state.AddObject(
			new Enemy
			{
				Name = name,
				Health = health,
				MaxHealth = health,
				Intent = attack > 0 ? IntentKind.Attack : IntentKind.Wait,
				IntentAmount = attack,
				Lane = lane,
			},
			state.ZoneId(ZoneType.Enemies)
		);
		return (s, e.Id);
	}

	// ===== The rule that must not bend =====

	/// <summary>
	/// The doom is a METRONOME, not a wall. It fires on schedule no matter what the board looks
	/// like, resets its own clock, and the battle carries on.
	///
	/// This test used to assert the opposite — that the countdown ENDED the battle. That rule is
	/// superseded (see DoomJam.md): it made a cleared board into dead air.
	/// </summary>
	[Test]
	public void TheDoomFiresOnScheduleAndTheBattleCarriesOn()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 2, opponentHealth: 500);
		(state, _) = AddEnemy(state, "Husk", health: 1, attack: 0, lane: 0);
		(state, var unitId) = AddUnit(state, "Stray", power: 9, toughness: 1);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId, Lane = 0 });

		(state, _) = Do(state, new EndTurnAction());
		Assert.That(state.LivingEnemies().Count(), Is.Zero, "enemy should be dead");
		Assert.That(state.GetBattle().DoomsFired, Is.Zero, "not yet");

		var (afterFirst, events) = Do(state, new EndTurnAction());
		state = afterFirst;

		Assert.That(state.GetBattle().DoomsFired, Is.EqualTo(1), "the doom landed");
		Assert.That(events.OfType<DoomResolvedEvent>().Single().FiringNumber, Is.EqualTo(1));
		Assert.That(
			state.GetBattle().IsOver,
			Is.False,
			"and the battle CARRIES ON — nothing ends on a counter"
		);
		Assert.That(
			state.GetBattle().CountdownRemaining,
			Is.EqualTo(2),
			"the clock reset rather than stopping"
		);

		// And it keeps happening.
		(state, _) = Do(state, new EndTurnAction());
		(state, _) = Do(state, new EndTurnAction());

		Assert.That(state.GetBattle().DoomsFired, Is.EqualTo(2));
		Assert.That(state.GetBattle().Firings, Has.Count.EqualTo(2));
	}

	// ===== Toughness is life =====

	[Test]
	public void AUnitAbsorbsItsToughnessAndTheExcessHitsThePlayer()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Nuclear, countdown: 5, life: 20);
		(state, _) = AddEnemy(state, "Leviathan", health: 20, attack: 5, lane: 0);
		(state, var chump) = AddUnit(state, "Chump", power: 0, toughness: 1);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = chump, Lane = 0 });

		(state, var events) = Do(state, new EndTurnAction());

		// 5 damage, 1 absorbed, 4 through. Toughness is life, exactly.
		Assert.That(state.GetPlayer().Life, Is.EqualTo(16));

		var dmg = events.OfType<PlayerDamagedEvent>().Single();
		Assert.That(dmg.Absorbed, Is.EqualTo(1));
		Assert.That(dmg.Amount, Is.EqualTo(4));
	}

	[Test]
	public void AnOpenLaneCostsYouTheEnemysWholeAttack()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Nuclear, countdown: 5, life: 20);
		(state, _) = AddEnemy(state, "Leviathan", health: 20, attack: 5, lane: 3);
		(state, var unitId) = AddUnit(state, "Elsewhere", power: 9, toughness: 9);

		(state, _) = state.BeginBattle();

		// Holding a DIFFERENT lane does nothing about lane 3. No global blocking.
		(state, _) = Do(state, new PlayCardAction { CardId = unitId, Lane = 0 });
		(state, var events) = Do(state, new EndTurnAction());

		Assert.That(state.GetPlayer().Life, Is.EqualTo(15), "nothing absorbed it");
		Assert.That(events.OfType<PlayerDamagedEvent>().Single().Absorbed, Is.Zero);
	}

	// ===== Lanes are independent, and both sides swing =====

	[Test]
	public void AUnitDamagesTheEnemyInItsLaneWhileAbsorbingItsAttack()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Nuclear, countdown: 5);
		(state, var enemyId) = AddEnemy(state, "Leviathan", health: 20, attack: 1, lane: 0);
		(state, var unitId) = AddUnit(state, "Bruiser", power: 7, toughness: 7);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId, Lane = 0 });
		(state, _) = Do(state, new EndTurnAction());

		var enemy = (Enemy)state.GetObject(enemyId);
		Assert.That(enemy.Health, Is.EqualTo(13), "combat is automatic and goes both ways");

		var unit = ((DoomCard)state.GetObject(unitId)).Unit();
		Assert.That(unit.Damage, Is.EqualTo(1), "and it took the hit in the same exchange");
	}

	[Test]
	public void AUnitOnlyFightsTheEnemySharingItsLane()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Nuclear, countdown: 5);
		(state, var enemyId) = AddEnemy(state, "Bystander", health: 20, attack: 0, lane: 4);
		(state, var unitId) = AddUnit(state, "Bruiser", power: 7, toughness: 7);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId, Lane = 1 });
		(state, _) = Do(state, new EndTurnAction());

		var enemy = (Enemy)state.GetObject(enemyId);
		Assert.That(enemy.Health, Is.EqualTo(20), "a unit cannot reach across lanes");
	}

	[Test]
	public void EveryLaneResolvesOnItsOwn()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Nuclear, countdown: 5, life: 20);
		(state, var left) = AddEnemy(state, "Left", health: 10, attack: 2, lane: 0);
		(state, var right) = AddEnemy(state, "Right", health: 10, attack: 3, lane: 1);
		(state, _) = AddEnemy(state, "Loose", health: 10, attack: 4, lane: 2);

		(state, var a) = AddUnit(state, "Guard A", power: 3, toughness: 5);
		(state, var b) = AddUnit(state, "Guard B", power: 4, toughness: 5);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = a, Lane = 0 });
		(state, _) = Do(state, new PlayCardAction { CardId = b, Lane = 1 });
		(state, _) = Do(state, new EndTurnAction());

		Assert.That(((Enemy)state.GetObject(left)).Health, Is.EqualTo(7));
		Assert.That(((Enemy)state.GetObject(right)).Health, Is.EqualTo(6));

		// Lanes 0 and 1 fully absorbed theirs; only lane 2 got through.
		Assert.That(state.GetPlayer().Life, Is.EqualTo(16));
	}

	/// <summary>
	/// Both sides are read before either is written, so resolving one lane before another never
	/// decides who swings first. Without that, whoever is processed second is silently weaker.
	/// </summary>
	[Test]
	public void AUnitAndAnEnemyThatKillEachOtherBothDie()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Zombie, countdown: 5);
		(state, var enemyId) = AddEnemy(state, "Twin", health: 3, attack: 3, lane: 0);
		(state, var unitId) = AddUnit(state, "Twin", power: 3, toughness: 3);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId, Lane = 0 });
		(state, var events) = Do(state, new EndTurnAction());

		Assert.That(((Enemy)state.GetObject(enemyId)).IsDead, Is.True, "it took the full 3");
		Assert.That(events.OfType<UnitDiedEvent>().Any(e => e.CardId == unitId), Is.True);
		Assert.That(state.GetPlayer().Life, Is.EqualTo(60), "3 absorbed of 3, nothing through");
	}

	// ===== One unit per lane =====

	[Test]
	public void ALaneHoldsOneUnitAndRefusesASecond()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 5, maxEnergy: 5);
		(state, var first) = AddUnit(state, "First", power: 1, toughness: 1);
		(state, var second) = AddUnit(state, "Second", power: 1, toughness: 1);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = first, Lane = 2 });

		var (after, ok) = state.TryAddAction(new PlayCardAction { CardId = second, Lane = 2 });

		Assert.That(ok, Is.False, "stacking would make the matchup unreadable");
		Assert.That(
			after.CardsIn(ZoneType.Hand).Any(c => c.Id == second),
			Is.True,
			"still in hand"
		);
	}

	[Test]
	public void ALaneOutsideTheBoardIsRefused()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 5);
		(state, var unitId) = AddUnit(state, "Stray", power: 1, toughness: 1);

		(state, _) = state.BeginBattle();

		Assert.That(
			state
				.TryAddAction(new PlayCardAction { CardId = unitId, Lane = DoomBattle.LaneCount })
				.Item2,
			Is.False
		);
		Assert.That(
			state.TryAddAction(new PlayCardAction { CardId = unitId, Lane = -1 }).Item2,
			Is.False
		);
	}

	[Test]
	public void AUnitHoldsItsLaneAcrossTurns()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 5);
		(state, _) = AddEnemy(state, "Idler", health: 20, attack: 0, lane: 3);
		(state, var unitId) = AddUnit(state, "Runner", power: 2, toughness: 4);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId, Lane = 3 });
		(state, _) = Do(state, new EndTurnAction());

		var unit = ((DoomCard)state.GetObject(unitId)).Unit();
		Assert.That(unit.Lane, Is.EqualTo(3), "a lane is chosen once, not re-picked every turn");
		Assert.That(state.UnitInLane(3)!.Id, Is.EqualTo(unitId));
	}

	// ===== Economy and upkeep =====

	[Test]
	public void ACardCannotBePlayedWithoutEnoughEnergy()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 5, maxEnergy: 3);
		(state, var expensive) = AddUnit(state, "Colossus", power: 9, toughness: 9, cost: 4);

		(state, _) = state.BeginBattle();
		var (after, ok) = state.TryAddAction(new PlayCardAction { CardId = expensive, Lane = 0 });

		Assert.That(ok, Is.False);
		Assert.That(after.CardsIn(ZoneType.Hand).Any(c => c.Id == expensive), Is.True);
	}

	[Test]
	public void ADeadUnitGoesToDiscardSoItStaysInTheDeck()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Zombie, countdown: 5);
		(state, _) = AddEnemy(state, "Crusher", health: 20, attack: 9, lane: 0);
		(state, var unitId) = AddUnit(state, "Fragile", power: 1, toughness: 2);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId, Lane = 0 });

		(state, var events) = Do(state, new EndTurnAction());

		Assert.That(events.OfType<UnitDiedEvent>().Any(e => e.CardId == unitId), Is.True);
		Assert.That(state.Units().Any(u => u.Id == unitId), Is.False, "it left the field");

		// It is NOT gone from the game — it went to Discard, and by the time we look the new turn
		// has already reshuffled Discard into Draw and may have drawn it again. That cycling is
		// the point: only a doom transform can remove a card from the run.
		Assert.That(state.HasObject(unitId), Is.True);
		Assert.That(state.GetParent(unitId), Is.Not.EqualTo(state.ZoneId(ZoneType.Field)));

		Assert.That(state.GetPlayer().Life, Is.EqualTo(53), "2 absorbed of 9");
	}

	[Test]
	public void ADeadUnitLeavesItsLaneOpenAgain()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Zombie, countdown: 5, maxEnergy: 5);
		(state, _) = AddEnemy(state, "Crusher", health: 20, attack: 9, lane: 1);
		(state, var doomed) = AddUnit(state, "Fragile", power: 1, toughness: 2);
		(state, var next) = AddUnit(state, "Relief", power: 1, toughness: 2);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = doomed, Lane = 1 });
		(state, _) = Do(state, new EndTurnAction());

		Assert.That(state.UnitInLane(1), Is.Null, "the hole it left is the pressure");

		// The replacement is drawable again from Discard, so play a fresh one into the same lane.
		(state, var ok) = state.TryAddAction(new PlayCardAction { CardId = next, Lane = 1 });
		Assert.That(ok, Is.True, "the lane reopened");
	}

	[Test]
	public void TheHandIsDiscardedAtTheEndOfEveryTurn()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 5);
		for (var i = 0; i < 8; i++)
			(state, _) = AddUnit(state, $"Filler {i}", 1, 1, cost: 1, zone: ZoneType.Draw);

		(state, _) = state.BeginBattle();
		Assert.That(state.CardsIn(ZoneType.Hand).Count(), Is.EqualTo(5));

		(state, _) = Do(state, new EndTurnAction());

		Assert.That(
			state.CardsIn(ZoneType.Hand).Count(),
			Is.EqualTo(5),
			"a fresh hand for the new turn"
		);
		Assert.That(state.GetBattle().TurnNumber, Is.EqualTo(2));
	}

	[Test]
	public void ThePlayerDiesAtZeroLifeAndTheRunEnds()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Nuclear, countdown: 5, life: 4);
		(state, _) = AddEnemy(state, "Executioner", health: 20, attack: 4, lane: 0);

		(state, _) = state.BeginBattle();
		(state, var events) = Do(state, new EndTurnAction());

		Assert.That(state.GetPlayer().Life, Is.LessThanOrEqualTo(0));
		Assert.That(state.GetBattle().PlayerIsDead, Is.True);
		Assert.That(state.GetBattle().IsOver, Is.True);
		Assert.That(events.OfType<PlayerDiedEvent>().Any(), Is.True);
	}
}
