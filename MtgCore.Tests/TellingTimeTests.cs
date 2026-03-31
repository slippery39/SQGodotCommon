using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

[TestFixture]
public class TellingTimeTests
{
	private GameState _state;
	private MtgGameIds _ids;
	private int _cardId;
	private int _topCardId;
	private int _midCardId;
	private int _botCardId;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.Create();

		// Add Telling Time to hand
		var tellingTime = CardLibrary.TellingTime() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s1, addedCard) = _state.AddObject(tellingTime, parentId: _ids.Player1HandId);
		_cardId = addedCard.Id;

		// Add 3 cards to library (top to bottom: Top, Mid, Bot)
		var (s2, topCard) = s1.AddObject(MakeCard("Top Card", 1), parentId: _ids.Player1LibraryId);
		var (s3, midCard) = s2.AddObject(MakeCard("Mid Card", 2), parentId: _ids.Player1LibraryId);
		var (s4, botCard) = s3.AddObject(MakeCard("Bot Card", 3), parentId: _ids.Player1LibraryId);

		_state = s4;
		_topCardId = topCard.Id;
		_midCardId = midCard.Id;
		_botCardId = botCard.Id;
	}

	// ===== FIRST CHOICE =====

	[Test]
	public void TellingTime_FirstChoice_OffersTopThreeCards()
	{
		var (stateAtChoice, _) = _state.AddAction(MakeCast()).ProcessAllActions();

		Assert.That(stateAtChoice.IsWaitingForChoice, Is.True);

		var choice = stateAtChoice.GetPendingChoice();
		Assert.That(choice!.Options.Count, Is.EqualTo(3));
		Assert.That(
			choice.Options.Select(o => o.Id),
			Contains.Item(_topCardId).And.Contains(_midCardId).And.Contains(_botCardId)
		);
	}

	[Test]
	public void TellingTime_FirstChoice_CardsRemainInLibrary()
	{
		var (stateAtChoice, _) = _state.AddAction(MakeCast()).ProcessAllActions();

		// Cards should not have moved yet
		Assert.That(stateAtChoice.GetCardsInZone(_ids.Player1LibraryId).Count(), Is.EqualTo(3));
		Assert.That(stateAtChoice.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(0));
	}

	// ===== SECOND CHOICE =====

	[Test]
	public void TellingTime_SecondChoice_ExcludesFirstChoice()
	{
		var (stateAtFirstChoice, _) = _state.AddAction(MakeCast()).ProcessAllActions();

		// Choose top card for hand
		var (stateAtSecondChoice, _) = stateAtFirstChoice.ResolveChoice(
			ImmutableList.Create(_topCardId)
		);

		Assert.That(stateAtSecondChoice.IsWaitingForChoice, Is.True);

		var choice = stateAtSecondChoice.GetPendingChoice();
		Assert.That(
			choice!.Options.Count,
			Is.EqualTo(2),
			"Second choice should only offer 2 remaining cards"
		);
		Assert.That(
			choice.Options.Select(o => o.Id),
			Does.Not.Contain(_topCardId),
			"Previously chosen card should be excluded"
		);
		Assert.That(
			choice.Options.Select(o => o.Id),
			Contains.Item(_midCardId).And.Contains(_botCardId)
		);
	}

	[Test]
	public void TellingTime_AfterFirstChoice_ChosenCardMovesToHand()
	{
		var (stateAtFirstChoice, _) = _state.AddAction(MakeCast()).ProcessAllActions();

		var (stateAtSecondChoice, _) = stateAtFirstChoice.ResolveChoice(
			ImmutableList.Create(_topCardId)
		);

		Assert.That(
			stateAtSecondChoice.GetCardZone(_topCardId).ZoneType,
			Is.EqualTo(ZoneType.Hand),
			"Chosen card should be in hand"
		);
	}

	// ===== FULL RESOLUTION =====

	[Test]
	public void TellingTime_FullFlow_OneCardInHand()
	{
		var (finalState, _) = ResolveFullFlow(handChoice: _topCardId, topChoice: _midCardId);

		Assert.That(finalState.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(1));
		Assert.That(finalState.GetCardZone(_topCardId).ZoneType, Is.EqualTo(ZoneType.Hand));
	}

	[Test]
	public void TellingTime_FullFlow_OneCardOnTopOfLibrary()
	{
		var (finalState, _) = ResolveFullFlow(handChoice: _topCardId, topChoice: _midCardId);

		var libraryIds = finalState.GetChildrenIds(_ids.Player1LibraryId).ToList();
		Assert.That(libraryIds[0], Is.EqualTo(_midCardId), "Mid card should be on top of library");
	}

	[Test]
	public void TellingTime_FullFlow_OneCardOnBottomOfLibrary()
	{
		var (finalState, _) = ResolveFullFlow(handChoice: _topCardId, topChoice: _midCardId);

		var libraryIds = finalState.GetChildrenIds(_ids.Player1LibraryId).ToList();
		Assert.That(
			libraryIds[^1],
			Is.EqualTo(_botCardId),
			"Bot card should be on bottom of library"
		);
	}

	[Test]
	public void TellingTime_FullFlow_LibraryHasTwoCards()
	{
		var (finalState, _) = ResolveFullFlow(handChoice: _topCardId, topChoice: _midCardId);

		Assert.That(
			finalState.GetCardsInZone(_ids.Player1LibraryId).Count(),
			Is.EqualTo(2),
			"Library should have 2 cards after putting one in hand"
		);
	}

	[Test]
	public void TellingTime_FullFlow_DifferentChoices_CorrectOutcome()
	{
		// Choose mid for hand, bot for top — top card goes to bottom
		var (finalState, _) = ResolveFullFlow(handChoice: _midCardId, topChoice: _botCardId);

		var libraryIds = finalState.GetChildrenIds(_ids.Player1LibraryId).ToList();

		Assert.That(finalState.GetCardZone(_midCardId).ZoneType, Is.EqualTo(ZoneType.Hand));
		Assert.That(libraryIds[0], Is.EqualTo(_botCardId), "Bot should be on top");
		Assert.That(libraryIds[^1], Is.EqualTo(_topCardId), "Top should be on bottom");
	}

	[Test]
	public void TellingTime_MovesToGraveyard_AfterResolving()
	{
		var (finalState, _) = ResolveFullFlow(handChoice: _topCardId, topChoice: _midCardId);

		Assert.That(
			finalState.GetCardZone(_cardId).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Telling Time should be in graveyard after resolving"
		);
	}

	[Test]
	public void TellingTime_NoActionsRemainAfterFullResolution()
	{
		var (finalState, _) = ResolveFullFlow(handChoice: _topCardId, topChoice: _midCardId);

		Assert.That(finalState.HasPendingActions, Is.False);
		Assert.That(finalState.IsWaitingForChoice, Is.False);
	}

	[Test]
	public void TellingTime_DoesNotAffectOpponent()
	{
		var (finalState, _) = ResolveFullFlow(handChoice: _topCardId, topChoice: _midCardId);

		Assert.That(finalState.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(20));
		Assert.That(finalState.GetCardsInZone(_ids.Player2HandId).Count(), Is.EqualTo(0));
	}

	// ===== FEWER THAN 3 CARDS =====

	[Test]
	public void TellingTime_WithOnlyOneCardInLibrary_OffersOneOption()
	{
		// Remove mid and bot cards, leave only top
		var state = _state
			.MoveObject(_midCardId, _ids.Player1GraveyardId)
			.MoveObject(_botCardId, _ids.Player1GraveyardId);

		var (stateAtChoice, _) = state.AddAction(MakeCast()).ProcessAllActions();

		var choice = stateAtChoice.GetPendingChoice();
		Assert.That(choice!.Options.Count, Is.EqualTo(1));
	}

	[Test]
	public void TellingTime_MoveToHand_UsesCastingPlayerIdFromContext()
	{
		// This test specifically verifies that MoveCardToHandAction reads
		// CastingPlayerId from pipeline context rather than relying on a
		// hardcoded PlayerId — the bug that caused the console to crash.
		var (stateAtFirstChoice, _) = _state.AddAction(MakeCast()).ProcessAllActions();

		// Resolve choice — this triggers MoveCardToHandAction
		// If PlayerId is not read from context, GetPlayerZoneId(0, Hand) throws
		Assert.DoesNotThrow(() =>
		{
			var (stateAfter, _) = stateAtFirstChoice.ResolveChoice(
				ImmutableList.Create(_topCardId)
			);
			// Card should be in hand — not just "no exception", but correct result
			Assert.That(stateAfter.GetCardZone(_topCardId).ZoneType, Is.EqualTo(ZoneType.Hand));
		});
	}

	// ===== HELPERS =====

	private CastSpellAction MakeCast() =>
		new()
		{
			CardId = _cardId,
			CastingPlayerId = _ids.Player1Id,
			GameId = _ids.GameId,
			TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty,
		};

	private (GameState, ImmutableList<GameEvent>) ResolveFullFlow(int handChoice, int topChoice)
	{
		var (stateAtFirstChoice, _) = _state.AddAction(MakeCast()).ProcessAllActions();

		var (stateAtSecondChoice, _) = stateAtFirstChoice.ResolveChoice(
			ImmutableList.Create(handChoice)
		);

		return stateAtSecondChoice.ResolveChoice(ImmutableList.Create(topChoice));
	}

	private InstantCard MakeCard(string name, int manaCost) =>
		new()
		{
			Name = name,
			ManaCost = manaCost,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
}
