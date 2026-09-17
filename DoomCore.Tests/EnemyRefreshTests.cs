using System.Collections.Immutable;
using DoomCore;
using ImmutableGameObjects;

namespace DoomCore.Tests;

/// <summary>
/// The Opponent puts bodies back in its line, telegraphed a turn ahead.
///
/// Without this a battle is won by out-tempoing the opening board once and the doom never gets to
/// matter. With it refilling too fast, the Opponent is unreachable. These pin both edges.
/// </summary>
public class EnemyRefreshTests
{
	private static (GameState State, ImmutableList<GameEvent> Events) Do(
		GameState state,
		GameAction action
	) => state.AddAction(action).ProcessAllActions();

	private static GameState Battle(int opponentHealth = 500, int summonInterval = 2)
	{
		var state = DoomBattleFactory.Create(
			DoomScenario.Flood,
			countdown: 99,
			life: 500,
			opponentHealth: opponentHealth
		);

		var opponent = state.GetOpponent();
		state = state.UpdateObject(opponent.Id, opponent with { SummonInterval = summonInterval });

		(state, _) = state.BeginBattle();
		return state;
	}

	[Test]
	public void TheOpponentAnnouncesASummonBeforeMakingIt()
	{
		var state = Battle();

		var (afterFirst, events) = Do(state, new EndTurnAction());
		state = afterFirst;

		var announced = state.GetOpponent().NextSummon;
		Assert.That(announced, Is.Not.Null, "it telegraphs");
		Assert.That(
			state.EnemyInLane(announced!.Lane),
			Is.Null,
			"and has NOT placed it yet — that is the turn you get to use"
		);
		Assert.That(events.OfType<EnemyTelegraphedEvent>().Any(), Is.True);
	}

	[Test]
	public void WhatWasAnnouncedArrivesTheFollowingTurn()
	{
		var state = Battle();

		(state, _) = Do(state, new EndTurnAction());
		var announced = state.GetOpponent().NextSummon!;

		(state, var events) = Do(state, new EndTurnAction());

		Assert.That(state.EnemyInLane(announced.Lane), Is.Not.Null, "it landed where it said");
		Assert.That(events.OfType<EnemySummonedEvent>().Single().Lane, Is.EqualTo(announced.Lane));
		Assert.That(state.GetOpponent().NextSummon, Is.Null, "and is no longer pending");
	}

	/// <summary>
	/// The reason the telegraph delay exists. A lane that refilled the instant you cleared it would
	/// mean never reaching the Opponent at all.
	/// </summary>
	[Test]
	public void ALaneYouClearStaysOpenLongEnoughToShootThrough()
	{
		var state = Battle(opponentHealth: 100, summonInterval: 2);

		// One enemy, and a unit that kills it outright.
		var (s, _) = state.AddObject(
			new Enemy
			{
				Name = "Wretch",
				Health = 1,
				MaxHealth = 1,
				Intent = IntentKind.Wait,
				Lane = 0,
			},
			state.ZoneId(ZoneType.Enemies)
		);
		state = s;

		var card = (DoomCard)
			new DoomCard
			{
				Name = "Striker",
				Cost = 0,
				RunCardId = 0,
			}.WithComponent(new UnitComponent { Power = 5, Toughness = 5 });
		var (s2, added) = state.AddObject(card, state.ZoneId(ZoneType.Hand));
		state = s2;

		(state, _) = Do(state, new PlayCardAction { CardId = added.Id, Lane = 0 });

		// Turn 1: the Wretch dies. Nothing reaches the Opponent yet — it was still in the way.
		(state, _) = Do(state, new EndTurnAction());
		Assert.That(state.EnemyInLane(0), Is.Null, "lane 0 is open");
		var afterKill = state.GetOpponent().Health;

		// **Combat v3: the Striker withdrew with the turn, so shooting through the hole costs
		// another card.** The promise this test guards is unchanged — the Opponent does not plug a
		// lane the instant you open it, and you get a full turn to use it — but the hole is now an
		// opportunity you have to pay to take rather than one a standing board takes for free.
		Assert.That(state.UnitInLane(0), Is.Null, "it held the lane for its turn, then left");

		var (s3, fresh) = state.AddObject(
			(DoomCard)
				new DoomCard
				{
					Name = "Striker",
					Cost = 0,
					RunCardId = 0,
				}.WithComponent(new UnitComponent { Power = 5, Toughness = 5 }),
			state.ZoneId(ZoneType.Hand)
		);
		state = s3;
		(state, _) = Do(state, new PlayCardAction { CardId = fresh.Id, Lane = 0 });

		// Turn 2: the lane is still open, so the Striker goes through.
		(state, _) = Do(state, new EndTurnAction());

		Assert.That(
			state.GetOpponent().Health,
			Is.LessThan(afterKill),
			"the hole you made was worth something"
		);
	}

	[Test]
	public void ItDoesNotAnnounceASummonWhenEveryLaneIsAlreadyHeld()
	{
		var state = Battle();

		for (var lane = 0; lane < DoomBattle.LaneCount; lane++)
		{
			var (s, _) = state.AddObject(
				new Enemy
				{
					Name = "Wall",
					Health = 50,
					MaxHealth = 50,
					Intent = IntentKind.Wait,
					Lane = lane,
				},
				state.ZoneId(ZoneType.Enemies)
			);
			state = s;
		}

		(state, _) = Do(state, new EndTurnAction());

		Assert.That(
			state.GetOpponent().NextSummon,
			Is.Null,
			"there is nowhere to put one, so nothing is promised"
		);
	}

	[Test]
	public void ADeadOpponentStopsReinforcing()
	{
		var state = Battle(opponentHealth: 1);

		var card = (DoomCard)
			new DoomCard
			{
				Name = "Striker",
				Cost = 0,
				RunCardId = 0,
			}.WithComponent(new UnitComponent { Power = 5, Toughness = 5 });
		var (s, added) = state.AddObject(card, state.ZoneId(ZoneType.Hand));
		state = s;

		(state, _) = Do(state, new PlayCardAction { CardId = added.Id, Lane = 0 });
		(state, _) = Do(state, new EndTurnAction());

		Assert.That(state.GetBattle().OpponentDefeated, Is.True);
		Assert.That(
			state.GetOpponent().NextSummon,
			Is.Null,
			"a corpse does not call for reinforcements"
		);
	}

	/// <summary>
	/// Reinforcements scale on the TURN, not the floor, so a stalled battle is not a safe one. This
	/// is the pressure that replaced the countdown ending the fight.
	/// </summary>
	[Test]
	public void ReinforcementsGetWorseTheLongerTheBattleRuns()
	{
		var early = StarterContent.SummonFor(turnNumber: 1, lane: 0);
		var late = StarterContent.SummonFor(turnNumber: 20, lane: 0);

		Assert.That(late.Health, Is.GreaterThan(early.Health));
		Assert.That(late.Attack, Is.GreaterThan(early.Attack));
	}
}
