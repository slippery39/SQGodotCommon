using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// A free repeatable ability must not be able to spin the AI until GameRunner's 200-action limit
/// ends the game as a draw.
///
/// Swiftfoot Boots at equip {0} is the case that exposed it. Measured over 14 000 games:
/// **28.7% of games with Boots on the board drew** against a 0.7% base rate, and Boots was on the
/// battlefield in 12 of 12 sampled ActionLimitReached games.
///
/// TWO THINGS WERE WRONG, and the second was misdiagnosed twice before being measured properly:
///
/// 1. Re-equipping to the CURRENT WEARER was a perfect no-op. Fixed in the targeting spec — see
///    PermanentCardBuilder.WithEquip.
/// 2. With that closed, the AI moved the boots BETWEEN two creatures forever. This was assumed to
///    be a scoring tie that the search broke toward acting, and two AI-side tie-break fixes were
///    built on that assumption. Both were reverted: instrumenting the search shows the equip
///    scoring **74.1 against 72.7 for ending the turn**, with the move straight back also scoring
///    +1.4. The evaluator rates both directions of one oscillation as an improvement, so no
///    tie-break can catch it — there is no tie. See DesignNotes.md.
///
/// The fix is therefore an unconditional per-turn cap in the engine, which holds whatever the
/// evaluator believes. VERIFIED end to end: ActionLimitReached draws went 86 -> 0 over 7 000
/// games, with the base win rate landing on exactly 50.0%.
///
/// These tests drive the REAL AI over boards taken verbatim from flagged action-limit games,
/// because the synthetic two-identical-token board did not reproduce it and that false negative
/// is what made the wrong diagnosis look confirmed.
/// </summary>
[TestFixture]
public class FreeAbilityLoopTests
{
	private const int StepCap = 40;

	[TestCase("Avaricious Dragon", "Chasm Skulker", "Sublime Archangel")]
	[TestCase("Birds of Paradise", "Cruel Sadist", "Deadly Recluse")]
	[TestCase("Vampire Nighthawk", "Plague Mare", "Indulgent Tormentor")]
	public void FreeEquip_DoesNotSpinUntilTheActionLimit(string a, string b, string c)
	{
		var picks = RunTurn(a, b, c);

		Assert.That(
			picks.Count,
			Is.LessThan(StepCap),
			$"the AI never ended its turn — it looped on the free equip: {string.Join(" ", picks)}"
		);
		Assert.That(picks[^1], Is.EqualTo("EndTurn"));
	}

	/// <summary>
	/// The cap must bound the ability, not disable it — equipment that can never be attached is a
	/// worse bug than the loop. One equip per turn is exactly the intent.
	/// </summary>
	[Test]
	public void Equip_IsStillTakenOncePerTurn()
	{
		var picks = RunTurn("Avaricious Dragon", "Chasm Skulker", "Sublime Archangel");

		Assert.That(picks.Count(p => p.StartsWith("Eq")), Is.EqualTo(1));
	}

	private static List<string> RunTurn(params string[] creatureNames)
	{
		var (state, ids) = MtgGameFactory.Create();
		state = state.WithoutDeckingLoss();
		var ai = new MultiTurnBeamSearchAiStrategy(ids, rng: new Random(7));

		// Rollouts draw; without filler the decking rule decides the game instead.
		for (var i = 0; i < 40; i++)
		{
			(state, _) = state.AddObject(
				new Card
				{
					Name = "F1",
					ManaCost = 99,
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
				},
				parentId: ids.Player1LibraryId
			);
			(state, _) = state.AddObject(
				new Card
				{
					Name = "F2",
					ManaCost = 99,
					OwnerId = ids.Player2Id,
					ControllerId = ids.Player2Id,
				},
				parentId: ids.Player2LibraryId
			);
		}

		foreach (var name in creatureNames)
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

		var picks = new List<string>();
		for (var step = 0; step < StepCap; step++)
		{
			var chosen = ai.SelectAction(state, ids, ids.Player1Id);
			if (chosen == null)
				break;

			picks.Add(
				chosen switch
				{
					ActivateAbilityAction aa => $"Eq{aa.TargetIds.FirstOrDefault()}",
					AttackAction at => $"Atk{at.AttackerId}",
					_ => chosen.GetType().Name.Replace("Action", ""),
				}
			);

			if (chosen is EndTurnAction)
				break;

			(state, _) = state.AddAction(chosen).ProcessAllActions();
		}

		return picks;
	}
}
