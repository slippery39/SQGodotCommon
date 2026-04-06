using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

[TestFixture]
public class LightningHelixTests
{
	private GameState _state;
	private MtgGameIds _ids;
	private int _helixId;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();

		var helix = CardLibrary.LightningHelix() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (newState, addedHelix) = _state.AddObject(helix, parentId: _ids.Player1HandId);
		_state = newState;
		_helixId = addedHelix.Id;
	}

	[Test]
	public void LightningHelix_Deals3DamageToTargetPlayer()
	{
		var (finalState, events) = _state
			.AddAction(MakeCastHelixAt(_ids.Player2Id))
			.ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(17));
		Assert.That(events.OfType<PlayerDamagedEvent>().Single().Amount, Is.EqualTo(3));
	}

	[Test]
	public void LightningHelix_CastingPlayerGains3Life()
	{
		var (finalState, events) = _state
			.AddAction(MakeCastHelixAt(_ids.Player2Id))
			.ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player1Id).Life, Is.EqualTo(23));
		Assert.That(events.OfType<PlayerGainedLifeEvent>().Single().Amount, Is.EqualTo(3));
	}

	[Test]
	public void LightningHelix_BothEffectsResolve()
	{
		var (finalState, events) = _state
			.AddAction(MakeCastHelixAt(_ids.Player2Id))
			.ProcessAllActions();

		Assert.That(events.OfType<PlayerDamagedEvent>().Count(), Is.EqualTo(1));
		Assert.That(events.OfType<PlayerGainedLifeEvent>().Count(), Is.EqualTo(1));
	}

	[Test]
	public void LightningHelix_LifeGainGoesToCasterNotTarget()
	{
		var (finalState, _) = _state.AddAction(MakeCastHelixAt(_ids.Player2Id)).ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(23),
			"Caster should gain 3 life"
		);
		Assert.That(
			finalState.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(17),
			"Target should lose 3 life"
		);
	}

	[Test]
	public void LightningHelix_TargetingCreature_DamagesCreatureAndCasterGainsLife()
	{
		var (stateWithCreature, creature) = _state.AddObject(
			MakeCreature("Hill Giant", power: 3, toughness: 4, ownerId: _ids.Player2Id),
			parentId: _ids.Player2BattlefieldId
		);

		var (finalState, events) = stateWithCreature
			.AddAction(
				new CastSpellAction
				{
					CardId = _helixId,
					CastingPlayerId = _ids.Player1Id,
					GameId = _ids.GameId,
					TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
						0,
						ImmutableList.Create(creature.Id)
					),
				}
			)
			.ProcessAllActions();

		var updatedCard = (Card)finalState.GetObject(creature.Id);
		Assert.That(updatedCard.GetComponent<CreatureComponent>()!.Damage, Is.EqualTo(3));
		Assert.That(finalState.GetCardZone(creature.Id).ZoneType, Is.EqualTo(ZoneType.Battlefield));
		Assert.That(finalState.GetPlayer(_ids.Player1Id).Life, Is.EqualTo(23));
		Assert.That(events.OfType<PlayerGainedLifeEvent>().Single().Amount, Is.EqualTo(3));
	}

	[Test]
	public void LightningHelix_MovesToGraveyardAfterResolving()
	{
		var (finalState, _) = _state.AddAction(MakeCastHelixAt(_ids.Player2Id)).ProcessAllActions();

		Assert.That(finalState.GetCardZone(_helixId).ZoneType, Is.EqualTo(ZoneType.Graveyard));
		Assert.That(finalState.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(0));
	}

	[Test]
	public void LightningHelix_NoActionsRemainAfterResolution()
	{
		var (finalState, _) = _state.AddAction(MakeCastHelixAt(_ids.Player2Id)).ProcessAllActions();

		Assert.That(finalState.HasPendingActions, Is.False);
	}

	// ===== HELPERS =====

	private CastSpellAction MakeCastHelixAt(int targetId) =>
		new()
		{
			CardId = _helixId,
			CastingPlayerId = _ids.Player1Id,
			GameId = _ids.GameId,
			TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
				0,
				ImmutableList.Create(targetId)
			),
		};

	private static Card MakeCreature(string name, int power, int toughness, int ownerId) =>
		new()
		{
			Name = name,
			ManaCost = 2,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = power, Toughness = toughness }
			),
		};
}
