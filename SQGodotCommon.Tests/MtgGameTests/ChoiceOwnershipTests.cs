using System.Collections.Immutable;
using System.Linq;
using ImmutableGameObjects;
using MtgCore;
using MtgGame;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

/// <summary>
/// Who answers a pending choice is decided by the choice's OWNER, never by whose turn it is.
///
/// Both front ends used to infer the answerer from the active player, and that is a different
/// question: a triggered ability fires whenever its condition is met, which is routinely on the
/// opponent's turn. The two directions failed independently, so both are pinned here —
/// an opponent's choice raised on your turn must not be shown to you, and yours raised on their
/// turn must not be silently answered by the AI.
///
/// The second direction is the one that constrains card design. A death trigger that scries
/// (Shadows of the Past, Return to the Winds) resolves on whichever turn the creature died, and
/// that is usually the opponent's — so "the AI takes any choice pending during its own turn" ate
/// the player's decision on exactly the cards built around dying.
/// </summary>
[TestFixture]
public class ChoiceOwnershipTests
{
	private static MtgGameManager NewManager()
	{
		var filler = CoresetCube.Cards.Take(40).ToList();
		var manager = new MtgGameManager(
			new DeckSetupData(new DeckChoice.Drafted(filler), new DeckChoice.Drafted(filler))
		);
		manager.StartGame();
		return manager;
	}

	/// <summary>Pauses the game on a discard choice belonging to the given player.</summary>
	private static void PauseOnDiscardChoiceFor(MtgGameManager manager, int playerId)
	{
		var (state, _) = manager
			.State.AddAction(
				new PipelineAction
				{
					Steps = ImmutableList.Create<GameAction>(
						new SelectCardsFromHandAction
						{
							Prompt = "Discard a card",
							PlayerId = playerId,
							MinChoices = 1,
							MaxChoices = 1,
							OutputKey = ContextKeys.SelectedCardIds,
						}
					),
				}
			)
			.ProcessAllActions();

		manager.DebugSetState(state);
	}

	private static void MakeItTheAisTurn(MtgGameManager manager)
	{
		var game = manager.State.TryGetGame()!;
		manager.DebugSetState(
			manager.State.UpdateObject(game.Id, game with { ActivePlayerId = manager.AiPlayerId })
		);
	}

	[Test]
	public void HumanChoice_OnTheAisTurn_StaysWithTheHuman()
	{
		var manager = NewManager();
		MakeItTheAisTurn(manager);
		PauseOnDiscardChoiceFor(manager, manager.HumanPlayerId);

		Assert.Multiple(() =>
		{
			Assert.That(manager.IsAiTurn, Is.True, "precondition: it is the opponent's turn");
			Assert.That(
				manager.IsHumanChoice,
				Is.True,
				"it is the human's hand, so the human answers it even on the opponent's turn"
			);
			Assert.That(
				manager.IsWaitingForChoice,
				Is.True,
				"and it must still be pending — nothing may resolve it on the human's behalf"
			);
		});
	}

	[Test]
	public void AiChoice_OnTheHumansTurn_IsNotShownToTheHuman()
	{
		var manager = NewManager();
		PauseOnDiscardChoiceFor(manager, manager.AiPlayerId);

		Assert.Multiple(() =>
		{
			Assert.That(manager.IsAiTurn, Is.False, "precondition: it is the human's turn");
			Assert.That(
				manager.IsHumanChoice,
				Is.False,
				"it is the opponent's hand — prompting the human to discard for them was the bug"
			);
		});
	}

	/// <summary>
	/// The flip side of owner-gating the UI: an opponent-owned choice raised during the human's
	/// turn has no panel to clear it, so something must. Without the drain it sits on the action
	/// stack and wedges the game silently.
	/// </summary>
	[Test]
	public void AiChoice_RaisedOnTheHumansTurn_IsDrainedSoTheStackNeverWedges()
	{
		var manager = NewManager();
		PauseOnDiscardChoiceFor(manager, manager.AiPlayerId);
		Assert.That(manager.IsWaitingForChoice, Is.True, "precondition: paused on their choice");

		// Any human-driven state change routes through SubmitAction, which drains.
		manager.EndTurn();

		Assert.That(
			manager.IsWaitingForChoice,
			Is.False,
			"the opponent's choice must have been answered for them, not left blocking the stack"
		);
	}
}
