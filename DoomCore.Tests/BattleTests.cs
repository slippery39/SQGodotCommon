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
		ZoneType zone = ZoneType.Hand
	)
	{
		var card = new DoomCard
		{
			Name = name,
			Cost = cost,
			RunCardId = 0,
		};
		card = (DoomCard)
			card.WithComponent(new UnitComponent { Power = power, Toughness = toughness });
		var (s, added) = state.AddObject(card, state.ZoneId(zone));
		return (s, added.Id);
	}

	private static (GameState, int) AddEnemy(GameState state, string name, int health, int attack)
	{
		var (s, e) = state.AddObject(
			new Enemy
			{
				Name = name,
				Health = health,
				MaxHealth = health,
				Intent = attack > 0 ? IntentKind.Attack : IntentKind.Wait,
				IntentAmount = attack,
			},
			state.ZoneId(ZoneType.Enemies)
		);
		return (s, e.Id);
	}

	// ===== The rule that must not bend =====

	[Test]
	public void TheDoomResolvesEvenWhenEveryEnemyIsAlreadyDead()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 3);
		(state, var enemyId) = AddEnemy(state, "Husk", health: 1, attack: 0);
		(state, var unitId) = AddUnit(state, "Stray", power: 9, toughness: 1);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId });
		(state, _) = Do(
			state,
			new AssignAction
			{
				UnitId = unitId,
				EnemyId = enemyId,
				Assignment = Assignment.Attack,
			}
		);

		(state, _) = Do(state, new EndTurnAction());
		Assert.That(state.LivingEnemies().Count(), Is.Zero, "enemy should be dead");
		Assert.That(
			state.GetBattle().IsOver,
			Is.False,
			"clearing the board must NOT end the battle"
		);

		(state, _) = Do(state, new EndTurnAction());
		Assert.That(state.GetBattle().IsOver, Is.False);

		var (final, events) = Do(state, new EndTurnAction());
		Assert.That(final.GetBattle().IsOver, Is.True, "the doom must land on schedule regardless");
		Assert.That(
			events.OfType<DoomResolvedEvent>().Single().Scenario,
			Is.EqualTo(DoomScenario.Flood)
		);
	}

	// ===== Blocking is absorption, not prevention =====

	[Test]
	public void ABlockerAbsorbsItsToughnessAndTheExcessHitsThePlayer()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Nuclear, countdown: 5, life: 20);
		(state, var enemyId) = AddEnemy(state, "Leviathan", health: 20, attack: 5);
		(state, var chump) = AddUnit(state, "Chump", power: 0, toughness: 1);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = chump });
		(state, _) = Do(
			state,
			new AssignAction
			{
				UnitId = chump,
				EnemyId = enemyId,
				Assignment = Assignment.Block,
			}
		);

		(state, var events) = Do(state, new EndTurnAction());

		// 5 damage, 1 absorbed, 4 through. Toughness is life, exactly.
		Assert.That(state.GetPlayer().Life, Is.EqualTo(16));

		var dmg = events.OfType<PlayerDamagedEvent>().Single();
		Assert.That(dmg.Absorbed, Is.EqualTo(1));
		Assert.That(dmg.Amount, Is.EqualTo(4));
	}

	[Test]
	public void TwoBlockersAbsorbTheirCombinedToughness()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Nuclear, countdown: 5, life: 20);
		(state, var enemyId) = AddEnemy(state, "Leviathan", health: 20, attack: 5);
		(state, var a) = AddUnit(state, "Wall A", power: 0, toughness: 1);
		(state, var b) = AddUnit(state, "Wall B", power: 0, toughness: 3);

		(state, _) = state.BeginBattle();
		foreach (var id in new[] { a, b })
		{
			(state, _) = Do(state, new PlayCardAction { CardId = id });
			(state, _) = Do(
				state,
				new AssignAction
				{
					UnitId = id,
					EnemyId = enemyId,
					Assignment = Assignment.Block,
				}
			);
		}

		(state, _) = Do(state, new EndTurnAction());

		Assert.That(state.GetPlayer().Life, Is.EqualTo(19), "1 + 3 absorbed, 1 through");
	}

	[Test]
	public void ABlockerDealsNoDamageToTheEnemyItBlocks()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Nuclear, countdown: 5);
		(state, var enemyId) = AddEnemy(state, "Leviathan", health: 20, attack: 1);
		(state, var unitId) = AddUnit(state, "Bruiser", power: 7, toughness: 7);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId });
		(state, _) = Do(
			state,
			new AssignAction
			{
				UnitId = unitId,
				EnemyId = enemyId,
				Assignment = Assignment.Block,
			}
		);

		(state, _) = Do(state, new EndTurnAction());

		var enemy = (Enemy)state.GetObject(enemyId);
		Assert.That(enemy.Health, Is.EqualTo(20), "blocking is pure absorption");
	}

	[Test]
	public void AUnitDoesOneThingPerTurn_AssigningBlockReplacesAttack()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Nuclear, countdown: 5);
		(state, var enemyId) = AddEnemy(state, "Leviathan", health: 20, attack: 2);
		(state, var unitId) = AddUnit(state, "Bruiser", power: 7, toughness: 7);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId });
		(state, _) = Do(
			state,
			new AssignAction
			{
				UnitId = unitId,
				EnemyId = enemyId,
				Assignment = Assignment.Attack,
			}
		);
		(state, _) = Do(
			state,
			new AssignAction
			{
				UnitId = unitId,
				EnemyId = enemyId,
				Assignment = Assignment.Block,
			}
		);

		(state, _) = Do(state, new EndTurnAction());

		var enemy = (Enemy)state.GetObject(enemyId);
		Assert.That(enemy.Health, Is.EqualTo(20), "it blocked, so it did not attack");
		Assert.That(state.GetPlayer().Life, Is.EqualTo(60), "and it absorbed the whole 2");
	}

	// ===== Economy and upkeep =====

	[Test]
	public void ACardCannotBePlayedWithoutEnoughEnergy()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 5, maxEnergy: 3);
		(state, var expensive) = AddUnit(state, "Colossus", power: 9, toughness: 9, cost: 4);

		(state, _) = state.BeginBattle();
		var (after, ok) = state.TryAddAction(new PlayCardAction { CardId = expensive });

		Assert.That(ok, Is.False);
		Assert.That(after.CardsIn(ZoneType.Hand).Any(c => c.Id == expensive), Is.True);
	}

	[Test]
	public void ADeadUnitGoesToDiscardSoItStaysInTheDeck()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Zombie, countdown: 5);
		(state, var enemyId) = AddEnemy(state, "Crusher", health: 20, attack: 9);
		(state, var unitId) = AddUnit(state, "Fragile", power: 1, toughness: 2);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId });
		(state, _) = Do(
			state,
			new AssignAction
			{
				UnitId = unitId,
				EnemyId = enemyId,
				Assignment = Assignment.Block,
			}
		);

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
		(state, _) = AddEnemy(state, "Executioner", health: 20, attack: 4);

		(state, _) = state.BeginBattle();
		(state, var events) = Do(state, new EndTurnAction());

		Assert.That(state.GetPlayer().Life, Is.LessThanOrEqualTo(0));
		Assert.That(state.GetBattle().PlayerIsDead, Is.True);
		Assert.That(state.GetBattle().IsOver, Is.True);
		Assert.That(events.OfType<PlayerDiedEvent>().Any(), Is.True);
	}

	[Test]
	public void AssignmentsAreClearedAtTheStartOfEachTurn()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 5);
		(state, var enemyId) = AddEnemy(state, "Idler", health: 20, attack: 0);
		(state, var unitId) = AddUnit(state, "Runner", power: 2, toughness: 4);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId });
		(state, _) = Do(
			state,
			new AssignAction
			{
				UnitId = unitId,
				EnemyId = enemyId,
				Assignment = Assignment.Attack,
			}
		);
		(state, _) = Do(state, new EndTurnAction());

		var unit = ((DoomCard)state.GetObject(unitId)).Unit();
		Assert.That(unit.Assignment, Is.EqualTo(Assignment.None));
		Assert.That(unit.AssignedEnemyId, Is.Zero);
	}
}
