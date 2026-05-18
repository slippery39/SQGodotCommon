using System.Collections.Immutable;
using System.Linq;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

[TestFixture]
public class SnapcasterMageTests
{
	private GameState _state;
	private MtgGameIds _ids;
	private int _snapcasterId;
	private int _fireboltId;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();

		// Snapcaster in hand
		var snapcaster = CardLibrary.GetByName("Snapcaster Mage") with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s1, addedSnap) = _state.AddObject(snapcaster, parentId: _ids.Player1HandId);
		_state = s1;
		_snapcasterId = addedSnap.Id;

		// Firebolt in graveyard, with built-in FlashbackComponent stripped so we
		// are testing only the Snapcaster grant, not the card's inherent flashback.
		var firebolt = CardLibrary.GetByName("Firebolt") with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		firebolt = firebolt with
		{
			Components = firebolt.Components.RemoveAll(c => c is FlashbackComponent),
		};
		var (s2, addedFirebolt) = _state.AddObject(firebolt, parentId: _ids.Player1GraveyardId);
		_state = s2;
		_fireboltId = addedFirebolt.Id;
	}

	private GameState CastSnapcaster() =>
		_state
			.AddAction(
				new CastCreatureAction { CardId = _snapcasterId, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions()
			.State;

	// ===== ETB TRIGGER =====

	[Test]
	public void CastingSnapcaster_GrantsFlashbackToGraveyardSpell()
	{
		var finalState = CastSnapcaster();

		var firebolt = finalState.GetObject(_fireboltId) as Card;
		Assert.That(
			firebolt!.HasComponent<FlashbackComponent>(),
			Is.True,
			"Firebolt should have gained FlashbackComponent from Snapcaster's ETB trigger"
		);
	}

	[Test]
	public void GrantedFlashbackCost_EqualsCardManaCost()
	{
		var finalState = CastSnapcaster();

		var firebolt = finalState.GetObject(_fireboltId) as Card;
		var fb = firebolt!.GetComponent<FlashbackComponent>();
		Assert.That(fb, Is.Not.Null);
		Assert.That(fb!.FlashbackManaCost, Is.EqualTo(firebolt.ManaCost));
	}

	[Test]
	public void CastingSnapcaster_WithEmptyGraveyard_DoesNotFail()
	{
		// Move Firebolt out so the graveyard is empty
		var stateNoGraveyard = _state.MoveObject(_fireboltId, _ids.Player1HandId);

		// Should still succeed — no graveyard card to target is fine
		Assert.DoesNotThrow(
			() =>
				stateNoGraveyard
					.AddAction(
						new CastCreatureAction
						{
							CardId = _snapcasterId,
							CastingPlayerId = _ids.Player1Id,
						}
					)
					.ProcessAllActions()
		);
	}

	[Test]
	public void CastingSnapcaster_DoesNotGrantFlashbackToCreature()
	{
		// Put a creature in the graveyard alongside the spell
		var bear = CardLibrary.GetByName("Grizzly Bears") with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (stateWithBear, addedBear) = _state.AddObject(bear, parentId: _ids.Player1GraveyardId);

		var finalState = stateWithBear
			.AddAction(
				new CastCreatureAction { CardId = _snapcasterId, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions()
			.State;

		var bearAfter = finalState.GetObject(addedBear.Id) as Card;
		Assert.That(
			bearAfter!.HasComponent<FlashbackComponent>(),
			Is.False,
			"Creatures in graveyard should not gain flashback"
		);
	}

	[Test]
	public void SnapcasterGrantedFlashback_AllowsCastFromGraveyard()
	{
		// Cast Snapcaster to grant flashback
		var stateAfterSnap = CastSnapcaster();

		// Firebolt now has flashback — try to cast it from graveyard
		var (_, success) = stateAfterSnap.TryAddAction(
			new CastFromGraveyardAction
			{
				CardId = _fireboltId,
				CastingPlayerId = _ids.Player1Id,
				TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
					0,
					ImmutableList.Create(_ids.Player2Id)
				),
			}
		);

		Assert.That(
			success,
			Is.True,
			"Firebolt with Snapcaster-granted flashback should be castable from graveyard"
		);
	}

	[Test]
	public void CardAlreadyHavingFlashback_IsNotDoubleGranted()
	{
		// Manually add a FlashbackComponent to simulate a card that already has one
		var existing = (_state.GetObject(_fireboltId) as Card)!;
		var alreadyHasFlashback = existing with
		{
			Components = existing.Components.Add(new FlashbackComponent { FlashbackManaCost = 99 }),
		};
		var stateWithFlashback = _state.UpdateObject(_fireboltId, alreadyHasFlashback);

		var finalState = stateWithFlashback
			.AddAction(
				new CastCreatureAction { CardId = _snapcasterId, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions()
			.State;

		var firebolt = finalState.GetObject(_fireboltId) as Card;
		var flashbackComponents = firebolt!.GetComponents<FlashbackComponent>().ToList();
		Assert.That(
			flashbackComponents.Count,
			Is.EqualTo(1),
			"Should not add a second FlashbackComponent if one already exists"
		);
		Assert.That(
			flashbackComponents[0].FlashbackManaCost,
			Is.EqualTo(99),
			"Existing FlashbackComponent should be unchanged"
		);
	}
}
