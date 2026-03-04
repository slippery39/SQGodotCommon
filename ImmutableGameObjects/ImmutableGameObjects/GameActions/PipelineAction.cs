using System.Collections.Immutable;

namespace ImmutableGameObjects;

/// <summary>
/// A sequence of actions that execute in order, passing output context to each step.
/// Supports pausing mid-sequence when a ChoiceAction is encountered.
///
/// PipelineAction is pure data — it does not execute itself.
/// GameState.ProcessNextAction() is the executor and handles all pipeline stepping logic.
///
/// To queue a pipeline:
///   var pipeline = new PipelineAction { Steps = [ actionA, actionB, actionC ] };
///   state = state.AddAction(pipeline);
/// </summary>
public record PipelineAction : GameAction
{
	public ImmutableList<GameAction> Steps { get; init; } = ImmutableList<GameAction>.Empty;

	/// <summary>
	/// Tracks which step the executor should run next.
	/// Managed exclusively by GameState.ProcessNextAction().
	/// </summary>
	public int CurrentStepIndex { get; init; } = 0;

	/// <summary>
	/// Accumulated context passed between steps (output of step N becomes input of step N+1).
	/// Managed exclusively by GameState.ProcessNextAction().
	/// </summary>
	public ImmutableDictionary<string, object> PipelineContext { get; init; } =
		ImmutableDictionary<string, object>.Empty;

	public bool IsComplete => CurrentStepIndex >= Steps.Count;

	public GameAction? CurrentStep =>
		CurrentStepIndex < Steps.Count ? Steps[CurrentStepIndex] : null;

	/// <summary>
	/// PipelineAction must never be executed directly — the executor intercepts it.
	/// </summary>
	public override ActionResult Execute(GameState gameState) =>
		throw new InvalidOperationException(
			"PipelineAction cannot be executed directly. "
				+ "It must be handled by GameState.ProcessNextAction()."
		);
}
