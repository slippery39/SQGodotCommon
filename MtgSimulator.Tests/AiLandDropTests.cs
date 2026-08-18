using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// Reported: the AI sometimes skipped its early land drops.
///
/// Playing a land scored +2.0 for the mana and -1.4 for the card leaving hand — a net +0.6,
/// small enough that the beam would sometimes prefer a different line and simply not play a land
/// that turn. Skipping an early land drop is close to the worst play available, so the margin
/// needs to be decisive rather than marginal.
///
/// The evaluator is scored directly rather than run through a full beam search: the search is
/// stochastic and time-budgeted, and a test that has to win a race is a test that flakes.
/// </summary>
[TestFixture]
public class AiLandDropTests
{
	[Test]
	public void PlayingALand_ImprovesTheEvaluationDecisively()
	{
		var (state, ids) = MtgGameFactory.Create();

		var handId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Hand);
		(state, var land) = state.AddObject(MakeLand(ids.Player1Id), parentId: handId);

		var before = StateEvaluator.Evaluate(state, ids, ids.Player1Id);

		var (after, _) = state
			.AddAction(new PlayLandAction { CardId = land.Id, CastingPlayerId = ids.Player1Id })
			.ProcessAllActions();

		var gain = StateEvaluator.Evaluate(after, ids, ids.Player1Id) - before;

		Assert.That(
			gain,
			Is.GreaterThanOrEqualTo(2.0f),
			"A land drop must be worth its full mana, not mana minus a card-in-hand penalty"
		);
	}

	/// <summary>
	/// The other half of the same rule: holding a land must not look like holding a spell, or the
	/// AI is rewarded for sandbagging.
	/// </summary>
	[Test]
	public void ALandInHand_IsNotCountedAsACardInHand()
	{
		var (baseState, ids) = MtgGameFactory.Create();
		var handId = baseState.GetPlayerZoneId(ids.Player1Id, ZoneType.Hand);

		var (withLand, _) = baseState.AddObject(MakeLand(ids.Player1Id), parentId: handId);
		var (withSpell, _) = baseState.AddObject(MakeSpell(ids.Player1Id), parentId: handId);

		var baseline = StateEvaluator.Evaluate(baseState, ids, ids.Player1Id);

		Assert.Multiple(() =>
		{
			Assert.That(
				StateEvaluator.Evaluate(withLand, ids, ids.Player1Id),
				Is.EqualTo(baseline).Within(0.001f),
				"A land sitting in hand is a resource not yet deployed, not an option"
			);
			Assert.That(
				StateEvaluator.Evaluate(withSpell, ids, ids.Player1Id),
				Is.GreaterThan(baseline),
				"A spell in hand still counts"
			);
		});
	}

	private static Card MakeLand(int ownerId) =>
		new()
		{
			Name = "Test Land",
			OwnerId = ownerId,
			ControllerId = ownerId,
			Types = CardType.Land,
			Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Land", "Basic"),
		};

	private static Card MakeSpell(int ownerId) =>
		new()
		{
			Name = "Test Spell",
			ManaCost = 2,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Types = CardType.Sorcery,
			Components = ImmutableArray.Create<GameComponent>(new SpellComponent()),
		};
}
