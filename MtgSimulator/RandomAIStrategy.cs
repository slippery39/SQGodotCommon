using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// An AI strategy that makes all decisions randomly.
///
/// SelectAction picks a random legal action.
/// ResolveChoice picks a random valid option, respecting min/max choice counts.
///
/// Used as the baseline AI and as the playout policy inside more sophisticated
/// strategies. Also suitable for testing and the console opponent.
/// </summary>
public class RandomAiStrategy : IAiStrategy
{
	private readonly Random _rng;

	public RandomAiStrategy(Random? rng = null)
	{
		_rng = rng ?? new Random();
	}

	public GameAction SelectAction(GameState state, MtgGameIds ids, int playerId)
	{
		var actions = MtgActionGenerator.GetLegalActions(state, ids, playerId);

		if (actions.Count == 0)
			throw new InvalidOperationException(
				"SelectAction called with no legal actions available"
			);

		return actions[_rng.Next(actions.Count)];
	}

	public ImmutableList<int> ResolveChoice(GameState state, ChoiceAction choice, int playerId)
	{
		if (choice.Options.IsEmpty)
			return ImmutableList<int>.Empty;

		// Pick MinChoices random distinct options
		var shuffled = choice.Options.OrderBy(_ => _rng.Next()).ToList();
		return shuffled.Take(choice.MinChoices).Select(o => o.Id).ToImmutableList();
	}
}
