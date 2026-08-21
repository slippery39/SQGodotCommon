using System.Collections.Immutable;

namespace ImmutableGameObjects;

/// <summary>
/// Base class for actions that require player input.
/// These actions pause execution when encountered inside a PipelineAction.
///
/// ChoiceActions must always live inside a PipelineAction — never placed on the
/// stack directly. GameState.ResolveChoice() advances the pipeline once the
/// player has made their selection.
///
/// Override GetOptions(GameState, PipelineContext) to provide dynamic options
/// based on game state and previously accumulated pipeline context at the moment
/// the pipeline pauses. The default implementation returns the fixed Options list.
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
	/// Returns the options available to the player at the moment the pipeline pauses.
	/// Override to provide dynamic options derived from the current game state or
	/// previously accumulated pipeline context.
	///
	/// Examples:
	///   - Cards in hand (game state query)
	///   - Top N cards of library (game state query)
	///   - Cards not yet chosen by a previous step (pipeline context filter)
	///
	/// The default implementation returns the fixed Options list set at construction time,
	/// which is correct for design-time choices like Charm or Command cards.
	/// </summary>
	public virtual ImmutableList<ChoiceOption> GetOptions(
		GameState gameState,
		ImmutableDictionary<string, object> pipelineContext
	) => Options;

	/// <summary>
	/// Context key naming the player who makes this choice. Set it to whichever key the game
	/// layer stores the controlling player under (in MtgCore: ContextKeys.CastingPlayerId).
	/// </summary>
	public string DecidingPlayerContextKey { get; init; } = "";

	/// <summary>
	/// WHO answers this choice — not whose turn it is.
	///
	/// Without it every front end had to guess from the active player, and both guessed the same
	/// wrong way: the Godot UI showed the panel whenever it was not the AI's turn, and GameRunner
	/// handed the choice to whoever was active. So an opponent's card that asked a question during
	/// YOUR turn asked YOU — you were prompted to discard for their Avaricious Dragon and to scry
	/// their library — and the mirror case let the AI silently answer yours.
	///
	/// Returns 0 when the owner cannot be determined, which callers must treat as "fall back to
	/// the active player" so a choice can never become unanswerable and wedge the stack.
	/// </summary>
	public virtual int GetDecidingPlayerId(
		GameState gameState,
		ImmutableDictionary<string, object> pipelineContext
	) =>
		!string.IsNullOrEmpty(DecidingPlayerContextKey)
		&& pipelineContext.TryGetValue(DecidingPlayerContextKey, out var value)
		&& value is int playerId
			? playerId
			: 0;

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
