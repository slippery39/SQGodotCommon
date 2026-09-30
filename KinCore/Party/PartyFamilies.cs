using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **The FAMILIES** (`KinFamiliesPlan.md`; Shayne, 2026-09-27): types in feel, with NO weakness chart —
/// a tag that exists only so your own monsters and cards synergise.
/// </summary>
public enum Family
{
	None,

	/// <summary>Block, and turning it into damage: Rooted walls, Thorns, Growth, tokens (`PartyGrove`).</summary>
	Grove,

	/// <summary>Spellslinging: Spell Power, Burn, chains, big spells (`PartyEmber`).</summary>
	Ember,

	Storm,
	Mire,
}

/// <summary>
/// **A card's RARITY** (Shayne, 2026-09-28): commons are the fuel, uncommons the bridges, rares the
/// build-arounds and big turns. A reward weighs them; a leader win guarantees a rare.
/// </summary>
public enum Rarity
{
	Common,
	Uncommon,
	Rare,
}

// ===== GROVE's shared pieces (the rest is in `PartyGrove`)

/// <summary>
/// **MOSSBACK** (Mosshell): all of its Block is Rooted — it survives your next turn start — and whenever
/// its Block stops a hit, it GROWS this much.
/// </summary>
public record Mossback : GameComponent
{
	public int Grow { get; init; } = 1;
}

// ===== EMBER's

/// <summary>**STOKER** (Emberling): when a card gives Spell Power, it gives this much more.</summary>
public record Stoker : GameComponent
{
	public int Extra { get; init; } = 1;
}

// ===== Card steps

/// <summary>Root: this much ROOTED Block — it survives your next turn start.</summary>
public record RootAction : CardStep
{
	public int Amount { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var ally = Target(s);
		s = s.UpdateObject(
			ally.Id,
			ally with
			{
				Block = ally.Block + Amount,
				Rooted = ally.Rooted + Amount,
			}
		);
		return new ActionResult(s).WithEvent(
			new BlockGainedEvent { AllyId = ally.Id, Amount = Amount }
		);
	}
}

/// <summary>Deep Roots: its Rooted Block doubles.</summary>
public record DeepRootsAction : CardStep
{
	public override ActionResult Execute(GameState s)
	{
		var ally = Target(s);
		var rooted = PartyFamilies.RootedOf(s, ally);
		s = s.UpdateObject(ally.Id, ally with { Block = ally.Block + rooted, Rooted = rooted * 2 });
		return new ActionResult(s).WithEvent(
			new BlockGainedEvent { AllyId = ally.Id, Amount = rooted }
		);
	}
}

/// <summary>A creature GREW: +Power for the rest of the fight. The screen floats it.</summary>
public record GrewEvent : GameEvent
{
	public int AllyId { get; init; }
	public int Power { get; init; }
	public int Hp { get; init; }
}

/// <summary>The families' rules that run at fixed moments: turn start, a token falling.</summary>
public static class PartyFamilies
{
	/// <summary>
	/// **GROW** (Grove draft 1): +this much Power for the rest of the fight, on a monster or a token.
	/// Only Power: growing HP is healing by another name, and a longer fight would pay it more.
	/// </summary>
	public static (GameState, ImmutableList<GameEvent>) GrowOnce(GameState s, Ally ally, int power)
	{
		if (power <= 0)
			return (s, []);
		var now = (Ally)s.GetObject(ally.Id);
		s = s.UpdateObject(now.Id, now with { Power = now.Power + power });
		return (s, [new GrewEvent { AllyId = now.Id, Power = power }]);
	}

	/// <summary>Every point of its Block is Rooted: a Mossback, or anything under ANCIENT BARK.</summary>
	private static bool AllRooted(GameState s, Ally ally) =>
		ally.HasComponent<Mossback>() || s.GetParty().HasComponent<AncientBarkAura>();

	/// <summary>
	/// **Its ROOTED Block** — what will survive your next turn start. For a Mossback or under Ancient
	/// Bark, all the Block gained since the last one; never the Block already carried over.
	/// </summary>
	public static int RootedOf(GameState s, Ally ally) =>
		AllRooted(s, ally) ? Math.Max(0, ally.Block - ally.Carried) : ally.Rooted;

	/// <summary>
	/// **Your turn starts**: Block drops to what is ROOTED, and that is carried ONE turn — kept now, gone
	/// at the next start unless rooted again. WILD HEART grows each of your monsters.
	/// </summary>
	public static (GameState, ImmutableList<GameEvent>) TurnStart(GameState s, bool firstTurn)
	{
		foreach (var ally in s.Allies().ToList())
		{
			var kept = Math.Min(ally.Block, RootedOf(s, ally));
			s = s.UpdateObject(ally.Id, ally with { Block = kept, Rooted = 0, Carried = kept });
		}
		var hearts = s.GetParty().GetComponents<WildHeartAura>().Count();
		var events = ImmutableList<GameEvent>.Empty;
		if (hearts > 0)
			foreach (var monster in s.LivingAllies().Where(a => !a.IsToken).ToList())
			{
				(s, var grew) = GrowOnce(s, monster, hearts);
				events = events.AddRange(grew);
			}
		return (s, events);
	}

	/// <summary>
	/// **A token of yours fell** (hit down, or sacrificed): PACK LEADER grows your monsters; LIFE CYCLE
	/// draws and roots your front. Growth is staged, so the screen floats it.
	/// </summary>
	public static GameState TokenFell(GameState s)
	{
		var pack = s.LivingAllies().SelectMany(a => a.GetComponents<PackLeader>()).Sum(p => p.Grow);
		if (pack > 0)
			foreach (var monster in s.LivingAllies().Where(a => !a.IsToken).ToList())
			{
				(s, var grew) = GrowOnce(s, monster, pack);
				foreach (var e in grew)
					s = s.StageEvent(e);
			}

		var cycles = s.GetParty().GetComponents<LifeCycleAura>().ToList();
		if (cycles.Count == 0)
			return s;
		if (s.LivingAllies().FirstOrDefault() is { } front)
		{
			var rooted = cycles.Sum(c => c.Rooted);
			s = s.UpdateObject(
				front.Id,
				front with
				{
					Block = front.Block + rooted,
					Rooted = front.Rooted + rooted,
				}
			);
		}
		(s, _) = StartTurnAction.DrawCards(s, cycles.Sum(c => c.Draw));
		return s;
	}
}
