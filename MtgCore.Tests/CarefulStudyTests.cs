using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

[TestFixture]
public class CarefulStudyTests
{
	private GameState _state;
	private MtgGameIds _ids;
	private int _studyId;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();

		var study = CardLibrary.CarefulStudy() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (newState, addedStudy) = _state.AddObject(study, parentId: _ids.Player1HandId);
		_state = newState;
		_studyId = addedStudy.Id;
	}

	// ===== DYNAMIC OPTIONS =====

	[Test]
	public void CarefulStudy_AfterDrawing_ChoiceOptionsReflectNewHand()
	{
		var state = AddCardsToLibrary(_state, "Card A", "Card B");

		var (stateAtChoice, _) = state.AddAction(MakeCastStudy()).ProcessAllActions();

		Assert.That(stateAtChoice.IsWaitingForChoice, Is.True);

		var choice = stateAtChoice.GetPendingChoice();
		Assert.That(choice, Is.Not.Null);
		Assert.That(
			choice!.Options.Count,
			Is.EqualTo(2),
			"Options should reflect the 2 cards drawn into hand"
		);
		Assert.That(
			choice.Options.Select(o => o.DisplayText),
			Contains.Item("Card A").And.Contains("Card B")
		);
	}

	[Test]
	public void CarefulStudy_ChoiceOptions_IncludeCardsAlreadyInHand()
	{
		var (stateWithExisting, _) = _state.AddObject(
			new Card
			{
				Name = "Existing Card",
				ManaCost = 1,
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: _ids.Player1HandId
		);
		var state = AddCardsToLibrary(stateWithExisting, "Drawn A", "Drawn B");

		var (stateAtChoice, _) = state.AddAction(MakeCastStudy()).ProcessAllActions();

		var choice = stateAtChoice.GetPendingChoice();
		Assert.That(
			choice!.Options.Count,
			Is.EqualTo(3),
			"Options should include pre-existing hand card plus 2 drawn cards"
		);
	}

	[Test]
	public void CarefulStudy_PausesAtDiscardChoice_AfterDrawing()
	{
		var state = AddCardsToLibrary(_state, "Card A", "Card B");

		var (stateAtChoice, _) = state.AddAction(MakeCastStudy()).ProcessAllActions();

		Assert.That(
			stateAtChoice.GetCardsInZone(_ids.Player1HandId).Count(),
			Is.EqualTo(2),
			"Both cards should be drawn before the choice pauses execution"
		);
		Assert.That(stateAtChoice.IsWaitingForChoice, Is.True);
	}

	[Test]
	public void CarefulStudy_MovesToGraveyard_AfterFullResolution()
	{
		var state = AddCardsToLibrary(_state, "Card A", "Card B");

		var (stateAtChoice, _) = state.AddAction(MakeCastStudy()).ProcessAllActions();

		// Card is still on the stack while the choice is pending —
		// spells move to the graveyard after their effects fully resolve, not before.
		Assert.That(
			stateAtChoice.GetCardZone(_studyId).ZoneType,
			Is.EqualTo(ZoneType.Stack),
			"Careful Study should still be on the stack while choice is pending"
		);

		// Resolve the choice and confirm the card moves to the graveyard afterwards
		var choice = stateAtChoice.GetPendingChoice()!;
		var (finalState, _) = stateAtChoice.ResolveChoice(
			ImmutableList.Create(choice.Options[0].Id, choice.Options[1].Id)
		);

		Assert.That(
			finalState.GetCardZone(_studyId).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Careful Study should be in the graveyard after full resolution"
		);
	}

	// ===== DISCARD RESOLUTION =====

	[Test]
	public void CarefulStudy_AfterChoosingCards_DiscardsMoveToGraveyard()
	{
		var state = AddCardsToLibrary(_state, "Card A", "Card B");

		var (stateAtChoice, _) = state.AddAction(MakeCastStudy()).ProcessAllActions();

		var choice = stateAtChoice.GetPendingChoice()!;
		var (finalState, _) = stateAtChoice.ResolveChoice(
			ImmutableList.Create(choice.Options[0].Id, choice.Options[1].Id)
		);

		Assert.That(
			finalState.GetCardsInZone(_ids.Player1HandId).Count(),
			Is.EqualTo(0),
			"Hand should be empty after discarding both drawn cards"
		);
		Assert.That(
			finalState.GetCardsInZone(_ids.Player1GraveyardId).Count(),
			Is.EqualTo(3),
			"Graveyard should have Careful Study + 2 discarded cards"
		);
	}

	[Test]
	public void CarefulStudy_CanKeepOneCardAndDiscardAnother()
	{
		var (stateWithExisting, existingCard) = _state.AddObject(
			new Card
			{
				Name = "Keep Me",
				ManaCost = 1,
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: _ids.Player1HandId
		);
		var state = AddCardsToLibrary(stateWithExisting, "Discard A", "Discard B");

		var (stateAtChoice, _) = state.AddAction(MakeCastStudy()).ProcessAllActions();

		var choice = stateAtChoice.GetPendingChoice()!;
		var discardIds = choice
			.Options.Where(o => o.Id != existingCard.Id)
			.Select(o => o.Id)
			.ToImmutableList();
		var (finalState, _) = stateAtChoice.ResolveChoice(discardIds);

		Assert.That(
			finalState.GetCardsInZone(_ids.Player1HandId).Count(),
			Is.EqualTo(1),
			"Should have 1 card remaining in hand"
		);
		Assert.That(
			finalState.GetCardZone(existingCard.Id).ZoneType,
			Is.EqualTo(ZoneType.Hand),
			"The kept card should still be in hand"
		);
	}

	// ===== EVENTS =====

	[Test]
	public void CarefulStudy_EmitsCardDrawnEventsBeforeChoice()
	{
		var state = AddCardsToLibrary(_state, "Card A", "Card B");

		var (_, eventsBeforeChoice) = state.AddAction(MakeCastStudy()).ProcessAllActions();

		Assert.That(eventsBeforeChoice.OfType<CardDrawnEvent>().Count(), Is.EqualTo(2));
	}

	[Test]
	public void CarefulStudy_EmitsCardDiscardedEventsAfterChoice()
	{
		var state = AddCardsToLibrary(_state, "Card A", "Card B");

		var (stateAtChoice, _) = state.AddAction(MakeCastStudy()).ProcessAllActions();

		var choice = stateAtChoice.GetPendingChoice()!;
		var (_, eventsAfterChoice) = stateAtChoice.ResolveChoice(
			ImmutableList.Create(choice.Options[0].Id, choice.Options[1].Id)
		);

		Assert.That(eventsAfterChoice.OfType<CardDiscardedEvent>().Count(), Is.EqualTo(2));
	}

	// ===== VALIDATION =====

	[Test]
	public void CarefulStudy_MustChooseExactlyTwoCards()
	{
		var state = AddCardsToLibrary(_state, "Card A", "Card B");

		var (stateAtChoice, _) = state.AddAction(MakeCastStudy()).ProcessAllActions();

		var choice = stateAtChoice.GetPendingChoice()!;

		Assert.Throws<InvalidOperationException>(
			() => stateAtChoice.ResolveChoice(ImmutableList.Create(choice.Options[0].Id)),
			"Should not be able to discard fewer than 2 cards"
		);
	}

	[Test]
	public void CarefulStudy_NoActionsRemainAfterFullResolution()
	{
		var state = AddCardsToLibrary(_state, "Card A", "Card B");

		var (stateAtChoice, _) = state.AddAction(MakeCastStudy()).ProcessAllActions();

		var choice = stateAtChoice.GetPendingChoice()!;
		Assert.That(choice, Is.Not.Null);
		Assert.That(choice.Options.Count, Is.GreaterThanOrEqualTo(2));

		var (finalState, _) = stateAtChoice.ResolveChoice(
			ImmutableList.Create(choice.Options[0].Id, choice.Options[1].Id)
		);

		Assert.That(finalState.HasPendingActions, Is.False);
		Assert.That(finalState.IsWaitingForChoice, Is.False);
	}

	// ===== HELPERS =====

	private CastSpellAction MakeCastStudy() =>
		new()
		{
			CardId = _studyId,
			CastingPlayerId = _ids.Player1Id,
			GameId = _ids.GameId,
			TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty,
		};

	private GameState AddCardsToLibrary(GameState state, params string[] cardNames)
	{
		var libraryId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Library);

		foreach (var name in cardNames)
		{
			var card = new Card
			{
				Name = name,
				ManaCost = 1,
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			};
			state = state.AddObject(card, parentId: libraryId).GameState;
		}

		return state;
	}
}
