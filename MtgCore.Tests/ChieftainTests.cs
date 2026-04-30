using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

[TestFixture]
public class ChieftainTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	// ===== P/T BOOST =====

	[Test]
	public void Chieftain_GivesOtherGoblins_PlusPlusOnePlusOne()
	{
		var (s1, _) = AddCreatureToBattlefield(
			_state,
			CardLibrary.GoblinChieftain(),
			_ids.Player1Id,
			_ids.GameId
		);
		var (s2, lackey) = AddCreatureToBattlefield(
			s1,
			CardLibrary.GoblinLackey(),
			_ids.Player1Id,
			_ids.GameId
		);

		Assert.That(s2.GetEffectivePower(lackey.Id), Is.EqualTo(2), "Lackey base 1 + Chieftain +1");
		Assert.That(
			s2.GetEffectiveToughness(lackey.Id),
			Is.EqualTo(2),
			"Lackey base 1 + Chieftain +1"
		);
	}

	[Test]
	public void Chieftain_DoesNotBoostItself()
	{
		var (s1, chieftain) = AddCreatureToBattlefield(
			_state,
			CardLibrary.GoblinChieftain(),
			_ids.Player1Id,
			_ids.GameId
		);

		Assert.That(
			s1.GetEffectivePower(chieftain.Id),
			Is.EqualTo(2),
			"Chieftain base 2, no self-boost"
		);
		Assert.That(s1.GetEffectiveToughness(chieftain.Id), Is.EqualTo(2));
	}

	[Test]
	public void Chieftain_DoesNotBoostOpponentGoblins()
	{
		var (s1, _) = AddCreatureToBattlefield(
			_state,
			CardLibrary.GoblinChieftain(),
			_ids.Player1Id,
			_ids.GameId
		);
		var (s2, opponentLackey) = AddCreatureToBattlefield(
			s1,
			CardLibrary.GoblinLackey(),
			_ids.Player2Id,
			_ids.GameId
		);

		Assert.That(
			s2.GetEffectivePower(opponentLackey.Id),
			Is.EqualTo(1),
			"Opponent Goblin should not be boosted"
		);
	}

	[Test]
	public void Chieftain_BoostIsLost_WhenChieftainLeavesPlay()
	{
		var (s1, chieftain) = AddCreatureToBattlefield(
			_state,
			CardLibrary.GoblinChieftain(),
			_ids.Player1Id,
			_ids.GameId
		);
		var (s2, lackey) = AddCreatureToBattlefield(
			s1,
			CardLibrary.GoblinLackey(),
			_ids.Player1Id,
			_ids.GameId
		);

		Assert.That(s2.GetEffectivePower(lackey.Id), Is.EqualTo(2));

		// Process LTB to clean up applied components, then move to graveyard
		var s3 = StaticAbilityEngine.ProcessPermanentLeft(s2, chieftain.Id, _ids.GameId);
		var graveyardId = s3.GetPlayerZoneId(_ids.Player1Id, ZoneType.Graveyard);
		s3 = s3.MoveObject(chieftain.Id, graveyardId);

		Assert.That(
			s3.GetEffectivePower(lackey.Id),
			Is.EqualTo(1),
			"Boost gone after Chieftain leaves"
		);
	}

	// ===== HASTE GRANT =====

	[Test]
	public void Chieftain_GrantsHaste_ToOtherGoblins()
	{
		var (s1, _) = AddCreatureToBattlefield(
			_state,
			CardLibrary.GoblinChieftain(),
			_ids.Player1Id,
			_ids.GameId
		);

		// Add a Lackey with summoning sickness (as it would ETB normally)
		var (s2, lackey) = AddCreatureToBattlefield(
			s1,
			CardLibrary.GoblinLackey(),
			_ids.Player1Id,
			_ids.GameId,
			hasSummoningSickness: true
		);

		Assert.That(
			s2.GetEffectiveHaste(lackey.Id),
			Is.True,
			"Chieftain should grant haste to other Goblins"
		);
	}

	[Test]
	public void Chieftain_GrantsHaste_AllowsGoblinToAttack()
	{
		var (s1, _) = AddCreatureToBattlefield(
			_state,
			CardLibrary.GoblinChieftain(),
			_ids.Player1Id,
			_ids.GameId
		);
		var (s2, lackey) = AddCreatureToBattlefield(
			s1,
			CardLibrary.GoblinLackey(),
			_ids.Player1Id,
			_ids.GameId,
			hasSummoningSickness: true
		);

		var actions = MtgActionGenerator.GetLegalActions(s2, _ids, _ids.Player1Id);
		var canAttack = actions.OfType<AttackAction>().Any(a => a.AttackerId == lackey.Id);

		Assert.That(
			canAttack,
			Is.True,
			"Goblin with summoning sickness should be able to attack when Chieftain is in play"
		);
	}

	[Test]
	public void Chieftain_DoesNotGrantHasteToItself_BeyondIntrinsic()
	{
		// Chieftain already has intrinsic haste — confirm its effective haste is still true
		var (s1, chieftain) = AddCreatureToBattlefield(
			_state,
			CardLibrary.GoblinChieftain(),
			_ids.Player1Id,
			_ids.GameId
		);
		Assert.That(s1.GetEffectiveHaste(chieftain.Id), Is.True);
	}

	[Test]
	public void NoChieftain_GoblinWithSummoningSickness_CannotAttack()
	{
		var (s1, lackey) = AddCreatureToBattlefield(
			_state,
			CardLibrary.GoblinLackey(),
			_ids.Player1Id,
			_ids.GameId,
			hasSummoningSickness: true
		);

		var actions = MtgActionGenerator.GetLegalActions(s1, _ids, _ids.Player1Id);
		var canAttack = actions.OfType<AttackAction>().Any(a => a.AttackerId == lackey.Id);

		Assert.That(canAttack, Is.False, "No Chieftain — summoning sickness should prevent attack");
	}

	// ===== HELPERS =====

	private static (GameState, Card) AddCreatureToBattlefield(
		GameState state,
		Card template,
		int playerId,
		int gameId,
		bool hasSummoningSickness = false
	)
	{
		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		var card = template with { OwnerId = playerId, ControllerId = playerId };
		var (newState, added) = state.AddObject(card, battlefieldId);

		var creature = added.GetComponent<CreatureComponent>()!;
		newState = newState.UpdateObject(
			added.Id,
			added.WithComponentReplaced(
				creature with
				{
					HasSummoningSickness = hasSummoningSickness,
				}
			)
		);

		newState = StaticAbilityEngine.ProcessPermanentEntered(newState, added.Id, gameId);

		return (newState, (Card)newState.GetObject(added.Id));
	}
}
