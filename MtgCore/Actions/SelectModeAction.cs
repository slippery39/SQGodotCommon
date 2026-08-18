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

	/// <summary>
	/// "Choose one that hasn't been chosen" — hides modes already recorded in the source card's
	/// ChosenModesComponent. Off by default: an ordinary modal spell resolves once and may pick
	/// anything.
	/// </summary>
	public bool ExcludeAlreadyChosen { get; init; } = false;

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState gameState,
		ImmutableDictionary<string, object> pipelineContext
	)
	{
		var taken = ExcludeAlreadyChosen
			? GetChosenIndices(gameState, pipelineContext)
			: ImmutableList<int>.Empty;

		var options = ModeNames
			// Id is the mode INDEX, not a game object id — ApplyChosenModeAction indexes its
			// Modes list with it.
			.Select((name, index) => new ChoiceOption { Id = index, DisplayText = name })
			.Where(o => !taken.Contains(o.Id))
			.ToImmutableList();

		// Never hand back an empty option list: a ChoiceAction with nothing to choose stalls the
		// pipeline. Demonic Pact cannot reach this — its last remaining mode ends the game — but
		// a future card with all-optional modes could.
		return options.IsEmpty
			? ModeNames
				.Select((n, i) => new ChoiceOption { Id = i, DisplayText = n })
				.ToImmutableList()
			: options;
	}

	private static ImmutableList<int> GetChosenIndices(
		GameState gameState,
		ImmutableDictionary<string, object> pipelineContext
	)
	{
		if (!pipelineContext.TryGetValue(ContextKeys.SourceCardId, out var raw))
			return ImmutableList<int>.Empty;
		if (raw is not int sourceCardId || !gameState.HasObject(sourceCardId))
			return ImmutableList<int>.Empty;
		if (gameState.GetObject(sourceCardId) is not Card card)
			return ImmutableList<int>.Empty;

		return card.GetComponent<ChosenModesComponent>()?.ChosenIndices ?? ImmutableList<int>.Empty;
	}
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

	/// <summary>
	/// Records the chosen index on the source card's ChosenModesComponent so it cannot be picked
	/// again. Must be paired with SelectModeAction.ExcludeAlreadyChosen — recording without
	/// excluding does nothing, and excluding without recording never excludes anything.
	/// </summary>
	public bool RecordChoice { get; init; } = false;

	public override ActionResult Execute(GameState gameState)
	{
		if (Modes.IsEmpty)
			return new ActionResult(gameState);

		var index = ResolveModeIndex();
		if (index < 0 || index >= Modes.Count)
			index = 0;

		var state = RecordChoice ? RecordOnSource(gameState, index) : gameState;
		var chosen = Modes[index] with { InputContext = InputContext };

		return new ActionResult(state.SpawnAction(chosen));
	}

	private GameState RecordOnSource(GameState gameState, int index)
	{
		if (!InputContext.TryGetValue(ContextKeys.SourceCardId, out var raw))
			return gameState;
		if (raw is not int sourceCardId || !gameState.HasObject(sourceCardId))
			return gameState;
		if (gameState.GetObject(sourceCardId) is not Card card)
			return gameState;

		var existing = card.GetComponent<ChosenModesComponent>() ?? new ChosenModesComponent();
		if (existing.ChosenIndices.Contains(index))
			return gameState;

		var updated = existing with { ChosenIndices = existing.ChosenIndices.Add(index) };
		return gameState.UpdateObject(
			sourceCardId,
			card.GetComponent<ChosenModesComponent>() == null
				? card.WithComponent(updated)
				: card.WithComponentReplaced(updated)
		);
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
