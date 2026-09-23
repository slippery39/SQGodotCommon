using System.Collections.Immutable;
using ImmutableGameObjects;
using KinCore;

namespace KinCore.Tests;

/// <summary>
/// Flier, Breakthrough, and buffs that grant Thorns or an extra strike — the rest of the mechanics
/// the Bulwark and Face vertical slice needs.
///
/// **Every test asserts a number that moved**, and none reads a unit back after the turn: it has
/// withdrawn by then. See <see cref="ThornsAndStrikesTests"/> for why.
///
/// Cards are defined INLINE, never loaded from a library.
/// </summary>
public class FlierAndBreakthroughTests
{
	private static (GameState State, ImmutableList<GameEvent> Events) Do(
		GameState state,
		GameAction action
	) => state.AddAction(action).ProcessAllActions();

	private static (GameState, int) AddUnit(
		GameState state,
		int power,
		int toughness,
		int thorns = 0,
		bool breakthrough = false
	)
	{
		var card = (KinCard)
			new KinCard { Name = "Body", Cost = 0 }.WithComponent(
				new UnitComponent
				{
					Power = power,
					Toughness = toughness,
					Thorns = thorns,
					Breakthrough = breakthrough,
				}
			);

		var (s, added) = state.AddObject(card, state.ZoneId(ZoneType.Hand));
		return (s, added.Id);
	}

	private static (GameState, int) AddRite(GameState state, KinEffect effect)
	{
		var (s, added) = state.AddObject(
			new KinCard
			{
				Name = "Rite",
				Cost = 0,
				Effects = [effect],
			},
			state.ZoneId(ZoneType.Hand)
		);
		return (s, added.Id);
	}

	private static GameState AddEnemy(
		GameState state,
		int health,
		int attack,
		bool flies = false,
		int thorns = 0
	) =>
		state
			.AddObject(
				new EnemyDefinition
				{
					Name = "Foe",
					Health = health,
					Attack = attack,
					Flies = flies,
					Thorns = thorns,
				}.ToEnemy(lane: 0),
				state.ZoneId(ZoneType.Enemies)
			)
			.Item1;

