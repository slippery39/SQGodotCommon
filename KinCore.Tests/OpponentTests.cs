using System.Collections.Immutable;
using KinCore;
using ImmutableGameObjects;

namespace KinCore.Tests;

/// <summary>
/// The board is symmetric: a lane you hold with nothing opposing it lands on the Opponent, and
/// killing the Opponent is the only way to WIN a battle.
///
/// Cards are defined INLINE, never loaded from a library.
/// </summary>
public class OpponentTests
{
	private static (GameState State, ImmutableList<GameEvent> Events) Do(
		GameState state,
		GameAction action
	) => state.AddAction(action).ProcessAllActions();

	private static (GameState, int) AddUnit(GameState state, string name, int power, int toughness)
	{
		var card = (KinCard)
			new KinCard
			{
				Name = name,
				Cost = 1,
				RunCardId = 0,
			}.WithComponent(new UnitComponent { Power = power, Toughness = toughness });
		var (s, added) = state.AddObject(card, state.ZoneId(ZoneType.Hand));
		return (s, added.Id);
	}

	private static GameState AddEnemy(GameState state, int health, int attack, int lane)
	{
		var (s, _) = state.AddObject(
			new Enemy
			{
				Name = "Wretch",
				Health = health,
				MaxHealth = health,
				Intent = attack > 0 ? IntentKind.Attack : IntentKind.Wait,
				IntentAmount = attack,
				Lane = lane,
			},
			state.ZoneId(ZoneType.Enemies)
		);
		return s;
	}

	[Test]
	public void AHeldLaneWithNoEnemyInItHitsTheOpponent()
	{
		var state = KinBattleFactory.Create(opponentHealth: 30);
		(state, var unitId) = AddUnit(state, "Striker", power: 4, toughness: 4);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId, Lane = 2 });
		(state, var events) = Do(state, new EndTurnAction());

		Assert.That(state.GetOpponent().Health, Is.EqualTo(26));
		Assert.That(events.OfType<OpponentDamagedEvent>().Single().Amount, Is.EqualTo(4));
	}

	[Test]
	public void ALaneWithAnEnemyInItDoesNotReachTheOpponent()
	{
		var state = KinBattleFactory.Create(opponentHealth: 30);
		state = AddEnemy(state, health: 20, attack: 0, lane: 2);
		(state, var unitId) = AddUnit(state, "Striker", power: 4, toughness: 4);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId, Lane = 2 });
		(state, _) = Do(state, new EndTurnAction());

		Assert.That(
			state.GetOpponent().Health,
			Is.EqualTo(30),
			"the enemy body is what keeps the lane closed"
		);
	}

	[Test]
	public void KillingTheOpponentEndsTheBattleAsAWin()
	{
		var state = KinBattleFactory.Create(opponentHealth: 3);
		(state, var unitId) = AddUnit(state, "Striker", power: 4, toughness: 4);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId, Lane = 0 });
		(state, var events) = Do(state, new EndTurnAction());

		var battle = state.GetBattle();
		Assert.That(battle.IsOver, Is.True);
		Assert.That(battle.OpponentDefeated, Is.True);
		Assert.That(battle.PlayerIsDead, Is.False);
		Assert.That(events.OfType<OpponentDefeatedEvent>().Any(), Is.True);
	}

	/// <summary>
	/// The run ending outranks winning the battle. A turn that kills both is still a loss, or a
	/// player could trade their own death for a floor they never survived.
	/// </summary>
	[Test]
	public void DyingOnTheSameTurnYouKillTheOpponentIsStillALoss()
	{
		var state = KinBattleFactory.Create(
			life: 2,
			opponentHealth: 3
		);
		state = AddEnemy(state, health: 20, attack: 5, lane: 4);
		(state, var unitId) = AddUnit(state, "Striker", power: 4, toughness: 4);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId, Lane = 0 });
		(state, var events) = Do(state, new EndTurnAction());

		var battle = state.GetBattle();
		Assert.That(battle.PlayerIsDead, Is.True);
		Assert.That(battle.OpponentDefeated, Is.False, "the loss wins the tie");
		Assert.That(events.OfType<PlayerDiedEvent>().Any(), Is.True);
	}

	[Test]
	public void AZeroPowerUnitHoldsALaneWithoutHittingTheOpponent()
	{
		var state = KinBattleFactory.Create(opponentHealth: 30);
		(state, var wallId) = AddUnit(state, "Bulwark", power: 0, toughness: 4);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = wallId, Lane = 1 });
		(state, var events) = Do(state, new EndTurnAction());

		Assert.That(state.GetOpponent().Health, Is.EqualTo(30));
		Assert.That(
			events.OfType<OpponentDamagedEvent>().Any(),
			Is.False,
			"a 0-power hit is not an event, it is nothing happening"
		);
	}

	// ===== The run only transforms if an apocalypse actually happened =====


}
