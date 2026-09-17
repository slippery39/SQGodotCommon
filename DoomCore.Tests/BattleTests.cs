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

	/// <summary>
	/// A unit that DIED earlier in the battle is a whole card again when it is replayed.
	///
	/// Dead units go to Discard and cycle back into the deck, and they used to take the damage that
	/// killed them with them. Replaying one put a unit with IsDead already true onto the Field,
	/// where `UnitInLane` skipped it: the card left your hand, the energy was spent, and the lane
	/// stayed empty. Found by PLAYING it — every state assertion in this suite passed.
	/// </summary>
	[Test]
	public void AUnitThatDiedEarlierComesBackWholeWhenItIsReplayed()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 9, opponentHealth: 500);
		(state, _) = AddEnemy(state, "Wretch", health: 20, attack: 5, lane: 0);
		(state, var unitId) = AddUnit(state, "Scavenger", power: 2, toughness: 2);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId, Lane = 0 });
		(state, _) = Do(state, new EndTurnAction());

		Assert.That(state.UnitInLane(0), Is.Null, "it should have died off the board");
		Assert.That(
			state.GetParent(unitId),
			Is.Not.EqualTo(state.ZoneId(ZoneType.Field)),
			"and left the Field — it goes to Discard, and the next draw may recycle it straight back"
		);

		// Into hand the way a reshuffle delivers it, then played into a free lane.
		state = state.MoveObject(unitId, state.ZoneId(ZoneType.Hand));
		(state, _) = Do(state, new PlayCardAction { CardId = unitId, Lane = 3 });

		var replayed = state.UnitInLane(3);
		Assert.That(replayed, Is.Not.Null, "the lane must actually be held — this is the bug");
		Assert.That(replayed!.Unit().Damage, Is.Zero, "it comes back whole");
		Assert.That(replayed.Unit().RemainingToughness, Is.EqualTo(2));
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

	/// <summary>
	/// **A lane is always playable, and the unit that was there is discarded. No refund.**
	///
	/// This test used to be `ALaneHoldsOneUnitAndRefusesASecond`. The refusal is superseded: a board
	/// you cannot play into is how five drawn units end a turn with End Turn as the only legal
	/// move, and under `Persistent` it would have been a lane denied for the whole battle. One unit
	/// per lane still holds — the second one replaces the first rather than stacking on it.
	///
	/// The cost is real and needs no penalty bolted on: you spent the energy and threw away a body
	/// that was absorbing damage.
	/// </summary>
	[Test]
	public void ALaneAcceptsASecondUnitAndDiscardsTheFirst()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 5, maxEnergy: 5);
		(state, var first) = AddUnit(state, "First", power: 1, toughness: 1);
		(state, var second) = AddUnit(state, "Second", power: 1, toughness: 1);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = first, Lane = 2 });
		(state, _) = Do(state, new PlayCardAction { CardId = second, Lane = 2 });

		Assert.That(state.UnitInLane(2)!.Id, Is.EqualTo(second), "the newcomer holds the lane");
		Assert.That(state.Units().Count(), Is.EqualTo(1), "one unit per lane, still");
		Assert.That(
			state.GetParent(first),
			Is.EqualTo(state.ZoneId(ZoneType.Discard)),
			"and the one it replaced is a card again, not a corpse"
		);
	}

	/// <summary>
	/// **Replaced is not dead**, the same way withdrawn is not. Overwriting your own unit must not
	/// fire its death trigger or feed a scenario that reads what died — otherwise replacing a body
	/// every turn would mint Zombies for units nothing ever killed.
	/// </summary>
	[Test]
	public void AReplacedUnitDidNotDie()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Zombie, countdown: 9, maxEnergy: 5);
		(state, var first) = AddUnit(state, "First", power: 1, toughness: 9);
		(state, var second) = AddUnit(state, "Second", power: 1, toughness: 9);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = first, Lane = 2 });
		(state, var events) = Do(state, new PlayCardAction { CardId = second, Lane = 2 });

		Assert.That(events.OfType<UnitDiedEvent>(), Is.Empty);
		Assert.That(state.GetBattle().DiedRunCardIds, Is.Empty);
		Assert.That(state.GetBattle().DiedThisTurnRunCardIds, Is.Empty);
	}

	/// <summary>
	/// The companion holds its lane against your own cards, and this is not a balance decision: it
	/// is not a card and has nowhere to be discarded TO. Discarding it would make it drawable, and
	/// it is meant to be the one thing that cannot be taken from you.
	///
	/// It can never produce a dead turn — the other four lanes are always open.
	/// </summary>
	[Test]
	public void YouCannotBuildOverYourOwnCompanion()
	{
		var run = new Run
		{
			Life = 100,
			MaxLife = 100,
			Companion = new Companion
			{
				Name = "Ash",
				BasePower = 1,
				BaseToughness = 5,
			},
		}.WithCards(
			[
				new RunCard
				{
					Name = "Pushy",
					Cost = 0,
					IsUnit = true,
					Power = 1,
					Toughness = 1,
				},
			]
		);

		var (state, _) = run.StartBattle(DoomScenario.Flood, countdown: 9, [], opponentHealth: 500);

		var companionLane = state.Units().Single().Unit().Lane;
		var card = state.CardsIn(ZoneType.Hand).First(c => c.Name == "Pushy");

		var (after, ok) = state.TryAddAction(
			new PlayCardAction { CardId = card.Id, Lane = companionLane }
		);

		Assert.That(ok, Is.False, "the companion is not something you may discard");
		Assert.That(after.CardsIn(ZoneType.Hand).Any(c => c.Id == card.Id), Is.True);

		// And every other lane still takes it, so the refusal costs the player nothing.
		var elsewhere = Enumerable.Range(0, DoomBattle.LaneCount).First(l => l != companionLane);
		Assert.That(
			state.TryAddAction(new PlayCardAction { CardId = card.Id, Lane = elsewhere }).Item2,
			Is.True
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

	/// <summary>
	/// A unit holds its lane for ONE turn and then leaves. **Combat v3.**
	///
	/// This test used to be `AUnitHoldsItsLaneAcrossTurns` and asserted the opposite. The rule is
	/// superseded (see DoomJam.md "Combat v3"): a permanent board fed by an ephemeral hand
	/// saturates, and then a drawn hand of units has nowhere to go and End Turn is the only legal
	/// move. Units are pieces you place each turn now, which is what makes every turn a decision.
	/// </summary>
	[Test]
	public void AUnitWithdrawsAtTheEndOfTheTurn()
	{
		var state = DoomBattleFactory.Create(DoomScenario.Flood, countdown: 5);
		(state, _) = AddEnemy(state, "Idler", health: 20, attack: 0, lane: 3);
		(state, var unitId) = AddUnit(state, "Runner", power: 2, toughness: 4);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId, Lane = 3 });

		Assert.That(state.UnitInLane(3)!.Id, Is.EqualTo(unitId), "it holds the lane this turn");

		(state, var events) = Do(state, new EndTurnAction());

		Assert.That(state.UnitInLane(3), Is.Null, "and the lane is empty again next turn");
		Assert.That(events.OfType<UnitsWithdrewEvent>().Single().Count, Is.EqualTo(1));

		// It is a card again, not a corpse — the deck keeps it and it can be drawn and replayed.
		Assert.That(state.HasObject(unitId), Is.True);
		Assert.That(state.GetParent(unitId), Is.Not.EqualTo(state.ZoneId(ZoneType.Field)));
	}

	/// <summary>
	/// **Withdrawn is not dead, and everything downstream depends on the difference.** A unit that
	/// walked off at the end of the turn fires no death trigger and is recorded as no death, so a
	/// scenario that reads what died is not paid for a board wiping itself every turn — and neither
	/// is a companion whose ability counts your losses.
	/// </summary>
	[Test]
	public void AWithdrawnUnitDidNotDie()
	{
		var state = DoomBattleFactory.Create(
			DoomScenario.Zombie,
			countdown: 9,
			opponentHealth: 500
		);
		(state, var unitId) = AddUnit(state, "Survivor", power: 1, toughness: 9);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId, Lane = 2 });
		(state, var events) = Do(state, new EndTurnAction());

		Assert.That(state.UnitInLane(2), Is.Null, "it withdrew");
		Assert.That(events.OfType<UnitDiedEvent>(), Is.Empty, "but nothing died");
		Assert.That(state.GetBattle().DiedRunCardIds, Is.Empty);
		Assert.That(state.GetBattle().DiedThisTurnRunCardIds, Is.Empty);
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
