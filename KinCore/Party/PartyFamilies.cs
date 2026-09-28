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

	/// <summary>Growth: bodies that grow each turn, Block that stays, walls that bite. Leans FRONT.</summary>
	Grove,

	/// <summary>Kindling: every spell stokes a fire that burns hotter all fight. Leans BACK.</summary>
	Ember,

	Storm,
	Mire,
}

// ===== GROVE — the engine is TIME

/// <summary>**GROW**: at the start of each of your turns it gains this much Power and max HP (and HP).</summary>
public record Grow : GameComponent
{
	public int Power { get; init; } = 1;
	public int Hp { get; init; } = 2;
}

/// <summary>**NURSERY** (Broodvine): tokens you summon while it stands arrive with Grow.</summary>
public record Nursery : GameComponent;

/// <summary>**MOSSBACK** (Mosshell): all of its Block is Rooted — none of it vanishes at your turn start.</summary>
public record Mossback : GameComponent;

/// <summary>**THORNWALL** (Bramble): a foe that attacks it takes damage equal to its Block, as well.</summary>
public record Thornwall : GameComponent;

/// <summary>**SPORES** (Hushcap): when a token of yours falls, draw this many and gain this much energy.</summary>
public record Spores : GameComponent
{
	public int Draw { get; init; } = 1;
	public int Energy { get; init; } = 1;
}

/// <summary>
/// **ALPHA** (Howler): tokens you summon while it stands ATTACK the front for their Power each round,
/// instead of whatever still thing they do — so a growing swarm hits harder every turn.
/// </summary>
public record Alpha : GameComponent;

// ===== EMBER — the engine is SPELL COUNT

/// <summary>**STOKER** (Emberling): every spell you play adds this much MORE Kindle.</summary>
public record Stoker : GameComponent
{
	public int Extra { get; init; } = 1;
}

/// <summary>**ECHO** (Echo Owl): the first spell you play each turn is cast twice.</summary>
public record EchoFirstSpell : GameComponent;

/// <summary>**EMBERSKIN** (Cinder Newt): at your turn start it gains Block equal to your Kindle.</summary>
public record Emberskin : GameComponent;

/// <summary>**FINISHER, Ember's half** (Pike): its attacks deal this much more per Kindle.</summary>
public record KindleFinisher : GameComponent
{
	public int PerKindle { get; init; } = 1;
}

// ===== Card steps

/// <summary>Graft: the monster it is dropped on gains Grow for this fight.</summary>
public record GraftAction : CardStep
{
	public override ActionResult Execute(GameState s)
	{
		var ally = Target(s);
		return ally.HasComponent<Grow>()
			? new(s)
			: new(
				s.UpdateObject(ally.Id, ally with { Components = ally.Components.Add(new Grow()) })
			);
	}
}

/// <summary>Overgrow: everything of yours with Grow grows now, this many times. Dropped on any monster.</summary>
public record GrowNowAction : CardStep
{
	public int Times { get; init; } = 1;

	public override ActionResult Execute(GameState s)
	{
		var events = ImmutableList<GameEvent>.Empty;
		for (var i = 0; i < Times; i++)
		{
			(s, var grew) = PartyFamilies.GrowAll(s);
			events = events.AddRange(grew);
		}
		return new ActionResult(s).WithEvents(events);
	}
}

/// <summary>Root: this much ROOTED Block — it does not vanish at your turn start.</summary>
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
		return new(
			s.UpdateObject(
				ally.Id,
				ally with
				{
					Block = ally.Block + ally.Rooted,
					Rooted = ally.Rooted * 2,
				}
			)
		);
	}
}

/// <summary>Thicket: each of your GROVE monsters gains Block equal to its Power. Dropped on any monster.</summary>
public record ThicketAction : CardStep
{
	public override ActionResult Execute(GameState s)
	{
		var events = ImmutableList<GameEvent>.Empty;
		foreach (var ally in s.LivingAllies().Where(a => a.Family == Family.Grove).ToList())
		{
			var amount = ally.Power + ally.BonusPower;
			if (amount <= 0)
				continue;
			s = s.UpdateObject(ally.Id, ally with { Block = ally.Block + amount });
			events = events.Add(new BlockGainedEvent { AllyId = ally.Id, Amount = amount });
		}
		return new ActionResult(s).WithEvents(events);
	}
}

