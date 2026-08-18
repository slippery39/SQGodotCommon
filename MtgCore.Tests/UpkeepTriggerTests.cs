using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// "At the beginning of your upkeep" must mean YOUR upkeep.
///
/// TriggerConditions.OnYourUpkeep() listened for TurnStarted with no filter at all, and
/// TurnStartedEvent's subject is the player whose turn began — so every upkeep trigger in the
/// engine fired on both players' turns, at double the printed rate. Nothing errored and nothing
/// looked wrong on the board; Phyrexian Arena-style cards simply drained twice as fast as the
/// card said, and the black section is built almost entirely out of upkeep triggers.
/// </summary>
[TestFixture]
public class UpkeepTriggerTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	private static Card MakeUpkeepDrainer(int ownerId) =>
		CardFactory
			.Creature("Upkeep Drainer", manaCost: 2, power: 1, toughness: 1)
			.WithTriggeredAbility(
				"Tick",
				TriggerConditions.OnYourUpkeep(),
				eb => eb.WithLoseLife(1)
			)
			.Build() with
		{
			OwnerId = ownerId,
			ControllerId = ownerId,
		};

	[Test]
	public void UpkeepTrigger_FiresOnItsControllersTurn()
	{
		var (withCard, _) = _state.AddObject(
			MakeUpkeepDrainer(_ids.Player1Id),
			parentId: _ids.Player1BattlefieldId
		);
		var lifeBefore = withCard.GetPlayer(_ids.Player1Id).Life;

		var (after, _) = withCard
			.AddAction(new StartTurnAction { ActivePlayerId = _ids.Player1Id, SkipDraw = true })
			.ProcessAllActions();

		Assert.That(after.GetPlayer(_ids.Player1Id).Life, Is.EqualTo(lifeBefore - 1));
	}

	[Test]
	public void UpkeepTrigger_DoesNotFireOnTheOpponentsTurn()
	{
		var (withCard, _) = _state.AddObject(
			MakeUpkeepDrainer(_ids.Player1Id),
			parentId: _ids.Player1BattlefieldId
		);
		var lifeBefore = withCard.GetPlayer(_ids.Player1Id).Life;

		var (after, _) = withCard
			.AddAction(new StartTurnAction { ActivePlayerId = _ids.Player2Id, SkipDraw = true })
			.ProcessAllActions();

		Assert.That(
			after.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(lifeBefore),
			"An upkeep trigger fired on the opponent's turn — it runs at double the printed rate"
		);
	}
}
