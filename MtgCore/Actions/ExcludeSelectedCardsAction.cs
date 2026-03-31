using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Query action — no game state change.
/// Reads a list of card IDs from CardIdsContextKey, subtracts the IDs
/// found under ExcludeContextKeys, and writes the remaining IDs as an
/// ImmutableList&lt;int&gt; to OutputKey.
///
/// Handles both single int and ImmutableList&lt;int&gt; values under ExcludeContextKeys,
/// since ResolveChoice stores a plain int when MaxChoices = 1 and a list when > 1.
///
/// Examples:
///   - Telling Time: 3 cards, exclude 2 chosen → 1 remaining
///   - "Pick 2 of 7, rest to bottom": 7 cards, exclude 2 chosen → 5 remaining
/// </summary>
public record ExcludeSelectedCardsAction : GameAction
{
	public string CardIdsContextKey { get; init; } = "";
	public ImmutableList<string> ExcludeContextKeys { get; init; } = ImmutableList<string>.Empty;
	public string OutputKey { get; init; } = ContextKeys.RemainingCardIds;

	public override ActionResult Execute(GameState gameState)
	{
		if (!InputContext.TryGetValue(CardIdsContextKey, out var raw))
			return new ActionResult(gameState);

		var allIds = raw as ImmutableList<int> ?? ImmutableList<int>.Empty;

		var excludedIds = ExcludeContextKeys
			.Where(key => InputContext.ContainsKey(key))
			.SelectMany(key => ExtractIds(InputContext[key]))
			.ToImmutableHashSet();

		var remaining = allIds.Where(id => !excludedIds.Contains(id)).ToImmutableList();

		if (remaining.IsEmpty)
			return new ActionResult(gameState);

		return new ActionResult(gameState).WithOutput(OutputKey, remaining);
	}

	private static IEnumerable<int> ExtractIds(object value) =>
		value switch
		{
			ImmutableList<int> list => list,
			int single => ImmutableList.Create(single),
			_ => Enumerable.Empty<int>(),
		};
}
