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
		(_state, _ids) = MtgGameFactory.CreateForTesting();

		var drawDiscard = new Card
		{
			Name = "Faithless Looting (simplified)",
			ManaCost = 1,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
			Components = ImmutableArray.Create<GameComponent>(
				new SpellComponent
				{
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
										TargetContextKey = ContextKeys.CastingPlayerId,
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
										TargetContextKey = ContextKeys.SelectedCardIds,
									}
								),
							},
						}
					),
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
		var (stateWithCard, _) = _state.AddObject(
			MakeLibraryCard(),
			parentId: _ids.Player1LibraryId
		);

		var (stateAtChoice, events) = stateWithCard.AddAction(MakeCast()).ProcessAllActions();

		Assert.That(stateAtChoice.IsWaitingForChoice, Is.True);
		Assert.That(events.OfType<CardDrawnEvent>().Count(), Is.EqualTo(1));
		Assert.That(stateAtChoice.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(1));
	}

	[Test]
	public void DrawOneDiscardOne_ChoiceOffersSingleCard()
	{
		var (stateWithCard, drawnCard) = _state.AddObject(
			MakeLibraryCard(),
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
		var (stateWithCard, drawnCard) = _state.AddObject(
			MakeLibraryCard(),
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
		var (stateWithCard, _) = _state.AddObject(
			MakeLibraryCard(),
			parentId: _ids.Player1LibraryId
		);

		var (stateAtChoice, _) = stateWithCard.AddAction(MakeCast()).ProcessAllActions();

		var choice = stateAtChoice.GetPendingChoice()!;
		var (finalState, _) = stateAtChoice.ResolveChoice(
			ImmutableList.Create(choice.Options[0].Id)
		);

		Assert.That(finalState.GetCardsInZone(_ids.Player1HandId).Count(), Is.EqualTo(0));
		Assert.That(finalState.HasPendingActions, Is.False);
	}

	/// <summary>
	/// A choice belongs to the player it is ABOUT, not to whoever is taking a turn.
	///
	/// Nothing recorded an owner, so both front ends inferred one from the active player and both
	/// got it wrong the same way: an opponent's trigger firing during your turn put THEIR discard
	/// and THEIR scry in front of you to answer. Player 2 here never becomes active, so an
	/// active-player inference cannot accidentally pass this.
	/// </summary>
	[Test]
	public void PendingChoice_IsOwnedByThePlayerItIsAbout_NotTheActivePlayer()
	{
		var (stateWithCard, _) = _state.AddObject(
			MakeLibraryCard(),
			parentId: _ids.Player1LibraryId
		);
		var (stateAtChoice, _) = stateWithCard.AddAction(MakeCast()).ProcessAllActions();

		Assert.That(stateAtChoice.IsWaitingForChoice, Is.True, "precondition: paused on a choice");
		Assert.That(
			stateAtChoice.GetPendingChoiceDecidingPlayerId(),
			Is.EqualTo(_ids.Player1Id),
			"player 1 cast the spell, so player 1 discards"
		);

		// The opponent's own discard — same action, other player. This is the Avaricious Dragon
		// shape: it fires on the opponent's end step and must never be handed to the human.
		var (opponentChoice, _) = _state
			.AddAction(
				new PipelineAction
				{
					Steps = ImmutableList.Create<GameAction>(
						new SelectCardsFromHandAction
						{
							Prompt = "Opponent discards",
							PlayerId = _ids.Player2Id,
							MinChoices = 1,
							MaxChoices = 1,
							OutputKey = ContextKeys.SelectedCardIds,
						}
					),
				}
			)
			.ProcessAllActions();

		Assert.That(
			opponentChoice.GetPendingChoiceDecidingPlayerId(),
			Is.EqualTo(_ids.Player2Id),
			"it is the opponent's hand, so it is the opponent's choice"
		);
	}

	// ===== HELPERS =====

	private CastSpellAction MakeCast() =>
		new()
		{
			CardId = _cardId,
			CastingPlayerId = _ids.Player1Id,
			TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty,
		};

	private Card MakeLibraryCard() => TestCardFactory.MakePlainCard("Library Card", _ids.Player1Id);
}
