using System.Collections.Immutable;
using NUnit.Framework;

namespace ImmutableGameObjects.Tests;

// =====================================================================
// Test Events
// =====================================================================

public record DamageDealtEvent : GameEvent
{
	public int TargetId { get; init; }
	public int Amount { get; init; }
}

public record HealingAppliedEvent : GameEvent
{
	public int TargetId { get; init; }
	public int Amount { get; init; }
}

// =====================================================================
// Test Actions
// =====================================================================

/// <summary>
/// Emits a DamageDealtEvent when executed.
/// </summary>
public record DealDamageAction : GameAction
{
	public int TargetId { get; init; }
	public int Amount { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		return new ActionResult(gameState).WithEvent(
			new DamageDealtEvent { TargetId = TargetId, Amount = Amount }
		);
	}
}

/// <summary>
/// Emits a HealingAppliedEvent when executed.
/// </summary>
public record ApplyHealingAction : GameAction
{
	public int TargetId { get; init; }
	public int Amount { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		return new ActionResult(gameState).WithEvent(
			new HealingAppliedEvent { TargetId = TargetId, Amount = Amount }
		);
	}
}

/// <summary>
/// Emits multiple events when executed.
/// </summary>
public record MultiEventAction : GameAction
{
	public override ActionResult Execute(GameState gameState)
	{
		return new ActionResult(gameState)
			.WithEvent(new DamageDealtEvent { TargetId = 1, Amount = 5 })
			.WithEvent(new DamageDealtEvent { TargetId = 2, Amount = 3 })
			.WithEvent(new HealingAppliedEvent { TargetId = 1, Amount = 2 });
	}
}

// =====================================================================
// Tests
// =====================================================================

[TestFixture]
public class EventSystemTests
{
	private GameState _initialState;

	[SetUp]
	public void Setup()
	{
		_initialState = new GameState();
	}

	/// <summary>
	/// Verifies that a single action's events are returned by ProcessNextAction.
	/// </summary>
	[Test]
	public void ProcessNextAction_ReturnsEventsFromAction()
	{
		var state = _initialState.AddAction(new DealDamageAction { TargetId = 42, Amount = 5 });

		var (_, events) = state.ProcessNextAction();

		Assert.That(events, Has.Count.EqualTo(1));
		var damageEvent = events[0] as DamageDealtEvent;
		Assert.That(damageEvent, Is.Not.Null);
		Assert.That(damageEvent!.TargetId, Is.EqualTo(42));
		Assert.That(damageEvent.Amount, Is.EqualTo(5));
	}

	/// <summary>
	/// Verifies that ProcessAllActions accumulates events across all steps.
	/// </summary>
	[Test]
	public void ProcessAllActions_AccumulatesEventsAcrossAllSteps()
	{
		var state = _initialState.AddActions(
			new GameAction[]
			{
				new DealDamageAction { TargetId = 1, Amount = 3 },
				new ApplyHealingAction { TargetId = 2, Amount = 5 },
				new DealDamageAction { TargetId = 3, Amount = 7 },
			}
		);

		var (_, events) = state.ProcessAllActions();

		Assert.That(events, Has.Count.EqualTo(3));
		Assert.That(events.OfType<DamageDealtEvent>().Count(), Is.EqualTo(2));
		Assert.That(events.OfType<HealingAppliedEvent>().Count(), Is.EqualTo(1));
	}

