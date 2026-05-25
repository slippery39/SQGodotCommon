using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Tests for EventTriggerCondition — the generic event-based trigger system.
///
/// Each test has a direct equivalent in TriggeredAbilityTests using the old
/// concrete condition classes. If a test passes here but the win rates differ
/// in simulation, the issue is in the card definition, not the condition itself.
/// </summary>
[TestFixture]
public class EventTriggerConditionTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	// ===== CREATURE DESTROYED =====

	[Test]
	public void CreatureDestroyed_NoFilter_FiresOnAnyCreatureDeath()
	{
		var (s1, _) = AddTriggerCard(
			_state,
			_ids.Player1Id,
			new EventTriggerCondition { EventTypeName = EventTypeNames.CreatureDestroyed },
			GainLifeEffect(1)
		);

		var (s2, attacker) = AddCreature(s1, "Bear", 2, 2, _ids.Player1Id);
		var (s3, defender) = AddCreature(s2, "Squire", 1, 1, _ids.Player2Id);

		var (finalState, _) = s3.AddAction(MakeAttack(attacker.Id, defender.Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(21),
			"Should gain 1 life when any creature dies"
		);
	}

	[Test]
	public void CreatureDestroyed_YourCreatureFilter_DoesNotFireOnOpponentDeath()
	{
		var (s1, _) = AddTriggerCard(
			_state,
			_ids.Player1Id,
			new EventTriggerCondition
			{
				EventTypeName = EventTypeNames.CreatureDestroyed,
				Filter = new IsControlledByYouSpecification(),
			},
			GainLifeEffect(1)
		);

		// Opponent's creature dies — should NOT trigger
		var (s2, attacker) = AddCreature(s1, "Bear", 2, 2, _ids.Player1Id);
		var (s3, defender) = AddCreature(s2, "Squire", 1, 1, _ids.Player2Id);

		var (finalState, _) = s3.AddAction(MakeAttack(attacker.Id, defender.Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(20),
			"Should not fire when opponent's creature dies with OnlyYour filter"
		);
	}

	[Test]
	public void CreatureDestroyed_YourCreatureFilter_FiresWhenYourCreatureDies()
	{
		var (s1, _) = AddTriggerCard(
			_state,
			_ids.Player1Id,
			new EventTriggerCondition
			{
				EventTypeName = EventTypeNames.CreatureDestroyed,
				Filter = new IsControlledByYouSpecification(),
			},
			GainLifeEffect(1)
		);

		// Player 1's creature dies — should trigger
		var (s2, attacker) = AddCreature(s1, "Squire", 1, 1, _ids.Player1Id);
		var (s3, defender) = AddCreature(s2, "Bear", 2, 2, _ids.Player2Id);

		var (finalState, _) = s3.AddAction(MakeAttack(attacker.Id, defender.Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(21),
			"Should fire when your own creature dies"
		);
	}

	[Test]
	public void CreatureDestroyed_FromSpellDamage_Fires()
	{
		var (s1, _) = AddTriggerCard(
			_state,
			_ids.Player1Id,
			new EventTriggerCondition { EventTypeName = EventTypeNames.CreatureDestroyed },
			DealDamageToOpponentEffect(1)
		);

		var (s2, target) = AddCreature(s1, "Squire", 1, 1, _ids.Player2Id);
		var bolt = MakeBolt(_ids.Player1Id);
		var (s3, card) = s2.AddObject(bolt, parentId: _ids.Player1HandId);

		var (finalState, _) = s3.AddAction(
				TestCardFactory.MakeCastActionWithTarget(card.Id, _ids.Player1Id, target.Id)
			)
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(19),
			"Trigger should fire when spell kills a creature"
		);
	}

	// ===== CREATURE PLAYED =====

	[Test]
	public void CreaturePlayed_NoFilter_FiresOnAnyCreatureEntering()
	{
		var (s1, _) = AddTriggerCard(
			_state,
			_ids.Player1Id,
			new EventTriggerCondition { EventTypeName = EventTypeNames.CreaturePlayed },
			GainLifeEffect(1)
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
			"Should gain 1 life when a creature enters the battlefield"
		);
	}

	[Test]
	public void CreaturePlayed_YourCreatureFilter_FiresOnlyForYourCreatures()
	{
		// Put trigger on Player 1, play Player 2's creature — should NOT fire
		var (s1, _) = AddTriggerCard(
			_state,
			_ids.Player1Id,
			new EventTriggerCondition
			{
				EventTypeName = EventTypeNames.CreaturePlayed,
				Filter = new IsControlledByYouSpecification(),
			},
			GainLifeEffect(1)
		);

		// Add card to Player 2's hand and play it
		var creature = TestCardFactory.MakeCreatureCard("Bear", _ids.Player2Id, 2, 2, manaCost: 0);
		var (s2, card) = s1.AddObject(creature, parentId: _ids.Player2HandId);

		var (finalState, _) = s2.AddAction(
				new CastCreatureAction { CardId = card.Id, CastingPlayerId = _ids.Player2Id }
			)
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(20),
			"Should not fire when opponent plays a creature with your-creature filter"
		);
	}

	// ===== CREATURE ATTACKED =====

	[Test]
	public void CreatureAttacked_NoFilter_FiresOnAnyAttack()
	{
		var (s1, _) = AddTriggerCard(
			_state,
			_ids.Player1Id,
			new EventTriggerCondition { EventTypeName = EventTypeNames.CreatureAttacked },
			GainLifeEffect(1)
		);

		var (s2, attacker) = AddCreature(s1, "Bear", 2, 2, _ids.Player1Id);

		var (finalState, _) = s2.AddAction(MakeAttack(attacker.Id, _ids.Player2Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(21),
			"Should gain 1 life when any creature attacks"
		);
	}

	[Test]
	public void CreatureAttacked_YourCreatureFilter_DoesNotFireOnOpponentAttack()
	{
		// Trigger on Player 2, attacker is Player 1's creature
		var (s1, _) = AddTriggerCard(
			_state,
			_ids.Player2Id,
			new EventTriggerCondition
			{
				EventTypeName = EventTypeNames.CreatureAttacked,
				Filter = new IsControlledByYouSpecification(),
			},
			GainLifeEffect(1)
		);

		var (s2, attacker) = AddCreature(s1, "Bear", 2, 2, _ids.Player1Id);

		var (finalState, _) = s2.AddAction(MakeAttack(attacker.Id, _ids.Player2Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(18), // took 2 damage, no life gain
			"Should not fire for opponent's creature attacking"
		);
	}

	// ===== EVENT TYPE NAME MATCHING =====

	[Test]
	public void EventTriggerCondition_DoesNotFire_ForDifferentEventType()
	{
		// Trigger set to CreatureDestroyed but we play a creature — should not fire
		var (s1, _) = AddTriggerCard(
			_state,
			_ids.Player1Id,
			new EventTriggerCondition { EventTypeName = EventTypeNames.CreatureDestroyed },
			GainLifeEffect(1)
		);

		var creature = TestCardFactory.MakeCreatureCard("Bear", _ids.Player1Id, 2, 2, manaCost: 0);
		var (s2, card) = s1.AddObject(creature, parentId: _ids.Player1HandId);

		var (finalState, _) = s2.AddAction(
				new CastCreatureAction { CardId = card.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(20),
			"CreatureDestroyed trigger should not fire on CreaturePlayed event"
		);
	}

	[Test]
	public void EventTriggerCondition_PendingGameEvents_ClearedAfterResolution()
	{
		var (s1, attacker) = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);
		var (s2, defender) = AddCreature(s1, "Squire", 1, 1, _ids.Player2Id);

		var (finalState, _) = s2.AddAction(MakeAttack(attacker.Id, defender.Id))
			.ProcessAllActions();

		Assert.That(
			finalState.PendingGameEvents.IsEmpty,
			Is.True,
			"PendingGameEvents should be cleared after resolution"
		);
	}

	// ===== HELPERS =====

	private (GameState state, int cardId) AddTriggerCard(
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
			Components = ImmutableArray.Create<GameComponent>(
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

	private static CardEffect GainLifeEffect(int amount) =>
		new()
		{
			TargetingStrategy = TargetingStrategy.Self(),
			ActionTemplate = new GainLifeAction { Amount = amount },
		};

	private static CardEffect DealDamageToOpponentEffect(int amount) =>
		new()
		{
			TargetingStrategy = TargetingStrategy.AllValid(
				new IsPlayerSpecification().And(new IsControlledByOpponentSpecification())
			),
			ActionTemplate = new DealDamageAction { Amount = amount },
		};

	private static Card MakeBolt(int ownerId) =>
		TestCardFactory.MakeSpellCard(
			"Bolt",
			ownerId,
			new CardEffect
			{
				TargetingStrategy = TargetingStrategy.SingleTarget(
					new IsPlayerSpecification().Or(new IsCreatureSpecification())
				),
				ActionTemplate = new DealDamageAction { Amount = 3 },
			},
			manaCost: 0
		);

	private (GameState, Card) AddCreature(
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
			Components = ImmutableArray.Create<GameComponent>(
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
