using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

[TestFixture]
public class PutIntoBattlefieldTests
{
	private GameState _state;
	private MtgGameIds _ids;
	private int _creatureId;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();

		var creature = TestCardFactory.MakeCreatureCard("Grizzly Bears", _ids.Player1Id, 2, 2);
		var (newState, added) = _state.AddObject(creature, parentId: _ids.Player1HandId);
		_state = newState;
		_creatureId = added.Id;
	}

	// ===== RESOLUTION =====

	[Test]
	public void PutIntoBattlefield_MovesCardToBattlefield()
	{
		var (finalState, _) = _state.AddAction(MakePut()).ProcessAllActions();

		Assert.That(finalState.GetCardZone(_creatureId).ZoneType, Is.EqualTo(ZoneType.Battlefield));
	}

	[Test]
	public void PutIntoBattlefield_CardLeavesHand()
	{
		var (finalState, _) = _state.AddAction(MakePut()).ProcessAllActions();

		Assert.That(finalState.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(0));
	}

	[Test]
	public void PutIntoBattlefield_StampsSummoningSickness()
	{
		var (finalState, _) = _state.AddAction(MakePut()).ProcessAllActions();

		var card = (Card)finalState.GetObject(_creatureId);
		Assert.That(card.GetComponent<CreatureComponent>()!.HasSummoningSickness, Is.True);
	}

	[Test]
	public void PutIntoBattlefield_DoesNotDeductMana()
	{
		var player = _state.GetPlayer(_ids.Player1Id);
		var manaBefore = player.CurrentMana;

		var (finalState, _) = _state.AddAction(MakePut()).ProcessAllActions();

		var playerAfter = finalState.GetPlayer(_ids.Player1Id);
		Assert.That(playerAfter.CurrentMana, Is.EqualTo(manaBefore));
	}

	[Test]
	public void PutIntoBattlefield_EmitsCreatureEnteredBattlefieldEvent()
	{
		var (_, events) = _state.AddAction(MakePut()).ProcessAllActions();

		var evt = events.OfType<CreatureEnteredBattlefieldEvent>().Single();
		Assert.That(evt.CardId, Is.EqualTo(_creatureId));
		Assert.That(evt.PlayerId, Is.EqualTo(_ids.Player1Id));
	}

	[Test]
	public void PutIntoBattlefield_DoesNotEmitCreaturePlayedEvent()
	{
		var (_, events) = _state.AddAction(MakePut()).ProcessAllActions();

		Assert.That(events.OfType<CreaturePlayedEvent>(), Is.Empty);
	}

	// ===== ETB TRIGGER =====

	[Test]
	public void PutIntoBattlefield_FiresEtbTrigger()
	{
		// Card on Player 1's battlefield: "when a creature enters the battlefield, deal 1 damage to opponent"
		var triggerCard = new Card
		{
			Name = "ETB Watcher",
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = 1, Toughness = 1 },
				new TriggeredAbilityComponent
				{
					Name = "ETB Ping",
					Condition = new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
					},
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.RandomTarget(
							new IsControlledByOpponentSpecification()
						),
						ActionTemplate = new DealDamageAction { Amount = 1 },
					},
				}
			),
		};

		var (stateWithWatcher, _) = _state.AddObject(
			triggerCard,
			parentId: _ids.Player1BattlefieldId
		);

		var player2Before = stateWithWatcher.GetPlayer(_ids.Player2Id);

		var (finalState, _) = stateWithWatcher.AddAction(MakePut()).ProcessAllActions();

		var player2After = finalState.GetPlayer(_ids.Player2Id);
		Assert.That(player2After.Life, Is.EqualTo(player2Before.Life - 1));
	}

	// ===== PlayCreatureAction also emits CreatureEnteredBattlefieldEvent =====

	[Test]
	public void CastCreature_AlsoEmitsCreatureEnteredBattlefieldEvent()
	{
		var (_, events) = _state
			.AddAction(
				new CastCreatureAction { CardId = _creatureId, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		var evt = events.OfType<CreatureEnteredBattlefieldEvent>().SingleOrDefault();
		Assert.That(evt, Is.Not.Null);
		Assert.That(evt!.CardId, Is.EqualTo(_creatureId));
	}

	// ===== HELPERS =====

	private PutIntoBattlefieldAction MakePut() =>
		new() { TargetIds = ImmutableList.Create(_creatureId) };
}
