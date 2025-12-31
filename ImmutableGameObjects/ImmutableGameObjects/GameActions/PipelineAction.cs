using System.Collections.Immutable;

namespace ImmutableGameObjects;

/// <summary>
/// Action that executes a pipeline of actions, passing output to input.
/// </summary>
public record PipelineAction : GameAction
{
	public ImmutableList<GameAction> Steps { get; init; } = ImmutableList<GameAction>.Empty;

	public int CurrentStepIndex { get; init; } = 0;

	public ImmutableDictionary<string, object> PipelineContext { get; init; } =
		ImmutableDictionary<string, object>.Empty;

	public GameAction? GetCurrentAction =>
		CurrentStepIndex < Steps.Count ? Steps[CurrentStepIndex] : null;

	/// <summary>
	/// Executes one step of the pipeline and returns itself to continue.
	/// This "re-add" pattern is necessary for immutable state with pausable execution.
	/// </summary>
	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var context = PipelineContext.SetItems(InputContext);

		// Execute ONE step (not a loop)
		if (CurrentStepIndex >= Steps.Count)
		{
			// Pipeline finished
			return new ActionResult(state) { OutputData = context };
		}

		var step = Steps[CurrentStepIndex];

		// If this is a choice, just stop here - don't execute it
		if (step is ChoiceAction)
		{
			return new ActionResult(state)
			{
				SpawnedActions = [this], // Put ourselves back on stack unchanged
				OutputData = context,
			};
		}

		// Execute regular action
		var actionWithContext = step with
		{
			InputContext = context,
		};
		var result = actionWithContext.Execute(state);

		state = result.GameState;
		context = context.SetItems(result.OutputData);

		// Handle spawned actions
		if (result.SpawnedActions.Any())
		{
			state = state.AddActions(result.SpawnedActions);
		}

		// Put ourselves back with incremented index
		var continuingPipeline = this with
		{
			CurrentStepIndex = CurrentStepIndex + 1,
			PipelineContext = context,
		};

		return new ActionResult(state)
		{
			SpawnedActions = [continuingPipeline],
			OutputData = context,
		};
	}
}
