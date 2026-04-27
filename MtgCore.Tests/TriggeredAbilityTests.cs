using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Tests for triggered abilities — CreatureDies, CreatureEntersBattlefield,
/// and CreatureAttacks conditions.
///
/// Each test verifies the trigger fires (or doesn't fire) correctly and that
/// the triggered effect resolves after the triggering action fully completes.
/// </summary>
[TestFixture]
public class TriggeredAbilityTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	// ===== CREATURE DIES =====

	[Test]
	public void CreatureDies_Trigger_FiresWhenCreatureDiesFromDamage()
	{
		// Card with "when any creature dies, deal 1 damage to opponent"
		var (stateWithTrigger, _) = AddTriggerCardToBattlefield(
			_state,
			_ids.Player1Id,
			new CreatureDiesCondition(),
			DealDamageToOpponentEffect(_ids.Player1Id, amount: 1)
		);

		// Put a 1/1 on opponent's battlefield
		var (stateWithCreature, target) = AddCreatureToBattlefield(
			stateWithTrigger,
			"Squire",
			1,
			1,
			_ids.Player2Id
		);

		// Cast a Lightning Bolt at the creature
		var bolt = MakeDamageSpell(_ids.Player1Id, amount: 3);
		var (stateWithCard, card) = stateWithCreature.AddObject(bolt, parentId: _ids.Player1HandId);

		var (finalState, _) = stateWithCard
			.AddAction(TestCardFactory.MakeCastActionWithTarget(card.Id, _ids.Player1Id, target.Id))
			.ProcessAllActions();

		// Bolt hit the creature (not the player), so only the death trigger deals damage.
		// Opponent life: 20 - 1 (trigger) = 19
		Assert.That(
			finalState.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(19),
			"Opponent should take 1 damage from the death trigger"
		);
	}

	[Test]
	public void CreatureDies_Trigger_FiresWhenCreatureDiesFromCombat()
	{
		// Card with "when any creature dies, gain 1 life"
		var (s1, _) = AddTriggerCardToBattlefield(
			_state,
			_ids.Player1Id,
			new CreatureDiesCondition(),
			GainLifeEffect(amount: 1)
		);

		var (s2, attacker) = AddCreatureToBattlefield(s1, "Bear", 2, 2, _ids.Player1Id);
		var (s3, defender) = AddCreatureToBattlefield(s2, "Squire", 1, 1, _ids.Player2Id);

		var (finalState, _) = s3.AddAction(MakeAttack(attacker.Id, defender.Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(21),
			"Player 1 should gain 1 life from the death trigger"
		);
	}

	[Test]
	public void CreatureDies_Trigger_OnlyYourCreatures_DoesNotFireOnOpponentDeath()
	{
		// Card with "when YOUR creature dies, gain 1 life"
		var (s1, _) = AddTriggerCardToBattlefield(
			_state,
			_ids.Player1Id,
			new CreatureDiesCondition { OnlyYourCreatures = true },
			GainLifeEffect(amount: 1)
		);

		// Opponent's creature dies — should NOT trigger
		var (s2, attacker) = AddCreatureToBattlefield(s1, "Bear", 2, 2, _ids.Player1Id);
		var (s3, defender) = AddCreatureToBattlefield(s2, "Squire", 1, 1, _ids.Player2Id);

		var (finalState, _) = s3.AddAction(MakeAttack(attacker.Id, defender.Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(20),
			"Trigger should not fire when opponent's creature dies"
		);
	}

	[Test]
	public void CreatureDies_Trigger_OnlyYourCreatures_FiresWhenYourCreatureDies()
	{
		// Card with "when YOUR creature dies, gain 1 life"
		var (s1, _) = AddTriggerCardToBattlefield(
			_state,
			_ids.Player1Id,
			new CreatureDiesCondition { OnlyYourCreatures = true },
			GainLifeEffect(amount: 1)
		);

		// Player 1's creature dies in combat
		var (s2, attacker) = AddCreatureToBattlefield(s1, "Squire", 1, 1, _ids.Player1Id);
		var (s3, defender) = AddCreatureToBattlefield(s2, "Bear", 2, 2, _ids.Player2Id);

		var (finalState, _) = s3.AddAction(MakeAttack(attacker.Id, defender.Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(21),
			"Trigger should fire when your own creature dies"
		);
	}

	[Test]
	public void CreatureDies_Trigger_FiresOncePerDeath()
	{
		// Pyroclasm kills two creatures — trigger fires twice
		var (s1, _) = AddTriggerCardToBattlefield(
			_state,
			_ids.Player1Id,
			new CreatureDiesCondition(),
			GainLifeEffect(amount: 1)
		);

		var (s2, _) = AddCreatureToBattlefield(s1, "Squire A", 1, 1, _ids.Player2Id);
		var (s3, _) = AddCreatureToBattlefield(s2, "Squire B", 1, 1, _ids.Player2Id);

		var pyroclasm = MakePyroclasm(_ids.Player1Id);
		var (s4, card) = s3.AddObject(pyroclasm, parentId: _ids.Player1HandId);

		var (finalState, _) = s4.AddAction(TestCardFactory.MakeCastAction(card.Id, _ids.Player1Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(22),
			"Trigger should fire once per creature death — 2 deaths = +2 life"
		);
	}

	// ===== CREATURE ENTERS BATTLEFIELD =====

	[Test]
	public void CreatureEntersBattlefield_Trigger_FiresWhenCreaturePlayed()
	{
		// Card with "when any creature enters the battlefield, gain 1 life"
		var (s1, _) = AddTriggerCardToBattlefield(
			_state,
			_ids.Player1Id,
			new CreatureEntersBattlefieldCondition(),
			GainLifeEffect(amount: 1)
		);

		var creature = TestCardFactory.MakeCreatureCard("Bear", _ids.Player1Id, 2, 2, manaCost: 0);
		var (s2, card) = s1.AddObject(creature, parentId: _ids.Player1HandId);

		var (finalState, _) = s2.AddAction(
				new CastCreatureAction { CardId = card.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(21),
			"Trigger should fire when a creature enters the battlefield"
		);
	}

	[Test]
	public void CreatureEntersBattlefield_Trigger_OnlyOpponentCreatures_DoesNotFireOnYourOwn()
	{
		// Card with "when an OPPONENT'S creature enters, deal 1 damage to opponent"
		var (s1, _) = AddTriggerCardToBattlefield(
			_state,
			_ids.Player1Id,
			new CreatureEntersBattlefieldCondition { OnlyOpponentCreatures = true },
			DealDamageToOpponentEffect(_ids.Player1Id, amount: 1)
		);

		// Player 1 plays their own creature — should NOT trigger
		var creature = TestCardFactory.MakeCreatureCard("Bear", _ids.Player1Id, 2, 2, manaCost: 0);
		var (s2, card) = s1.AddObject(creature, parentId: _ids.Player1HandId);

		var (finalState, _) = s2.AddAction(
				new CastCreatureAction { CardId = card.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(20),
			"Trigger should not fire when your own creature enters"
		);
	}

	// ===== CREATURE ATTACKS =====

	[Test]
	public void CreatureAttacks_Trigger_FiresWhenCreatureAttacks()
	{
		// Card with "when any creature attacks, gain 1 life"
		var (s1, _) = AddTriggerCardToBattlefield(
			_state,
			_ids.Player1Id,
			new CreatureAttacksCondition(),
			GainLifeEffect(amount: 1)
		);

		var (s2, attacker) = AddCreatureToBattlefield(s1, "Bear", 2, 2, _ids.Player1Id);

		var (finalState, _) = s2.AddAction(MakeAttack(attacker.Id, _ids.Player2Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(21),
			"Trigger should fire when a creature attacks"
		);
	}

	[Test]
	public void CreatureAttacks_Trigger_OnlyYourCreatures_DoesNotFireOnOpponentAttack()
	{
		// Card with "when YOUR creature attacks, gain 1 life"
		// Place it on Player 2's side, have Player 1 attack
		var (s1, _) = AddTriggerCardToBattlefield(
			_state,
			_ids.Player2Id,
			new CreatureAttacksCondition { OnlyYourCreatures = true },
			GainLifeEffect(amount: 1)
		);

		// Player 1 attacks — should not fire Player 2's "your creature attacks" trigger
		var (s2, attacker) = AddCreatureToBattlefield(s1, "Bear", 2, 2, _ids.Player1Id);

		var (finalState, _) = s2.AddAction(MakeAttack(attacker.Id, _ids.Player2Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(18), // took 2 damage from the attack, no life gain
			"Trigger should not fire for opponent's creature attacking"
		);
	}

	// ===== MULTIPLE TRIGGERS =====

	[Test]
	public void MultipleTriggers_AllFireWhenConditionMet()
	{
		// Two separate cards each with a death trigger
		var (s1, _) = AddTriggerCardToBattlefield(
			_state,
			_ids.Player1Id,
			new CreatureDiesCondition(),
			GainLifeEffect(amount: 1)
		);
		var (s2, _) = AddTriggerCardToBattlefield(
			s1,
			_ids.Player1Id,
			new CreatureDiesCondition(),
			GainLifeEffect(amount: 1)
		);

		var (s3, attacker) = AddCreatureToBattlefield(s2, "Bear", 2, 2, _ids.Player1Id);
		var (s4, defender) = AddCreatureToBattlefield(s3, "Squire", 1, 1, _ids.Player2Id);

		var (finalState, _) = s4.AddAction(MakeAttack(attacker.Id, defender.Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(22),
			"Both death triggers should fire — +1 life each = +2 total"
		);
	}

	[Test]
	public void Trigger_PendingGameEvents_ClearedAfterResolution()
	{
		var (s1, attacker) = AddCreatureToBattlefield(_state, "Bear", 2, 2, _ids.Player1Id);
		var (s2, defender) = AddCreatureToBattlefield(s1, "Squire", 1, 1, _ids.Player2Id);

		var (finalState, _) = s2.AddAction(MakeAttack(attacker.Id, defender.Id))
			.ProcessAllActions();

		Assert.That(
			finalState.PendingGameEvents.IsEmpty,
			Is.True,
			"PendingGameEvents should be cleared after PostActionProcessor runs"
		);
	}

	// ===== HELPERS =====

	/// <summary>
	/// Adds a plain card with a single triggered ability to the given player's battlefield.
	/// Returns the updated state and the card's ID.
	/// </summary>
	private (GameState state, int cardId) AddTriggerCardToBattlefield(
		GameState state,
		int ownerId,
		TriggerCondition condition,
		CardEffect effect
	)
	{
		var card = new Card
		{
			Name = "Trigger Card",
			ManaCost = 0,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableList.Create<GameComponent>(
				new TriggeredAbilityComponent
				{
					Name = "Trigger",
					Condition = condition,
					Effect = effect,
				}
			),
		};

		var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);
		var (newState, added) = state.AddObject(card, parentId: battlefieldId);
		return (newState, added.Id);
	}

	/// <summary>
	/// A CardEffect that gains life for the controlling player.
	/// Uses CastingPlayer targeting — seeded by ResolveEffectAction.
	/// </summary>
	private static CardEffect GainLifeEffect(int amount) =>
		new()
		{
			TargetingStrategy = TargetingStrategy.Self(),
			ActionTemplate = new GainLifeAction { Amount = amount },
		};

	/// <summary>
	/// A CardEffect that deals damage to all opponent players (AllValid).
	/// </summary>
	private static CardEffect DealDamageToOpponentEffect(int castingPlayerId, int amount) =>
		new()
		{
			TargetingStrategy = TargetingStrategy.AllValid(
				new IsPlayerSpecification().And(new IsControlledByOpponentSpecification())
			),
			ActionTemplate = new DealDamageAction { Amount = amount },
		};

	/// <summary>
	/// A Pyroclasm-style spell: deals 2 damage to all creatures.
	/// </summary>
	private static Card MakePyroclasm(int ownerId) =>
		TestCardFactory.MakeSpellCard(
			"Pyroclasm",
			ownerId,
			new CardEffect
			{
				TargetingStrategy = TargetingStrategy.AllValid(new IsCreatureSpecification()),
				ActionTemplate = new DealDamageAction { Amount = 2 },
			},
			manaCost: 0
		);

	/// <summary>
	/// A single-target damage spell targeting a specific creature or player.
	/// </summary>
	private static Card MakeDamageSpell(int ownerId, int amount) =>
		TestCardFactory.MakeSpellCard(
			"Bolt",
			ownerId,
			new CardEffect
			{
				TargetingStrategy = TargetingStrategy.SingleTarget(
					new IsPlayerSpecification().Or(new IsCreatureSpecification())
				),
				ActionTemplate = new DealDamageAction { Amount = amount },
			},
			manaCost: 0
		);

	private (GameState, Card) AddCreatureToBattlefield(
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
			ManaCost = 0,
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

	private AttackAction MakeAttack(int attackerId, int targetId) =>
		new()
		{
			AttackerId = attackerId,
			TargetId = targetId,
			AttackingPlayerId = _ids.Player1Id,
		};
}