	/// <summary>
	/// Verifies that events from multiple pipeline steps are all collected.
	/// </summary>
	[Test]
	public void ProcessAllActions_CollectsEventsFromPipelineSteps()
	{
		var pipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new DealDamageAction { TargetId = 1, Amount = 3 },
				new ApplyHealingAction { TargetId = 1, Amount = 2 },
				new DealDamageAction { TargetId = 2, Amount = 5 }
			),
		};

		var (_, events) = _initialState.AddAction(pipeline).ProcessAllActions();

		Assert.That(events, Has.Count.EqualTo(3));
		Assert.That(events.OfType<DamageDealtEvent>().Count(), Is.EqualTo(2));
		Assert.That(events.OfType<HealingAppliedEvent>().Count(), Is.EqualTo(1));
	}

	/// <summary>
	/// Verifies that an action emitting multiple events returns them all.
	/// </summary>
	[Test]
	public void ProcessNextAction_ReturnsMultipleEventsFromSingleAction()
	{
		var state = _initialState.AddAction(new MultiEventAction());

		var (_, events) = state.ProcessNextAction();

		Assert.That(events, Has.Count.EqualTo(3));
		Assert.That(events.OfType<DamageDealtEvent>().Count(), Is.EqualTo(2));
		Assert.That(events.OfType<HealingAppliedEvent>().Count(), Is.EqualTo(1));
	}

	/// <summary>
	/// Verifies that events are NOT stored in GameState — they only exist
	/// in the returned tuple.
	/// </summary>
	[Test]
	public void GameState_DoesNotStoreEvents()
	{
		var state = _initialState.AddAction(new DealDamageAction { TargetId = 1, Amount = 5 });

		var (newState, events) = state.ProcessNextAction();

		// Events are in the tuple, not in state
		Assert.That(events, Has.Count.EqualTo(1));

		// GameState has no event storage — calling ProcessNextAction again
		// on the same state should return the same events, not accumulate them
		var (_, eventsAgain) = state.ProcessNextAction();
		Assert.That(
			eventsAgain,
			Has.Count.EqualTo(1),
			"Re-processing the same state should return the same events, not accumulate"
		);

		// And the new state itself has no pending events baked into it
		var (__, noEvents) = newState.ProcessNextAction();
		Assert.That(
			noEvents,
			Is.Empty,
			"New state has no pending actions so no events should be emitted"
		);
	}

	/// <summary>
	/// Verifies that events from steps before a choice are returned correctly,
	/// and events from steps after choice resolution are also returned.
	/// </summary>
	[Test]
	public void ResolveChoice_ReturnsEventsFromResumedExecution()
	{
		var pipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new DealDamageAction { TargetId = 1, Amount = 3 },
				new TestChoiceAction(),
				new ApplyHealingAction { TargetId = 1, Amount = 5 }
			),
		};

		// ProcessAllActions returns events from before the choice
		var (stateAtChoice, eventsBeforeChoice) = _initialState
			.AddAction(pipeline)
			.ProcessAllActions();

		Assert.That(stateAtChoice.IsWaitingForChoice, Is.True);
		Assert.That(eventsBeforeChoice, Has.Count.EqualTo(1));
		Assert.That(eventsBeforeChoice[0], Is.TypeOf<DamageDealtEvent>());

		// ResolveChoice returns events from after the choice
		var (finalState, eventsAfterChoice) = stateAtChoice.ResolveChoice(ImmutableList.Create(1));

		Assert.That(finalState.HasPendingActions, Is.False);
		Assert.That(eventsAfterChoice, Has.Count.EqualTo(1));
		Assert.That(eventsAfterChoice[0], Is.TypeOf<HealingAppliedEvent>());
	}

	/// <summary>
	/// Verifies that events from nested pipelines (spawned actions) are collected.
	/// </summary>
	[Test]
	public void ProcessAllActions_CollectsEventsFromNestedPipelines()
	{
		// An action that spawns an inner pipeline which emits its own events
		var outerPipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new DealDamageAction { TargetId = 1, Amount = 3 },
				new SpawnInnerPipelineAction()
			),
		};

		var (finalState, events) = _initialState.AddAction(outerPipeline).ProcessAllActions();

		Assert.That(finalState.HasPendingActions, Is.False);

		// Should have outer pipeline's damage event + inner pipeline's healing event
		Assert.That(events.OfType<DamageDealtEvent>().Count(), Is.EqualTo(1));
		Assert.That(events.OfType<HealingAppliedEvent>().Count(), Is.EqualTo(1));
	}

	/// <summary>
	/// Verifies that no events are returned when no actions emit any.
	/// </summary>
	[Test]
	public void ProcessAllActions_ReturnsEmptyEvents_WhenNoActionsEmitEvents()
	{
		var pipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new IncrementAction { IncrementBy = 1 },
				new IncrementAction { IncrementBy = 1 }
			),
		};

		var (_, events) = _initialState.AddAction(pipeline).ProcessAllActions();

		Assert.That(events, Is.Empty);
	}
}

/// <summary>
/// Spawns an inner pipeline that emits a HealingAppliedEvent.
/// Used to test event collection from nested pipelines.
/// </summary>
public record SpawnInnerPipelineAction : GameAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var innerPipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new ApplyHealingAction { TargetId = 1, Amount = 5 }
			),
		};

		return new ActionResult(gameState.SpawnAction(innerPipeline));
	}
}
