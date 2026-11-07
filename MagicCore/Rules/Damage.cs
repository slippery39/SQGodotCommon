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
		amount = Replaced(s, source, targetId, amount, combat);
		if (Prevented(s, targetId))
			return s;

		if (s.GetObject(targetId) is MagicPlayer player)
		{
			s = Life.Lose(s, player.Id, amount)
				.Raise(new PlayerDealtDamageEvent(player.Id, combat, source.Id, amount));
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
						DamagedByDeathtouch =
							target.DamagedByDeathtouch || s.Has(source, "Deathtouch"),
					}
				)
				.Raise(new CreatureDealtDamageEvent(targetId, amount));
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

	/// <summary>The damage after replacements — Tomik: +1 noncombat to an opponent or a permanent they control.</summary>
	/// <summary>
	/// The Needs of the Many: "Prevent all damage that would be dealt to you this turn." — none is dealt, so
	/// no life lost, no lifelink, and combat's "dealt combat damage to a player" doesn't count them.
	/// </summary>
	public static bool Prevented(GameState s, int targetId) =>
		s.GetObject(targetId) is MagicPlayer shielded
		&& s.ThisTurnEffects<Effects.PreventAllDamageToYou>(shielded.Id).Any();

	static int Replaced(GameState s, Card source, int targetId, int amount, bool combat) =>
		Replacements.Apply(
			s,
			Effects.ReplacedEvent.Damage,
			s.GetObject(targetId),
			amount,
			source,
			combat: combat
		);

	/// <summary>
	/// CR 120.4a: how much of this damage would be beyond lethal — past a planeswalker's loyalty, or
	/// a creature's toughness less the damage already marked (1 is lethal from deathtouch).
	/// </summary>
	public static int Excess(GameState s, Card source, int targetId, int amount)
	{
		if (!s.IsOnBattlefield(targetId))
			return 0;
		amount = Replaced(s, source, targetId, amount, combat: false);
		var target = s.GetCard(targetId);
		var lethal =
			target.Is(CardType.Planeswalker) ? target.Loyalty
			: target.Is(CardType.Creature) ? Math.Max(0, s.Toughness(target) - target.Damage)
			: amount;
		// A planeswalker creature loses loyalty, where deathtouch means nothing (CR 120.3c).
		if (
			!target.Is(CardType.Planeswalker)
			&& target.Is(CardType.Creature)
			&& s.Has(source, "Deathtouch")
		)
			lethal = Math.Min(lethal, 1);
		return Math.Max(0, amount - lethal);
	}
}
