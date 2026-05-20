using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Tests for Valakut, the Molten Pinnacle — the first emblem-granting land.
///
/// Valakut grants the player a Valakut emblem when played. The emblem fires
/// LandsPlayedCondition: once LandsPlayedTotal >= 7, each land the player plays
/// deals 3 damage to a random opponent or opponent creature.
/// LandsPlayedTotal is incremented inside PlayLandAction before the event fires,
/// so the 7th land played (Valakut itself) can trigger the emblem.
/// </summary>
[TestFixture]
public class ValakutTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	[Test]
	public void Valakut_PlayedAs7thLand_GrantsEmblemAndDeals3Damage()
	{
		var state = SetLandsPlayedTotal(_state, _ids.Player1Id, 6);
		var (s2, valakut) = AddValakutToHand(state, _ids.Player1Id);

		var (finalState, _) = s2.AddAction(
				new PlayLandAction { CardId = valakut.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		var player1 = finalState.GetPlayer(_ids.Player1Id);
		Assert.That(
			player1.Emblems.Count,
			Is.EqualTo(1),
			"Playing Valakut should grant the player one emblem"
		);
		Assert.That(
			player1.Emblems[0].Name,
			Is.EqualTo("Valakut"),
			"The emblem should be named Valakut"
		);

		var player2 = finalState.GetPlayer(_ids.Player2Id);
		Assert.That(
			player2.Life,
			Is.EqualTo(17),
			"Valakut emblem should deal 3 damage to the opponent when the 7th land is played"
		);
	}

	[Test]
	public void Valakut_PlayedBeforeThreshold_GrantsEmblemButNoDamage()
	{
		// LandsPlayedTotal will be 5 after playing Valakut — below the threshold of 7
		var state = SetLandsPlayedTotal(_state, _ids.Player1Id, 4);
		var (s2, valakut) = AddValakutToHand(state, _ids.Player1Id);

		var (finalState, _) = s2.AddAction(
				new PlayLandAction { CardId = valakut.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		var player1 = finalState.GetPlayer(_ids.Player1Id);
		Assert.That(
			player1.Emblems.Count,
			Is.EqualTo(1),
			"Valakut should still grant the emblem regardless of threshold"
		);

		var player2 = finalState.GetPlayer(_ids.Player2Id);
		Assert.That(
			player2.Life,
			Is.EqualTo(20),
			"No damage should be dealt when LandsPlayedTotal is below the threshold"
		);
	}

	[Test]
	public void Valakut_OpponentLandPlay_DoesNotTriggerEmblem()
	{
		// Player1 plays Valakut as the 7th land, granting the emblem.
		// Player2 then plays a land — the emblem must not fire (wrong owner).
		var s1 = SetLandsPlayedTotal(_state, _ids.Player1Id, 6);
		var (s2, valakut) = AddValakutToHand(s1, _ids.Player1Id);

		var (s3, _) = s2.AddAction(
				new PlayLandAction { CardId = valakut.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		// Confirm 3 damage was dealt to Player2 on Valakut's play
		Assert.That(s3.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(17));

		var (s4, opponentLand) = AddLandToHand(s3, _ids.Player2Id);
		var (finalState, _) = s4.AddAction(
				new PlayLandAction { CardId = opponentLand.Id, CastingPlayerId = _ids.Player2Id }
			)
			.ProcessAllActions();

		// Player2 played the land — should not trigger Player1's Valakut emblem
		var player2 = finalState.GetPlayer(_ids.Player2Id);
		Assert.That(
			player2.Life,
			Is.EqualTo(17),
			"Opponent land plays must not trigger Player1's Valakut emblem"
		);
	}

	[Test]
	public void Valakut_SubsequentLandsAboveThreshold_ContinueToDealDamage()
	{
		// After Valakut is played as the 7th land (3 damage), a 2nd land play
		// (the 8th total) should deal another 3 damage.
		var s1 = SetLandsPlayedTotal(_state, _ids.Player1Id, 6);
		var (s2, valakut) = AddValakutToHand(s1, _ids.Player1Id);

		var (s3, _) = s2.AddAction(
				new PlayLandAction { CardId = valakut.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		Assert.That(s3.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(17));

		// Reset LandsPlayedThisTurn so a second land can be played in this "turn"
		var p1 = s3.GetPlayer(_ids.Player1Id);
		var s4 = s3.UpdateObject(_ids.Player1Id, p1 with { LandsPlayedThisTurn = 0 });

		var (s5, secondLand) = AddLandToHand(s4, _ids.Player1Id);
		var (finalState, _) = s5.AddAction(
				new PlayLandAction { CardId = secondLand.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(14),
			"Each subsequent land play while at 7+ lands should deal 3 more damage"
		);
	}

	// ===== HELPERS =====

	private static GameState SetLandsPlayedTotal(GameState state, int playerId, int total)
	{
		var player = state.GetPlayer(playerId);
		return state.UpdateObject(playerId, player with { LandsPlayedTotal = total });
	}

	private (GameState, Card) AddValakutToHand(GameState state, int playerId)
	{
		var valakut = CardLibrary.Valakut() with { OwnerId = playerId, ControllerId = playerId };
		var handId = state.GetPlayerZoneId(playerId, ZoneType.Hand);
		return state.AddObject(valakut, parentId: handId);
	}

	private (GameState, Card) AddLandToHand(GameState state, int playerId)
	{
		var land = new Card
		{
			Name = "Forest",
			ManaCost = 0,
			OwnerId = playerId,
			ControllerId = playerId,
			Subtypes = ImmutableList.Create("Land", "Basic"),
		};
		var handId = state.GetPlayerZoneId(playerId, ZoneType.Hand);
		return state.AddObject(land, parentId: handId);
	}
}
