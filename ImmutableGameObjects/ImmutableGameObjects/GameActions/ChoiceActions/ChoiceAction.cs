using System.Collections.Immutable;

namespace ImmutableGameObjects;

/// <summary>
/// Base class for actions that require player input.
/// These actions pause execution when encountered inside a PipelineAction.
///
/// ChoiceActions must always live inside a PipelineAction — never placed on the
/// stack directly. GameState.ResolveChoice() advances the pipeline once the
/// player has made their selection.
/// </summary>
public abstract record ChoiceAction : GameAction
{
	public string Prompt { get; init; } = "";
	public ImmutableList<ChoiceOption> Options { get; init; } = ImmutableList<ChoiceOption>.Empty;
	public int MinChoices { get; init; } = 1;
	public int MaxChoices { get; init; } = 1;

	/// <summary>
	/// The key used to store the player's selection in the pipeline context,
	/// making it available to subsequent steps via GetInput().
	/// </summary>
	public string OutputKey { get; init; } = "choice";

	/// <summary>
	/// ChoiceActions are never executed directly — the executor pauses when it
	/// encounters one and waits for GameState.ResolveChoice() to be called.
	/// </summary>
	public override ActionResult Execute(GameState gameState) =>
		throw new InvalidOperationException(
			"ChoiceAction cannot be executed directly. "
				+ "The executor pauses on ChoiceActions and resumes via GameState.ResolveChoice()."
		);
}
