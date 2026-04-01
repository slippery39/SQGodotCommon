using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

[TestFixture]
public class CombatTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.Create();
	}

	// ===== ATTACKING A PLAYER =====

	[Test]
	public void Attack_CreatureAttacksPlayer_DealsDamage()
	{
		var (state, attacker) = AddCreatureToBattlefield(_state, "Bear", 2, 2, _ids.Player1Id);

		var (finalState, _) = state
			.AddAction(MakeAttack(attacker.Id, _ids.Player2Id))
			.ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(18));
	}

	[Test]
	public void Attack_CreatureAttacksPlayer_EmitsPlayerDamagedEvent()
	{
		var (state, attacker) = AddCreatureToBattlefield(_state, "Bear", 2, 2, _ids.Player1Id);

		var (_, events) = state
			.AddAction(MakeAttack(attacker.Id, _ids.Player2Id))
			.ProcessAllActions();

		var evt = events.OfType<PlayerDamagedEvent>().Single();
		Assert.That(evt.PlayerId, Is.EqualTo(_ids.Player2Id));
		Assert.That(evt.Amount, Is.EqualTo(2));
	}

	[Test]
	public void Attack_CreatureAttacksPlayer_AttackerIsUnharmed()
	{
		var (state, attacker) = AddCreatureToBattlefield(_state, "Bear", 2, 2, _ids.Player1Id);

		var (finalState, _) = state
			.AddAction(MakeAttack(attacker.Id, _ids.Player2Id))
			.ProcessAllActions();

		var card = (Card)finalState.GetObject(attacker.Id);
		Assert.That(card.GetComponent<CreatureComponent>()!.Damage, Is.EqualTo(0));
		Assert.That(finalState.GetCardZone(attacker.Id).ZoneType, Is.EqualTo(ZoneType.Battlefield));
	}

	// ===== ATTACKING A CREATURE =====

	[Test]
	public void Attack_CreatureAttacksCreature_BothTakeDamage()
	{
		var (s1, attacker) = AddCreatureToBattlefield(_state, "Bear", 2, 2, _ids.Player1Id);
		var (s2, defender) = AddCreatureToBattlefield(s1, "Hill Giant", 3, 4, _ids.Player2Id);

		var (finalState, _) = s2.AddAction(MakeAttack(attacker.Id, defender.Id))
			.ProcessAllActions();

		var attackerCard = (Card)finalState.GetObject(attacker.Id);
		var defenderCard = (Card)finalState.GetObject(defender.Id);

		Assert.That(
			attackerCard.GetComponent<CreatureComponent>()!.Damage,
			Is.EqualTo(3),
			"Attacker should take defender's power as damage"
		);
		Assert.That(
			defenderCard.GetComponent<CreatureComponent>()!.Damage,
			Is.EqualTo(2),
			"Defender should take attacker's power as damage"
		);
	}

	[Test]
	public void Attack_CreatureAttacksCreature_AttackerDiesIfLethal()
	{
		var (s1, attacker) = AddCreatureToBattlefield(_state, "Squire", 1, 1, _ids.Player1Id);
		var (s2, defender) = AddCreatureToBattlefield(s1, "Bear", 2, 2, _ids.Player2Id);

		var (finalState, events) = s2.AddAction(MakeAttack(attacker.Id, defender.Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetCardZone(attacker.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Attacker should die"
		);
		Assert.That(
			finalState.GetCardZone(defender.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"Defender should survive"
		);
		Assert.That(
			events.OfType<CreatureDestroyedEvent>().Any(e => e.CreatureId == attacker.Id),
			Is.True
		);
	}

	[Test]
	public void Attack_CreatureAttacksCreature_DefenderDiesIfLethal()
	{
		var (s1, attacker) = AddCreatureToBattlefield(_state, "Bear", 2, 2, _ids.Player1Id);
		var (s2, defender) = AddCreatureToBattlefield(s1, "Squire", 1, 1, _ids.Player2Id);

		var (finalState, events) = s2.AddAction(MakeAttack(attacker.Id, defender.Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetCardZone(defender.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Defender should die"
		);
		Assert.That(
			finalState.GetCardZone(attacker.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"Attacker should survive"
		);
		Assert.That(
			events.OfType<CreatureDestroyedEvent>().Any(e => e.CreatureId == defender.Id),
			Is.True
		);
	}

	[Test]
	public void Attack_CreatureAttacksCreature_BothDieIfLethal()
	{
		var (s1, attacker) = AddCreatureToBattlefield(_state, "Bear", 2, 2, _ids.Player1Id);
		var (s2, defender) = AddCreatureToBattlefield(s1, "Bear", 2, 2, _ids.Player2Id);

		var (finalState, events) = s2.AddAction(MakeAttack(attacker.Id, defender.Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetCardZone(attacker.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Attacker should die"
		);
		Assert.That(
			finalState.GetCardZone(defender.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Defender should die"
		);
		Assert.That(events.OfType<CreatureDestroyedEvent>().Count(), Is.EqualTo(2));
	}

	// ===== VALIDATION =====

	[Test]
	public void Attack_FailsIfAttackerNotOnBattlefield()
	{
		var attacker = TestCardFactory.MakeCreatureCard("Bear", _ids.Player1Id, 2, 2);
		var (state, added) = _state.AddObject(attacker, parentId: _ids.Player1HandId);

		var (_, success) = state.TryAddAction(MakeAttack(added.Id, _ids.Player2Id));

		Assert.That(success, Is.False);
	}

	[Test]
	public void Attack_FailsIfAttackerIsNotACreature()
	{
		var spell = TestCardFactory.MakeSpellCard(
			"Lightning Bolt",
			_ids.Player1Id,
			new CardEffect
			{
				TargetingStrategy = TargetingStrategy.NoTarget(),
				ActionTemplate = new DealDamageAction { Amount = 3 },
			}
		);
		var (state, added) = _state.AddObject(spell, parentId: _ids.Player1BattlefieldId);

		var (_, success) = state.TryAddAction(MakeAttack(added.Id, _ids.Player2Id));

		Assert.That(success, Is.False);
	}

	[Test]
	public void Attack_FailsIfNotController()
	{
		var (state, attacker) = AddCreatureToBattlefield(_state, "Bear", 2, 2, _ids.Player2Id);

		var (_, success) = state.TryAddAction(
			new AttackAction
			{
				AttackerId = attacker.Id,
				TargetId = _ids.Player1Id,
				AttackingPlayerId = _ids.Player1Id,
			}
		);

		Assert.That(success, Is.False);
	}

	[Test]
	public void Attack_FailsIfTargetingOwnPlayer()
	{
		var (state, attacker) = AddCreatureToBattlefield(_state, "Bear", 2, 2, _ids.Player1Id);

		var (_, success) = state.TryAddAction(MakeAttack(attacker.Id, _ids.Player1Id));

		Assert.That(success, Is.False);
	}

	[Test]
	public void Attack_FailsIfTargetingOwnCreature()
	{
		var (s1, attacker) = AddCreatureToBattlefield(_state, "Bear", 2, 2, _ids.Player1Id);
		var (s2, friendlyCreature) = AddCreatureToBattlefield(s1, "Ally", 1, 1, _ids.Player1Id);

		var (_, success) = s2.TryAddAction(MakeAttack(attacker.Id, friendlyCreature.Id));

		Assert.That(success, Is.False);
	}

	[Test]
	public void Attack_NoActionsRemainAfterResolution()
	{
		var (state, attacker) = AddCreatureToBattlefield(_state, "Bear", 2, 2, _ids.Player1Id);

		var (finalState, _) = state
			.AddAction(MakeAttack(attacker.Id, _ids.Player2Id))
			.ProcessAllActions();

		Assert.That(finalState.HasPendingActions, Is.False);
	}

	// ===== HELPERS =====

	private AttackAction MakeAttack(int attackerId, int targetId) =>
		new()
		{
			AttackerId = attackerId,
			TargetId = targetId,
			AttackingPlayerId = _ids.Player1Id,
		};

	private (GameState, Card) AddCreatureToBattlefield(
		GameState state,
		string name,
		int power,
		int toughness,
		int ownerId
	)
	{
		var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);
		var creature = TestCardFactory.MakeCreatureCard(name, ownerId, power, toughness);
		var (newState, added) = state.AddObject(creature, parentId: battlefieldId);
		return (newState, added);
	}
}