/// <summary>
/// **HARVEST — Grove's big turn (an EXPERIMENT, `KinFamiliesPlan.md` §6)**: each of your tokens falls,
/// and each deals its HP to their front. Plain card, no hooks elsewhere — cheap to drop.
/// </summary>
public record HarvestAction : CardStep
{
	public override string? Refusal(GameState s, int space, bool foeRow) =>
		base.Refusal(s, space, foeRow)
		?? (s.LivingAllies().Any(a => a.FadesIn > 0) ? null : "You have no tokens to harvest");

	public override ActionResult Execute(GameState s)
	{
		var events = ImmutableList<GameEvent>.Empty;
		foreach (var token in s.LivingAllies().Where(a => a.FadesIn > 0).ToList())
		{
			if (s.GetParty().IsOver)
				break;
			var power = token.Hp;
			s = s.UpdateObject(token.Id, token with { Hp = 0 });
			s = PartySummon.TokenFainted(s, token);
			events = events.Add(new AllyKnockedOutEvent { AllyId = token.Id });
			if (s.LivingFoes().OrderBy(f => f.Position).FirstOrDefault() is { } front)
			{
				ImmutableList<GameEvent> hit;
				(s, hit) = PartyState.HitFoe(s, front, power);
				events = events.AddRange(hit);
			}
		}
		return new ActionResult(s).WithEvents(events);
	}
}

/// <summary>Stoke (and the Kindle every spell adds): this much Kindle.</summary>
public record KindleAction : GameAction
{
	public int Amount { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var party = s.GetParty();
		return new ActionResult(
			s.UpdateObject(party.Id, party with { Kindle = party.Kindle + Amount })
		).WithEvent(new KindleGainedEvent { Amount = Amount });
	}
}

/// <summary>
/// **KIN — a family card pays for each monster of its family standing in your line** (Shayne,
/// 2026-09-28: "cards pay on kin", scaled per kin, feeding the family's own engine). Grove: each
/// Grove kin grows once. Ember: +1 Kindle per Ember kin. Appended to the card's steps on play.
/// </summary>
public record KinGrowAction : GameAction
{
	public override ActionResult Execute(GameState s)
	{
		var events = ImmutableList<GameEvent>.Empty;
		foreach (var ally in s.KinOf(Family.Grove).ToList())
		{
			(s, var grew) = PartyFamilies.GrowOnce(s, ally, new Grow());
			events = events.AddRange(grew);
		}
		return new ActionResult(s).WithEvents(events);
	}
}

public record KindleGainedEvent : GameEvent
{
	public int Amount { get; init; }
}

public record GrewEvent : GameEvent
{
	public int AllyId { get; init; }
	public int Power { get; init; }
	public int Hp { get; init; }
}

/// <summary>Cinderwall: the monster it is dropped on gains Block equal to your Kindle.</summary>
public record CinderwallAction : CardStep
{
	public override ActionResult Execute(GameState s)
	{
		var ally = Target(s);
		var amount = s.GetParty().Kindle;
		s = s.UpdateObject(ally.Id, ally with { Block = ally.Block + amount });
		return new ActionResult(s).WithEvent(
			new BlockGainedEvent { AllyId = ally.Id, Amount = amount }
		);
	}
}

/// <summary>Fan the Flames: your next spell is cast twice.</summary>
public record FanFlamesAction : GameAction
{
	public override ActionResult Execute(GameState s)
	{
		var party = s.GetParty();
		return new(s.UpdateObject(party.Id, party with { NextSpellTwice = true }));
	}
}

/// <summary>
/// **FLASHPOINT — Ember's big turn (an EXPERIMENT)**: spend ALL your Kindle; the foe it is dropped
/// on takes this many times as much. Not a spell itself (it does not stoke). Plain card, cheap to drop.
/// </summary>
public record FlashpointAction : CardStep
{
	public int PerKindle { get; init; } = 3;

	public override string? Refusal(GameState s, int space, bool foeRow) =>
		!foeRow || s.FoeAt(space) is null ? "Drop it on a foe"
		: s.GetParty().Kindle == 0 ? "You have no Kindle to spend"
		: null;

	public override ActionResult Execute(GameState s)
	{
		var party = s.GetParty();
		var damage = party.Kindle * PerKindle;
		s = s.UpdateObject(party.Id, party with { Kindle = 0 });
		if (s.FoeAt(Space) is not { } foe)
			return new(s);
		var (after, events) = PartyState.HitFoe(s, foe, damage);
		return new ActionResult(after).WithEvents(events);
	}
}

