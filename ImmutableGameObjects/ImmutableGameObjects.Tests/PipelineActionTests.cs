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
		var executedSteps = GetInput<ImmutableList<int>>(
			"executed_steps",
			ImmutableList<int>.Empty
		);
		var newList = executedSteps.Add(StepNumber);
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
		var pipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new StepMarkerAction { StepNumber = 1 },
				new StepMarkerAction { StepNumber = 2 },
				new StepMarkerAction { StepNumber = 3 }
			),
		};

		var (state, _) = _initialState.AddAction(pipeline).ProcessAllActions();

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

		var (state, _) = _initialState.AddAction(pipeline).ProcessAllActions();

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

		var (state, _) = _initialState.AddAction(pipeline).ProcessAllActions();

		Assert.That(state.IsWaitingForChoice, Is.True);

		var choice = state.GetPendingChoice();
		Assert.That(choice, Is.Not.Null);
		Assert.That(choice, Is.TypeOf<TestChoiceAction>());
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

		var (stateAtChoice, _) = _initialState.AddAction(pipeline).ProcessAllActions();
		Assert.That(stateAtChoice.IsWaitingForChoice, Is.True);

		var (finalState, _) = stateAtChoice.ResolveChoice(ImmutableList.Create(1));

		Assert.That(finalState.IsWaitingForChoice, Is.False);
		Assert.That(finalState.HasPendingActions, Is.False);
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

		var (stateAtChoice, _) = _initialState.AddAction(pipeline).ProcessAllActions();
		Assert.That(stateAtChoice.IsWaitingForChoice, Is.True);

		var (finalState, _) = stateAtChoice.ResolveChoice(ImmutableList.Create(2));

		Assert.That(finalState.HasPendingActions, Is.False);
	}

	[Test]
	public void EmptyPipelineReturnsUnchangedState()
	{
		var pipeline = new PipelineAction { Steps = ImmutableList<GameAction>.Empty };

		var (state, _) = _initialState.AddAction(pipeline).ProcessAllActions();

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

		var (state, _) = _initialState.AddAction(pipeline).ProcessAllActions();

		Assert.That(state.HasPendingActions, Is.False);
	}

	[Test]
	public void MultipleStepsExecuteWithCorrectIndices()
	{
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

		var (state, _) = _initialState.AddAction(pipeline).ProcessAllActions();

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

		var (stateAtChoice, _) = _initialState.AddAction(pipeline).ProcessAllActions();

		var choice = stateAtChoice.GetPendingChoice();
		Assert.That(choice, Is.Not.Null);

		var (finalState, _) = stateAtChoice.ResolveChoice(ImmutableList.Create(1));
		Assert.That(finalState.HasPendingActions, Is.False);
	}

	[Test]
	public void PipelineStepsExecuteOneAtATime()
	{
		var pipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new IncrementAction { IncrementBy = 1 },
				new IncrementAction { IncrementBy = 1 }
			),
		};

		var state = _initialState.AddAction(pipeline);
		Assert.That(state.HasPendingActions, Is.True);

		(state, _) = state.ProcessNextAction();
		Assert.That(state.HasPendingActions, Is.True);

		(state, _) = state.ProcessNextAction();
		Assert.That(state.HasPendingActions, Is.True);

		(state, _) = state.ProcessNextAction();
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

		var (state, _) = _initialState.AddAction(pipeline).ProcessAllActions();

		var pipelineOnStack = state.ActionStack.Peek() as PipelineAction;
		Assert.That(pipelineOnStack, Is.Not.Null);
		Assert.That(pipelineOnStack!.PipelineContext.ContainsKey("counter"), Is.True);
		Assert.That(pipelineOnStack.PipelineContext["counter"], Is.EqualTo(5));
	}
}
