using ImmutableGameObjects;
using MagicCore.Effects;
using MagicCore.Turns;

namespace MagicCore.Rules;

/// <summary>The ONE place life is gained — so "whenever you gain life" sees lifelink and effects alike.</summary>
public static class Life
{
	public static GameState Gain(GameState s, int playerId, int amount)
	{
		if (amount <= 0 || CantChange(s, playerId))
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
		if (amount <= 0 || CantChange(s, playerId))
			return s;
		var player = s.GetPlayer(playerId);
		return s.WithPlayer(player with { Life = player.Life - amount })
			.Tally(playerId, t => t with { LifeLost = t.LifeLost + amount });
	}

	/// <summary>Flare of Fortitude: "your life total can't change" — no gain, no loss, damage still dealt (CR 119.7).</summary>
	public static bool CantChange(GameState s, int playerId) =>
		s.ThisTurnEffects<LifeTotalCantChange>(playerId).Any();

	/// <summary>
	/// THE "may this player pay that much life": only with at least that much (CR 119.4), and never while
	/// their life total can't change (CR 119.8) — Phyrexian mana, the shocklands, Necrodominance, Hive Mind
	/// Coprocessor. Paying 0 is always possible.
	/// </summary>
	public static bool CanPay(GameState s, int playerId, int amount) =>
		amount <= 0 || (s.GetPlayer(playerId).Life >= amount && !CantChange(s, playerId));
}
