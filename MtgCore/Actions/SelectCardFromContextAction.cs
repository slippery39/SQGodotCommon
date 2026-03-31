using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// A ChoiceAction that builds its options from a list of card IDs stored
/// in pipeline context under CardIdsContextKey.
///
/// Supports excluding previously chosen cards via ExcludeContextKeys —
/// any card ID found under those keys is filtered out of the options.
/// This enables sequential choices where each step excludes what was
/// already chosen (e.g. Telling Time's three-step selection).
/// </summary>
public record SelectCardFromContextAction : ChoiceAction
{
	/// <summary>
	/// The context key containing the ImmutableList&lt;int&gt; of candidate card IDs.
	/// </summary>
	public string CardIdsContextKey { get; init; } = "";

	/// <summary>
	/// Context keys whose values should be excluded from the options.
	/// Used to filter out cards already chosen by previous steps.
	/// </summary>
	public ImmutableList<string> ExcludeContextKeys { get; init; } = ImmutableList<string>.Empty;

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState gameState,
		ImmutableDictionary<string, object> pipelineContext
	)
	{
		if (!pipelineContext.TryGetValue(CardIdsContextKey, out var raw))
			return ImmutableList<ChoiceOption>.Empty;

		var allIds = raw as ImmutableList<int> ?? ImmutableList<int>.Empty;

		// Collect all excluded IDs from previous choices
		var excludedIds = ExcludeContextKeys
			.Where(key => pipelineContext.ContainsKey(key))
			.Select(key => (int)pipelineContext[key])
			.ToImmutableHashSet();

		return allIds
			.Where(id => !excludedIds.Contains(id) && gameState.HasObject(id))
			.Select(id =>
			{
				var card = gameState.GetObject(id) as Card;
				return new ChoiceOption { Id = id, DisplayText = card?.Name ?? $"Card {id}" };
			})
			.ToImmutableList();
	}
}
