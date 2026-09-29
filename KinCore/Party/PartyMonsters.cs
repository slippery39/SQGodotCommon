using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **THE FIRST-ATTACK BONUS** (`KinFamiliesPlan.md`, round 4; Shayne, 2026-09-28): the first ATTACK
/// card played on a monster EACH TURN also does this — so spreading attacks across the team is the
/// good play, and each monster is its own. A monster has one; every field is optional. Spells are not
/// attacks (they are cast by the team), so an Ember monster's bonus FEEDS spells instead.
/// </summary>
public record FirstAttack : GameComponent
{
	/// <summary>More damage on that attack.</summary>
	public int Damage { get; init; }

	/// <summary>Block for the monster that attacked.</summary>
	public int Block { get; init; }

	/// <summary>ROOTED Block for it — Block that stays.</summary>
	public int Rooted { get; init; }

	/// <summary>Thorns on it for this turn.</summary>
	public int Thorns { get; init; }

	/// <summary>Spell Power for the whole team, this turn.</summary>
	public int SpellPower { get; init; }

	/// <summary>Kindle — Spell Power for the rest of the fight.</summary>
	public int Kindle { get; init; }

	public int Draw { get; init; }

	/// <summary>The bonus in words — its badge and its inspector line.</summary>
	public string Text =>
		string.Join(
			", ",
			new[]
			{
				Damage > 0 ? $"+{Damage} damage" : "",
				Block > 0 ? $"+{Block} Block" : "",
				Rooted > 0 ? $"+{Rooted} Rooted Block" : "",
				Thorns > 0 ? $"+{Thorns} Thorns" : "",
				SpellPower > 0 ? $"+{SpellPower} Spell Power this turn" : "",
				Kindle > 0 ? $"+{Kindle} Kindle" : "",
				Draw > 0 ? $"draw {Draw}" : "",
			}.Where(p => p.Length > 0)
		);
}

public static class PartyMonsters
{
	/// <summary>Whether this monster's first-attack bonus is still to come this turn.</summary>
	public static bool BonusReady(Ally ally) =>
		!ally.AttackedThisTurn && ally.GetComponent<FirstAttack>() is not null;

	/// <summary>
	/// **An attack card was played on this monster**: the bonus's damage to add (0 after the first),
	/// and the state with everything else the bonus gives — and the monster marked as having attacked.
	/// </summary>
	public static (GameState State, int Damage, ImmutableList<GameEvent> Events) Attacks(
		GameState s,
		Ally ally
	)
	{
		var bonus = BonusReady(ally) ? ally.GetComponent<FirstAttack>() : null;
		var events = ImmutableList<GameEvent>.Empty;
		ally = ally with { AttackedThisTurn = true };
		if (bonus is not null)
		{
			ally = ally with
			{
				Block = ally.Block + bonus.Block + bonus.Rooted,
				Rooted = ally.Rooted + bonus.Rooted,
				BonusThorns = ally.BonusThorns + bonus.Thorns,
				BonusSpellPower = ally.BonusSpellPower + bonus.SpellPower,
			};
			if (bonus.Block + bonus.Rooted > 0)
				events = events.Add(
					new BlockGainedEvent { AllyId = ally.Id, Amount = bonus.Block + bonus.Rooted }
				);
			events = events.Add(new FirstAttackEvent { AllyId = ally.Id, Text = bonus.Text });
		}
		s = s.UpdateObject(ally.Id, ally);
		if (bonus is { Kindle: > 0 })
		{
			var party = s.GetParty();
			s = s.UpdateObject(party.Id, party with { Kindle = party.Kindle + bonus.Kindle });
		}
		if (bonus is { Draw: > 0 })
			(s, _) = StartTurnAction.DrawCards(s, bonus.Draw);
		return (s, bonus?.Damage ?? 0, events);
	}
}

/// <summary>A monster's first-attack bonus fired — the screen says what it gave.</summary>
public record FirstAttackEvent : GameEvent
{
	public int AllyId { get; init; }
	public string Text { get; init; } = "";
}
