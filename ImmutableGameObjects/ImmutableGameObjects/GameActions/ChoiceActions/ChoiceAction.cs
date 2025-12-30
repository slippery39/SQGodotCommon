using System.Collections.Immutable;

namespace ImmutableGameObjects;

/// <summary>
/// Base class for actions that require player input.
/// These actions don't execute immediately - they wait for ResolveChoice().
/// </summary>
public abstract record ChoiceAction : GameAction
{
	public string Prompt { get; init; } = "";
	public ImmutableList<ChoiceOption> Options { get; init; } = ImmutableList<ChoiceOption>.Empty;
	public int MinChoices { get; init; } = 1;
	public int MaxChoices { get; init; } = 1;

	// The key to store the player's choice in the output context
	public string OutputKey { get; init; } = "choice";

	// Continuation pipeline to run after choice is made (set by PipelineAction)
	public PipelineAction? ContinuationPipeline { get; init; }

	/// <summary>
	/// Default Execute does nothing - choice actions wait for ResolveChoice().
	/// </summary>
	public override ActionResult Execute(GameState gameState)
	{
		// Don't process - just return state unchanged
		// The processor will detect this is a ChoiceAction and pause
		return new ActionResult(gameState);
	}

	/// <summary>
	/// Called when player makes their choice. Returns actions to spawn.
	/// </summary>
	public GameState ResolveChoice(ImmutableList<int> selectedIds, GameState gameState)
	{
		if (selectedIds.Count < MinChoices || selectedIds.Count > MaxChoices)
		{
			throw new Exception($"Must select between {MinChoices} and {MaxChoices} options");
		}

		// Store the choice in context
		var outputContext = InputContext.Add(
			OutputKey,
			selectedIds.Count == 1 ? selectedIds[0] : (object)selectedIds
		);

		// If there's a continuation, run it with the choice data
		if (ContinuationPipeline != null)
		{
			var continuationWithContext = ContinuationPipeline with
			{
				InputContext = outputContext,
			};
			return gameState.AddAction(continuationWithContext).ProcessAllActions();
		}

		return gameState;
	}
}
