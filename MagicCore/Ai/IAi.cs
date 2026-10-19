using ImmutableGameObjects;

namespace MagicCore.Ai;

/// <summary>
/// A player the computer controls. Asked whenever <see cref="PlayerId"/> must decide: it makes that
/// decision — an action, or an answer to a pending choice — and says what it was.
/// </summary>
public interface IAi
{
	int PlayerId { get; }

	AiDecision Decide(GameState s);
}

/// <summary>
/// What an AI decided: the state after it, a description, and the move itself — the action, or the
/// ids answering the pending choice — so a game can be recorded and replayed (story AI-6.4). Made only
/// by <see cref="Acting"/> and <see cref="Answering"/>, so it is always exactly one of the two.
/// Deconstructs to (State, What), as Decide always returned.
/// </summary>
public sealed record AiDecision
{
	AiDecision(GameState state, string what, GameAction? action, IReadOnlyList<int>? answer) =>
		(State, What, Action, Answer) = (state, what, action, answer);

	public GameState State { get; }
	public string What { get; }

	/// <summary>The action taken, or null when this answered a choice.</summary>
	public GameAction? Action { get; }

	/// <summary>The ids answering the pending choice, or null when this was an action.</summary>
	public IReadOnlyList<int>? Answer { get; }

	public static AiDecision Acting(GameState s, GameAction action) =>
		new(s.Act(action), Describe.Action(s, action), action, null);

	public static AiDecision Answering(GameState s, IReadOnlyList<int> ids) =>
		new(s.Choose(ids), Describe.Answer(s, s.GetPendingChoice()!, ids), null, ids);

	public void Deconstruct(out GameState state, out string what) => (state, what) = (State, What);
}
