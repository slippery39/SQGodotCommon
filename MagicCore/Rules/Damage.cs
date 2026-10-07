using ImmutableGameObjects;
using MagicCore.Cards;

namespace MagicCore.Rules;

/// <summary>
/// The ONE place damage is dealt — combat, spells and abilities alike (CR 120). Lifelink and
/// deathtouch apply to all damage from a source, not just combat, so a second copy would forget one.
/// </summary>
public static class Damage
{
	public static GameState Deal(GameState s, Card source, int targetId, int amount)
	{
		if (amount <= 0 || !s.HasObject(targetId))
			return s;

		if (s.GetObject(targetId) is MagicPlayer player)
			s = s.WithPlayer(player with { Life = player.Life - amount });
		else if (s.GetObject(targetId) is Card target && s.IsOnBattlefield(targetId))
			s = s.UpdateObject(
				targetId,
				target with
				{
					Damage = target.Damage + amount,
					DamagedByDeathtouch = target.DamagedByDeathtouch || source.Has("Deathtouch"),
				}
			);
		else
			return s;

		if (source.Has("Lifelink"))
		{
			var controller = s.GetPlayer(source.ControllerId);
			s = s.WithPlayer(controller with { Life = controller.Life + amount });
		}
		return s;
	}
}