/// <summary>The families' rules that run at fixed moments: turn start, a spell cast, a token falling.</summary>
public static class PartyFamilies
{
	/// <summary>Everything of yours with Grow grows once.</summary>
	public static (GameState, ImmutableList<GameEvent>) GrowAll(GameState s)
	{
		var events = ImmutableList<GameEvent>.Empty;
		foreach (var ally in s.LivingAllies().ToList())
		{
			var grow = ally.GetComponents<Grow>().ToList();
			if (grow.Count == 0)
				continue;
			(s, var grew) = GrowOnce(
				s,
				ally,
				new Grow { Power = grow.Sum(g => g.Power), Hp = grow.Sum(g => g.Hp) }
			);
			events = events.AddRange(grew);
		}
		return (s, events);
	}

	/// <summary>One monster grows by this much, now.</summary>
	public static (GameState, ImmutableList<GameEvent>) GrowOnce(GameState s, Ally ally, Grow by)
	{
		s = s.UpdateObject(
			ally.Id,
			ally with
			{
				Power = ally.Power + by.Power,
				MaxHp = ally.MaxHp + by.Hp,
				Hp = ally.Hp + by.Hp,
			}
		);
		return (
			s,
			[
				new GrewEvent
				{
					AllyId = ally.Id,
					Power = by.Power,
					Hp = by.Hp,
				},
			]
		);
	}

	/// <summary>Your KIN of a family: its REAL monsters standing in your line — tokens are not kin.</summary>
	public static IEnumerable<Ally> KinOf(this GameState s, Family family) =>
		family == Family.None
			? []
			: s.LivingAllies().Where(a => a.Family == family && a.FadesIn == 0);

	/// <summary>What a card of this family pays on top, now — nothing for a family without a kin rule yet.</summary>
	public static IEnumerable<GameAction> KinBonus(GameState s, Family family)
	{
		var kin = s.KinOf(family).Count();
		if (kin == 0)
			return [];
		return family switch
		{
			Family.Grove => [new KinGrowAction()],
			Family.Ember => [new KindleAction { Amount = kin }],
			_ => [],
		};
	}

	/// <summary>
	/// **Your turn starts**: Block drops to what is Rooted (all of it, for a Mossback); Grow grows;
	/// Emberskin shields from the Kindle.
	/// </summary>
	public static (GameState, ImmutableList<GameEvent>) TurnStart(GameState s, bool firstTurn)
	{
		var kindle = s.GetParty().Kindle;
		foreach (var ally in s.Allies().ToList())
		{
			var kept = ally.HasComponent<Mossback>()
				? ally.Block
				: Math.Min(ally.Block, ally.Rooted);
			var skin = !ally.IsDown && ally.HasComponent<Emberskin>() ? kindle : 0;
			s = s.UpdateObject(ally.Id, ally with { Block = kept + skin, Rooted = kept });
		}
		return firstTurn ? (s, []) : GrowAll(s);
	}

	/// <summary>How much Kindle one spell cast adds: 1, plus every Stoker standing.</summary>
	public static int KindlePerSpell(GameState s) =>
		1 + s.LivingAllies().SelectMany(a => a.GetComponents<Stoker>()).Sum(k => k.Extra);

	/// <summary>How many times a spell played now is cast: once, +1 for an Echo's first, +1 for Fan the Flames.</summary>
	public static int SpellCasts(GameState s)
	{
		var party = s.GetParty();
		var echo =
			party.SpellsThisTurn == 0
			&& s.LivingAllies().Any(a => a.HasComponent<EchoFirstSpell>());
		return 1 + (echo ? 1 : 0) + (party.NextSpellTwice ? 1 : 0);
	}

	/// <summary>A token of yours fell: every Spores standing draws and pays out.</summary>
	public static GameState TokenFell(GameState s)
	{
		var spores = s.LivingAllies().SelectMany(a => a.GetComponents<Spores>()).ToList();
		if (spores.Count == 0)
			return s;
		var party = s.GetParty();
		s = s.UpdateObject(
			party.Id,
			party with
			{
				Energy = party.Energy + spores.Sum(p => p.Energy),
			}
		);
		(s, _) = StartTurnAction.DrawCards(s, spores.Sum(p => p.Draw));
		return s;
	}

	/// <summary>A token summoned while a Nursery or an Alpha stands: Grow, and an attack each round.</summary>
	public static Ally Nurture(GameState s, Ally token)
	{
		var standing = s.LivingAllies().ToList();
		if (standing.Any(a => a.HasComponent<Nursery>()) && !token.HasComponent<Grow>())
			token = token with { Components = token.Components.Add(new Grow()) };
		if (standing.Any(a => a.HasComponent<Alpha>()))
			token = token with
			{
				Pattern =
				[
					new Intent
					{
						Name = "Pounce",
						Kind = IntentType.Attack,
						Amount = 0,
					},
				],
			};
		return token;
	}
}
