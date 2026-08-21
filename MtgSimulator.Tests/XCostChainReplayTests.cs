using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// The multi-turn beam search plans a CHAIN of actions and then replays it, re-finding each
/// planned action in a freshly generated legal-action list (FindCommittedAction → ActionsMatch).
///
/// That matcher compared the two cast actions on card id ALONE, and MtgActionGenerator enumerates
/// X ascending — so a chain that planned "cast this for X=4" would re-find, and execute, the X=0
/// action. Every {X} card in the cube is exposed: Banefire and Earthquake dealing no damage, Mind
/// Spring drawing nothing, and the green Hydras arriving as 0/0s that the zero-toughness rule
/// destroys on the spot. All of those are indistinguishable from a blank card.
///
/// TESTED AT THE MATCHER, NOT THROUGH A GAME, and deliberately so. Forcing the beam to commit a
/// chain with a specific card at position ≥1 is not reliably reproducible from a unit test — the
/// first action of a chain is returned directly and never goes through the matcher at all, so a
/// game-level test passes whether or not the bug is present, which makes it worse than no test.
/// The invariant is the thing worth pinning: a planned action must never be re-found as a
/// materially different one.
/// </summary>
[TestFixture]
public class XCostChainReplayTests
{
	[Test]
	public void PlannedXValue_IsPartOfACreatureCastsIdentity()
	{
		var plannedForFour = new CastCreatureAction
		{
			CardId = 42,
			CastingPlayerId = 1,
			XValue = 4,
		};
		var sameCardForZero = plannedForFour with { XValue = 0 };

		Assert.Multiple(() =>
		{
			Assert.That(
				MultiTurnBeamSearchAiStrategy.ActionsMatch(sameCardForZero, plannedForFour),
				Is.False,
				"an X=0 cast must not satisfy a chain that planned X=4 — the Hydra arrives as a 0/0"
			);
			Assert.That(
				MultiTurnBeamSearchAiStrategy.ActionsMatch(plannedForFour, plannedForFour),
				Is.True,
				"and the planned action must still re-find itself"
			);
		});
	}

	[Test]
	public void PlannedXValue_IsPartOfASpellCastsIdentity()
	{
		var plannedForSix = new CastSpellAction
		{
			CardId = 7,
			CastingPlayerId = 1,
			XValue = 6,
		};
		var sameCardForZero = plannedForSix with { XValue = 0 };

		Assert.Multiple(() =>
		{
			Assert.That(
				MultiTurnBeamSearchAiStrategy.ActionsMatch(sameCardForZero, plannedForSix),
				Is.False,
				"Banefire for X=0 deals no damage — it is not the spell the search chose"
			);
			Assert.That(
				MultiTurnBeamSearchAiStrategy.ActionsMatch(plannedForSix, plannedForSix),
				Is.True
			);
		});
	}

	/// <summary>
	/// The generator really does offer X ascending, which is what makes "first match wins" land on
	/// X=0 specifically rather than on an arbitrary value. If this ordering ever changes the bug
	/// above changes shape, so it is worth stating.
	/// </summary>
	[Test]
	public void ActionGenerator_OffersXAscendingFromZero()
	{
		var (state, ids) = MtgGameFactory.Create();
		state = state.UpdateObject(
			ids.Player1Id,
			state.GetPlayer(ids.Player1Id) with
			{
				CurrentMana = 6,
				MaxMana = 6,
			}
		);

		var hydra = CoresetCube.Cards.First(c => c.Name == "Primordial Hydra") with
		{
			OwnerId = ids.Player1Id,
			ControllerId = ids.Player1Id,
		};
		(state, var added) = state.AddObject(hydra, parentId: ids.Player1HandId);

		var xValues = MtgActionGenerator
			.GetLegalActions(state, ids, ids.Player1Id)
			.OfType<CastCreatureAction>()
			.Where(a => a.CardId == added.Id)
			.Select(a => a.XValue)
			.ToList();

		Assert.Multiple(() =>
		{
			Assert.That(xValues, Is.Ordered, "X is enumerated ascending");
			Assert.That(xValues.First(), Is.Zero, "so the FIRST match is always X=0");
			Assert.That(
				xValues.Count(x => x > 0),
				Is.GreaterThan(0),
				"and a real choice of X must exist at all, or the Hydra is unplayable"
			);
		});
	}
}
