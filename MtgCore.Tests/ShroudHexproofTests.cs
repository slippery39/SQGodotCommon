using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

[TestFixture]
public class ShroudHexproofTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	[Test]
	public void Hexproof_BlocksOpponentTargeting()
	{
		var tyrant = CardLibrary.GetByName("Carnage Tyrant") with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s1, card) = _state.AddObject(tyrant, parentId: _ids.Player1BattlefieldId);

		var spec = new IsCreatureSpecification();
		var opponentContext = new TargetingContext
		{
			GameState = s1,
			SourceCardId = 0,
			CastingPlayerId = _ids.Player2Id,
		};

		Assert.That(
			spec.IsSatisfiedBy(card.Id, opponentContext),
			Is.False,
			"Hexproof should prevent opponents from targeting Carnage Tyrant"
		);
	}

	[Test]
	public void Hexproof_AllowsControllerTargeting()
	{
		var tyrant = CardLibrary.GetByName("Carnage Tyrant") with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s1, card) = _state.AddObject(tyrant, parentId: _ids.Player1BattlefieldId);

		var spec = new IsCreatureSpecification();
		var controllerContext = new TargetingContext
		{
			GameState = s1,
			SourceCardId = 0,
			CastingPlayerId = _ids.Player1Id,
		};

		Assert.That(
			spec.IsSatisfiedBy(card.Id, controllerContext),
			Is.True,
			"Hexproof should not prevent the controller from targeting their own Carnage Tyrant"
		);
	}

	[Test]
	public void Shroud_BlocksAllTargeting_IncludingController()
	{
		var shrouded = new Card
		{
			Name = "Shrouded Test Creature",
			ManaCost = 3,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
			Components = ImmutableList.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent
				{
					Power = 3,
					Toughness = 3,
					HasShroud = true,
				}
			),
		};
		var (s1, card) = _state.AddObject(shrouded, parentId: _ids.Player1BattlefieldId);

		var spec = new IsCreatureSpecification();

		var controllerContext = new TargetingContext
		{
			GameState = s1,
			SourceCardId = 0,
			CastingPlayerId = _ids.Player1Id,
		};
		var opponentContext = new TargetingContext
		{
			GameState = s1,
			SourceCardId = 0,
			CastingPlayerId = _ids.Player2Id,
		};

		Assert.That(
			spec.IsSatisfiedBy(card.Id, controllerContext),
			Is.False,
			"Shroud should prevent even the controller from targeting the creature"
		);
		Assert.That(
			spec.IsSatisfiedBy(card.Id, opponentContext),
			Is.False,
			"Shroud should prevent opponents from targeting the creature"
		);
	}

	[Test]
	public void Hexproof_DoesNotAffectGraveyardTargeting()
	{
		var tyrant = CardLibrary.GetByName("Carnage Tyrant") with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s1, card) = _state.AddObject(tyrant, parentId: _ids.Player1GraveyardId);

		var spec = new IsCreatureInOwnGraveyardSpecification();
		var context = new TargetingContext
		{
			GameState = s1,
			SourceCardId = 0,
			CastingPlayerId = _ids.Player1Id,
		};

		Assert.That(
			spec.IsSatisfiedBy(card.Id, context),
			Is.True,
			"Hexproof/Shroud applies on the battlefield only — Reanimate should still be able to target a Hexproof creature in the graveyard"
		);
	}
}
