using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Tests for Phase 1 of the permanent system:
///   - Non-creature permanents cast and resolve onto the battlefield
///   - PermanentEnteredBattlefieldEvent fires correctly
///   - Routing: PermanentComponent-only cards use CastPermanentAction, not CastSpellAction
///   - Creature cards still route through CastCreatureAction (no regression)
///   - Mana and validation work identically to CastCreatureAction
/// </summary>
[TestFixture]
public class CastPermanentTests
{
	private GameState _state;
	private MtgGameIds _ids;
	private int _permanentId;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();

		var permanent = TestCardFactory.MakePermanentCard(
			"Test Artifact",
			_ids.Player1Id,
			manaCost: 2,
			subtype: "Artifact"
		);
		var (newState, added) = _state.AddObject(permanent, parentId: _ids.Player1HandId);
		_state = newState;
		_permanentId = added.Id;
	}

	// ===== RESOLUTION =====

	[Test]
	public void CastPermanent_MovesCardToBattlefield()
	{
		var (finalState, _) = _state.AddAction(MakeCast()).ProcessAllActions();

		Assert.That(
			finalState.GetCardZone(_permanentId).ZoneType,
			Is.EqualTo(ZoneType.Battlefield)
		);
	}

	[Test]
	public void CastPermanent_CardLeavesHand()
	{
		var (finalState, _) = _state.AddAction(MakeCast()).ProcessAllActions();

		Assert.That(finalState.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(0));
	}

	[Test]
	public void CastPermanent_EmitsPermanentEnteredBattlefieldEvent()
	{
		var (_, events) = _state.AddAction(MakeCast()).ProcessAllActions();

		var evt = events.OfType<PermanentEnteredBattlefieldEvent>().Single();
		Assert.That(evt.CardId, Is.EqualTo(_permanentId));
		Assert.That(evt.PlayerId, Is.EqualTo(_ids.Player1Id));
	}

	[Test]
	public void CastPermanent_DoesNotEmitCreatureEnteredBattlefieldEvent()
	{
		var (_, events) = _state.AddAction(MakeCast()).ProcessAllActions();

		Assert.That(events.OfType<CreatureEnteredBattlefieldEvent>(), Is.Empty);
	}

	[Test]
	public void CastPermanent_NoSummoningSickness()
	{
		var (finalState, _) = _state.AddAction(MakeCast()).ProcessAllActions();

		var card = (Card)finalState.GetObject(_permanentId);
		Assert.That(card.HasComponent<CreatureComponent>(), Is.False);
	}

	[Test]
	public void CastPermanent_NoActionsRemainAfterResolution()
	{
		var (finalState, _) = _state.AddAction(MakeCast()).ProcessAllActions();

		Assert.That(finalState.HasPendingActions, Is.False);
	}

	// ===== MANA =====

	[Test]
	public void CastPermanent_DeductsMana()
	{
		var stateBefore = _state;
		var manaBefore = stateBefore.GetPlayer(_ids.Player1Id).CurrentMana;

		var (finalState, _) = _state.AddAction(MakeCast()).ProcessAllActions();

		var manaAfter = finalState.GetPlayer(_ids.Player1Id).CurrentMana;
		Assert.That(manaAfter, Is.EqualTo(manaBefore - 2));
	}

	[Test]
	public void CastPermanent_FailsIfNotEnoughMana()
	{
		var expensiveCard = TestCardFactory.MakePermanentCard(
			"Expensive",
			_ids.Player1Id,
			manaCost: 100
		);
		var (stateWithCard, addedCard) = _state.AddObject(
			expensiveCard,
			parentId: _ids.Player1HandId
		);

		var (_, success) = stateWithCard.TryAddAction(
			new CastPermanentAction { CardId = addedCard.Id, CastingPlayerId = _ids.Player1Id }
		);

		Assert.That(success, Is.False);
	}

	// ===== VALIDATION =====

	[Test]
	public void CastPermanent_FailsIfCardNotInHand()
	{
		var stateWithCardInGraveyard = _state.MoveObject(_permanentId, _ids.Player1GraveyardId);

		var (_, success) = stateWithCardInGraveyard.TryAddAction(MakeCast());

		Assert.That(success, Is.False);
	}

	[Test]
	public void CastPermanent_FailsIfNotController()
	{
		var (_, success) = _state.TryAddAction(
			new CastPermanentAction { CardId = _permanentId, CastingPlayerId = _ids.Player2Id }
		);

		Assert.That(success, Is.False);
	}

	[Test]
	public void CastPermanent_FailsIfCardHasNoComponent()
	{
		var plainCard = new Card
		{
			Name = "Plain Card",
			ManaCost = 1,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (stateWithCard, addedCard) = _state.AddObject(plainCard, parentId: _ids.Player1HandId);

		var (_, success) = stateWithCard.TryAddAction(
			new CastPermanentAction { CardId = addedCard.Id, CastingPlayerId = _ids.Player1Id }
		);

		Assert.That(success, Is.False);
	}

	[Test]
	public void CastPermanent_FailsIfCardIsCreature()
	{
		var creature = TestCardFactory.MakeCreatureCard("Bear", _ids.Player1Id, 2, 2);
		var (stateWithCard, addedCard) = _state.AddObject(creature, parentId: _ids.Player1HandId);

		var (_, success) = stateWithCard.TryAddAction(
			new CastPermanentAction { CardId = addedCard.Id, CastingPlayerId = _ids.Player1Id }
		);

		Assert.That(success, Is.False);
	}

	// ===== ROUTING =====

	[Test]
	public void ActionGenerator_IncludesPermanentInLegalActions()
	{
		var actions = MtgActionGenerator.GetLegalActions(_state, _ids, _ids.Player1Id);

		Assert.That(
			actions.OfType<CastPermanentAction>().Any(a => a.CardId == _permanentId),
			Is.True
		);
	}

	[Test]
	public void ActionGenerator_DoesNotIncludeSpellActionForPermanent()
	{
		var actions = MtgActionGenerator.GetLegalActions(_state, _ids, _ids.Player1Id);

		Assert.That(actions.OfType<CastSpellAction>().Any(a => a.CardId == _permanentId), Is.False);
	}

	[Test]
	public void ActionGenerator_CreatureStillRoutesToCastCreatureAction()
	{
		var creature = TestCardFactory.MakeCreatureCard("Bear", _ids.Player1Id, 2, 2);
		var (stateWithCreature, addedCreature) = _state.AddObject(
			creature,
			parentId: _ids.Player1HandId
		);

		var actions = MtgActionGenerator.GetLegalActions(stateWithCreature, _ids, _ids.Player1Id);

		Assert.That(
			actions.OfType<CastCreatureAction>().Any(a => a.CardId == addedCreature.Id),
			Is.True
		);
		Assert.That(
			actions.OfType<CastPermanentAction>().Any(a => a.CardId == addedCreature.Id),
			Is.False
		);
	}

	// ===== HELPERS =====

	private CastPermanentAction MakeCast() =>
		new() { CardId = _permanentId, CastingPlayerId = _ids.Player1Id };
}
