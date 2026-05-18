using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

[TestFixture]
public class FlashbackTests
{
	private GameState _state;
	private MtgGameIds _ids;
	private int _fireboltId;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();

		var firebolt = CardLibrary.GetByName("Firebolt") with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (newState, addedCard) = _state.AddObject(firebolt, parentId: _ids.Player1GraveyardId);
		_state = newState;
		_fireboltId = addedCard.Id;
	}

	// ===== VALIDATION =====

	[Test]
	public void CastFromGraveyard_FailsIfCardIsInHand()
	{
		var stateWithCardInHand = _state.MoveObject(_fireboltId, _ids.Player1HandId);
		var action = MakeCastFireboltAt(_ids.Player2Id);

		var (_, success) = stateWithCardInHand.TryAddAction(action);

		Assert.That(success, Is.False);
	}

	[Test]
	public void CastFromGraveyard_FailsIfCardHasNoFlashback()
	{
		var boltInGraveyard = CardLibrary.LightningBolt() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (stateWithBolt, addedBolt) = _state.AddObject(
			boltInGraveyard,
			parentId: _ids.Player1GraveyardId
		);

		var action = new CastFromGraveyardAction
		{
			CardId = addedBolt.Id,
			CastingPlayerId = _ids.Player1Id,
			TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
				0,
				ImmutableList.Create(_ids.Player2Id)
			),
		};

		var (_, success) = stateWithBolt.TryAddAction(action);
		Assert.That(success, Is.False);
	}

	[Test]
	public void CastFromGraveyard_FailsIfNotEnoughMana()
	{
		var player = _state.GetPlayer(_ids.Player1Id);
		var brokeState = _state.UpdateObject(
			_ids.Player1Id,
			player with
			{
				CurrentMana = 1,
			} // Firebolt flashback costs 2
		);

		var (_, success) = brokeState.TryAddAction(MakeCastFireboltAt(_ids.Player2Id));

		Assert.That(success, Is.False);
	}

	[Test]
	public void CastFromGraveyard_SucceedsWithValidSetup()
	{
		var (_, success) = _state.TryAddAction(MakeCastFireboltAt(_ids.Player2Id));

		Assert.That(success, Is.True);
	}

	// ===== RESOLUTION =====

	[Test]
	public void Firebolt_Flashback_Deals2DamageToPlayer()
	{
		var (finalState, _) = _state
			.AddAction(MakeCastFireboltAt(_ids.Player2Id))
			.ProcessAllActions();

		var player2 = finalState.GetPlayer(_ids.Player2Id);
		Assert.That(player2.Life, Is.EqualTo(18));
	}

	[Test]
	public void Firebolt_Flashback_ExilesCardAfterResolution()
	{
		var (finalState, _) = _state
			.AddAction(MakeCastFireboltAt(_ids.Player2Id))
			.ProcessAllActions();

		var zone = finalState.GetCardZone(_fireboltId);
		Assert.That(zone.ZoneType, Is.EqualTo(ZoneType.Exile));
	}

	[Test]
	public void Firebolt_Flashback_DoesNotGoToGraveyard()
	{
		var (finalState, _) = _state
			.AddAction(MakeCastFireboltAt(_ids.Player2Id))
			.ProcessAllActions();

		var graveyardCards = finalState.GetCardsInZone(_ids.Player1GraveyardId);
		Assert.That(graveyardCards.Any(c => c.Id == _fireboltId), Is.False);
	}

	[Test]
	public void Firebolt_Flashback_SpendsMana()
	{
		var (finalState, _) = _state
			.AddAction(MakeCastFireboltAt(_ids.Player2Id))
			.ProcessAllActions();

		var player1 = finalState.GetPlayer(_ids.Player1Id);
		Assert.That(player1.CurrentMana, Is.EqualTo(97)); // 99 - flashback cost 2
	}

	// ===== ACTION GENERATOR =====

	[Test]
	public void ActionGenerator_IncludesFlashbackAction_WhenFlashbackCardInGraveyard()
	{
		var actions = MtgActionGenerator.GetLegalActions(_state, _ids.Player1Id);

		Assert.That(
			actions.OfType<CastFromGraveyardAction>().Any(a => a.CardId == _fireboltId),
			Is.True
		);
	}

	[Test]
	public void ActionGenerator_DoesNotIncludeFlashbackAction_WhenNoFlashbackCardsInGraveyard()
	{
		var emptyGraveyardState = _state.MoveObject(_fireboltId, _ids.Player1HandId);
		var actions = MtgActionGenerator.GetLegalActions(emptyGraveyardState, _ids.Player1Id);

		Assert.That(actions.OfType<CastFromGraveyardAction>().Any(), Is.False);
	}

	[Test]
	public void ActionGenerator_ExcludesFlashbackAction_WhenNotEnoughMana()
	{
		var player = _state.GetPlayer(_ids.Player1Id);
		var brokeState = _state.UpdateObject(_ids.Player1Id, player with { CurrentMana = 1 });

		var actions = MtgActionGenerator.GetLegalActions(brokeState, _ids.Player1Id);

		Assert.That(actions.OfType<CastFromGraveyardAction>().Any(), Is.False);
	}

	// ===== HELPERS =====

	private CastFromGraveyardAction MakeCastFireboltAt(int targetId) =>
		new()
		{
			CardId = _fireboltId,
			CastingPlayerId = _ids.Player1Id,
			TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
				0,
				ImmutableList.Create(targetId)
			),
		};
}
