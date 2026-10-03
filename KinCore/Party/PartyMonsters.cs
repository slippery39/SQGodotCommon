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

	/// <summary>Spell Power for the rest of the fight.</summary>
	public int FightSpellPower { get; init; }

	/// <summary>Burn on the foe (or foes) the attack hits.</summary>
	public int Burn { get; init; }

	public int Energy { get; init; }

	public int Draw { get; init; }

	/// <summary>The monster GROWS this much (Howler).</summary>
	public int Grow { get; init; }

	/// <summary>A token arrives at your front (Broodvine).</summary>
	public TokenTemplate? Summons { get; init; }

	/// <summary>How many of <see cref="Summons"/> — the Broodmother calls two.</summary>
	public int SummonCount { get; init; } = 1;

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
				FightSpellPower > 0 ? $"+{FightSpellPower} Spell Power this fight" : "",
				Burn > 0 ? $"{Burn} Burn on the foe it hits" : "",
				Energy > 0 ? $"+{Energy} energy" : "",
				Draw > 0 ? $"draw {Draw}" : "",
				Grow > 0 ? $"it grows {Grow}" : "",
				Summons is { } t
					? SummonCount > 1
						? $"summon {SummonCount} {t.Creature.Name}s"
						: $"summon a {t.Creature.Name}"
					: "",
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
	public static (GameState State, int Damage, ImmutableList<GameEvent> Events, int Burn) Attacks(
		GameState s,
		Ally ally
	)
	{
		var bonus = BonusReady(ally) ? ally.GetComponent<FirstAttack>() : null;
		var first = !ally.AttackedThisTurn;
		var events = ImmutableList<GameEvent>.Empty;
		ally = ally with { AttackedThisTurn = true };
		if (bonus is not null)
		{
			ally = ally with
			{
				Block = ally.Block + bonus.Block + bonus.Rooted,
				Rooted = ally.Rooted + bonus.Rooted,
				BonusThorns = ally.BonusThorns + bonus.Thorns,
			};
			if (bonus.Block + bonus.Rooted > 0)
				events = events.Add(
					new BlockGainedEvent { AllyId = ally.Id, Amount = bonus.Block + bonus.Rooted }
				);
			events = events.Add(new FirstAttackEvent { AllyId = ally.Id, Text = bonus.Text });
		}
		s = s.UpdateObject(ally.Id, ally);
		if (bonus is not null)
		{
			var party = s.GetParty();
			s = s.UpdateObject(
				party.Id,
				party with
				{
					TurnSpellPower = party.TurnSpellPower + bonus.SpellPower,
					FightSpellPower = party.FightSpellPower + bonus.FightSpellPower,
					Energy = party.Energy + bonus.Energy,
				}
			);
		}
		if (bonus is { Draw: > 0 })
			(s, _) = StartTurnAction.DrawCards(s, bonus.Draw);
		if (bonus is { Grow: > 0 })
		{
			(s, var grew) = PartyFamilies.GrowOnce(s, ally, bonus.Grow);
			events = events.AddRange(grew);
		}
		if (bonus?.Summons is { } token)
			for (var i = 0; i < bonus.SummonCount; i++)
				s = PartySummon.SummonAlly(s, token);
		// HUNT CALL: its first attack each turn sends the tokens in with it.
		if (first && ally.HasComponent<HuntCall>())
		{
			(s, var pack) = PartySummon.TokensAttack(s, 0);
			events = events.AddRange(pack);
		}
		return (s, bonus?.Damage ?? 0, events, bonus?.Burn ?? 0);
	}
}

/// <summary>A monster's first-attack bonus fired — the screen says what it gave.</summary>
public record FirstAttackEvent : GameEvent
{
	public int AllyId { get; init; }
	public string Text { get; init; } = "";
}
