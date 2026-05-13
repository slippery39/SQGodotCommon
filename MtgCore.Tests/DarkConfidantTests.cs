using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Sanity tests for Dark Confidant's TurnStarted triggered ability pipeline.
///
/// The pipeline is: RevealTopCard → AddTemporaryMana(4) → GainLife×5 (by mana cost) → DrawCards(2)
/// Each test isolates one step of that pipeline to confirm it works independently.
/// </summary>
[TestFixture]
public class DarkConfidantTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	// ===== REVEAL =====

	[Test]
	public void TurnStartedTrigger_EmitsCardRevealedEvent()
	{
		var (s1, _) = AddCardToTopOfLibrary(_state, _ids.Player1Id, manaCost: 3);
		var (s2, _) = AddDarkConfidantToBattlefield(s1, _ids.Player1Id);

		var (_, events) = s2.AddAction(MakeStartTurn(_ids.Player1Id, skipDraw: true))
			.ProcessAllActions();

		Assert.That(
			events.OfType<CardRevealedEvent>().Any(),
			Is.True,
			"RevealTopCardAction should emit a CardRevealedEvent"
		);
	}

	[Test]
	public void TurnStartedTrigger_RevealedEvent_HasCorrectManaCost()
	{
		var (s1, _) = AddCardToTopOfLibrary(_state, _ids.Player1Id, manaCost: 3);
		var (s2, _) = AddDarkConfidantToBattlefield(s1, _ids.Player1Id);

		var (_, events) = s2.AddAction(MakeStartTurn(_ids.Player1Id, skipDraw: true))
			.ProcessAllActions();

		var reveal = events.OfType<CardRevealedEvent>().SingleOrDefault();
		Assert.That(reveal, Is.Not.Null, "CardRevealedEvent must be emitted");
		Assert.That(
			reveal!.ManaCost,
			Is.EqualTo(3),
			"Revealed mana cost should match the top card"
		);
	}

	[Test]
	public void TurnStartedTrigger_RevealedCard_StaysInLibrary()
	{
		var (s1, topCard) = AddCardToTopOfLibrary(_state, _ids.Player1Id, manaCost: 3);
		var (s2, _) = AddDarkConfidantToBattlefield(s1, _ids.Player1Id);

		var (finalState, _) = s2.AddAction(MakeStartTurn(_ids.Player1Id, skipDraw: true))
			.ProcessAllActions();

		var libraryId = finalState.GetPlayerZoneId(_ids.Player1Id, ZoneType.Library);

		// The reveal step peeks only — top card should still be in the library
		// (unless subsequently drawn by the DrawCardsAction step of the same pipeline)
		var libraryCardIds = finalState.GetChildrenIds(libraryId).ToList();
		Assert.That(
			libraryCardIds.Contains(topCard.Id)
				|| finalState.GetCardsInZone(_ids.Player1HandId).Any(c => c.Id == topCard.Id),
			Is.True,
			"Top card should either still be in library (if no draw) or in hand (if drawn by pipeline) — it must not be lost"
		);
	}

	// ===== MANA =====

	[Test]
	public void TurnStartedTrigger_AddsFourTemporaryMana()
	{
		var (s1, _) = AddCardToTopOfLibrary(_state, _ids.Player1Id, manaCost: 3);
		var (s2, _) = AddDarkConfidantToBattlefield(s1, _ids.Player1Id);

		// Set known mana to isolate the +4 from the trigger
		var s3 = SetPlayerMana(s2, _ids.Player1Id, current: 0, max: 0);

		var (finalState, _) = s3.AddAction(MakeStartTurn(_ids.Player1Id, skipDraw: true))
			.ProcessAllActions();

		// StartTurn: MaxMana 0→1, CurrentMana refills to 1. Then trigger adds 4.
		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).CurrentMana,
			Is.EqualTo(5),
			"Player should have 1 (from turn start) + 4 (from Dark Confidant) = 5 mana"
		);
	}

	// ===== LIFE GAIN =====

	[Test]
	public void TurnStartedTrigger_GainsLifeEqualToRevealedCardManaCost_FiveTimes()
	{
		const int topCardManaCost = 3;
		const int gainLifeSteps = 5;

		var (s1, _) = AddCardToTopOfLibrary(_state, _ids.Player1Id, manaCost: topCardManaCost);
		var (s2, _) = AddDarkConfidantToBattlefield(s1, _ids.Player1Id);

		var (finalState, _) = s2.AddAction(MakeStartTurn(_ids.Player1Id, skipDraw: true))
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(20 + topCardManaCost * gainLifeSteps),
			$"Player should gain {topCardManaCost} life × {gainLifeSteps} steps = +{topCardManaCost * gainLifeSteps} total"
		);
	}

	[Test]
	public void TurnStartedTrigger_ZeroManaCostCard_GainsNoLife()
	{
		var (s1, _) = AddCardToTopOfLibrary(_state, _ids.Player1Id, manaCost: 0);
		var (s2, _) = AddDarkConfidantToBattlefield(s1, _ids.Player1Id);

		var (finalState, _) = s2.AddAction(MakeStartTurn(_ids.Player1Id, skipDraw: true))
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(20),
			"A 0-mana top card should result in 0 life gained"
		);
	}

	// ===== DRAW =====

	[Test]
	public void TurnStartedTrigger_DrawsTwoCards()
	{
		var (s1, _) = AddCardToTopOfLibrary(_state, _ids.Player1Id, manaCost: 3);
		var s2 = TestCardFactory.AddCardsToLibrary(s1, _ids.Player1Id, "Filler A", "Filler B");
		var (s3, _) = AddDarkConfidantToBattlefield(s2, _ids.Player1Id);

		// Skip the normal turn draw so only the trigger's DrawCardsAction puts cards in hand
		var (finalState, _) = s3.AddAction(MakeStartTurn(_ids.Player1Id, skipDraw: true))
			.ProcessAllActions();

		Assert.That(
			finalState.GetCardsInZone(_ids.Player1HandId).Count(),
			Is.EqualTo(2),
			"Dark Confidant's trigger should draw exactly 2 cards"
		);
	}

	[Test]
	public void TurnStartedTrigger_DoesNotFireOnOpponentTurn()
	{
		var (s1, _) = AddCardToTopOfLibrary(_state, _ids.Player1Id, manaCost: 3);
		var s2 = TestCardFactory.AddCardsToLibrary(s1, _ids.Player1Id, "Filler A", "Filler B");
		var (s3, _) = AddDarkConfidantToBattlefield(s2, _ids.Player1Id);

		// Trigger P2's turn — DC is controlled by P1, so it must not fire
		var (finalState, _) = s3.AddAction(MakeStartTurn(_ids.Player2Id, skipDraw: true))
			.ProcessAllActions();

		Assert.That(
			finalState.GetCardsInZone(_ids.Player1HandId).Count(),
			Is.EqualTo(0),
			"Dark Confidant must not draw cards on the opponent's turn"
		);
	}

	// ===== HELPERS =====

	private (GameState, Card) AddCardToTopOfLibrary(GameState state, int ownerId, int manaCost)
	{
		var libraryId = state.GetPlayerZoneId(ownerId, ZoneType.Library);
		var card = TestCardFactory.MakePlainCard("Top Card", ownerId, manaCost);
		var (newState, added) = state.AddObject(card, parentId: libraryId);
		return (newState, added);
	}

	private (GameState, int cardId) AddDarkConfidantToBattlefield(GameState state, int ownerId)
	{
		var card = MakeDarkConfidant(ownerId);
		var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);
		var (newState, added) = state.AddObject(card, parentId: battlefieldId);
		return (newState, added.Id);
	}

	private static Card MakeDarkConfidant(int ownerId) =>
		new()
		{
			Name = "Dark Confidant",
			ManaCost = 0,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = 1, Toughness = 5 },
				new TriggeredAbilityComponent
				{
					Name = "Dark Confidant Trigger",
					Condition = new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.TurnStarted,
						Filter = new IsControlledByYouSpecification(),
					},
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.NoTarget(),
						ActionTemplate = new PipelineAction
						{
							Steps = ImmutableList.Create<GameAction>(
								new RevealTopCardAction
								{
									PlayerIdContextKey = ContextKeys.CastingPlayerId,
								},
								new AddTemporaryManaAction
								{
									TargetContextKey = ContextKeys.CastingPlayerId,
									Amount = 4,
								},
								new GainLifeAction
								{
									TargetContextKey = ContextKeys.CastingPlayerId,
									AmountContextKey = ContextKeys.RevealedCardManaCost,
								},
								new GainLifeAction
								{
									TargetContextKey = ContextKeys.CastingPlayerId,
									AmountContextKey = ContextKeys.RevealedCardManaCost,
								},
								new GainLifeAction
								{
									TargetContextKey = ContextKeys.CastingPlayerId,
									AmountContextKey = ContextKeys.RevealedCardManaCost,
								},
								new GainLifeAction
								{
									TargetContextKey = ContextKeys.CastingPlayerId,
									AmountContextKey = ContextKeys.RevealedCardManaCost,
								},
								new GainLifeAction
								{
									TargetContextKey = ContextKeys.CastingPlayerId,
									AmountContextKey = ContextKeys.RevealedCardManaCost,
								},
								new DrawCardsAction
								{
									Amount = 2,
									TargetContextKey = ContextKeys.CastingPlayerId,
								}
							),
						},
					},
				}
			),
		};

	private StartTurnAction MakeStartTurn(int playerId, bool skipDraw = false) =>
		new()
		{
			ActivePlayerId = playerId,
			BattlefieldId = _state.GetPlayerZoneId(playerId, ZoneType.Battlefield),
			SkipDraw = skipDraw,
		};

	private static GameState SetPlayerMana(GameState state, int playerId, int current, int max)
	{
		var player = state.GetPlayer(playerId);
		return state.UpdateObject(playerId, player with { CurrentMana = current, MaxMana = max });
	}
}
