using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// "Discard a land card" is a real cost that the evaluator prices at ZERO.
///
/// Raised by Molten Vortex sitting near the bottom of the trained model despite reading well —
/// it converts flood into reach, which ought to be good. Raising its damage from 2 to 3 was the
/// first guess; these tests were written to check the alternative, that the AI plays it badly.
///
/// It does, and the mechanism is exact. StateEvaluator deliberately counts only NON-land cards in
/// hand (see AiLandDropTests for why — counting them made the AI skip land drops). The
/// consequence nobody had followed through: a cost that discards a LAND is therefore free, while
/// the same card played is worth a permanent +2.0 of MaxMana. Measured:
///
///   discard a land   +0.000     <- what Vortex charges
///   play that land   +2.000     <- what it was worth
///   3 damage to face +0.600     <- what Vortex pays
///
/// So the AI sees a free 0.6 where the real trade is 2.0 for 0.6, and takes it every time it has
/// a land to spare. The 2-turn rollout is the only thing pushing back, which is why it keeps
/// exactly one land in reserve and throws away every drop beyond that.
///
/// THESE TESTS CHARACTERISE CURRENT BEHAVIOUR — they pass today, and they are the diagnosis
/// rather than the fix. If the last one starts FAILING, the underlying pricing was fixed and this
/// fixture should be re-pointed at the new behaviour rather than "repaired".
///
/// The fix is a StateEvaluator change (a small non-zero weight for lands in hand) and is
/// deliberately NOT made here: that weight is the exact thing AiLandDropTests pins from the other
/// side, so it needs a head-to-head strength measurement, not an intuition.
/// </summary>
[TestFixture]
public class LandDiscardCostTests
{
	// ===== THE PRICING =====

	[Test]
	public void DiscardingALand_IsInvisibleToTheEvaluator()
	{
		var (state, ids) = MtgGameFactory.Create();
		var handId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Hand);
		(state, var land) = state.AddObject(MakeLand(ids.Player1Id), parentId: handId);

		var before = StateEvaluator.Evaluate(state, ids, ids.Player1Id);

		var graveyard = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Graveyard);
		var after = state.MoveCardTracked(land.Id, graveyard);

		Assert.That(
			StateEvaluator.Evaluate(after, ids, ids.Player1Id) - before,
			Is.EqualTo(0f),
			"a discarded land costs the evaluator nothing — this is the whole defect"
		);
	}

	/// <summary>
	/// The asymmetry that makes the trade bad. Both numbers are correct on their own; it is
	/// pricing the COST at zero while the same card is worth 2.0 that produces the bad play.
	/// </summary>
	[Test]
	public void TheLandThrownAway_IsWorthMoreThanTheDamageItBuys()
	{
		var (state, ids) = MtgGameFactory.Create();
		var handId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Hand);
		(state, var land) = state.AddObject(MakeLand(ids.Player1Id), parentId: handId);

		var before = StateEvaluator.Evaluate(state, ids, ids.Player1Id);

		var (played, _) = state
			.AddAction(new PlayLandAction { CardId = land.Id, CastingPlayerId = ids.Player1Id })
			.ProcessAllActions();
		var landValue = StateEvaluator.Evaluate(played, ids, ids.Player1Id) - before;

		var (damaged, _) = state
			.AddAction(
				new DealDamageAction { Amount = 3, TargetIds = ImmutableList.Create(ids.Player2Id) }
			)
			.ProcessAllActions();
		var damageValue = StateEvaluator.Evaluate(damaged, ids, ids.Player1Id) - before;

		Assert.That(
			landValue,
			Is.GreaterThan(damageValue * 2),
			$"land {landValue} vs 3 damage {damageValue} — Vortex trades the bigger for the smaller"
		);
	}

	// ===== THE CONSEQUENCE =====

	/// <summary>
	/// The behaviour that pricing produces. It holds exactly one land back — the 2-turn rollout
	/// can see that far and no further — and vents every land beyond it, at any mana total.
	///
	/// A 40-card drafted deck runs 17 lands and wants drops through roughly turn six, so these are
	/// not surplus lands being converted; they are turn-three-onward drops being burned for 3
	/// damage each.
	/// </summary>
	[TestCase(1, 3, TestName = "VentsASpareLand_AtOneMana")]
	[TestCase(2, 4, TestName = "VentsASpareLand_AtTwoMana")]
	public void TheAi_VentsAwayFutureLandDrops_WheneverItHoldsASpare(int maxMana, int landsInHand)
	{
		var picks = PlayOneTurn(maxMana, landsInHand);

		Assert.That(
			picks,
			Does.Contain("Vent"),
			$"expected the AI to discard a land it still needs: {string.Join(" ", picks)}"
		);
	}

	/// <summary>
	/// The other side of the boundary, and the reason this reads as a horizon problem rather than
	/// simple recklessness: with only one land left the rollout DOES see the missed drop, and the
	/// AI correctly declines. Everything beyond the 2-turn lookahead is free.
	/// </summary>
	[TestCase(1, 2, TestName = "KeepsItsLastLand_AtOneMana")]
	[TestCase(3, 2, TestName = "KeepsItsLastLand_AtThreeMana")]
	public void TheAi_KeepsTheLandItCanStillSeeItNeeding(int maxMana, int landsInHand)
	{
		var picks = PlayOneTurn(maxMana, landsInHand);

		Assert.That(picks, Does.Not.Contain("Vent"));
	}

	// ===== helpers =====

	private static List<string> PlayOneTurn(int maxMana, int landsInHand)
	{
		var (state, ids) = MtgGameFactory.Create();
		state = state.WithoutDeckingLoss();
		var ai = new MultiTurnBeamSearchAiStrategy(ids, rng: new Random(11));

		// Rollouts draw; without filler the decking rule decides the game instead.
		for (var i = 0; i < 40; i++)
		{
			(state, _) = state.AddObject(
				Filler("F1", ids.Player1Id),
				parentId: ids.Player1LibraryId
			);
			(state, _) = state.AddObject(
				Filler("F2", ids.Player2Id),
				parentId: ids.Player2LibraryId
			);
		}

		var player = state.GetPlayer(ids.Player1Id);
		state = state.UpdateObject(
			ids.Player1Id,
			player with
			{
				MaxMana = maxMana,
				CurrentMana = maxMana,
			}
		);

		var handId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Hand);
		for (var i = 0; i < landsInHand; i++)
			(state, _) = state.AddObject(MakeLand(ids.Player1Id), parentId: handId);

		var vortex = CoresetCube.Cards.First(c => c.Name == "Molten Vortex");
		(state, _) = state.AddObject(
			vortex with
			{
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
			},
			parentId: ids.Player1BattlefieldId
		);

		var picks = new List<string>();
		for (var step = 0; step < 20; step++)
		{
			var chosen = ai.SelectAction(state, ids, ids.Player1Id);
			picks.Add(
				chosen switch
				{
					ActivateAbilityAction => "Vent",
					PlayLandAction => "PlayLand",
					_ => chosen.GetType().Name.Replace("Action", ""),
				}
			);

			if (chosen is EndTurnAction)
				break;

			(state, _) = state.AddAction(chosen).ProcessAllActions();
		}

		return picks;
	}

	private static Card MakeLand(int ownerId) =>
		new()
		{
			Name = "Plains",
			OwnerId = ownerId,
			ControllerId = ownerId,
			Subtypes = ["Land", "Basic"],
		};

	private static Card Filler(string name, int ownerId) =>
		new()
		{
			Name = name,
			ManaCost = 99,
			OwnerId = ownerId,
			ControllerId = ownerId,
		};
}
