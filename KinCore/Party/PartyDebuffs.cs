using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>A debuff a foe's move can put on your monsters (`KinEnemiesPlan.md`).</summary>
public enum Debuff
{
	None,

	/// <summary>Its attacks deal 25% less.</summary>
	Weak,

	/// <summary>It takes 50% more from each hit.</summary>
	Vulnerable,

	/// <summary>Its first-attack bonus does not fire.</summary>
	Silence,

	/// <summary>Attack cards cannot be played on it.</summary>
	Shaken,
}

/// <summary>
/// **DEBUFFS on your monsters** (interview 2026-10-03, every answer as recommended): a number of TURNS,
/// STS's way — stacking when applied again, counting down at the END of your turn, so "Weak 1" put on
/// in the foes' turn covers your next one. Foes apply them as a RIDER on a move; nothing removes them
/// but time (yet). Foes only for now — your cards may use the same counters later.
/// </summary>
public static class PartyDebuffs
{
	/// <summary>A Weak monster's attack, rounded down.</summary>
	public static int Weakened(Ally ally, int damage) => ally.Weak > 0 ? damage * 3 / 4 : damage;

	/// <summary>A hit on a Vulnerable monster, rounded down.</summary>
	public static int Exposed(Ally ally, int amount) =>
		ally.Vulnerable > 0 ? amount * 3 / 2 : amount;

	/// <summary>This many more turns of the debuff on the monster — they stack.</summary>
	public static GameState Afflict(GameState s, int allyId, Debuff debuff, int turns)
	{
		if (turns <= 0 || s.GetObject(allyId) is not Ally { IsKnockedOut: false } ally)
			return s;
		return s.UpdateObject(
			allyId,
			debuff switch
			{
				Debuff.Weak => ally with { Weak = ally.Weak + turns },
				Debuff.Vulnerable => ally with { Vulnerable = ally.Vulnerable + turns },
				Debuff.Silence => ally with { Silenced = ally.Silenced + turns },
				Debuff.Shaken => ally with { Shaken = ally.Shaken + turns },
				_ => ally,
			}
		);
	}

	/// <summary>Your turn is over: every debuff on your monsters has a turn less.</summary>
	public static GameState Tick(GameState s)
	{
		foreach (var ally in s.Allies().ToList())
			s = s.UpdateObject(
				ally.Id,
				ally with
				{
					Weak = Math.Max(0, ally.Weak - 1),
					Vulnerable = Math.Max(0, ally.Vulnerable - 1),
					Silenced = Math.Max(0, ally.Silenced - 1),
					Shaken = Math.Max(0, ally.Shaken - 1),
				}
			);
		return s;
	}
}
