using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

[TestFixture]
public class MtgCoreDataModelTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.Create();
	}

	// ===== GAME ROOT =====

	[Test]
	public void GameRoot_Exists()
	{
		Assert.That(_state.HasObject(_ids.GameId), Is.True);
		Assert.That(_state.GetObject(_ids.GameId), Is.TypeOf<MtgGame>());
	}

	// ===== STACK (only shared zone) =====

	[Test]
	public void Stack_ExistsAsChildOfGame()
	{
		var gameChildren = _state.GetChildren(_ids.GameId).ToList();
		Assert.That(gameChildren.OfType<Zone>().Any(z => z.ZoneType == ZoneType.Stack), Is.True);
	}

	[Test]
	public void Stack_IsPublic()
	{
		var stack = _state.GetZone(_ids.StackId);
		Assert.That(stack.IsPublic, Is.True);
	}

	[Test]
	public void Stack_IsChildOfGameRoot_NotAPlayer()
	{
		Assert.That(_state.GetParent(_ids.StackId), Is.EqualTo(_ids.GameId));
	}

	[Test]
	public void GameRoot_HasNoOtherSharedZones()
	{
		var sharedZones = _state.GetChildren(_ids.GameId).OfType<Zone>().ToList();
		Assert.That(sharedZones, Has.Count.EqualTo(1));
		Assert.That(sharedZones[0].ZoneType, Is.EqualTo(ZoneType.Stack));
	}

	// ===== PLAYERS =====

	[Test]
	public void Players_ExistAsChildrenOfGame()
	{
		var gameChildren = _state.GetChildren(_ids.GameId).ToList();
		Assert.That(gameChildren.OfType<MtgPlayer>().Count(), Is.EqualTo(2));
	}

	[Test]
	public void Players_StartWithTwentyLife()
	{
		var p1 = _state.GetPlayer(_ids.Player1Id);
		var p2 = _state.GetPlayer(_ids.Player2Id);

		Assert.That(p1.Life, Is.EqualTo(20));
		Assert.That(p2.Life, Is.EqualTo(20));
	}

	[Test]
	public void Players_AreChildrenOfGameRoot()
	{
		Assert.That(_state.GetParent(_ids.Player1Id), Is.EqualTo(_ids.GameId));
		Assert.That(_state.GetParent(_ids.Player2Id), Is.EqualTo(_ids.GameId));
	}

	// ===== PLAYER ZONES =====

	[Test]
	public void EachPlayer_HasAllFiveZones()
	{
		foreach (var playerId in new[] { _ids.Player1Id, _ids.Player2Id })
		{
			var zones = _state.GetChildren(playerId).OfType<Zone>().ToList();

			Assert.That(
				zones.Any(z => z.ZoneType == ZoneType.Hand),
				Is.True,
				$"Player {playerId} should have a Hand"
			);
			Assert.That(
				zones.Any(z => z.ZoneType == ZoneType.Library),
				Is.True,
				$"Player {playerId} should have a Library"
			);
			Assert.That(
				zones.Any(z => z.ZoneType == ZoneType.Graveyard),
				Is.True,
				$"Player {playerId} should have a Graveyard"
			);
			Assert.That(
				zones.Any(z => z.ZoneType == ZoneType.Battlefield),
				Is.True,
				$"Player {playerId} should have a Battlefield"
			);
			Assert.That(
				zones.Any(z => z.ZoneType == ZoneType.Exile),
				Is.True,
				$"Player {playerId} should have an Exile"
			);
		}
	}

	[Test]
	public void EachPlayer_HasExactlyFiveZones()
	{
		foreach (var playerId in new[] { _ids.Player1Id, _ids.Player2Id })
		{
			var zones = _state.GetChildren(playerId).OfType<Zone>().ToList();
			Assert.That(
				zones,
				Has.Count.EqualTo(5),
				$"Player {playerId} should have exactly 5 zones"
			);
		}
	}

	[Test]
	public void PlayerZones_HaveCorrectVisibility()
	{
		var p1Hand = _state.GetZone(_ids.Player1HandId);
		var p1Library = _state.GetZone(_ids.Player1LibraryId);
		var p1Graveyard = _state.GetZone(_ids.Player1GraveyardId);
		var p1Battlefield = _state.GetZone(_ids.Player1BattlefieldId);
		var p1Exile = _state.GetZone(_ids.Player1ExileId);

		Assert.That(p1Hand.IsPublic, Is.False, "Hand should be private");
		Assert.That(p1Library.IsPublic, Is.False, "Library should be private");
		Assert.That(p1Graveyard.IsPublic, Is.True, "Graveyard should be public");
		Assert.That(p1Battlefield.IsPublic, Is.True, "Battlefield should be public");
		Assert.That(p1Exile.IsPublic, Is.True, "Exile should be public");
	}

	[Test]
	public void PlayerZones_AreChildrenOfCorrectPlayer()
	{
		Assert.That(_state.GetParent(_ids.Player1HandId), Is.EqualTo(_ids.Player1Id));
		Assert.That(_state.GetParent(_ids.Player1LibraryId), Is.EqualTo(_ids.Player1Id));
		Assert.That(_state.GetParent(_ids.Player1GraveyardId), Is.EqualTo(_ids.Player1Id));
		Assert.That(_state.GetParent(_ids.Player1BattlefieldId), Is.EqualTo(_ids.Player1Id));
		Assert.That(_state.GetParent(_ids.Player1ExileId), Is.EqualTo(_ids.Player1Id));

		Assert.That(_state.GetParent(_ids.Player2HandId), Is.EqualTo(_ids.Player2Id));
		Assert.That(_state.GetParent(_ids.Player2LibraryId), Is.EqualTo(_ids.Player2Id));
		Assert.That(_state.GetParent(_ids.Player2GraveyardId), Is.EqualTo(_ids.Player2Id));
		Assert.That(_state.GetParent(_ids.Player2BattlefieldId), Is.EqualTo(_ids.Player2Id));
		Assert.That(_state.GetParent(_ids.Player2ExileId), Is.EqualTo(_ids.Player2Id));
	}

	// ===== EXTENSION HELPERS =====

	[Test]
	public void GetPlayerZone_ReturnsCorrectZone()
	{
		var hand = _state.GetPlayerZone(_ids.Player1Id, ZoneType.Hand);
		Assert.That(hand.ZoneType, Is.EqualTo(ZoneType.Hand));
		Assert.That(hand.Id, Is.EqualTo(_ids.Player1HandId));

		var battlefield = _state.GetPlayerZone(_ids.Player1Id, ZoneType.Battlefield);
		Assert.That(battlefield.ZoneType, Is.EqualTo(ZoneType.Battlefield));
		Assert.That(battlefield.Id, Is.EqualTo(_ids.Player1BattlefieldId));
	}

	[Test]
	public void GetStack_ReturnsCorrectZone()
	{
		var stack = _state.GetStack(_ids.GameId);
		Assert.That(stack.ZoneType, Is.EqualTo(ZoneType.Stack));
		Assert.That(stack.Id, Is.EqualTo(_ids.StackId));
	}

	// ===== CARDS IN ZONES =====

	[Test]
	public void AllZones_StartEmpty()
	{
		var allZoneIds = new[]
		{
			_ids.StackId,
			_ids.Player1HandId,
			_ids.Player1LibraryId,
			_ids.Player1GraveyardId,
			_ids.Player1BattlefieldId,
			_ids.Player1ExileId,
			_ids.Player2HandId,
			_ids.Player2LibraryId,
			_ids.Player2GraveyardId,
			_ids.Player2BattlefieldId,
			_ids.Player2ExileId,
		};

		foreach (var zoneId in allZoneIds)
			Assert.That(
				_state.GetCardsInZone(zoneId).Count(),
				Is.EqualTo(0),
				$"Zone {zoneId} should start empty"
			);
	}

	[Test]
	public void Card_AddedToHand_IsQueryableInThatZone()
	{
		var (newState, card) = _state.AddObject(
			new Card
			{
				Name = "Lightning Bolt",
				ManaCost = 1,
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: _ids.Player1HandId
		);

		var handCards = newState.GetCardsInZone(_ids.Player1HandId).ToList();

		Assert.That(handCards, Has.Count.EqualTo(1));
		Assert.That(handCards[0].Name, Is.EqualTo("Lightning Bolt"));
	}

	[Test]
	public void Card_MovedBetweenZones_ReflectsNewZone()
	{
		var (stateWithCard, card) = _state.AddObject(
			new Card
			{
				Name = "Lightning Bolt",
				ManaCost = 1,
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: _ids.Player1HandId
		);

		var stateAfterMove = stateWithCard.MoveObject(card.Id, _ids.Player1GraveyardId);

		Assert.That(
			stateAfterMove.GetCardsInZone(_ids.Player1HandId).Count(),
			Is.EqualTo(0),
			"Hand should be empty after moving card"
		);
		Assert.That(
			stateAfterMove.GetCardsInZone(_ids.Player1GraveyardId).Count(),
			Is.EqualTo(1),
			"Graveyard should contain the card"
		);

		var cardZone = stateAfterMove.GetCardZone(card.Id);
		Assert.That(cardZone.ZoneType, Is.EqualTo(ZoneType.Graveyard));
	}

	[Test]
	public void TwoPlayers_HaveSeparateBattlefields()
	{
		var (s1, p1Creature) = _state.AddObject(
			new Card
			{
				Name = "Bear",
				ManaCost = 2,
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: _ids.Player1BattlefieldId
		);
		var (s2, p2Creature) = s1.AddObject(
			new Card
			{
				Name = "Bear",
				ManaCost = 2,
				OwnerId = _ids.Player2Id,
				ControllerId = _ids.Player2Id,
			},
			parentId: _ids.Player2BattlefieldId
		);

		Assert.That(
			s2.GetCardsInZone(_ids.Player1BattlefieldId).Count(),
			Is.EqualTo(1),
			"Player 1 battlefield should only have Player 1's creature"
		);
		Assert.That(
			s2.GetCardsInZone(_ids.Player2BattlefieldId).Count(),
			Is.EqualTo(1),
			"Player 2 battlefield should only have Player 2's creature"
		);
	}

	[Test]
	public void GameState_IsImmutable_MovingCardDoesNotAffectOriginalState()
	{
		var (stateWithCard, card) = _state.AddObject(
			new Card
			{
				Name = "Lightning Bolt",
				ManaCost = 1,
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: _ids.Player1HandId
		);

		var _ = stateWithCard.MoveObject(card.Id, _ids.Player1GraveyardId);

		Assert.That(
			stateWithCard.GetCardsInZone(_ids.Player1HandId).Count(),
			Is.EqualTo(1),
			"Original state hand should be unchanged"
		);
		Assert.That(
			stateWithCard.GetCardsInZone(_ids.Player1GraveyardId).Count(),
			Is.EqualTo(0),
			"Original state graveyard should still be empty"
		);
	}
}
