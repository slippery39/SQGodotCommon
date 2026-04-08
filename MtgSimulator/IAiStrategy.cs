using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Defines how an AI player makes decisions during a game.
///
/// Implementations are completely swappable — random, depth-limited search,
/// deck-specific scripted strategies, etc. The game runner and presentation
/// layers depend only on this interface, never on a specific implementation.
///
/// Two decision points are covered:
///   - SelectAction: choosing from legal game actions on the player's turn
///   - ResolveChoice: responding to a mid-resolution ChoiceAction (e.g. discard targets)
/// </summary>
public interface IAiStrategy
{
	/// <summary>
	/// Selects an action to take from the available legal actions.
	/// Called when it is the player's turn and there are legal actions available.
	/// </summary>
	GameAction SelectAction(GameState state, MtgGameIds ids, int playerId);

	/// <summary>
	/// Resolves a pending ChoiceAction by returning the selected option IDs.
	/// Called when the game is waiting for player input mid-resolution.
	/// </summary>
	ImmutableList<int> ResolveChoice(GameState state, ChoiceAction choice, int playerId);
}
