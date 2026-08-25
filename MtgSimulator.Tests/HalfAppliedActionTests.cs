using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// An action must be FULLY applied before the search scores the position it leads to.
///
/// ProcessAllActions stops at a pending ChoiceAction, so ExecuteAction used to hand back states
/// that were half-resolved: the action had executed, its triggers had not, and the active player
/// had not changed. The rollout then read such a state, saw it was still our turn, and ended the
/// turn a SECOND time — firing every end-of-turn trigger twice.
///
/// On a board with Avaricious Dragon (discard 1 when your turn ends) plus a free equip, that made
/// a pointless equip outscore ending the turn 74.10 to 72.70, and the engine needed a per-turn cap
/// on equips to contain the resulting loop.
///
/// **It presented as a horizon effect** — the answer inverted with search depth, which is exactly
/// what a horizon effect looks like — and three separate diagnoses were built and discarded on
/// that reading before anyone printed IsWaitingForChoice. Only actions that raise a choice were
/// affected, which is also why it looked card-specific.
/// </summary>
[TestFixture]
public class HalfAppliedActionTests
{
	private static (GameState State, MtgGameIds Ids) BootsBoard()
	{
		var (state, ids) = MtgGameFactory.Create();
		state = state.WithoutDeckingLoss();

		for (var i = 0; i < 40; i++)
			foreach (
				var (pid, lib) in new[]
				{
					(ids.Player1Id, ids.Player1LibraryId),
					(ids.Player2Id, ids.Player2LibraryId),
				}
			)
				(state, _) = state.AddObject(
					new Card
					{
						Name = "F",
						ManaCost = 99,
						OwnerId = pid,
						ControllerId = pid,
					},
					parentId: lib
				);

		foreach (var name in new[] { "Avaricious Dragon", "Chasm Skulker", "Sublime Archangel" })
		{
			var card = CoresetCube.Cards.First(x => x.Name == name);
			var cc = card.GetComponent<CreatureComponent>()!;
			card = (Card)card.WithComponentReplaced(cc with { HasSummoningSickness = false });
			(state, _) = state.AddObject(
				card with
				{
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
				},
				parentId: ids.Player1BattlefieldId
			);
		}

		var boots = CoresetCube.Cards.First(x => x.Name == "Swiftfoot Boots");
		(state, _) = state.AddObject(
			boots with
			{
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
			},
			parentId: ids.Player1BattlefieldId
		);

		// Spend every attack. The interesting position is a turn with nothing left to do but the
		// free equip — which is when deferring the end-of-turn discard is the only thing on offer.
		for (var i = 0; i < 12; i++)
		{
			var attack = MtgActionGenerator
				.GetLegalActions(state, ids, ids.Player1Id)
				.OfType<AttackAction>()
				.FirstOrDefault();
			if (attack == null)
				break;
			(state, _) = state.AddAction(attack).ProcessAllActions();
		}

		return (state, ids);
	}

	private static (float Equip, float EndTurn) Scores(bool resolveChoicesOnExecute)
	{
		var (state, ids) = BootsBoard();
		var ai = new MultiTurnBeamSearchAiStrategy(
			ids,
			rng: new Random(7),
			captureDecisions: true,
			// Keywords OFF. This fixture is about choice resolution, not about what equipment is
			// worth: with the keyword term live, attaching the boots to a bare creature is a real
			// +4.67 of hexproof and SHOULD outscore ending the turn, which would mask the property
			// under test. Pinning against a fixed evaluator keeps the two concerns separable.
			evaluator: WeightedStateEvaluator.Default with
			{
				KeywordWeight = 0f,
			},
			resolveChoicesOnExecute: resolveChoicesOnExecute
		);
		ai.SelectAction(state, ids, ids.Player1Id);

		var candidates = ai.LastDecision!.Candidates;
		return (
			candidates.First(c => c.Description.Contains("Boots")).Score,
			candidates.First(c => c.Description.Contains("End Turn")).Score
		);
	}

	[Test]
	public void AFreeEquip_DoesNotOutscoreEndingTheTurn()
	{
		var (equip, endTurn) = Scores(resolveChoicesOnExecute: true);

		Assert.That(
			equip,
			Is.EqualTo(endTurn).Within(0.001f),
			$"a free equip that changes nothing must not beat ending the turn "
				+ $"(equip {equip:F2}, end turn {endTurn:F2})"
		);
	}

	/// <summary>
	/// The gate on the gate. Without this, the test above would pass just as happily on a board
	/// where the bug never applied, and would tell us nothing.
	/// </summary>
	[Test]
	public void TheBug_IsReproducibleWhenTheFixIsDisabled()
	{
		var (equip, endTurn) = Scores(resolveChoicesOnExecute: false);

		Assert.That(
			equip,
			Is.GreaterThan(endTurn),
			"with choices left unresolved the equip should win, or this fixture is not "
				+ "exercising the defect it was written for"
		);
	}
}
