using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using NUnit.Framework;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore.Tests;

/// <summary>
/// **The Sanguine Bond + Exquisite Blood pair, and whether this engine can express it as a WIN.**
///
/// The two halves feed each other: you gain life, so an opponent loses that much, so you gain that
/// much. That is the exact shape <see cref="GameState.MaxActionsPerResolution"/> exists to catch —
/// its message reads "a trigger that produces the event it triggers on" — and a resolution that
/// throws becomes <c>GameEndReason.UnhandledException</c>, which every trainer EXCLUDES. A combo
/// that throws is invisible, not dominant.
///
/// The question is therefore whether the loop terminates on its own before the cap: the opponent
/// starts at 20 and loses at least 1 per iteration, so if the loss check stops the cascade it is a
/// win in ~20 iterations, far under 10 000.
///
/// Cards are inline so card balance changes cannot break these.
/// </summary>
[TestFixture]
public class LifeDrainLoopTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup() => (_state, _ids) = MtgGameFactory.CreateForTesting();

	/// "Whenever you gain life, target opponent loses that much life."
	private static Card SanguineBond() =>
		CardFactory
			.Enchantment("Sanguine Bond", manaCost: 2)
			.WithComponent(
				new TriggeredAbilityComponent
				{
					Name = "Bond",
					Condition = TriggerConditions.OnGainLife(),
					Effect = new CardEffect
					{
						// **Random(), not Single().** A trigger has no cast-time targeting step, so
						// a UserSelect strategy resolves to an empty list and the effect silently
						// hits nobody — the same failure CLAUDE.md records for modes. There is one
						// opponent, so Random is deterministic here.
						TargetingStrategy = Random().Opponent(),
						ActionTemplate = new LoseLifeAction
						{
							AmountContextKey = ContextKeys.TriggerAmount,
						},
					},
				}
			)
			.Build();

	/// "Whenever an opponent loses life, you gain that much life."
	private static Card ExquisiteBlood() =>
		CardFactory
			.Enchantment("Exquisite Blood", manaCost: 2)
			.WithComponent(
				new TriggeredAbilityComponent
				{
					Name = "Blood",
					Condition = new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.PlayerLostLife,
						Filter = new IsControlledByOpponentSpecification(),
					},
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.NoTarget(),
						ActionTemplate = new GainLifeAction
						{
							AmountContextKey = ContextKeys.TriggerAmount,
							TargetContextKey = ContextKeys.CastingPlayerId,
						},
					},
				}
			)
			.Build();

	/// <summary>
	/// Each half alone must be a plain, terminating trigger — otherwise a failure in the pair test
	/// says nothing about the interaction.
	/// </summary>
	[Test]
	public void EitherHalfAlone_ResolvesOnceAndStops()
	{
		var bondOnly = Play(_state, SanguineBond());
		var afterBond = GainLife(bondOnly, 3);

		var bloodOnly = Play(_state, ExquisiteBlood());
		var afterBlood = GainLife(bloodOnly, 3);

		Assert.Multiple(() =>
		{
			Assert.That(Life(afterBond, _ids.Player2Id), Is.EqualTo(17), "Bond drained 3");
			Assert.That(Life(afterBond, _ids.Player1Id), Is.EqualTo(23), "and gained 3");
			Assert.That(
				Life(afterBlood, _ids.Player2Id),
				Is.EqualTo(20),
				"Blood alone does nothing off your own life gain"
			);
		});
	}

	/// <summary>
	/// **The combo.** Assembling both and gaining any life at all should drain the opponent out.
	/// </summary>
	[Test]
	public void BothHalvesTogether_DrainTheOpponentToZero()
	{
		var assembled = Play(Play(_state, SanguineBond()), ExquisiteBlood());

		var after = GainLife(assembled, 1);

		TestContext.Out.WriteLine(
			$"opponent {Life(after, _ids.Player2Id)}, you {Life(after, _ids.Player1Id)}, "
				+ $"opponent lost: {((MtgPlayer)after.GetObject(_ids.Player2Id)).HasLost}"
		);

		Assert.Multiple(() =>
		{
			Assert.That(
				Life(after, _ids.Player2Id),
				Is.LessThanOrEqualTo(0),
				"opponent drained out"
			);
			Assert.That(
				((MtgPlayer)after.GetObject(_ids.Player2Id)).HasLost,
				Is.True,
				"and the state-based check recorded the loss"
			);
		});
	}

	// ===== helpers =====

	private static int Life(GameState state, int playerId) =>
		((MtgPlayer)state.GetObject(playerId)).Life;

	private GameState Play(GameState state, Card card)
	{
		var (next, _) = state.AddObject(
			card with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: _ids.Player1BattlefieldId
		);
		return next;
	}

	private GameState GainLife(GameState state, int amount)
	{
		var (next, _) = state
			.AddAction(
				new GainLifeAction
				{
					Amount = amount,
					TargetIds = ImmutableList.Create(_ids.Player1Id),
				}
			)
			.ProcessAllActions();
		return next;
	}
}
