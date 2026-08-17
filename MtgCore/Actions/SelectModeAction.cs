using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "Choose one —" on a modal spell (Fortify).
///
/// A ChoiceAction over the spell's modes: the chosen mode's index is written to OutputKey and
/// ApplyChosenModeAction spawns the matching action. Split in two so the choice can pause the
/// pipeline for a human while the AI resolves it inline, which is how every other ChoiceAction
/// in this engine works.
///
/// Modes are stored as data (a list of actions plus display text), never as delegates, so a
/// modal card stays serializable inside GameState.
/// </summary>
public record SelectModeAction : ChoiceAction
{
	public ImmutableList<string> ModeNames { get; init; } = ImmutableList<string>.Empty;

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState gameState,
		ImmutableDictionary<string, object> pipelineContext
	) =>
		ModeNames
			// Id is the mode INDEX, not a game object id — ApplyChosenModeAction indexes its
			// Modes list with it.
			.Select((name, index) => new ChoiceOption { Id = index, DisplayText = name })
			.ToImmutableList();
}

/// <summary>
/// Runs the mode chosen by a preceding SelectModeAction.
///
/// Reads the chosen index from ModeContextKey. Falls back to mode 0 if nothing was chosen, so a
/// modal spell resolved without a choice still does something rather than silently fizzling.
/// </summary>
public record ApplyChosenModeAction : GameAction
{
	public ImmutableList<GameAction> Modes { get; init; } = ImmutableList<GameAction>.Empty;
	public string ModeContextKey { get; init; } = "chosen_mode";

	public override ActionResult Execute(GameState gameState)
	{
		if (Modes.IsEmpty)
			return new ActionResult(gameState);

		var index = ResolveModeIndex();
		if (index < 0 || index >= Modes.Count)
			index = 0;

		var chosen = Modes[index] with { InputContext = InputContext };

		return new ActionResult(gameState.SpawnAction(chosen));
	}

	/// <summary>
	/// ChoiceAction writes its result as a list of selected option IDs, but a single-select
	/// choice may arrive as a bare int — both shapes are handled, matching
	/// MoveCardToBottomOfLibraryAction.
	/// </summary>
	private int ResolveModeIndex()
	{
		if (!InputContext.TryGetValue(ModeContextKey, out var raw))
			return 0;

		return raw switch
		{
			int single => single,
			ImmutableList<int> list => list.IsEmpty ? 0 : list[0],
			_ => 0,
		};
	}
}
