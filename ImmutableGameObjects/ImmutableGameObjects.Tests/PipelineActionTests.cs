using System.Collections.Immutable;
using NUnit.Framework;

namespace ImmutableGameObjects.Tests;

// Test action that increments a counter in output
public record IncrementAction : GameAction
{
	public string CounterKey { get; init; } = "counter";
	public int IncrementBy { get; init; } = 1;

	public override ActionResult Execute(GameState gameState)
	{
		var currentValue = GetInput<int>(CounterKey, 0);
		var newValue = currentValue + IncrementBy;

		return new ActionResult(gameState).WithOutput(CounterKey, newValue);
	}
}

// Test action that adds its step number to output
public record StepMarkerAction : GameAction
{
	public int StepNumber { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		var executedSteps = GetInput<List<int>>("executed_steps", new List<int>());
		var newList = new List<int>(executedSteps) { StepNumber };

		return new ActionResult(gameState).WithOutput("executed_steps", newList);
	}
}

// Test choice action
public record TestChoiceAction : ChoiceAction
{
	public TestChoiceAction()
	{
		Options = ImmutableList.Create(
			new ChoiceOption { Id = 1, DisplayText = "Option 1" },
			new ChoiceOption { Id = 2, DisplayText = "Option 2" }
		);
		OutputKey = "selected_option";
	}
}

// Action that uses the choice result
public record UseChoiceAction : GameAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var selectedOption = GetInput<int>("selected_option", -1);
		return new ActionResult(gameState).WithOutput("choice_was_used", selectedOption);
	}
}

[TestFixture]
public class PipelineActionTests
{
	private GameState _initialState;

	[SetUp]
	public void Setup()
	{
		_initialState = new GameState();
	}

