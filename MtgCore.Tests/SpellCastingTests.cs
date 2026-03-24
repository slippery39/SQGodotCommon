using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

[TestFixture]
public class LightningBoltTests
{
	private GameState _state;
	private MtgGameIds _ids;
	private int _boltId;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.Create();

		var bolt = CardLibrary.LightningBolt() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (newState, addedBolt) = _state.AddObject(bolt, parentId: _ids.Player1HandId);
		_state = newState;
		_boltId = addedBolt.Id;
	}

	// ===== TARGETING SPECS =====

	[Test]
	public void IsPlayerSpecification_MatchesPlayers()
	{
		var spec = new IsPlayerSpecification();
		var context = MakeTargetingContext();

		Assert.That(spec.IsSatisfiedBy(_ids.Player1Id, context), Is.True);
		Assert.That(spec.IsSatisfiedBy(_ids.Player2Id, context), Is.True);
	}

	[Test]
	public void IsPlayerSpecification_DoesNotMatchCards()
	{
		var spec = new IsPlayerSpecification();
		var context = MakeTargetingContext();

		Assert.That(spec.IsSatisfiedBy(_boltId, context), Is.False);
	}

	[Test]
	public void IsCreatureSpecification_MatchesCreatureOnBattlefield()
	{
		var (stateWithCreature, creature) = _state.AddObject(
			new CreatureCard
			{
				Name = "Grizzly Bears",
				Power = 2,
				Toughness = 2,
				ManaCost = 2,
				OwnerId = _ids.Player2Id,
				ControllerId = _ids.Player2Id,
			},
			parentId: _ids.Player2BattlefieldId
		);

		var spec = new IsCreatureSpecification();
		var context = MakeTargetingContext(stateWithCreature);

		Assert.That(spec.IsSatisfiedBy(creature.Id, context), Is.True);
	}

	[Test]
	public void IsCreatureSpecification_DoesNotMatchCreatureInHand()
	{
		var (stateWithCreature, creature) = _state.AddObject(
			new CreatureCard
			{
				Name = "Grizzly Bears",
				Power = 2,
				Toughness = 2,
				ManaCost = 2,
				OwnerId = _ids.Player2Id,
				ControllerId = _ids.Player2Id,
			},
			parentId: _ids.Player2HandId
		);

		var spec = new IsCreatureSpecification();
		var context = MakeTargetingContext(stateWithCreature);

		Assert.That(spec.IsSatisfiedBy(creature.Id, context), Is.False);
	}

	[Test]
	public void OrSpecification_MatchesPlayerOrCreature()
	{
		var (stateWithCreature, creature) = _state.AddObject(
			new CreatureCard
			{
				Name = "Grizzly Bears",
				Power = 2,
				Toughness = 2,
				ManaCost = 2,
				OwnerId = _ids.Player2Id,
				ControllerId = _ids.Player2Id,
			},
			parentId: _ids.Player2BattlefieldId
		);

		var spec = new IsPlayerSpecification().Or(new IsCreatureSpecification());
		var context = MakeTargetingContext(stateWithCreature);

		Assert.That(spec.IsSatisfiedBy(_ids.Player2Id, context), Is.True, "Player should match");
		Assert.That(spec.IsSatisfiedBy(creature.Id, context), Is.True, "Creature should match");
		Assert.That(
			spec.IsSatisfiedBy(_boltId, context),
			Is.False,
			"Card in hand should not match"
		);
	}

	// ===== TARGETING STRATEGY =====

	[Test]
	public void TargetingStrategy_GetValidTargets_ReturnsBothPlayersWhenNoCreatures()
	{
		var strategy = TargetingStrategy.SingleTarget(
			new IsPlayerSpecification().Or(new IsCreatureSpecification())
		);
		var context = MakeTargetingContext();
		var validTargets = strategy.GetValidTargets(context);

		Assert.That(validTargets, Contains.Item(_ids.Player1Id));
		Assert.That(validTargets, Contains.Item(_ids.Player2Id));
	}

	[Test]
	public void TargetingStrategy_ValidateTargets_AcceptsValidTarget()
	{
		var strategy = TargetingStrategy.SingleTarget(
			new IsPlayerSpecification().Or(new IsCreatureSpecification())
		);
		var context = MakeTargetingContext();

		Assert.That(
			strategy.ValidateTargets(ImmutableList.Create(_ids.Player2Id), context),
			Is.True
		);
	}

	[Test]
	public void TargetingStrategy_ValidateTargets_RejectsInvalidTarget()
	{
		var strategy = TargetingStrategy.SingleTarget(
			new IsPlayerSpecification().Or(new IsCreatureSpecification())
		);
		var context = MakeTargetingContext();

		Assert.That(strategy.ValidateTargets(ImmutableList.Create(_boltId), context), Is.False);
	}

	// ===== CAST SPELL VALIDATION =====

	[Test]
	public void CastSpellAction_ValidateAdd_FailsIfCardNotInHand()
	{
		var stateWithBoltInGraveyard = _state.MoveObject(_boltId, _ids.Player1GraveyardId);
		var (_, success) = stateWithBoltInGraveyard.TryAddAction(MakeCastBoltAt(_ids.Player2Id));

		Assert.That(success, Is.False);
	}

	[Test]
	public void CastSpellAction_ValidateAdd_FailsIfTargetIsInvalid()
	{
		var action = new CastSpellAction
		{
			CardId = _boltId,
			CastingPlayerId = _ids.Player1Id,
			GameId = _ids.GameId,
			TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
				0,
				ImmutableList.Create(_ids.Player1HandId)
			),
		};

		var (_, success) = _state.TryAddAction(action);
		Assert.That(success, Is.False);
	}

	[Test]
	public void CastSpellAction_ValidateAdd_SucceedsWithValidPlayerTarget()
	{
		var (_, success) = _state.TryAddAction(MakeCastBoltAt(_ids.Player2Id));
		Assert.That(success, Is.True);
	}

	// ===== FULL RESOLUTION =====

	[Test]
	public void LightningBolt_TargetingPlayer_Deals3Damage()
	{
		var (finalState, events) = _state
			.AddAction(MakeCastBoltAt(_ids.Player2Id))
			.ProcessAllActions();

		var player2 = finalState.GetPlayer(_ids.Player2Id);
		Assert.That(player2.Life, Is.EqualTo(17));
		Assert.That(events.OfType<PlayerDamagedEvent>().Single().Amount, Is.EqualTo(3));
	}

	[Test]
	public void LightningBolt_AfterResolving_MovesToGraveyard()
	{
		var (finalState, _) = _state.AddAction(MakeCastBoltAt(_ids.Player2Id)).ProcessAllActions();

		var cardZone = finalState.GetCardZone(_boltId);
		Assert.That(cardZone.ZoneType, Is.EqualTo(ZoneType.Graveyard));
		Assert.That(finalState.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(0));
	}

	[Test]
	public void LightningBolt_TargetingCreature_DestroysItIfLethal()
	{
		var (stateWithCreature, creature) = _state.AddObject(
			new CreatureCard
			{
				Name = "Grizzly Bears",
				Power = 2,
				Toughness = 2,
				ManaCost = 2,
				OwnerId = _ids.Player2Id,
				ControllerId = _ids.Player2Id,
			},
			parentId: _ids.Player2BattlefieldId
		);

		var (finalState, events) = stateWithCreature
			.AddAction(
				new CastSpellAction
				{
					CardId = _boltId,
					CastingPlayerId = _ids.Player1Id,
					GameId = _ids.GameId,
					TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
						0,
						ImmutableList.Create(creature.Id)
					),
				}
			)
			.ProcessAllActions();

		var cardZone = finalState.GetCardZone(creature.Id);
		Assert.That(cardZone.ZoneType, Is.EqualTo(ZoneType.Graveyard));
		Assert.That(
			events.OfType<CreatureDestroyedEvent>().Single().CreatureId,
			Is.EqualTo(creature.Id)
		);
	}

	[Test]
	public void LightningBolt_TargetingCreature_DoesNotDestroyIfTough()
	{
		var (stateWithCreature, creature) = _state.AddObject(
			new CreatureCard
			{
				Name = "Hill Giant",
				Power = 3,
				Toughness = 4,
				ManaCost = 4,
				OwnerId = _ids.Player2Id,
				ControllerId = _ids.Player2Id,
			},
			parentId: _ids.Player2BattlefieldId
		);

		var (finalState, events) = stateWithCreature
			.AddAction(
				new CastSpellAction
				{
					CardId = _boltId,
					CastingPlayerId = _ids.Player1Id,
					GameId = _ids.GameId,
					TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
						0,
						ImmutableList.Create(creature.Id)
					),
				}
			)
			.ProcessAllActions();

		var cardZone = finalState.GetCardZone(creature.Id);
		Assert.That(cardZone.ZoneType, Is.EqualTo(ZoneType.Battlefield));
		Assert.That(events.OfType<CreatureDestroyedEvent>(), Is.Empty);
		Assert.That(events.OfType<CreatureDamagedEvent>().Single().Amount, Is.EqualTo(3));
	}

	[Test]
	public void LightningBolt_NoActionsRemainAfterResolution()
	{
		var (finalState, _) = _state.AddAction(MakeCastBoltAt(_ids.Player2Id)).ProcessAllActions();

		Assert.That(finalState.HasPendingActions, Is.False);
	}

	// ===== HELPERS =====

	private TargetingContext MakeTargetingContext(GameState? state = null) =>
		new()
		{
			GameState = state ?? _state,
			SourceCardId = _boltId,
			CastingPlayerId = _ids.Player1Id,
		};

	private CastSpellAction MakeCastBoltAt(int targetId) =>
		new()
		{
			CardId = _boltId,
			CastingPlayerId = _ids.Player1Id,
			GameId = _ids.GameId,
			TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
				0,
				ImmutableList.Create(targetId)
			),
		};
}
