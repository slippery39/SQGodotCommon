using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Tests for Phase 4 equipment: Bonesplitter.
///
/// Bonesplitter (1-mana Artifact Equipment):
///   - Enters the battlefield as a non-creature permanent
///   - Equip {2}: attach to a creature you control, giving it +2/+0
///   - Can only be equipped once per turn (HasActivated gate)
///   - Equipping to a second creature removes the boost from the first
///   - When the equipped creature leaves the battlefield, Bonesplitter detaches
///     and stays on the battlefield unequipped
/// </summary>
[TestFixture]
public class EquipmentTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	[Test]
	public void Bonesplitter_EntersBattlefield()
	{
		var (state, bsId) = CastFromHand(TestCardLibrary.Bonesplitter());

		Assert.That(state.GetCardZone(bsId).ZoneType, Is.EqualTo(ZoneType.Battlefield));
	}

	[Test]
	public void Bonesplitter_DoesNotBoostCreatureBeforeEquipping()
	{
		var (s1, creatureId) = AddCreatureToBattlefield(
			_state,
			_ids.Player1Id,
			power: 2,
			toughness: 2
		);
		var (s2, _) = CastFromHand(s1, TestCardLibrary.Bonesplitter());

		Assert.That(s2.GetEffectivePower(creatureId), Is.EqualTo(2));
	}

	[Test]
	public void Bonesplitter_BoostsPowerWhenEquipped()
	{
		var (s1, creatureId) = AddCreatureToBattlefield(
			_state,
			_ids.Player1Id,
			power: 2,
			toughness: 2
		);
		var (s2, bsId) = CastFromHand(s1, TestCardLibrary.Bonesplitter());
		var s3 = Equip(s2, bsId, creatureId);

		Assert.That(s3.GetEffectivePower(creatureId), Is.EqualTo(4));
		Assert.That(s3.GetEffectiveToughness(creatureId), Is.EqualTo(2));
	}

	[Test]
	public void Bonesplitter_CanOnlyEquipOncePerTurn()
	{
		var (s1, creatureId) = AddCreatureToBattlefield(
			_state,
			_ids.Player1Id,
			power: 1,
			toughness: 1
		);
		var (s2, bsId) = CastFromHand(s1, TestCardLibrary.Bonesplitter());
		var s3 = Equip(s2, bsId, creatureId);

		var (_, success) = s3.TryAddAction(MakeEquipAction(s3, bsId, creatureId));

		Assert.That(success, Is.False);
	}

	[Test]
	public void Bonesplitter_EquipResetsEachTurn()
	{
		var (s1, creatureId) = AddCreatureToBattlefield(
			_state,
			_ids.Player1Id,
			power: 1,
			toughness: 1
		);
		var (s2, bsId) = CastFromHand(s1, TestCardLibrary.Bonesplitter());
		var s3 = Equip(s2, bsId, creatureId);
		var s4 = SimulateNextTurn(s3);

		var (_, success) = s4.TryAddAction(MakeEquipAction(s4, bsId, creatureId));

		Assert.That(success, Is.True);
	}

	[Test]
	public void Bonesplitter_MovingToNewCreatureRemovesBoostFromOld()
	{
		var (s1, creature1Id) = AddCreatureToBattlefield(
			_state,
			_ids.Player1Id,
			power: 1,
			toughness: 1
		);
		var (s2, creature2Id) = AddCreatureToBattlefield(
			s1,
			_ids.Player1Id,
			power: 1,
			toughness: 1
		);
		var (s3, bsId) = CastFromHand(s2, TestCardLibrary.Bonesplitter());

		// Equip to creature1 this turn, then simulate next turn and re-equip to creature2
		var s4 = Equip(s3, bsId, creature1Id);
		Assert.That(
			s4.GetEffectivePower(creature1Id),
			Is.EqualTo(3),
			"creature1 should be boosted"
		);

		var s5 = SimulateNextTurn(s4);
		var s6 = Equip(s5, bsId, creature2Id);

		Assert.That(
			s6.GetEffectivePower(creature1Id),
			Is.EqualTo(1),
			"boost removed from creature1"
		);
		Assert.That(s6.GetEffectivePower(creature2Id), Is.EqualTo(3), "boost applied to creature2");
	}

	[Test]
	public void Bonesplitter_DetachesWhenEquippedCreatureDies()
	{
		var (s1, creatureId) = AddCreatureToBattlefield(
			_state,
			_ids.Player1Id,
			power: 2,
			toughness: 2
		);
		var (s2, bsId) = CastFromHand(s1, TestCardLibrary.Bonesplitter());
		var s3 = Equip(s2, bsId, creatureId);

		// Kill the creature â€” fires PermanentLeftBattlefieldEvent
		var s4 = KillCreature(s3, creatureId, _ids.Player1Id);

		// Bonesplitter stays on the battlefield
		Assert.That(s4.GetCardZone(bsId).ZoneType, Is.EqualTo(ZoneType.Battlefield));

		// EquipmentComponent.EquippedToCardId reset to 0
		var bs = (Card)s4.GetObject(bsId);
		var equip = bs.GetComponent<EquipmentComponent>();
		Assert.That(equip!.EquippedToCardId, Is.EqualTo(0));
	}

	[Test]
	public void Bonesplitter_StaysOnBattlefieldWhenCreatureDies()
	{
		var (s1, creatureId) = AddCreatureToBattlefield(
			_state,
			_ids.Player1Id,
			power: 2,
			toughness: 2
		);
		var (s2, bsId) = CastFromHand(s1, TestCardLibrary.Bonesplitter());
		var s3 = Equip(s2, bsId, creatureId);

		var s4 = KillCreature(s3, creatureId, _ids.Player1Id);

		Assert.That(s4.GetCardZone(bsId).ZoneType, Is.EqualTo(ZoneType.Battlefield));
	}

	[Test]
	public void Bonesplitter_DoesNotBoostOpponentCreatures()
	{
		var (s1, oppCreatureId) = AddCreatureToBattlefield(
			_state,
			_ids.Player2Id,
			power: 2,
			toughness: 2
		);
		var (s2, bsId) = CastFromHand(s1, TestCardLibrary.Bonesplitter());

		// Try to equip to opponent's creature â€” should fail validation
		var equipAction = new ActivateAbilityAction
		{
			CardId = bsId,
			ActivatingPlayerId = _ids.Player1Id,
			AbilityIndex = 0,
			TargetIds = ImmutableList.Create(oppCreatureId),
		};
		var (_, success) = s2.TryAddAction(equipAction);

		Assert.That(success, Is.False);
	}

	/// <summary>
	/// An Aura's OWN "when this leaves the battlefield" trigger has to fire when the creature it
	/// enchanted dies — that is the only way most Auras ever leave play.
	///
	/// The Aura is moved to the graveyard by DetachEquipmentFromLeavingCard, which runs inside
	/// ProcessStaticAbilityUpdates — i.e. DURING the post-processor pass, after the local
	/// pendingEvents list was captured from state. EvaluateTriggeredAbilities reads that local and
	/// Execute wipes state.PendingGameEvents immediately after, so the Aura's departure event was
	/// staged and then discarded without ever being offered to a trigger. Rancor never returned to
	/// hand. Same class as the zero-toughness sweep, which threads `ref pendingEvents` for exactly
	/// this reason.
	///
	/// Inline card so rebalancing Rancor cannot delete the coverage.
	/// </summary>
	[Test]
	public void Aura_LeaveTheBattlefieldTrigger_FiresWhenTheEnchantedCreatureDies()
	{
		var rancor = MtgCore
			.Cards.Builders.CardFactory.Enchantment("Test Rancor", manaCost: 1)
			.AsAura(
				powerBonus: 2,
				toughnessBonus: 0,
				targeting: TargetingStrategy.SingleTarget(
					TargetSpecification.CreatureControlledByYou()
				)
			)
			.WithTriggeredAbility(
				"Undying",
				new EventTriggerCondition
				{
					EventTypeName = EventTypeNames.PermanentLeftBattlefield,
					Filter = new IsSourceCardSpecification(),
				},
				eb =>
					eb.WithAction(
						new MoveCardToHandAction
						{
							CardIdContextKey = ContextKeys.SourceCardId,
							PlayerIdContextKey = ContextKeys.CastingPlayerId,
						},
						TargetingStrategy.NoTarget()
					),
				activeInZone: ZoneType.Graveyard
			)
			.Build();

		var (withCreature, creatureId) = AddCreatureToBattlefield(_state, _ids.Player1Id, 2, 2);

		var card = rancor with { OwnerId = _ids.Player1Id, ControllerId = _ids.Player1Id };
		var (withAura, aura) = withCreature.AddObject(card, parentId: _ids.Player1HandId);
		var (attached, _) = withAura
			.AddAction(
				new CastPermanentAction
				{
					CardId = aura.Id,
					CastingPlayerId = _ids.Player1Id,
					TargetIds = ImmutableList.Create(creatureId),
				}
			)
			.ProcessAllActions();

		Assert.That(
			attached.GetCardZone(aura.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"precondition: the Aura resolved and attached"
		);

		var (final, _) = attached
			.AddAction(new DestroyCreatureAction { TargetIds = [creatureId] })
			.ProcessAllActions();

		Assert.That(
			final.GetCardZone(aura.Id).ZoneType,
			Is.EqualTo(ZoneType.Hand),
			"the Aura's own departure trigger must fire when its host dies"
		);
	}

	// ===== HELPERS =====

	private (GameState, int cardId) CastFromHand(Card template) => CastFromHand(_state, template);

	private (GameState state, int cardId) CastFromHand(GameState state, Card template)
	{
		var card = template with { OwnerId = _ids.Player1Id, ControllerId = _ids.Player1Id };
		var (stateWithCard, added) = state.AddObject(card, parentId: _ids.Player1HandId);
		var (resolved, _) = stateWithCard
			.AddAction(
				new CastPermanentAction { CardId = added.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();
		return (resolved, added.Id);
	}

	private (GameState, int creatureId) AddCreatureToBattlefield(
		GameState state,
		int ownerId,
		int power,
		int toughness
	)
	{
		var card = TestCardFactory.MakeCreatureCard("Creature", ownerId, power, toughness);
		var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);
		var (newState, added) = state.AddObject(card, parentId: battlefieldId);
		var enteredEvent = new CreatureEnteredBattlefieldEvent
		{
			CardId = added.Id,
			PlayerId = ownerId,
		};
		newState = newState with
		{
			PendingGameEvents = newState.PendingGameEvents.Add(enteredEvent),
		};
		var (processed, _) = newState.AddAction(new EndResolutionScopeAction()).ProcessAllActions();
		return (processed, added.Id);
	}

	private GameState Equip(GameState state, int equipmentId, int targetCreatureId)
	{
		var action = MakeEquipAction(state, equipmentId, targetCreatureId);
		var (result, _) = state.AddAction(action).ProcessAllActions();
		return result;
	}

	private static ActivateAbilityAction MakeEquipAction(
		GameState state,
		int equipmentId,
		int targetCreatureId
	) =>
		new()
		{
			CardId = equipmentId,
			ActivatingPlayerId = ((Card)state.GetObject(equipmentId)).ControllerId,
			AbilityIndex = 0,
			TargetIds = ImmutableList.Create(targetCreatureId),
		};

	private GameState KillCreature(GameState state, int creatureId, int ownerId)
	{
		var graveyardId = state.GetPlayerZoneId(ownerId, ZoneType.Graveyard);
		state = state.MoveObject(creatureId, graveyardId);
		var leftEvent = new PermanentLeftBattlefieldEvent
		{
			CardId = creatureId,
			OwnerId = ownerId,
		};
		state = state with { PendingGameEvents = ImmutableList.Create<GameEvent>(leftEvent) };
		var (processed, _) = state.AddAction(new EndResolutionScopeAction()).ProcessAllActions();
		return processed;
	}

	private GameState SimulateNextTurn(GameState state)
	{
		var (next, _) = state
			.AddAction(
				new StartTurnAction
				{
					ActivePlayerId = _ids.Player1Id,
					BattlefieldId = _ids.Player1BattlefieldId,
					SkipDraw = true,
				}
			)
			.ProcessAllActions();
		return next;
	}
}
