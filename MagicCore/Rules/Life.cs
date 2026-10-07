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
			.Raise(new LifeGainedEvent(playerId));
	}
}
