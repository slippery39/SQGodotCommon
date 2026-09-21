using System.Collections.Immutable;
using KinCore;
using ImmutableGameObjects;

namespace KinCore.Tests;

/// <summary>
/// Effects are NOT a card feature. The same KinEffect, on the same triggers, fires from an enemy,
/// from the Opponent and from your own units — which is the whole reason it lives under Effects/
/// rather than on KinCard.
///
/// Every test asserts the CONSEQUENCE. A trigger that is declared and never fires throws no error
/// and looks exactly like one that worked.
/// </summary>
public class HolderEffectTests
{
	private static (GameState State, ImmutableList<GameEvent> Events) Do(
		GameState state,
		GameAction action
	) => state.AddAction(action).ProcessAllActions();

	private static KinEffect On(EffectTrigger trigger, KinTarget target, GameAction template) =>
		new()
		{
			Trigger = trigger,
			Target = target,
			Template = template,
		};

	private static (GameState, int) AddEnemy(
		GameState state,
		int health,
		int attack,
		int lane,
		params KinEffect[] effects
	)
	{
		var (s, e) = state.AddObject(
			new Enemy
			{
				Name = "Husk",
				Health = health,
				MaxHealth = health,
				Intent = attack > 0 ? IntentKind.Attack : IntentKind.Wait,
				IntentAmount = attack,
				Lane = lane,
				Effects = [.. effects],
			},
			state.ZoneId(ZoneType.Enemies)
		);
		return (s, e.Id);
	}

	private static (GameState, int) AddUnit(GameState state, int power, int toughness, int lane)
	{
		var card = new KinCard
		{
			Name = "Stray",
			Cost = 0,
			RunCardId = 0,
		};
		card = (KinCard)
			card.WithComponent(
				new UnitComponent
				{
					Power = power,
					Toughness = toughness,
					Lane = lane,
				}
			);

		var (s, added) = state.AddObject(card, state.ZoneId(ZoneType.Hand));
		return (s, added.Id);
	}

	/// <summary>An enemy that hurts you on the way out. Killing it is no longer strictly free.</summary>
	[Test]
	public void AnEnemyCanFireAnEffectWhenItDies()
	{
		var state = KinBattleFactory.Create(
			life: 40,
			opponentHealth: 500
		);
		(state, _) = AddEnemy(
			state,
			health: 2,
			attack: 0,
			lane: 0,
			On(EffectTrigger.OnDeath, KinTarget.Player, new DealDamageAction { Amount = 7 })
		);
		(state, var unitId) = AddUnit(state, power: 5, toughness: 5, lane: 0);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = unitId, Lane = 0 });
		(state, _) = Do(state, new EndTurnAction());

		Assert.That(state.LivingEnemies(), Is.Empty, "it should be dead");
		Assert.That(
			state.GetPlayer().Life,
			Is.EqualTo(33),
			"and its death should have cost 7 — the effect has to actually fire"
		);
	}

	/// <summary>
	/// The per-battle inevitability lever KinJam.md wanted: an Opponent that undoes your progress
	/// makes one fight a guaranteed apocalypse without retuning every other fight.
	/// </summary>
	[Test]
	public void TheOpponentCanFireAnEffectEveryTurn()
	{
		var state = KinBattleFactory.Create(opponentHealth: 30);

		var opponent = state.GetOpponent();
		state = state.UpdateObject(
			opponent.Id,
			opponent with
			{
				Effects =
				[
					On(
						EffectTrigger.OnTurnEnd,
						KinTarget.Player,
						new DealDamageAction { Amount = 3 }
					),
				],
			}
		);

		(state, _) = state.BeginBattle();
		var before = state.GetPlayer().Life;

		(state, _) = Do(state, new EndTurnAction());
		Assert.That(state.GetPlayer().Life, Is.EqualTo(before - 3));

		(state, _) = Do(state, new EndTurnAction());
		Assert.That(
			state.GetPlayer().Life,
			Is.EqualTo(before - 6),
			"EVERY turn, not just the first — OnTurnEnd must keep firing"
		);
	}

	/// <summary>
	/// An effect that reacts to the apocalypse itself. This is what lets a scenario's fallout be
	/// content rather than something hardcoded into the doom.
	/// </summary>

	/// <summary>A holder with no effect for a trigger must queue nothing at all.</summary>
	[Test]
	public void AHolderWithNoEffectsChangesNothing()
	{
		var state = KinBattleFactory.Create(opponentHealth: 30);
		(state, _) = AddEnemy(state, health: 5, attack: 0, lane: 0);

		(state, _) = state.BeginBattle();
		var life = state.GetPlayer().Life;

		(state, _) = Do(state, new EndTurnAction());

		Assert.That(state.GetPlayer().Life, Is.EqualTo(life));
		Assert.That(state.GetOpponent().Health, Is.EqualTo(30));
	}
}
