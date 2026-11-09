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

	public AiDecision Decide(GameState s)
	{
		if (s.GetPendingChoice() is { } choice)
			return AiDecision.Answering(
				s,
				[
					.. choice
						.Options.Where(o => o.IsEnabled)
						.OrderBy(_ => rng.Next())
						.Take(choice is ChooseCastChoice ? 1 : choice.MinChoices) // a cast choice may be left empty (TBL-2.16): the floor still casts
						.Select(o => o.Id),
				]
			);
		var pool = s.DistinctMoves(s.LegalActions()).Where(a => a is not MulliganAction).ToList();
		return AiDecision.Acting(s, pool[rng.Next(pool.Count)]);
	}
}
