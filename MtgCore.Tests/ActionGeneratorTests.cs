using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Tests for MtgActionGenerator.GetLegalActions — attack action coverage.
///
/// The tests in the "With opponent creatures" section are regression tests for a bug
/// where the generator only produced "attack player" actions and never
/// "attack creature" actions, causing boards to grow unbounded with no combat deaths.
/// </summary>
[TestFixture]
public class ActionGeneratorTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		// CreateForTesting gives both players 99 mana — no hand cards needed,
		// so the only legal actions generated are attack actions from the battlefield.
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	// ===== NO OPPONENT CREATURES =====

	[Test]
	public void GetLegalActions_OneAttacker_NoOpponentCreatures_HasOneAttackAction()
	{
		var (state, _) = AddAttacker(_state, "Bear", 2, 2, _ids.Player1Id);

		var attacks = GetAttacks(state);

		Assert.That(attacks.Count, Is.EqualTo(1));
		Assert.That(attacks[0].TargetId, Is.EqualTo(_ids.Player2Id));
	}

	[Test]
	public void GetLegalActions_TwoAttackers_NoOpponentCreatures_HasTwoAttackActions()
	{
		var (s1, _) = AddAttacker(_state, "Bear", 2, 2, _ids.Player1Id);
		var (s2, _) = AddAttacker(s1, "Wolf", 2, 2, _ids.Player1Id);

		var attacks = GetAttacks(s2);

		Assert.That(attacks.Count, Is.EqualTo(2));
		Assert.That(attacks.All(a => a.TargetId == _ids.Player2Id), Is.True);
	}

	// ===== WITH OPPONENT CREATURES =====
	// These tests currently FAIL due to the break bug in MtgActionGenerator.

	[Test]
	public void GetLegalActions_OneAttacker_OneOpponentCreature_HasTwoAttackActions()
	{
		var (s1, _) = AddAttacker(_state, "Bear", 2, 2, _ids.Player1Id);
		var (s2, defender) = AddDefender(s1, "Goblin", 1, 1, _ids.Player2Id);

		var attacks = GetAttacks(s2);

		Assert.That(
			attacks.Count,
			Is.EqualTo(2),
			"Attacker should have one action targeting the player and one targeting the opponent creature"
		);
		Assert.That(
			attacks.Any(a => a.TargetId == _ids.Player2Id),
			Is.True,
			"Should include attack targeting the opponent player"
		);
		Assert.That(
			attacks.Any(a => a.TargetId == defender.Id),
			Is.True,
			"Should include attack targeting the opponent creature"
		);
	}

	[Test]
	public void GetLegalActions_OneAttacker_TwoOpponentCreatures_HasThreeAttackActions()
	{
		var (s1, _) = AddAttacker(_state, "Bear", 2, 2, _ids.Player1Id);
		var (s2, defender1) = AddDefender(s1, "Goblin", 1, 1, _ids.Player2Id);
		var (s3, defender2) = AddDefender(s2, "Hill Giant", 3, 4, _ids.Player2Id);

		var attacks = GetAttacks(s3);

		Assert.That(
			attacks.Count,
			Is.EqualTo(3),
			"Attacker should have one action per valid target: player + 2 creatures"
		);
		Assert.That(attacks.Any(a => a.TargetId == _ids.Player2Id), Is.True);
		Assert.That(attacks.Any(a => a.TargetId == defender1.Id), Is.True);
		Assert.That(attacks.Any(a => a.TargetId == defender2.Id), Is.True);
	}

	[Test]
	public void GetLegalActions_TwoAttackers_OneOpponentCreature_HasFourAttackActions()
	{
		var (s1, _) = AddAttacker(_state, "Bear", 2, 2, _ids.Player1Id);
		var (s2, _) = AddAttacker(s1, "Wolf", 2, 2, _ids.Player1Id);
		var (s3, defender) = AddDefender(s2, "Goblin", 1, 1, _ids.Player2Id);

		var attacks = GetAttacks(s3);

		Assert.That(
			attacks.Count,
			Is.EqualTo(4),
			"Each of the 2 attackers should have 2 actions (player + creature) = 4 total"
		);

		var grouped = attacks.GroupBy(a => a.AttackerId).ToList();
		Assert.That(grouped.Count, Is.EqualTo(2), "Both attackers should be represented");
		foreach (var group in grouped)
		{
			Assert.That(
				group.Any(a => a.TargetId == _ids.Player2Id),
				Is.True,
				"Each attacker should have a player-targeting action"
			);
			Assert.That(
				group.Any(a => a.TargetId == defender.Id),
				Is.True,
				"Each attacker should have a creature-targeting action"
			);
		}
	}

	// ===== ELIGIBILITY =====

	[Test]
	public void GetLegalActions_CreatureWithSummoningSickness_HasNoAttackActions()
	{
		var battlefieldId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var sickCreature = MakeSickCreature("Sick Bear", 2, 2, _ids.Player1Id);
		var (state, _) = _state.AddObject(sickCreature, parentId: battlefieldId);

		var attacks = GetAttacks(state);

		Assert.That(attacks.Count, Is.EqualTo(0));
	}

	[Test]
	public void GetLegalActions_CreatureThatAlreadyAttacked_HasNoAttackActions()
	{
		var battlefieldId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var tired = MakeAttackedCreature("Tired Bear", 2, 2, _ids.Player1Id);
		var (state, _) = _state.AddObject(tired, parentId: battlefieldId);

		var attacks = GetAttacks(state);

		Assert.That(attacks.Count, Is.EqualTo(0));
	}

	// ===== HELPERS =====

	private List<AttackAction> GetAttacks(GameState state) =>
		MtgActionGenerator
			.GetLegalActions(state, _ids, _ids.Player1Id)
			.OfType<AttackAction>()
			.ToList();

	private (GameState, Card) AddAttacker(
		GameState state,
		string name,
		int power,
		int toughness,
		int ownerId
	)
	{
		var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);
		var creature = new Card
		{
			Name = name,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent
				{
					Power = power,
					Toughness = toughness,
					HasSummoningSickness = false,
				}
			),
		};
		var (newState, added) = state.AddObject(creature, parentId: battlefieldId);
		return (newState, added);
	}

	// Defenders live on Player 2's battlefield — valid targets but never generate attack actions.
	private (GameState, Card) AddDefender(
		GameState state,
		string name,
		int power,
		int toughness,
		int ownerId
	)
	{
		var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);
		var creature = new Card
		{
			Name = name,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent
				{
					Power = power,
					Toughness = toughness,
					HasSummoningSickness = false,
				}
			),
		};
		var (newState, added) = state.AddObject(creature, parentId: battlefieldId);
		return (newState, added);
	}

	private static Card MakeSickCreature(string name, int power, int toughness, int ownerId) =>
		new()
		{
			Name = name,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent
				{
					Power = power,
					Toughness = toughness,
					HasSummoningSickness = true,
				}
			),
		};

	private static Card MakeAttackedCreature(string name, int power, int toughness, int ownerId) =>
		new()
		{
			Name = name,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent
				{
					Power = power,
					Toughness = toughness,
					HasSummoningSickness = false,
					HasAttacked = true,
				}
			),
		};
}
