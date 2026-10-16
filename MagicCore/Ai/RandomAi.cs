using ImmutableGameObjects;
using MagicCore.Actions;

namespace MagicCore.Ai;

/// <summary>
/// Random moves and random choice answers (the fewest picks allowed) — the floor every real AI must
/// clear, and how the strength harness proves it can see a difference at all. Picks among DISTINCT
/// moves, as GreedyAi does — or three Plains in hand would make "play a land" three times as likely.
/// Never mulligans: random mulligans go to nothing and never reach a real game.
/// </summary>
public sealed class RandomAi(int playerId, int seed) : IAi
{
	readonly Random rng = new(seed);

	public int PlayerId { get; } = playerId;

	public (GameState State, string What) Decide(GameState s)
	{
		if (s.GetPendingChoice() is { } choice)
		{
			var picks = choice
				.Options.OrderBy(_ => rng.Next())
				.Take(choice.MinChoices)
				.Select(o => o.Id)
				.ToList();
			return (s.Choose(picks), Describe.Answer(s, choice, picks));
		}
		var pool = s.DistinctMoves(s.LegalActions()).Where(a => a is not MulliganAction).ToList();
		var move = pool[rng.Next(pool.Count)];
		return (s.Act(move), Describe.Action(s, move));
	}
}
