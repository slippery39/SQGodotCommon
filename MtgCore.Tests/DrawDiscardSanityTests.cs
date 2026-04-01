using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

[TestFixture]
public class DrawDiscardSanityTests
{
	private GameState _state;
	private MtgGameIds _ids;
	private int _cardId;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.Create();

		// A "Draw 1, Discard 1" card — same pipeline as Careful Study but with Amount = 1
		var drawDiscard = new InstantCard
		{
			Name = "Faithless Looting (simplified)",
			ManaCost = 1,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
			Effects = ImmutableList.Create(
				new CardEffect
				{
					TargetingStrategy = TargetingStrategy.NoTarget(),
					ActionTemplate = new PipelineAction
					{
						Steps = ImmutableList.Create<GameAction>(
							new DrawCardsAction
							{
								Amount = 1,
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
							},
							new SelectCardsFromHandAction
							{
								Prompt = "Choose 1 card to discard",
								MinChoices = 1,
								MaxChoices = 1,
								OutputKey = ContextKeys.SelectedCardIds,
							},
							new DiscardCardsAction
							{
								CardIdsContextKey = ContextKeys.SelectedCardIds,
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
							}
						),
					},
				}
			),
		};

		var (newState, added) = _state.AddObject(drawDiscard, parentId: _ids.Player1HandId);
		_state = newState;
		_cardId = added.Id;
	}

	[Test]
	public void DrawOneDiscardOne_DrawsOneCard()
	{
		var libraryCard = new InstantCard
		{
			Name = "Library Card",
			ManaCost = 1,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (stateWithCard, _) = _state.AddObject(libraryCard, parentId: _ids.Player1LibraryId);

		var (stateAtChoice, events) = stateWithCard.AddAction(MakeCast()).ProcessAllActions();

		Assert.That(stateAtChoice.IsWaitingForChoice, Is.True);
		Assert.That(events.OfType<CardDrawnEvent>().Count(), Is.EqualTo(1));

		// Hand should have the drawn card (draw-discard spell itself went to graveyard on cast)
		Assert.That(stateAtChoice.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(1));
	}

	[Test]
	public void DrawOneDiscardOne_ChoiceOffersSingleCard()
	{
		var libraryCard = new InstantCard
		{
			Name = "Library Card",
			ManaCost = 1,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (stateWithCard, drawnCard) = _state.AddObject(
			libraryCard,
			parentId: _ids.Player1LibraryId
		);

		var (stateAtChoice, _) = stateWithCard.AddAction(MakeCast()).ProcessAllActions();

		var choice = stateAtChoice.GetPendingChoice();
		Assert.That(choice, Is.Not.Null);
		Assert.That(choice!.Options.Count, Is.EqualTo(1));
		Assert.That(choice.Options[0].Id, Is.EqualTo(drawnCard.Id));
	}

	[Test]
	public void DrawOneDiscardOne_AfterChoice_CardMovesToGraveyard()
	{
		var libraryCard = new InstantCard
		{
			Name = "Library Card",
			ManaCost = 1,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (stateWithCard, drawnCard) = _state.AddObject(
			libraryCard,
			parentId: _ids.Player1LibraryId
		);

		var (stateAtChoice, _) = stateWithCard.AddAction(MakeCast()).ProcessAllActions();

		var choice = stateAtChoice.GetPendingChoice()!;
		var (finalState, events) = stateAtChoice.ResolveChoice(
			ImmutableList.Create(choice.Options[0].Id)
		);

		Assert.That(
			finalState.GetCardZone(drawnCard.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Drawn card should be discarded to graveyard"
		);
		Assert.That(events.OfType<CardDiscardedEvent>().Count(), Is.EqualTo(1));
	}

	[Test]
	public void DrawOneDiscardOne_HandEmptyAfterDiscard()
	{
		var libraryCard = new InstantCard
		{
			Name = "Library Card",
			ManaCost = 1,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (stateWithCard, _) = _state.AddObject(libraryCard, parentId: _ids.Player1LibraryId);

		var (stateAtChoice, _) = stateWithCard.AddAction(MakeCast()).ProcessAllActions();

		var choice = stateAtChoice.GetPendingChoice()!;
		var (finalState, _) = stateAtChoice.ResolveChoice([choice.Options[0].Id]);

		Assert.That(finalState.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(0));
		Assert.That(finalState.HasPendingActions, Is.False);
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
}
