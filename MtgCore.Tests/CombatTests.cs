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
		(_state, _ids) = MtgGameFactory.CreateForTesting();
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
		// Both combatants must SURVIVE for this test to be about marked damage: damage falls off
		// a creature that changes zones, so a dead attacker carries none into the graveyard.
		var (s1, attacker) = AddCreatureToBattlefield(_state, "Wall", 2, 4, _ids.Player1Id);
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
	public void ChangingZones_ClearsMarkedDamage()
	{
		// Survives combat with 3 damage marked, then is bounced to hand. Marked damage belongs to
		// the permanent, so the card must arrive in hand — and come back — undamaged.
		var (s1, attacker) = AddCreatureToBattlefield(_state, "Wall", 1, 5, _ids.Player1Id);
		var (s2, defender) = AddCreatureToBattlefield(s1, "Hill Giant", 3, 4, _ids.Player2Id);

		var (afterCombat, _) = s2.AddAction(MakeAttack(attacker.Id, defender.Id))
			.ProcessAllActions();

		Assert.That(
			((Card)afterCombat.GetObject(attacker.Id)).GetComponent<CreatureComponent>()!.Damage,
			Is.EqualTo(3),
			"Precondition: the attacker survives combat with damage marked on it"
		);

		var handId = afterCombat.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var bounced = afterCombat.MoveCardTracked(attacker.Id, handId);

		Assert.That(
			((Card)bounced.GetObject(attacker.Id)).GetComponent<CreatureComponent>()!.Damage,
			Is.EqualTo(0),
			"Damage should fall off when the creature changes zones"
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

		Assert.That(finalState.GetCardZone(attacker.Id).ZoneType, Is.EqualTo(ZoneType.Graveyard));
		Assert.That(finalState.GetCardZone(defender.Id).ZoneType, Is.EqualTo(ZoneType.Graveyard));
		Assert.That(events.OfType<CreatureDestroyedEvent>().Count(), Is.EqualTo(2));
	}

	// ===== HAS ATTACKED =====

	[Test]
	public void Attack_SetsHasAttacked_OnAttacker()
	{
		var (state, attacker) = AddCreatureToBattlefield(_state, "Bear", 2, 2, _ids.Player1Id);

		var (finalState, _) = state
			.AddAction(MakeAttack(attacker.Id, _ids.Player2Id))
			.ProcessAllActions();

		var card = (Card)finalState.GetObject(attacker.Id);
		Assert.That(card.GetComponent<CreatureComponent>()!.HasAttacked, Is.True);
	}

	[Test]
	public void Attack_FailsIfCreatureAlreadyAttacked()
	{
		var (state, attacker) = AddCreatureToBattlefield(_state, "Bear", 2, 2, _ids.Player1Id);

		var (stateAfterAttack, _) = state
			.AddAction(MakeAttack(attacker.Id, _ids.Player2Id))
			.ProcessAllActions();

		var (_, success) = stateAfterAttack.TryAddAction(MakeAttack(attacker.Id, _ids.Player2Id));
		Assert.That(success, Is.False);
	}

	// ===== VALIDATION =====

	[Test]
	public void Attack_FailsIfAttackerHasSummoningSickness()
	{
		var battlefieldId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var sickCreature = new Card
		{
			Name = "Sick Bear",
			ManaCost = 2,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
			Components = ImmutableArray.Create<GameComponent>(
				new CreatureComponent
				{
					Power = 2,
					Toughness = 2,
					HasSummoningSickness = true,
				}
			),
		};
		var (state, added) = _state.AddObject(sickCreature, parentId: battlefieldId);

		var (_, success) = state.TryAddAction(MakeAttack(added.Id, _ids.Player2Id));
		Assert.That(success, Is.False);
	}

	[Test]
	public void Attack_FailsIfNotController()
	{
		var (state, attacker) = AddCreatureToBattlefield(_state, "Bear", 2, 2, _ids.Player1Id);

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

	// ===== LIFELINK =====

	[Test]
	public void Lifelink_AttackerVsPlayer_ControllerGainsLife()
	{
		var (state, attacker) = AddCreatureToBattlefield(
			_state,
			"Lifelinker",
			3,
			3,
			_ids.Player1Id,
			hasLifelink: true
		);

		var (finalState, _) = state
			.AddAction(MakeAttack(attacker.Id, _ids.Player2Id))
			.ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player1Id).Life, Is.EqualTo(23));
		Assert.That(finalState.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(17));
	}

	[Test]
	public void Lifelink_AttackerVsCreature_ControllerGainsLife()
	{
		var (s1, attacker) = AddCreatureToBattlefield(
			_state,
			"Lifelinker",
			3,
			3,
			_ids.Player1Id,
			hasLifelink: true
		);
		var (s2, defender) = AddCreatureToBattlefield(s1, "Tank", 1, 5, _ids.Player2Id);

		var (finalState, _) = s2.AddAction(MakeAttack(attacker.Id, defender.Id))
			.ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player1Id).Life, Is.EqualTo(23));
	}

	[Test]
	public void Lifelink_Defender_ControllerGainsLife()
	{
		var (s1, attacker) = AddCreatureToBattlefield(_state, "Attacker", 3, 3, _ids.Player1Id);
		var (s2, defender) = AddCreatureToBattlefield(
			s1,
			"Lifelinker",
			2,
			5,
			_ids.Player2Id,
			hasLifelink: true
		);

		var (finalState, _) = s2.AddAction(MakeAttack(attacker.Id, defender.Id))
			.ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(22));
	}

	[Test]
	public void Lifelink_DoubleStrike_GainsLifeForBothStrikes()
	{
		var (s1, attacker) = AddCreatureToBattlefield(
			_state,
			"Double Lifelinker",
			2,
			5,
			_ids.Player1Id,
			hasDoubleStrike: true,
			hasLifelink: true
		);

		var (finalState, _) = s1.AddAction(MakeAttack(attacker.Id, _ids.Player2Id))
			.ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player1Id).Life, Is.EqualTo(24));
	}

	// ===== TRAMPLE =====

	[Test]
	public void Trample_ExcessDamageGoesToDefendingPlayer()
	{
		var (s1, attacker) = AddCreatureToBattlefield(
			_state,
			"Trampler",
			5,
			5,
			_ids.Player1Id,
			hasTrample: true
		);
		var (s2, defender) = AddCreatureToBattlefield(s1, "Chump", 1, 2, _ids.Player2Id);

		var (finalState, _) = s2.AddAction(MakeAttack(attacker.Id, defender.Id))
			.ProcessAllActions();

		Assert.That(finalState.GetCardZone(defender.Id).ZoneType, Is.EqualTo(ZoneType.Graveyard));
		Assert.That(finalState.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(17));
	}

	[Test]
	public void Trample_NoExcessWhenPowerDoesNotExceedToughness()
	{
		var (s1, attacker) = AddCreatureToBattlefield(
			_state,
			"Trampler",
			2,
			2,
			_ids.Player1Id,
			hasTrample: true
		);
		var (s2, defender) = AddCreatureToBattlefield(s1, "Tank", 1, 5, _ids.Player2Id);

		var (finalState, _) = s2.AddAction(MakeAttack(attacker.Id, defender.Id))
			.ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(20));
	}

	[Test]
	public void Trample_AndLifelink_GainsLifeForTotalDamageNotJustCreature()
	{
		var (s1, attacker) = AddCreatureToBattlefield(
			_state,
			"Trampling Lifelinker",
			5,
			5,
			_ids.Player1Id,
			hasLifelink: true,
			hasTrample: true
		);
		var (s2, defender) = AddCreatureToBattlefield(s1, "Chump", 1, 2, _ids.Player2Id);

		var (finalState, _) = s2.AddAction(MakeAttack(attacker.Id, defender.Id))
			.ProcessAllActions();

		// Total damage dealt = 5. Lifelink gains 5 (not 5+3).
		// Trample excess = 3 goes to P2.
		Assert.That(finalState.GetPlayer(_ids.Player1Id).Life, Is.EqualTo(25));
		Assert.That(finalState.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(17));
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
		int ownerId,
		bool hasLifelink = false,
		bool hasTrample = false,
		bool hasDoubleStrike = false
	)
	{
		var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);
		var creature = new Card
		{
			Name = name,
			ManaCost = 2,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableArray.Create<GameComponent>(
				new CreatureComponent
				{
					Power = power,
					Toughness = toughness,
					HasSummoningSickness = false,
					HasLifelink = hasLifelink,
					HasTrample = hasTrample,
					HasDoubleStrike = hasDoubleStrike,
				}
			),
		};
		var (newState, added) = state.AddObject(creature, parentId: battlefieldId);
		return (newState, added);
	}
}
