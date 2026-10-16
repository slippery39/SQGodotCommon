using ImmutableGameObjects;

namespace MagicCore.Ai;

/// <summary>
/// A player the computer controls. Asked whenever <see cref="PlayerId"/> must decide: it makes that
/// decision — an action, or an answer to a pending choice — and says what it was.
/// </summary>
public interface IAi
{
	int PlayerId { get; }

	(GameState State, string What) Decide(GameState s);
}
