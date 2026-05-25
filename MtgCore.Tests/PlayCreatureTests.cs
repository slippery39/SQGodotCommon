using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

[TestFixture]
public class CastCreatureTests
{
	private GameState _state;
	private MtgGameIds _ids;
	private int _creatureId;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();

		var creature = MakeCreature("Grizzly Bears", power: 2, toughness: 2);
		var (newState, added) = _state.AddObject(creature, parentId: _ids.Player1HandId);
		_state = newState;
		_creatureId = added.Id;
	}

	// ===== RESOLUTION =====

	[Test]
	public void CastCreature_MovesCardToBattlefield()
	{
		var (finalState, _) = _state.AddAction(MakeCast()).ProcessAllActions();

		Assert.That(finalState.GetCardZone(_creatureId).ZoneType, Is.EqualTo(ZoneType.Battlefield));
	}

	[Test]
	public void CastCreature_CardLeavesHand()
	{
		var (finalState, _) = _state.AddAction(MakeCast()).ProcessAllActions();

		Assert.That(finalState.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(0));
	}

	[Test]
	public void CastCreature_HasSummoningSickness()
	{
		var (finalState, _) = _state.AddAction(MakeCast()).ProcessAllActions();

		var card = (Card)finalState.GetObject(_creatureId);
		Assert.That(card.GetComponent<CreatureComponent>()!.HasSummoningSickness, Is.True);
	}

	[Test]
	public void CastCreature_EmitsCreaturePlayedEvent()
	{
		var (_, events) = _state.AddAction(MakeCast()).ProcessAllActions();

		var evt = events.OfType<CreaturePlayedEvent>().Single();
		Assert.That(evt.CardId, Is.EqualTo(_creatureId));
		Assert.That(evt.PlayerId, Is.EqualTo(_ids.Player1Id));
	}

	[Test]
	public void CastCreature_EmitsCreatureEnteredBattlefieldEvent()
	{
		var (_, events) = _state.AddAction(MakeCast()).ProcessAllActions();

		var evt = events.OfType<CreatureEnteredBattlefieldEvent>().Single();
		Assert.That(evt.CardId, Is.EqualTo(_creatureId));
		Assert.That(evt.PlayerId, Is.EqualTo(_ids.Player1Id));
	}

	[Test]
	public void CastCreature_NoActionsRemainAfterResolution()
	{
		var (finalState, _) = _state.AddAction(MakeCast()).ProcessAllActions();

		Assert.That(finalState.HasPendingActions, Is.False);
	}

	// ===== VALIDATION =====

	[Test]
	public void CastCreature_FailsIfCardNotInHand()
	{
		var stateWithCardInGraveyard = _state.MoveObject(_creatureId, _ids.Player1GraveyardId);
		var (_, success) = stateWithCardInGraveyard.TryAddAction(MakeCast());

		Assert.That(success, Is.False);
	}

	[Test]
	public void CastCreature_FailsIfCardHasNoCreatureComponent()
	{
		var spell = new Card
		{
			Name = "Lightning Bolt",
			ManaCost = 1,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
			Components = ImmutableArray.Create<GameComponent>(
				new SpellComponent { Effects = ImmutableList<CardEffect>.Empty }
			),
		};
		var (stateWithSpell, addedSpell) = _state.AddObject(spell, parentId: _ids.Player1HandId);

		var (_, success) = stateWithSpell.TryAddAction(
			new CastCreatureAction { CardId = addedSpell.Id, CastingPlayerId = _ids.Player1Id }
		);

		Assert.That(success, Is.False);
	}

	[Test]
	public void CastCreature_FailsIfNotController()
	{
		var (_, success) = _state.TryAddAction(
			new CastCreatureAction { CardId = _creatureId, CastingPlayerId = _ids.Player2Id }
		);

		Assert.That(success, Is.False);
	}

	// ===== HELPERS =====

	private CastCreatureAction MakeCast() =>
		new() { CardId = _creatureId, CastingPlayerId = _ids.Player1Id };

	private Card MakeCreature(string name, int power, int toughness) =>
		TestCardFactory.MakeCreatureCard(name, _ids.Player1Id, power, toughness);
}
