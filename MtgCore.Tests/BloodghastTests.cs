using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Tests for Bloodghast's graveyard landfall trigger and the ActiveInZone mechanism.
///
/// Bloodghast has a TriggeredAbilityComponent with ActiveInZone = Graveyard.
/// CheckStateBasedEffectsAction scans both graveyards after every action, firing
/// abilities with ActiveInZone = Graveyard only from that pass — not from the
/// battlefield pass.
/// </summary>
[TestFixture]
public class BloodghastTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	[Test]
	public void Bloodghast_InGraveyard_ReturnsWhenLandPlayed()
	{
		var bloodghast = CardLibrary.GetByName("Bloodghast") with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s1, ghost) = _state.AddObject(bloodghast, parentId: _ids.Player1GraveyardId);

		var (s2, land) = AddLandToHand(s1, _ids.Player1Id);
		var (finalState, _) = s2.AddAction(
				new PlayLandAction { CardId = land.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		var p1BattlefieldId = finalState.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var battlefieldCards = finalState.GetCardsInZone(p1BattlefieldId);
		Assert.That(
			battlefieldCards.Any(c => c.Id == ghost.Id),
			Is.True,
			"Bloodghast should return to the battlefield after a land is played"
		);

		var graveyardId = finalState.GetPlayerZoneId(_ids.Player1Id, ZoneType.Graveyard);
		var graveyardCards = finalState.GetCardsInZone(graveyardId);
		Assert.That(
			graveyardCards.Any(c => c.Id == ghost.Id),
			Is.False,
			"Bloodghast should no longer be in the graveyard"
		);
	}

	[Test]
	public void Bloodghast_OnBattlefield_DoesNotRetriggerWhenLandPlayed()
	{
		// Bloodghast is alive on the battlefield — landfall graveyard trigger must not fire
		var bloodghast = CardLibrary.GetByName("Bloodghast") with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var p1BattlefieldId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var (s1, ghost) = _state.AddObject(bloodghast, parentId: p1BattlefieldId);

		var (s2, land) = AddLandToHand(s1, _ids.Player1Id);
		var (finalState, _) = s2.AddAction(
				new PlayLandAction { CardId = land.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		// Bloodghast should still be on the battlefield exactly once (no copies created)
		var finalBattlefield = finalState.GetCardsInZone(
			finalState.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield)
		);
		Assert.That(
			finalBattlefield.Count(c => c.Name == "Bloodghast"),
			Is.EqualTo(1),
			"Battlefield Bloodghast should not trigger — ActiveInZone = Graveyard means the battlefield scan skips it"
		);
	}

	[Test]
	public void Bloodghast_OpponentLandPlay_DoesNotTrigger()
	{
		// Only the controller's land plays trigger Bloodghast (IsControlledByYouSpecification filter)
		var bloodghast = CardLibrary.GetByName("Bloodghast") with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s1, ghost) = _state.AddObject(bloodghast, parentId: _ids.Player1GraveyardId);

		// Player 2 plays a land
		var (s2, land) = AddLandToHand(s1, _ids.Player2Id);
		var (finalState, _) = s2.AddAction(
				new PlayLandAction { CardId = land.Id, CastingPlayerId = _ids.Player2Id }
			)
			.ProcessAllActions();

		// Bloodghast should still be in P1's graveyard
		var graveyardId = finalState.GetPlayerZoneId(_ids.Player1Id, ZoneType.Graveyard);
		Assert.That(
			finalState.GetCardsInZone(graveyardId).Any(c => c.Id == ghost.Id),
			Is.True,
			"Bloodghast should not return when the opponent plays a land"
		);
	}

	[Test]
	public void SteppeLynx_LandfallRegression_StillFiresFromBattlefield()
	{
		// Regression: existing battlefield landfall trigger (Steppe Lynx) must still work
		// after the graveyard scan was added to CheckStateBasedEffectsAction.
		var lynx = CardLibrary.GetByName("Steppe Lynx") with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var p1BattlefieldId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var (s1, addedLynx) = _state.AddObject(lynx, parentId: p1BattlefieldId);

		var (s2, land) = AddLandToHand(s1, _ids.Player1Id);
		var (finalState, _) = s2.AddAction(
				new PlayLandAction { CardId = land.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		var power = CreatureEvaluator.GetEffectivePower(finalState, addedLynx.Id);
		Assert.That(power, Is.EqualTo(2), "Steppe Lynx should still get +2 power from landfall");
	}

	// ===== HELPERS =====

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
		var (newState, added) = state.AddObject(land, parentId: handId);
		return (newState, added);
	}
}
