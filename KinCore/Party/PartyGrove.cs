using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

// **GROVE — Block, and turning it into damage** (`KinFamiliesPlan.md`, "GROVE — the family draft",
// DRAFT 1 approved by Shayne, 2026-09-29). Four archetypes over one substrate: a ROOTED wall, THORNS,
// BLOCK INTO DAMAGE (read, never spent), GROWTH (+Power for the fight, from cards and passives) — and
// TOKENS, which are wall, fuel and attacker at once. The mechanics are here; the cards and monsters in
// `GroveCards`; Mossback, Root and GROW itself in `PartyFamilies`.

// ===== Monsters' passives

/// <summary>**SPORECAP** (Hushcap): a card that gives Thorns gives this much more.</summary>
public record Sporecap : GameComponent
{
	public int Extra { get; init; } = 3;
}

/// <summary>**PACK LEADER** (Howler): when a token of yours falls, each of your monsters GROWS this much.</summary>
public record PackLeader : GameComponent
{
	public int Grow { get; init; } = 1;
}

/// <summary>**The Log**: when this token falls, draw this many.</summary>
public record DrawOnFall : GameComponent
{
	public int Count { get; init; } = 2;
}

// ===== AURAS

/// <summary>ANCIENT BARK: all your Block is Rooted.</summary>
public record AncientBarkAura : Aura
{
	public override string Name => "Ancient Bark";
}

/// <summary>THORNMAIL: a foe that hits any creature on your line takes this much.</summary>
public record ThornmailAura : Aura
{
	public int Damage { get; init; } = 3;
	public override string Name => "Thornmail";
}

/// <summary>WILD HEART: at the start of each of your turns, each of your monsters GROWS 1.</summary>
public record WildHeartAura : Aura
{
	public override string Name => "Wild Heart";
}

/// <summary>LIFE CYCLE: when a token of yours falls, draw this many and your front gains Rooted Block.</summary>
public record LifeCycleAura : Aura
{
	public int Draw { get; init; } = 1;
	public int Rooted { get; init; } = 4;
	public override string Name => "Life Cycle";
}

// ===== Card steps

/// <summary>
/// **GROW** — the creature it is dropped on (or, `AllLine`, every creature on your line) gains this
/// much Power for the fight. `ByOwnPower`: Rampant Growth, it grows by its own Power.
/// </summary>
public record GrowAction : CardStep
{
	public int Amount { get; init; }
	public bool AllLine { get; init; }
	public bool ByOwnPower { get; init; }

	public override bool NeedsTarget => !AllLine;

	public override ActionResult Execute(GameState s)
	{
		var events = ImmutableList<GameEvent>.Empty;
		foreach (var ally in AllLine ? s.LivingAllies().ToList() : [Target(s)])
		{
			(s, var grew) = PartyFamilies.GrowOnce(s, ally, ByOwnPower ? ally.Power : Amount);
			events = events.AddRange(grew);
		}
		return new ActionResult(s).WithEvents(events);
	}
}

/// <summary>
/// **Grove's Block** — `Amount` on the creature it is dropped on (or every creature on your line);
/// `IfHadBlock` more if it already had Block; `PerPower` for each of its Power; `RootAll` then roots
/// every point of its Block (Brace Roots).
/// </summary>
public record GroveBlockAction : CardStep
{
	public int Amount { get; init; }
	public bool AllLine { get; init; }
	public int IfHadBlock { get; init; }
	public int PerPower { get; init; }
	public bool RootAll { get; init; }

	public override bool NeedsTarget => !AllLine;

	public override ActionResult Execute(GameState s)
	{
		var events = ImmutableList<GameEvent>.Empty;
		foreach (var ally in AllLine ? s.LivingAllies().ToList() : [Target(s)])
		{
			var block =
				Amount
				+ (ally.Block > 0 ? IfHadBlock : 0)
				+ PerPower * (ally.Power + ally.BonusPower);
			var after = ally.Block + block;
			s = s.UpdateObject(
				ally.Id,
				ally with
				{
					Block = after,
					Rooted = RootAll ? after : ally.Rooted,
					// Brace Roots roots the carried Block too: it earns another turn.
					Carried = RootAll ? 0 : ally.Carried,
				}
			);
			events = events.Add(new BlockGainedEvent { AllyId = ally.Id, Amount = block });
		}
		return new ActionResult(s).WithEvents(events);
	}
}

/// <summary>
/// **GRAFT** — dropped on a monster: your front token (another than it) is sacrificed, and the
/// monster GROWS by the token's Power and gains Block equal to its HP.
/// </summary>
public record GraftAction : CardStep
{
	public override string? Refusal(GameState s, int space, bool foeRow) =>
		base.Refusal(s, space, foeRow)
		?? (FrontToken(s, s.AllyAt(space)!.Id) is null ? "You have no token to graft" : null);

	private static Ally? FrontToken(GameState s, int not) =>
		s.LivingAllies().FirstOrDefault(a => a.IsToken && a.Id != not);

	public override ActionResult Execute(GameState s)
	{
		var ally = Target(s);
		if (FrontToken(s, ally.Id) is not { } token)
			return new(s);
		(s, var fell) = PartyGrove.Sacrifice(s, token);
		(s, var grew) = PartyFamilies.GrowOnce(s, ally, token.Power);
		var now = (Ally)s.GetObject(ally.Id);
		s = s.UpdateObject(now.Id, now with { Block = now.Block + token.Hp });
		return new ActionResult(s).WithEvents(
			fell.AddRange(grew).Add(new BlockGainedEvent { AllyId = now.Id, Amount = token.Hp })
		);
	}
}

/// <summary>**HARVEST** — dropped on a foe: every token of yours is sacrificed, and it takes their total HP.</summary>
public record HarvestAction : CardStep
{
	public override string? Refusal(GameState s, int space, bool foeRow) =>
		!foeRow || s.FoeAt(space) is null ? "Drop it on a foe"
		: !s.LivingAllies().Any(a => a.IsToken) ? "You have no tokens to harvest"
		: null;

	public override ActionResult Execute(GameState s)
	{
		var events = ImmutableList<GameEvent>.Empty;
		var total = 0;
		foreach (var token in s.LivingAllies().Where(a => a.IsToken).ToList())
		{
			total += token.Hp;
			(s, var fell) = PartyGrove.Sacrifice(s, token);
			events = events.AddRange(fell);
		}
		if (s.FoeAt(Space) is { } foe && total > 0)
		{
			(s, var hit) = PartyState.HitFoe(s, foe, total);
			events = events.AddRange(hit);
		}
		return new ActionResult(s).WithEvents(events);
	}
}

public static class PartyGrove
{
	/// <summary>A token of yours is SACRIFICED: it falls, and everything that cares about a fall hears it.</summary>
	public static (GameState, ImmutableList<GameEvent>) Sacrifice(GameState s, Ally token)
	{
		var now = (Ally)s.GetObject(token.Id);
		s = s.UpdateObject(now.Id, now with { Hp = 0 });
		s = PartySummon.TokenFainted(s, now);
		return (s, [new AllyKnockedOutEvent { AllyId = now.Id }]);
	}

	/// <summary>THORNMAIL, summed: what a foe takes for hitting any creature on your line.</summary>
	public static int Thornmail(GameState s) =>
		s.GetParty().GetComponents<ThornmailAura>().Sum(t => t.Damage);

	/// <summary>SPORECAP, summed: the extra a Thorns card gives.</summary>
	public static int Sporecap(GameState s) =>
		s.LivingAllies().SelectMany(a => a.GetComponents<Sporecap>()).Sum(p => p.Extra);
}
