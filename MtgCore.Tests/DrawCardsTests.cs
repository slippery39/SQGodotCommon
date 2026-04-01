using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

[TestFixture]
public class DrawCardsTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.Create();
	}

	// ===== BASIC DRAW =====

	[Test]
	public void DrawCards_MovesTopCardToHand()
	{
		var state = AddCardsToLibrary(_state, _ids.Player1Id, "Card A", "Card B", "Card C");

		var (finalState, _) = state
			.AddAction(new DrawCardsAction { PlayerId = _ids.Player1Id, Amount = 1 })
			.ProcessAllActions();

		Assert.That(finalState.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(1));
		Assert.That(finalState.GetCardsInZone(_ids.Player1LibraryId).Count(), Is.EqualTo(2));
	}

	[Test]
	public void DrawCards_DrawsFromTopOfLibrary()
	{
		var state = AddCardsToLibrary(_state, _ids.Player1Id, "Card A", "Card B", "Card C");
		var topCardId = state.GetChildrenIds(_ids.Player1LibraryId).First();

		var (finalState, _) = state
			.AddAction(new DrawCardsAction { PlayerId = _ids.Player1Id, Amount = 1 })
			.ProcessAllActions();

		var handCards = finalState.GetCardsInZone(_ids.Player1HandId).ToList();
		Assert.That(
			handCards.Single().Id,
			Is.EqualTo(topCardId),
			"The card drawn should be the top card of the library"
		);
	}

	[Test]
	public void DrawCards_DrawMultiple_MovesCorrectNumberToHand()
	{
		var state = AddCardsToLibrary(_state, _ids.Player1Id, "Card A", "Card B", "Card C");

		var (finalState, _) = state
			.AddAction(new DrawCardsAction { PlayerId = _ids.Player1Id, Amount = 2 })
			.ProcessAllActions();

		Assert.That(finalState.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(2));
		Assert.That(finalState.GetCardsInZone(_ids.Player1LibraryId).Count(), Is.EqualTo(1));
	}

	[Test]
	public void DrawCards_DrawEntireLibrary_HandHasAllCards()
	{
		var state = AddCardsToLibrary(_state, _ids.Player1Id, "Card A", "Card B", "Card C");

		var (finalState, _) = state
			.AddAction(new DrawCardsAction { PlayerId = _ids.Player1Id, Amount = 3 })
			.ProcessAllActions();

		Assert.That(finalState.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(3));
		Assert.That(finalState.GetCardsInZone(_ids.Player1LibraryId).Count(), Is.EqualTo(0));
	}

	// ===== EVENTS =====

	[Test]
	public void DrawCards_EmitsCardDrawnEventPerCard()
	{
		var state = AddCardsToLibrary(_state, _ids.Player1Id, "Card A", "Card B", "Card C");

		var (_, events) = state
			.AddAction(new DrawCardsAction { PlayerId = _ids.Player1Id, Amount = 2 })
			.ProcessAllActions();

		var drawEvents = events.OfType<CardDrawnEvent>().ToList();
		Assert.That(drawEvents.Count, Is.EqualTo(2));
		Assert.That(drawEvents.All(e => e.PlayerId == _ids.Player1Id), Is.True);
	}

	[Test]
	public void DrawCards_CardDrawnEvent_ContainsCorrectCardId()
	{
		var state = AddCardsToLibrary(_state, _ids.Player1Id, "Card A");
		var topCardId = state.GetChildrenIds(_ids.Player1LibraryId).First();

		var (_, events) = state
			.AddAction(new DrawCardsAction { PlayerId = _ids.Player1Id, Amount = 1 })
			.ProcessAllActions();

		var drawEvent = events.OfType<CardDrawnEvent>().Single();
		Assert.That(drawEvent.CardId, Is.EqualTo(topCardId));
	}

	// ===== EMPTY LIBRARY =====

	[Test]
	public void DrawCards_EmptyLibrary_EmitsLibraryEmptyEvent()
	{
		var (_, events) = _state
			.AddAction(new DrawCardsAction { PlayerId = _ids.Player1Id, Amount = 1 })
			.ProcessAllActions();

		Assert.That(events.OfType<LibraryEmptyEvent>().Count(), Is.EqualTo(1));
		Assert.That(
			events.OfType<LibraryEmptyEvent>().Single().PlayerId,
			Is.EqualTo(_ids.Player1Id)
		);
	}

	[Test]
	public void DrawCards_RunsOutMidDraw_DrawsRemainingAndEmitsLibraryEmpty()
	{
		var state = AddCardsToLibrary(_state, _ids.Player1Id, "Card A", "Card B");

		var (finalState, events) = state
			.AddAction(new DrawCardsAction { PlayerId = _ids.Player1Id, Amount = 5 })
			.ProcessAllActions();

		Assert.That(finalState.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(2));
		Assert.That(events.OfType<LibraryEmptyEvent>().Count(), Is.EqualTo(1));
	}

	// ===== HELPERS =====

	private GameState AddCardsToLibrary(GameState state, int playerId, params string[] cardNames)
	{
		var libraryId = state.GetPlayerZoneId(playerId, ZoneType.Library);

		foreach (var name in cardNames)
		{
			var card = new Card
			{
				Name = name,
				ManaCost = 1,
				OwnerId = playerId,
				ControllerId = playerId,
			};
			state = state.AddObject(card, parentId: libraryId).GameState;
		}

		return state;
	}
}
