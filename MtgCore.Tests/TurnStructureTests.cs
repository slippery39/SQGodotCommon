using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

[TestFixture]
public class TurnStructureTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.Create();
	}

	// ===== START TURN ACTION =====

	[Test]
	public void StartTurn_IncrementsMaxMana()
	{
		var (finalState, _) = _state.AddAction(MakeStartTurn(_ids.Player1Id)).ProcessAllActions();

		var player = finalState.GetPlayer(_ids.Player1Id);
		Assert.That(player.MaxMana, Is.EqualTo(1));
	}

	[Test]
	public void StartTurn_RefillsCurrentManaToMax()
	{
		var (finalState, _) = _state.AddAction(MakeStartTurn(_ids.Player1Id)).ProcessAllActions();

		var player = finalState.GetPlayer(_ids.Player1Id);
		Assert.That(player.CurrentMana, Is.EqualTo(player.MaxMana));
	}

	[Test]
	public void StartTurn_MaxManaCapsAtTen()
	{
		// Set player to MaxMana = 10 already
		var player = _state.GetPlayer(_ids.Player1Id);
		var state = _state.UpdateObject(
			_ids.Player1Id,
			player with
			{
				MaxMana = 10,
				CurrentMana = 10,
			}
		);

		var (finalState, _) = state.AddAction(MakeStartTurn(_ids.Player1Id)).ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player1Id).MaxMana, Is.EqualTo(10));
	}

	[Test]
	public void StartTurn_DrawsACard_WhenSkipDrawFalse()
	{
		var state = TestCardFactory.AddCardsToLibrary(_state, _ids.Player1Id, "Card A");

		var (finalState, events) = state
			.AddAction(MakeStartTurn(_ids.Player1Id, skipDraw: false))
			.ProcessAllActions();

		Assert.That(finalState.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(1));
		Assert.That(events.OfType<CardDrawnEvent>().Count(), Is.EqualTo(1));
	}

	[Test]
	public void StartTurn_DoesNotDrawACard_WhenSkipDrawTrue()
	{
		var state = TestCardFactory.AddCardsToLibrary(_state, _ids.Player1Id, "Card A");

		var (finalState, events) = state
			.AddAction(MakeStartTurn(_ids.Player1Id, skipDraw: true))
			.ProcessAllActions();

		Assert.That(finalState.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(0));
		Assert.That(events.OfType<CardDrawnEvent>().Count(), Is.EqualTo(0));
	}

	[Test]
	public void StartTurn_ClearsSummoningSickness_OnControlledCreatures()
	{
		var creature = AddCreatureToBattlefield(_state, _ids.Player1Id, sickness: true);
		var (finalState, _) = creature
			.state.AddAction(MakeStartTurn(_ids.Player1Id))
			.ProcessAllActions();

		var card = (Card)finalState.GetObject(creature.id);
		Assert.That(card.GetComponent<CreatureComponent>()!.HasSummoningSickness, Is.False);
	}

	[Test]
	public void StartTurn_ClearsHasAttacked_OnControlledCreatures()
	{
		var creature = AddCreatureToBattlefield(_state, _ids.Player1Id);
		// Manually mark as attacked
		var card = (Card)creature.state.GetObject(creature.id);
		var comp = card.GetComponent<CreatureComponent>()!;
		var attacked = creature.state.UpdateObject(
			creature.id,
			card.WithComponentReplaced(comp with { HasAttacked = true })
		);

		var (finalState, _) = attacked.AddAction(MakeStartTurn(_ids.Player1Id)).ProcessAllActions();

		var updated = (Card)finalState.GetObject(creature.id);
		Assert.That(updated.GetComponent<CreatureComponent>()!.HasAttacked, Is.False);
	}

	[Test]
	public void StartTurn_ResetsDamage_OnControlledCreatures()
	{
		var creature = AddCreatureToBattlefield(_state, _ids.Player1Id);
		var card = (Card)creature.state.GetObject(creature.id);
		var comp = card.GetComponent<CreatureComponent>()!;
		var damaged = creature.state.UpdateObject(
			creature.id,
			card.WithComponentReplaced(comp with { Damage = 3 })
		);

		var (finalState, _) = damaged.AddAction(MakeStartTurn(_ids.Player1Id)).ProcessAllActions();

		var updated = (Card)finalState.GetObject(creature.id);
		Assert.That(updated.GetComponent<CreatureComponent>()!.Damage, Is.EqualTo(0));
	}

	[Test]
	public void StartTurn_DoesNotClearFlags_OnOpponentCreatures()
	{
		var creature = AddCreatureToBattlefield(_state, _ids.Player2Id, sickness: true);

		var (finalState, _) = creature
			.state.AddAction(MakeStartTurn(_ids.Player1Id))
			.ProcessAllActions();

		// Opponent's creature should still have summoning sickness
		var card = (Card)finalState.GetObject(creature.id);
		Assert.That(card.GetComponent<CreatureComponent>()!.HasSummoningSickness, Is.True);
	}

	[Test]
	public void StartTurn_EmitsTurnStartedEvent()
	{
		var (_, events) = _state.AddAction(MakeStartTurn(_ids.Player1Id)).ProcessAllActions();

		var evt = events.OfType<TurnStartedEvent>().Single();
		Assert.That(evt.PlayerId, Is.EqualTo(_ids.Player1Id));
	}

	// ===== END TURN ACTION =====

	[Test]
	public void EndTurn_SwitchesActivePlayer()
	{
		var (finalState, _) = _state.AddAction(MakeEndTurn()).ProcessAllActions();

		Assert.That(finalState.GetActivePlayerId(), Is.EqualTo(_ids.Player2Id));
	}

	[Test]
	public void EndTurn_DoesNotIncrementTurnNumber_WhenPlayer1Ends()
	{
		var (finalState, _) = _state.AddAction(MakeEndTurn()).ProcessAllActions();

		Assert.That(finalState.GetGame(_ids.GameId).TurnNumber, Is.EqualTo(1));
	}

	[Test]
	public void EndTurn_IncrementsTurnNumber_WhenPlayer2Ends()
	{
		// Switch to Player 2's turn first
		var game = _state.GetGame(_ids.GameId);
		var stateOnP2Turn = _state.UpdateObject(
			_ids.GameId,
			game with
			{
				ActivePlayerId = _ids.Player2Id,
			}
		);

		var (finalState, _) = stateOnP2Turn.AddAction(MakeEndTurn()).ProcessAllActions();

		Assert.That(finalState.GetGame(_ids.GameId).TurnNumber, Is.EqualTo(2));
	}

	[Test]
	public void EndTurn_SpawnsStartTurnForNextPlayer()
	{
		var state = TestCardFactory.AddCardsToLibrary(_state, _ids.Player2Id, "Card A");

		var (finalState, events) = state.AddAction(MakeEndTurn()).ProcessAllActions();

		// StartTurnAction should have fired for Player 2, giving them mana and drawing a card
		Assert.That(finalState.GetPlayer(_ids.Player2Id).MaxMana, Is.EqualTo(1));
		Assert.That(
			events.OfType<TurnStartedEvent>().Any(e => e.PlayerId == _ids.Player2Id),
			Is.True
		);
	}

	[Test]
	public void EndTurn_EmitsTurnEndedEvent()
	{
		var (_, events) = _state.AddAction(MakeEndTurn()).ProcessAllActions();

		var evt = events.OfType<TurnEndedEvent>().Single();
		Assert.That(evt.PlayerId, Is.EqualTo(_ids.Player1Id));
	}

	// ===== BEGIN GAME ACTION =====

	[Test]
	public void BeginGame_ShufflesAndDealsOpeningHands()
	{
		var state = TestCardFactory.AddCardsToLibrary(
			_state,
			_ids.Player1Id,
			"A",
			"B",
			"C",
			"D",
			"E",
			"F",
			"G",
			"H"
		);
		state = TestCardFactory.AddCardsToLibrary(
			state,
			_ids.Player2Id,
			"A",
			"B",
			"C",
			"D",
			"E",
			"F",
			"G",
			"H"
		);

		var (finalState, _) = state.BeginGame(_ids.GameId, _ids.Player1Id, _ids.Player2Id);

		Assert.That(finalState.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(4));
		Assert.That(finalState.GetCardsInZone(_ids.Player2HandId).Count(), Is.EqualTo(4));
	}

	[Test]
	public void BeginGame_Player1HasMana_AfterBegin()
	{
		var (finalState, _) = _state.BeginGame(_ids.GameId, _ids.Player1Id, _ids.Player2Id);

		Assert.That(finalState.GetPlayer(_ids.Player1Id).CurrentMana, Is.EqualTo(1));
		Assert.That(finalState.GetPlayer(_ids.Player1Id).MaxMana, Is.EqualTo(1));
	}

	[Test]
	public void BeginGame_Player1DoesNotDraw_OnFirstTurn()
	{
		var state = TestCardFactory.AddCardsToLibrary(
			_state,
			_ids.Player1Id,
			"A",
			"B",
			"C",
			"D",
			"E",
			"F",
			"G",
			"H"
		);
		state = TestCardFactory.AddCardsToLibrary(
			state,
			_ids.Player2Id,
			"A",
			"B",
			"C",
			"D",
			"E",
			"F",
			"G",
			"H"
		);

		var (finalState, _) = state.BeginGame(_ids.GameId, _ids.Player1Id, _ids.Player2Id);

		// Hand should be exactly 4 — the opening hand, no extra draw
		Assert.That(finalState.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(4));
	}

	[Test]
	public void BeginGame_ActivePlayerIsPlayer1()
	{
		var (finalState, _) = _state.BeginGame(_ids.GameId, _ids.Player1Id, _ids.Player2Id);

		Assert.That(finalState.GetActivePlayerId(), Is.EqualTo(_ids.Player1Id));
	}

	// ===== MANA =====

	[Test]
	public void Mana_SpentWhenPlayingCreature()
	{
		var state = GivePlayerMana(_state, _ids.Player1Id, current: 3, max: 3);
		var creature = TestCardFactory.MakeCreatureCard("Bear", _ids.Player1Id, 2, 2, manaCost: 2);
		var (stateWithCard, added) = state.AddObject(creature, parentId: _ids.Player1HandId);

		var (finalState, _) = stateWithCard
			.AddAction(
				new CastCreatureAction { CardId = added.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player1Id).CurrentMana, Is.EqualTo(1));
	}

	[Test]
	public void Mana_CannotPlayCreature_WithInsufficientMana()
	{
		var state = GivePlayerMana(_state, _ids.Player1Id, current: 1, max: 1);
		var creature = TestCardFactory.MakeCreatureCard("Bear", _ids.Player1Id, 2, 2, manaCost: 3);
		var (stateWithCard, added) = state.AddObject(creature, parentId: _ids.Player1HandId);

		var (_, success) = stateWithCard.TryAddAction(
			new CastCreatureAction { CardId = added.Id, CastingPlayerId = _ids.Player1Id }
		);

		Assert.That(success, Is.False);
	}

	// ===== HELPERS =====

	private StartTurnAction MakeStartTurn(int playerId, bool skipDraw = false) =>
		new()
		{
			ActivePlayerId = playerId,
			BattlefieldId = _state.GetPlayerZoneId(playerId, ZoneType.Battlefield),
			SkipDraw = skipDraw,
		};

	private EndTurnAction MakeEndTurn() =>
		new()
		{
			GameId = _ids.GameId,
			Player1Id = _ids.Player1Id,
			Player2Id = _ids.Player2Id,
		};

	private (GameState state, int id) AddCreatureToBattlefield(
		GameState state,
		int ownerId,
		bool sickness = false
	)
	{
		var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);
		var creature = new Card
		{
			Name = "Test Creature",
			ManaCost = 2,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent
				{
					Power = 2,
					Toughness = 2,
					HasSummoningSickness = sickness,
				}
			),
		};
		var (newState, added) = state.AddObject(creature, parentId: battlefieldId);
		return (newState, added.Id);
	}

	private static GameState GivePlayerMana(GameState state, int playerId, int current, int max)
	{
		var player = state.GetPlayer(playerId);
		return state.UpdateObject(playerId, player with { CurrentMana = current, MaxMana = max });
	}
}