	private static (GameState State, ImmutableList<GameEvent> Events) Fight(
		GameState state,
		int unitId
	)
	{
		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId, Lane = 0 });
		return Do(state, new EndTurnAction());
	}

	// ===== Flier =====

	[Test]
	public void AFliersAttackGoesOverYourUnitAndLandsOnYou()
	{
		var state = AddEnemy(KinBattleFactory.Create(life: 60), health: 40, attack: 6, flies: true);
		(state, var id) = AddUnit(state, power: 5, toughness: 20);

		(state, var events) = Fight(state, id);

		var dmg = events.OfType<PlayerDamagedEvent>().Single();

		// A 20-toughness wall would have soaked all 6 of an ordinary attack. That is the point.
		Assert.Multiple(() =>
		{
			Assert.That(dmg.Amount, Is.EqualTo(6));
			Assert.That(dmg.Absorbed, Is.Zero, "the wall never got in the way");
			Assert.That(state.GetPlayer().Life, Is.EqualTo(54));
		});
	}

	[Test]
	public void YourUnitStillHitsAFlier()
	{
		var state = AddEnemy(KinBattleFactory.Create(), health: 12, attack: 6, flies: true);
		(state, var id) = AddUnit(state, power: 5, toughness: 20);

		(state, _) = Fight(state, id);

		// The answer to a Flier is to kill it, so hitting it must still work.
		Assert.That(state.EnemyInLane(0)!.Health, Is.EqualTo(7));
	}

	[Test]
	public void YourThornsNeverAnswerAFlierBecauseItNeverAttackedYourUnit()
	{
		var state = AddEnemy(KinBattleFactory.Create(), health: 40, attack: 6, flies: true);
		(state, var id) = AddUnit(state, power: 1, toughness: 20, thorns: 9);

		(state, _) = Fight(state, id);

		// 1 power, and none of the 9 thorns. Against a walking enemy this would read 30.
		Assert.That(state.EnemyInLane(0)!.Health, Is.EqualTo(39));
	}

	// ===== Breakthrough =====

	[Test]
	public void BreakthroughCarriesPowerPastTheEnemyOnToTheOpponent()
	{
		var state = AddEnemy(KinBattleFactory.Create(opponentHealth: 100), health: 4, attack: 0);
		(state, var id) = AddUnit(state, power: 10, toughness: 4, breakthrough: true);

		(state, _) = Fight(state, id);

		Assert.That(state.GetOpponent().Health, Is.EqualTo(94), "10 swing, 4 to kill, 6 through");
	}

	[Test]
	public void WithoutBreakthroughAKillCarriesNothing()
	{
		var state = AddEnemy(KinBattleFactory.Create(opponentHealth: 100), health: 4, attack: 0);
		(state, var id) = AddUnit(state, power: 10, toughness: 4);

		(state, _) = Fight(state, id);

		// The control for the test above. Without it, a Breakthrough that did nothing and an
		// Opponent that took damage from somewhere else would pass together.
		Assert.That(state.GetOpponent().Health, Is.EqualTo(100));
	}

	[Test]
	public void BreakthroughWithNoExcessCarriesNothing()
	{
		var state = AddEnemy(KinBattleFactory.Create(opponentHealth: 100), health: 20, attack: 0);
		(state, var id) = AddUnit(state, power: 10, toughness: 4, breakthrough: true);

		(state, _) = Fight(state, id);

		Assert.That(state.GetOpponent().Health, Is.EqualTo(100));
	}

	// ===== Buffs that grant the keywords =====

	/// <summary>
	/// **Whetstone's shape: a rite dropped on a lane gives that unit power AND an extra strike.**
	/// The extra power counts on every strike, which is what makes the pair a combo rather than
	/// two small buffs.
	/// </summary>
	[Test]
	public void ALaneRiteCanGrantAnExtraStrikeAndThePowerCountsOnBoth()
	{
		var state = AddEnemy(KinBattleFactory.Create(), health: 40, attack: 0);
		(state, var unitId) = AddUnit(state, power: 5, toughness: 10);
		(state, var riteId) = AddRite(
			state,
			new KinEffect
			{
				Trigger = EffectTrigger.OnPlay,
				Target = KinTarget.UnitInSourceLane,
				Template = new BuffAction { Power = 3, Strikes = 1 },
			}
		);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId, Lane = 0 });
		(state, _) = Do(state, new PlayCardAction { CardId = riteId, Lane = 0 });
		(state, _) = Do(state, new EndTurnAction());

		Assert.That(state.EnemyInLane(0)!.Health, Is.EqualTo(24), "(5 + 3) twice");
	}

	[Test]
	public void ABuffCanGrantThorns()
	{
		var state = AddEnemy(KinBattleFactory.Create(), health: 40, attack: 3);
		(state, var unitId) = AddUnit(state, power: 0, toughness: 10);
		(state, var riteId) = AddRite(
			state,
			new KinEffect
			{
				Trigger = EffectTrigger.OnPlay,
				Target = KinTarget.YourUnits,
				Template = new BuffAction { Thorns = 4 },
			}
		);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId, Lane = 0 });
		(state, _) = Do(state, new PlayCardAction { CardId = riteId, Lane = 0 });
		(state, _) = Do(state, new EndTurnAction());

		// A 0-power body deals nothing on its own, so all 4 is the granted spikes answering the hit.
		Assert.That(state.EnemyInLane(0)!.Health, Is.EqualTo(36));
	}

	// ===== The telegraph must not drop them =====

	[Test]
	public void AReinforcementArrivesWithItsKeywordsIntact()
	{
		var definition = new EnemyDefinition
		{
			Name = "Razorback",
			Health = 20,
			Attack = 4,
			Thorns = 8,
			Strikes = 2,
			Flies = true,
		};

		// Announced a turn ahead as a PendingSummon, then made real. Effects already had to be
		// threaded through this hop by hand once; a keyword dropped here would make a summoned
		// Razorback a Razorback in name only.
		var arrived = definition.ToSummon(lane: 2).ToEnemy();

		Assert.Multiple(() =>
		{
			Assert.That(arrived.Thorns, Is.EqualTo(definition.Thorns));
			Assert.That(arrived.Strikes, Is.EqualTo(definition.Strikes));
			Assert.That(arrived.Flies, Is.EqualTo(definition.Flies));
		});
	}
}
