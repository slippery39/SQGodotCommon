using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Tests for Phase 3 global enchantments: Glorious Anthem and Phyrexian Arena.
///
/// Glorious Anthem (StaticPTBoostAbility):
///   - Boosts creatures already on the battlefield when it enters
///   - Boosts creatures that enter after it
///   - Boost is removed when the Anthem leaves
///   - Does not boost itself (it has no CreatureComponent)
///
/// Phyrexian Arena (TriggeredAbilityComponent on TurnStarted):
///   - Draws one card at the start of the controller's turn
///   - Costs one life at the start of the controller's turn
///   - Does not trigger on the opponent's turn
/// </summary>
[TestFixture]
public class EnchantmentTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	// ===== GLORIOUS ANTHEM — STATIC BOOST =====

	[Test]
	public void GloriousAnthem_BoostsCreatureAlreadyOnBattlefield()
	{
		var (s1, creatureId) = AddCreatureToBattlefield(
			_state,
			_ids.Player1Id,
			power: 2,
			toughness: 2
		);
		var s2 = CastAnthem(s1, _ids.Player1Id);

		Assert.That(s2.GetEffectivePower(creatureId), Is.EqualTo(3));
		Assert.That(s2.GetEffectiveToughness(creatureId), Is.EqualTo(3));
	}

	[Test]
	public void GloriousAnthem_BoostsCreatureThatEntersAfter()
	{
		var s1 = CastAnthem(_state, _ids.Player1Id);
		var (s2, creatureId) = AddCreatureToBattlefield(s1, _ids.Player1Id, power: 1, toughness: 1);

		Assert.That(s2.GetEffectivePower(creatureId), Is.EqualTo(2));
		Assert.That(s2.GetEffectiveToughness(creatureId), Is.EqualTo(2));
	}

	[Test]
	public void GloriousAnthem_DoesNotBoostOpponentCreatures()
	{
		var (s1, opponentCreatureId) = AddCreatureToBattlefield(
			_state,
			_ids.Player2Id,
			power: 2,
			toughness: 2
		);
		var s2 = CastAnthem(s1, _ids.Player1Id);

		Assert.That(s2.GetEffectivePower(opponentCreatureId), Is.EqualTo(2));
		Assert.That(s2.GetEffectiveToughness(opponentCreatureId), Is.EqualTo(2));
	}

	[Test]
	public void GloriousAnthem_BoostRemovedWhenAnthemLeavesBattlefield()
	{
		var (s1, creatureId) = AddCreatureToBattlefield(
			_state,
			_ids.Player1Id,
			power: 2,
			toughness: 2
		);
		var (s2, anthemId) = CastAnthemReturnId(s1, _ids.Player1Id);
		Assert.That(s2.GetEffectivePower(creatureId), Is.EqualTo(3), "boost should be active");

		var s3 = RemoveFromBattlefield(s2, anthemId, _ids.Player1Id);

		Assert.That(s3.GetEffectivePower(creatureId), Is.EqualTo(2), "boost should be removed");
		Assert.That(s3.GetEffectiveToughness(creatureId), Is.EqualTo(2), "boost should be removed");
	}

	[Test]
	public void GloriousAnthem_DoesNotBoostItself()
	{
		var (s1, anthemId) = CastAnthemReturnId(_state, _ids.Player1Id);

		var anthem = (Card)s1.GetObject(anthemId);
		Assert.That(anthem.HasComponent<CreatureComponent>(), Is.False);
	}

	[Test]
	public void GloriousAnthem_StacksWithMultipleAnthems()
	{
		var (s1, creatureId) = AddCreatureToBattlefield(
			_state,
			_ids.Player1Id,
			power: 1,
			toughness: 1
		);
		var s2 = CastAnthem(s1, _ids.Player1Id);
		var s3 = CastAnthem(s2, _ids.Player1Id);

		Assert.That(s3.GetEffectivePower(creatureId), Is.EqualTo(3));
		Assert.That(s3.GetEffectiveToughness(creatureId), Is.EqualTo(3));
	}

	// ===== PHYREXIAN ARENA — TRIGGERED DRAW/LOSS =====

	[Test]
	public void PhyrexianArena_DrawsOneCardOnUpkeep()
	{
		var s1 = TestCardFactory.AddCardsToLibrary(_state, _ids.Player1Id, "Card A", "Card B");
		var s2 = AddArenaToBattlefield(s1, _ids.Player1Id);

		var (finalState, _) = s2.AddAction(MakeStartTurn(_ids.Player1Id, skipDraw: true))
			.ProcessAllActions();

		Assert.That(finalState.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(1));
	}

	[Test]
	public void PhyrexianArena_LosesOneLifeOnUpkeep()
	{
		var s1 = TestCardFactory.AddCardsToLibrary(_state, _ids.Player1Id, "Card A");
		var s2 = AddArenaToBattlefield(s1, _ids.Player1Id);
		var lifeBefore = s2.GetPlayer(_ids.Player1Id).Life;

		var (finalState, _) = s2.AddAction(MakeStartTurn(_ids.Player1Id, skipDraw: true))
			.ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player1Id).Life, Is.EqualTo(lifeBefore - 1));
	}

	[Test]
	public void PhyrexianArena_DoesNotTriggerOnOpponentTurn()
	{
		var s1 = TestCardFactory.AddCardsToLibrary(_state, _ids.Player1Id, "Card A");
		var s2 = AddArenaToBattlefield(s1, _ids.Player1Id);
		var lifeBefore = s2.GetPlayer(_ids.Player1Id).Life;

		var (finalState, _) = s2.AddAction(MakeStartTurn(_ids.Player2Id, skipDraw: true))
			.ProcessAllActions();

		Assert.That(finalState.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(0));
		Assert.That(finalState.GetPlayer(_ids.Player1Id).Life, Is.EqualTo(lifeBefore));
	}

	[Test]
	public void PhyrexianArena_TriggersEachTurn()
	{
		var s1 = TestCardFactory.AddCardsToLibrary(_state, _ids.Player1Id, "A", "B", "C");
		var s2 = AddArenaToBattlefield(s1, _ids.Player1Id);
		var startingLife = s2.GetPlayer(_ids.Player1Id).Life;

		var (afterTurn1, _) = s2.AddAction(MakeStartTurn(_ids.Player1Id, skipDraw: true))
			.ProcessAllActions();
		var (afterTurn2, _) = afterTurn1
			.AddAction(MakeStartTurn(_ids.Player1Id, skipDraw: true))
			.ProcessAllActions();

		Assert.That(afterTurn2.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(2));
		Assert.That(afterTurn2.GetPlayer(_ids.Player1Id).Life, Is.EqualTo(startingLife - 2));
	}

	// ===== HELPERS =====

	private (GameState, int creatureId) AddCreatureToBattlefield(
		GameState state,
		int ownerId,
		int power,
		int toughness
	)
	{
		var card = TestCardFactory.MakeCreatureCard("Creature", ownerId, power, toughness);
		var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);
		var (newState, added) = state.AddObject(card, parentId: battlefieldId);
		// Fire the ETB event so StaticAbilityEngine can apply existing sources to this creature
		var enteredEvent = new CreatureEnteredBattlefieldEvent
		{
			CardId = added.Id,
			PlayerId = ownerId,
		};
		newState = newState with
		{
			PendingGameEvents = newState.PendingGameEvents.Add(enteredEvent),
		};
		var (processed, _) = newState.AddAction(new EndResolutionScopeAction()).ProcessAllActions();
		return (processed, added.Id);
	}

	private GameState CastAnthem(GameState state, int playerId)
	{
		var (s, _) = CastAnthemReturnId(state, playerId);
		return s;
	}

	private (GameState, int anthemId) CastAnthemReturnId(GameState state, int playerId)
	{
		var card = CardLibrary.GloriousAnthem() with
		{
			OwnerId = playerId,
			ControllerId = playerId,
		};
		var (stateWithCard, added) = state.AddObject(
			card,
			parentId: state.GetPlayerZoneId(playerId, ZoneType.Hand)
		);
		var (resolved, _) = stateWithCard
			.AddAction(new CastPermanentAction { CardId = added.Id, CastingPlayerId = playerId })
			.ProcessAllActions();
		return (resolved, added.Id);
	}

	private GameState AddArenaToBattlefield(GameState state, int ownerId)
	{
		var card = CardLibrary.PhyrexianArena() with { OwnerId = ownerId, ControllerId = ownerId };
		var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);
		var (newState, _) = state.AddObject(card, parentId: battlefieldId);
		return newState;
	}

	private GameState RemoveFromBattlefield(GameState state, int cardId, int ownerId)
	{
		var graveyardId = state.GetPlayerZoneId(ownerId, ZoneType.Graveyard);
		state = state.MoveObject(cardId, graveyardId);
		var leavingEvent = new PermanentLeftBattlefieldEvent { CardId = cardId, OwnerId = ownerId };
		state = state with { PendingGameEvents = ImmutableList.Create<GameEvent>(leavingEvent) };
		var (processed, _) = state.AddAction(new EndResolutionScopeAction()).ProcessAllActions();
		return processed;
	}

	private StartTurnAction MakeStartTurn(int playerId, bool skipDraw = false) =>
		new()
		{
			ActivePlayerId = playerId,
			BattlefieldId = _state.GetPlayerZoneId(playerId, ZoneType.Battlefield),
			SkipDraw = skipDraw,
		};
}