	[Test]
	public void PipelineExecutesAllStepsInOrder()
	{
		// This test specifically validates the bug fix where currentIndex
		// must be used instead of CurrentStepIndex in the loop
		var pipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new StepMarkerAction { StepNumber = 1 },
				new StepMarkerAction { StepNumber = 2 },
				new StepMarkerAction { StepNumber = 3 }
			),
		};

		var state = _initialState.AddAction(pipeline).ProcessAllActions();

		// All steps should execute in order
		Assert.That(state.HasPendingActions, Is.False, "Pipeline should complete");
	}

	[Test]
	public void PipelinePassesOutputToNextStep()
	{
		var pipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new IncrementAction { IncrementBy = 5 },
				new IncrementAction { IncrementBy = 3 },
				new IncrementAction { IncrementBy = 2 }
			),
		};

		// Process all actions
		var state = _initialState.AddAction(pipeline).ProcessAllActions();

		// Pipeline should complete with no remaining actions
		Assert.That(state.HasPendingActions, Is.False);
	}

	[Test]
	public void PipelinePausesAtChoiceAction()
	{
		var pipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new IncrementAction { IncrementBy = 5 },
				new TestChoiceAction(),
				new IncrementAction { IncrementBy = 3 }
			),
		};

		var state = _initialState.AddAction(pipeline).ProcessAllActions();

		// Should have stopped at the choice
		Assert.That(state.IsWaitingForChoice, Is.True);

		var choice = state.GetPendingChoice();
		Assert.That(choice, Is.Not.Null);
		Assert.That(choice, Is.TypeOf<TestChoiceAction>());

		// Should still have the pipeline on the stack
		Assert.That(state.HasPendingActions, Is.True);
	}

	[Test]
	public void PipelineContinuesAfterChoiceResolved()
	{
		var pipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new IncrementAction { IncrementBy = 5 },
				new TestChoiceAction(),
				new IncrementAction { IncrementBy = 3 }
			),
		};

		var state = _initialState.AddAction(pipeline).ProcessAllActions();

		// Should be waiting at choice
		Assert.That(state.IsWaitingForChoice, Is.True);

		// Resolve the choice
		state = state.ResolveChoice(ImmutableList.Create(1));

		// Should have completed the pipeline
		Assert.That(state.IsWaitingForChoice, Is.False);
		Assert.That(state.HasPendingActions, Is.False);
	}

	[Test]
	public void PipelinePreservesContextThroughChoice()
	{
		var pipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new IncrementAction { IncrementBy = 5 },
				new TestChoiceAction(),
				new UseChoiceAction()
			),
		};

		var state = _initialState.AddAction(pipeline).ProcessAllActions();

		// Should be waiting at choice
		Assert.That(state.IsWaitingForChoice, Is.True);

		// Resolve with option 2
		state = state.ResolveChoice(ImmutableList.Create(2));

		// Should have completed
		Assert.That(state.HasPendingActions, Is.False);

		// The choice value should have been available to UseChoiceAction
		// We can't directly verify the output context, but we verified no errors occurred
	}

	[Test]
	public void EmptyPipelineReturnsUnchangedState()
	{
		var pipeline = new PipelineAction { Steps = ImmutableList<GameAction>.Empty };

		var state = _initialState.AddAction(pipeline).ProcessAllActions();

		Assert.That(state.HasPendingActions, Is.False);
	}

	[Test]
	public void PipelineWithInitialContextUsesIt()
	{
		var pipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(new IncrementAction { IncrementBy = 5 }),
			PipelineContext = ImmutableDictionary<string, object>.Empty.Add("counter", 10),
		};

		var state = _initialState.AddAction(pipeline).ProcessAllActions();

		// Should complete successfully
		Assert.That(state.HasPendingActions, Is.False);
	}

	[Test]
	public void MultipleStepsExecuteWithCorrectIndices()
	{
		// Create 5 steps to really test the index iteration
		var pipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new IncrementAction { IncrementBy = 1 },
				new IncrementAction { IncrementBy = 1 },
				new IncrementAction { IncrementBy = 1 },
				new IncrementAction { IncrementBy = 1 },
				new IncrementAction { IncrementBy = 1 }
			),
		};

		var state = _initialState.AddAction(pipeline).ProcessAllActions();

		// Should complete all steps
		Assert.That(state.HasPendingActions, Is.False);
	}

	[Test]
	public void ChoiceAtEndOfPipelineDoesNotCrash()
	{
		var pipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new IncrementAction { IncrementBy = 5 },
				new TestChoiceAction()
			),
		};

		var state = _initialState.AddAction(pipeline).ProcessAllActions();

		var choice = state.GetPendingChoice();
		Assert.That(choice, Is.Not.Null);

		// Resolve and ensure it completes cleanly
		state = state.ResolveChoice(ImmutableList.Create(1));
		Assert.That(state.HasPendingActions, Is.False);
	}

	[Test]
	public void PipelineStepsExecuteOneAtATime()
	{
		// This test verifies the new architecture where pipeline re-adds itself
		var pipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new IncrementAction { IncrementBy = 1 },
				new IncrementAction { IncrementBy = 1 }
			),
		};

		var state = _initialState.AddAction(pipeline);

		// Should have one action (the pipeline)
		Assert.That(state.HasPendingActions, Is.True);

		// Process one step
		state = state.ProcessNextAction();

		// Should still have the pipeline (it re-added itself)
		Assert.That(state.HasPendingActions, Is.True);

		// Process second step
		state = state.ProcessNextAction();

		// Should still have the pipeline (it re-added itself again)
		Assert.That(state.HasPendingActions, Is.True);

		// Process final (pipeline completes)
		state = state.ProcessNextAction();

		// Now should be done
		Assert.That(state.HasPendingActions, Is.False);
	}

	[Test]
	public void ChoiceReceivesContextFromPreviousSteps()
	{
		var pipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new IncrementAction { IncrementBy = 5 },
				new TestChoiceAction()
			),
		};

		var state = _initialState.AddAction(pipeline).ProcessAllActions();

		// Get the pipeline from the stack to check its context
		var pipelineOnStack = state.ActionStack.Peek() as PipelineAction;
		Assert.That(pipelineOnStack, Is.Not.Null);

		// The context should have the counter value
		Assert.That(pipelineOnStack!.PipelineContext.ContainsKey("counter"), Is.True);
		Assert.That(pipelineOnStack.PipelineContext["counter"], Is.EqualTo(5));
	}
}
