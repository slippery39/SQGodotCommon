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
		Steps.Count - 1 > CurrentStepIndex ? Steps[CurrentStepIndex] : null;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var context = PipelineContext.SetItems(InputContext);

		var currentIndex = CurrentStepIndex;

		//Execute steps until we hit a choice or finish.
		while (currentIndex < Steps.Count)
		{
			var step = Steps[CurrentStepIndex];

			//If this is a ChoiceAction, we need to pause the pipeline
			if (step is ChoiceAction choiceAction)
			{
				var choiceWithContext = choiceAction with { InputContext = context };

				var continuationSteps = Steps.Skip(currentIndex + 1).ToImmutableList();

				if (continuationSteps.Any())
				{
					choiceWithContext = choiceWithContext with
					{
						ContinuationPipeline = new PipelineAction
						{
							Steps = continuationSteps,
							CurrentStepIndex = 0,
							PipelineContext = context,
						},
					};
				}

				//Return the choice action - it will pause execution
				return new ActionResult(state)
				{
					SpawnedActions = [choiceWithContext],
					OutputData = context,
				};
			}

			//Execute regular action.
			// Execute regular action
			var actionWithContext = step with
			{
				InputContext = context,
			};
			var result = actionWithContext.Execute(state);

			state = result.GameState;

			// Merge output into context for next step
			context = context.SetItems(result.OutputData);

			// Handle any spawned actions
			if (result.SpawnedActions.Any())
			{
				state = state.AddActions(result.SpawnedActions);
			}

			currentIndex++;
		}

		return new ActionResult(state) { OutputData = context };
	}
}
