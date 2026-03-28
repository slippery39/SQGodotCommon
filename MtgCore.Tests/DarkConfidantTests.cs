using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

[TestFixture]
public class DarkConfidantTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.Create();
	}

	// ===== CORE TRIGGER BEHAVIOUR =====

	[Test]
	public void DarkConfidantTrigger_RevealedCard_MovesToHand()
	{
		var (state, card) = AddCardToLibrary(_state, _ids.Player1Id, "Shock", manaCost: 1);

		var (finalState, _) = state
			.AddAction(CardLibrary.DarkConfidantTrigger(_ids.Player1Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetCardZone(card.Id).ZoneType,
			Is.EqualTo(ZoneType.Hand),
			"Revealed card should be in hand"
		);
		Assert.That(
			finalState.GetCardsInZone(_ids.Player1LibraryId).Count(),
			Is.EqualTo(0),
			"Library should be empty"
		);
	}

	[Test]
	public void DarkConfidantTrigger_PlayerLosesLifeEqualToManaCost()
	{
		var (state, _) = AddCardToLibrary(_state, _ids.Player1Id, "Shock", manaCost: 1);

		var (finalState, _) = state
			.AddAction(CardLibrary.DarkConfidantTrigger(_ids.Player1Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(19),
			"Player should lose 1 life for a 1 mana card"
		);
	}

	[Test]
	public void DarkConfidantTrigger_HighManaCostCard_PlayerLosesMoreLife()
	{
		var (state, _) = AddCardToLibrary(_state, _ids.Player1Id, "Nicol Bolas", manaCost: 8);

		var (finalState, _) = state
			.AddAction(CardLibrary.DarkConfidantTrigger(_ids.Player1Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(12),
			"Player should lose 8 life for an 8 mana card"
		);
	}

	[Test]
	public void DarkConfidantTrigger_EmitsCardRevealedEvent()
	{
		var (state, card) = AddCardToLibrary(_state, _ids.Player1Id, "Shock", manaCost: 1);

		var (_, events) = state
			.AddAction(CardLibrary.DarkConfidantTrigger(_ids.Player1Id))
			.ProcessAllActions();

		var revealEvent = events.OfType<CardRevealedEvent>().Single();
		Assert.That(revealEvent.CardId, Is.EqualTo(card.Id));
		Assert.That(revealEvent.ManaCost, Is.EqualTo(1));
		Assert.That(revealEvent.PlayerId, Is.EqualTo(_ids.Player1Id));
	}

	[Test]
	public void DarkConfidantTrigger_EmitsPlayerLostLifeEvent()
	{
		var (state, _) = AddCardToLibrary(_state, _ids.Player1Id, "Shock", manaCost: 1);

		var (_, events) = state
			.AddAction(CardLibrary.DarkConfidantTrigger(_ids.Player1Id))
			.ProcessAllActions();

		var lostLifeEvent = events.OfType<PlayerLostLifeEvent>().Single();
		Assert.That(lostLifeEvent.PlayerId, Is.EqualTo(_ids.Player1Id));
		Assert.That(lostLifeEvent.Amount, Is.EqualTo(1));
	}

	// ===== PIPELINE CONTEXT =====

	[Test]
	public void DarkConfidantTrigger_ManaCostPassedThroughContext_NotHardCoded()
	{
		// Run trigger with two different mana costs to confirm
		// the life loss is dynamic not fixed
		var (stateA, _) = AddCardToLibrary(_state, _ids.Player1Id, "Cheap", manaCost: 1);
		var (stateB, _) = AddCardToLibrary(_state, _ids.Player1Id, "Expensive", manaCost: 5);

		var (finalA, _) = stateA
			.AddAction(CardLibrary.DarkConfidantTrigger(_ids.Player1Id))
			.ProcessAllActions();
		var (finalB, _) = stateB
			.AddAction(CardLibrary.DarkConfidantTrigger(_ids.Player1Id))
			.ProcessAllActions();

		Assert.That(
			finalA.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(19),
			"1 mana card should cost 1 life"
		);
		Assert.That(
			finalB.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(15),
			"5 mana card should cost 5 life"
		);
	}

	[Test]
	public void DarkConfidantTrigger_RevealsTopCard_NotOtherCards()
	{
		var state = _state;
		// Add two cards — top card is added first
		var (s1, topCard) = AddCardToLibrary(state, _ids.Player1Id, "Top Card", manaCost: 2);
		var (s2, _) = AddCardToLibrary(s1, _ids.Player1Id, "Bottom Card", manaCost: 4);

		var (finalState, events) = s2.AddAction(CardLibrary.DarkConfidantTrigger(_ids.Player1Id))
			.ProcessAllActions();

		// Should reveal and draw the top card (mana cost 2, not 4)
		var revealEvent = events.OfType<CardRevealedEvent>().Single();
		Assert.That(revealEvent.CardId, Is.EqualTo(topCard.Id));
		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(18),
			"Should lose 2 life for the top card, not 4 for the bottom"
		);

		Assert.That(
			finalState.GetCardsInZone(_ids.Player1LibraryId).Count(),
			Is.EqualTo(1),
			"Bottom card should remain in library"
		);
	}

	// ===== EDGE CASES =====

	[Test]
	public void DarkConfidantTrigger_EmptyLibrary_NoLifeLost_NoCardDrawn()
	{
		// No cards in library
		var (finalState, events) = _state
			.AddAction(CardLibrary.DarkConfidantTrigger(_ids.Player1Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(20),
			"Player should not lose life with empty library"
		);
		Assert.That(
			finalState.GetCardsInZone(_ids.Player1HandId).Count(),
			Is.EqualTo(0),
			"Hand should remain empty"
		);
		Assert.That(events.OfType<CardRevealedEvent>(), Is.Empty);
		Assert.That(events.OfType<PlayerLostLifeEvent>(), Is.Empty);
	}

	[Test]
	public void DarkConfidantTrigger_ZeroManaCostCard_NoLifeLost()
	{
		var (state, card) = AddCardToLibrary(_state, _ids.Player1Id, "Zero Cost", manaCost: 0);

		var (finalState, events) = state
			.AddAction(CardLibrary.DarkConfidantTrigger(_ids.Player1Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(20),
			"Zero mana card should not cost any life"
		);
		Assert.That(
			finalState.GetCardZone(card.Id).ZoneType,
			Is.EqualTo(ZoneType.Hand),
			"Card should still move to hand even with zero mana cost"
		);
		Assert.That(events.OfType<PlayerLostLifeEvent>(), Is.Empty);
	}

	[Test]
	public void DarkConfidantTrigger_DoesNotAffectOpponent()
	{
		var (state, _) = AddCardToLibrary(_state, _ids.Player1Id, "Shock", manaCost: 1);

		var (finalState, _) = state
			.AddAction(CardLibrary.DarkConfidantTrigger(_ids.Player1Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(20),
			"Opponent should be unaffected"
		);
		Assert.That(
			finalState.GetCardsInZone(_ids.Player2HandId).Count(),
			Is.EqualTo(0),
			"Opponent's hand should be empty"
		);
	}

	[Test]
	public void DarkConfidantTrigger_NoActionsRemainAfterResolution()
	{
		var (state, _) = AddCardToLibrary(_state, _ids.Player1Id, "Shock", manaCost: 1);

		var (finalState, _) = state
			.AddAction(CardLibrary.DarkConfidantTrigger(_ids.Player1Id))
			.ProcessAllActions();

		Assert.That(finalState.HasPendingActions, Is.False);
	}

	// ===== HELPERS =====

	private (GameState, Card) AddCardToLibrary(
		GameState state,
		int playerId,
		string name,
		int manaCost
	)
	{
		var libraryId = state.GetPlayerZoneId(playerId, ZoneType.Library);
		var card = new InstantCard
		{
			Name = name,
			ManaCost = manaCost,
			OwnerId = playerId,
			ControllerId = playerId,
		};
		var (newState, addedCard) = state.AddObject(card, parentId: libraryId);
		return (newState, addedCard);
	}
}
