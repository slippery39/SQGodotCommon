using ImmutableGameObjects;
using MagicCore.Turns;

namespace MagicCore.Rules;

/// <summary>The ONE place life is gained — so "whenever you gain life" sees lifelink and effects alike.</summary>
public static class Life
{
	public static GameState Gain(GameState s, int playerId, int amount)
	{
		if (amount <= 0)
			return s;
		var player = s.GetPlayer(playerId);
		return s.WithPlayer(player with { Life = player.Life + amount })
			.Tally(playerId, t => t with { LifeGained = t.LifeGained + amount })
			.Raise(new LifeGainedEvent(playerId));
	}

	/// <summary>
	/// THE place life is lost — damage (CR 120.3a), LoseLife and paying life alike — so Gul Dukat's
	/// "the life that player lost this turn" (TurnTally.LifeLost) sees every loss.
	/// </summary>
	public static GameState Lose(GameState s, int playerId, int amount)
	{
		if (amount <= 0)
			return s;
		var player = s.GetPlayer(playerId);
		return s.WithPlayer(player with { Life = player.Life - amount })
			.Tally(playerId, t => t with { LifeLost = t.LifeLost + amount });
	}
}
