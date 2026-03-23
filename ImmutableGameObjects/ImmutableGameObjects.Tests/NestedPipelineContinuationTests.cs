using System.Collections.Immutable;
using NUnit.Framework;

namespace ImmutableGameObjects.Tests;

// =====================================================================
// Additional Test Actions
// =====================================================================

/// <summary>
/// Runs after the inner pipeline completes. Records what it sees in the outer
/// pipeline context so we can verify the outer pipeline resumed correctly.
/// </summary>
public record RecordOutcomeAction : GameAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var abilityTriggeredBy = GetInput<int>("ability_triggered_by", -1);

		var outcome = new OutcomeMarker { WasExecuted = true, SeenCreatureId = abilityTriggeredBy };

		var (newState, _) = gameState.AddObject(outcome);
		return new ActionResult(newState);
	}
}

/// <summary>
/// Marker written by RecordOutcomeAction so tests can verify it ran
/// and what context it saw.
/// </summary>
public record OutcomeMarker : GameObject
{
	public bool WasExecuted { get; init; }

	/// <summary>
	/// The creature ID the outer pipeline had in context when this step ran.
	/// Should match the creature chosen in the first step.
	/// </summary>
	public int SeenCreatureId { get; init; }
}

// =====================================================================
// Tests
// =====================================================================

[TestFixture]
public class NestedPipelineContinuationTests
{
	private GameState _initialState;

	[SetUp]
	public void Setup()
	{
		_initialState = new GameState();
	}

	/// <summary>
	/// Verifies that the outer pipeline resumes and executes steps that come
	/// AFTER the step that spawned the inner pipeline.
	/// </summary>
	[Test]
	public void OuterPipeline_ResumesAfterInnerPipelineCompletes()
	{
		// Outer pipeline: ChooseCreature -> UseCreatureAbility (spawns inner) -> RecordOutcome
		var outerPipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new ChooseCreatureAction(),
				new UseCreatureAbilityAction(),
				new RecordOutcomeAction() // this step must run AFTER inner pipeline completes
			),
		};

		var state = _initialState.AddAction(outerPipeline).ProcessAllActions();

		// Paused at creature choice
		Assert.That(state.IsWaitingForChoice, Is.True);
		Assert.That(state.GetPendingChoice(), Is.TypeOf<ChooseCreatureAction>());

		// Choose creature A (ID: 10)
		state = state.ResolveChoice(ImmutableList.Create(10));

		// Should now be paused at inner pipeline's target choice
		Assert.That(state.IsWaitingForChoice, Is.True);
		Assert.That(
			state.GetPendingChoice(),
			Is.TypeOf<ChooseTargetAction>(),
			"Should be waiting for inner pipeline's target choice, not outer pipeline's next step"
		);

		// OutcomeMarker should NOT exist yet - outer pipeline hasn't resumed
		var earlyMarker = state.GetObjectOfType<OutcomeMarker>();
		Assert.That(
			earlyMarker,
			Is.Null,
			"RecordOutcomeAction should not have run before inner pipeline completes"
		);

		// Choose target A (ID: 100)
		state = state.ResolveChoice(ImmutableList.Create(100));

		// Everything should now be complete
		Assert.That(state.HasPendingActions, Is.False);
		Assert.That(state.IsWaitingForChoice, Is.False);

		// OutcomeMarker should now exist
		var outcomeMarker = state.GetObjectOfType<OutcomeMarker>();
		Assert.That(
			outcomeMarker,
			Is.Not.Null,
			"RecordOutcomeAction should have run after inner pipeline completed"
		);
		Assert.That(outcomeMarker!.WasExecuted, Is.True);
	}

	/// <summary>
	/// Verifies that the outer pipeline context is still intact when it resumes
	/// after the inner pipeline completes. RecordOutcomeAction should be able to
	/// read values written by earlier outer pipeline steps.
	/// </summary>
	[Test]
	public void OuterPipeline_ContextIntact_WhenResumingAfterInnerPipeline()
	{
		var outerPipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new ChooseCreatureAction(),
				new UseCreatureAbilityAction(),
				new RecordOutcomeAction()
			),
		};

		var state = _initialState.AddAction(outerPipeline).ProcessAllActions();

		// Choose Creature B (ID: 20)
		state = state.ResolveChoice(ImmutableList.Create(20));

		// Choose Target B (ID: 200)
		state = state.ResolveChoice(ImmutableList.Create(200));

		// RecordOutcomeAction reads "ability_triggered_by" which was written
		// by UseCreatureAbilityAction in the outer pipeline context.
		// It should be 20 (the creature we chose).
		var outcomeMarker = state.GetObjectOfType<OutcomeMarker>();
		Assert.That(outcomeMarker, Is.Not.Null);
		Assert.That(
			outcomeMarker!.SeenCreatureId,
			Is.EqualTo(20),
			"Outer pipeline context should still contain 'ability_triggered_by' when RecordOutcomeAction runs"
		);
	}

	/// <summary>
	/// Verifies both the DamageMarker (from inner pipeline) and OutcomeMarker
	/// (from outer pipeline) exist in final state, confirming both pipelines
	/// fully executed.
	/// </summary>
	[Test]
	public void BothPipelines_FullyExecute_AndBothMarkersExistInFinalState()
	{
		var outerPipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new ChooseCreatureAction(),
				new UseCreatureAbilityAction(),
				new RecordOutcomeAction()
			),
		};

		var state = _initialState.AddAction(outerPipeline).ProcessAllActions();

		state = state.ResolveChoice(ImmutableList.Create(10)); // creature choice
		state = state.ResolveChoice(ImmutableList.Create(100)); // target choice

		var damageMarker = state.GetObjectOfType<DamageMarker>();
		var outcomeMarker = state.GetObjectOfType<OutcomeMarker>();

		Assert.That(damageMarker, Is.Not.Null, "Inner pipeline should have written a DamageMarker");
		Assert.That(
			outcomeMarker,
			Is.Not.Null,
			"Outer pipeline should have written an OutcomeMarker"
		);

		// Verify correct values in both
		Assert.That(damageMarker!.SourceCreatureId, Is.EqualTo(10));
		Assert.That(damageMarker.TargetId, Is.EqualTo(100));
		Assert.That(outcomeMarker!.SeenCreatureId, Is.EqualTo(10));
	}
}
