using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Turns;

namespace MagicCore.Rules;

/// <summary>
/// The ONE place damage is dealt — combat, spells and abilities alike (CR 120). Lifelink and
/// deathtouch apply to all damage from a source, not just combat, so a second copy would forget one.
/// </summary>
public static class Damage
{
	public static GameState Deal(
		GameState s,
		Card source,
		int targetId,
		int amount,
		bool combat = false
	)
	{
		if (amount <= 0 || !s.HasObject(targetId))
			return s;

		if (s.GetObject(targetId) is MagicPlayer player)
		{
			s = s.WithPlayer(player with { Life = player.Life - amount })
				.Raise(new PlayerDealtDamageEvent(player.Id, combat, source.Id));
			if (!combat)
				s = s.Tally(
					player.Id,
					t => t with { NoncombatDamageTaken = t.NoncombatDamageTaken + amount }
				);
		}
		else if (
			s.GetObject(targetId) is Card { } walker
			&& s.IsOnBattlefield(targetId)
			&& walker.Is(CardType.Planeswalker)
		)
			s = Actions.Loyalty.Change(s, targetId, -amount); // CR 120.3c
		else if (s.GetObject(targetId) is Card target && s.IsOnBattlefield(targetId))
			s = s.UpdateObject(
				targetId,
				target with
				{
					Damage = target.Damage + amount,
					DamagedByDeathtouch = target.DamagedByDeathtouch || s.Has(source, "Deathtouch"),
				}
			);
		else
			return s; // its target is gone: no damage was dealt

		// Ruric Thar, Magecrusher: "as long as they haven't dealt combat damage yet".
		if (combat && s.IsOnBattlefield(source.Id) && !s.GetCard(source.Id).HasDealtCombatDamage)
			s = s.UpdateObject(
				source.Id,
				s.GetCard(source.Id) with
				{
					HasDealtCombatDamage = true,
				}
			);
		return s.Has(source, "Lifelink") ? Life.Gain(s, source.ControllerId, amount) : s;
	}
}
