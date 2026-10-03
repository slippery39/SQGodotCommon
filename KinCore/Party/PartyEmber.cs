using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

// **EMBER — spellslinging** (`KinFamiliesPlan.md`, "EMBER — the family draft", DRAFT 2 approved by
// Shayne, 2026-09-28). Four archetypes that bridge: STACK Spell Power then nuke; CHAIN many spells in a
// turn; BURN then detonate; bank ENERGY then dump it. The mechanics are here; the cards and monsters
// in `EmberCards`.

// ===== Monsters' passives

/// <summary>**SPELLBLADE** (Pike): its attacks add your Spell Power.</summary>
public record Spellblade : GameComponent;

/// <summary>**SMOULDER** (Cinder Newt): your spells apply this much Burn to each foe they hit.</summary>
public record Smoulder : GameComponent
{
	public int Burn { get; init; } = 1;
}

/// <summary>**ECHO** (Echo Owl): your Nth spell each turn is cast twice.</summary>
public record EchoNthSpell : GameComponent
{
	public int Nth { get; init; } = 3;
}

/// <summary>**BANK** (Ironhorn): up to this much unspent energy carries into your next turn.</summary>
public record Bank : GameComponent
{
	public int Most { get; init; } = 2;
}

// ===== AURAS — a rule on your side for the rest of the fight (STS's powers)

/// <summary>An aura: held by the battle, not a monster — it outlives any of them.</summary>
public abstract record Aura : GameComponent
{
	public abstract string Name { get; }
}

/// <summary>INNER FIRE: at the start of each of your turns, +1 Spell Power for the rest of the fight.</summary>
// ===== EVOLVED PASSIVES (KinFamiliesPlan.md, round 5)

/// <summary>**REACH** (Lancepike): its attacks aimed at the front hit the front TWO.</summary>
public record Reach : GameComponent;

/// <summary>**SHELTERED** (Flamekin): this much Spell Power while it is not at the front.</summary>
public record Sheltered : GameComponent
{
	public int Amount { get; init; } = 2;
}

/// <summary>**CINDERFALL** (Cinder Drake): a Burning foe that falls passes its Burn to the one behind.</summary>
public record Cinderfall : GameComponent;

/// <summary>**CADENCE** (Echo Strix): your Nth spell each turn costs 0.</summary>
public record Cadence : GameComponent
{
	public int Nth { get; init; } = 3;
}

public record InnerFireAura : Aura
{
	public override string Name => "Inner Fire";
}

/// <summary>EVERBURN: Burn no longer drops at the foe's turn.</summary>
public record EverburnAura : Aura
{
	public override string Name => "Everburn";
}

/// <summary>SPELLWEAVER: whenever you cast your 3rd spell in a turn, draw 2 and gain 1 energy.</summary>
public record SpellweaverAura : Aura
{
	public override string Name => "Spellweaver";
}

/// <summary>**Plays an aura**: it joins the battle, for the rest of the fight. Two of one stack.</summary>
public record AuraAction : GameAction
{
	public Aura Aura { get; init; } = null!;

	public override ActionResult Execute(GameState s)
	{
		var party = s.GetParty();
		return new(
			s.UpdateObject(party.Id, party with { Components = party.Components.Add(Aura) })
		);
	}
}

// ===== Spell Power

/// <summary>
/// **+Spell Power** — for this turn, or for the rest of the fight. **STOKER** (Emberling) adds more to
/// every gain a card makes.
/// </summary>
public record SpellPowerAction : GameAction
{
	public int Amount { get; init; }
	public bool ForFight { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var party = s.GetParty();
		var amount =
			Amount + s.LivingAllies().SelectMany(a => a.GetComponents<Stoker>()).Sum(k => k.Extra);
		s = s.UpdateObject(
			party.Id,
			ForFight
				? party with
				{
					FightSpellPower = party.FightSpellPower + amount,
				}
				: party with
				{
					TurnSpellPower = party.TurnSpellPower + amount,
				}
		);
		return new ActionResult(s).WithEvent(new SpellPowerGainedEvent { Amount = amount });
	}
}

public record SpellPowerGainedEvent : GameEvent
{
	public int Amount { get; init; }
}

// ===== BURN

/// <summary>
/// **Apply BURN** — to the foe it is dropped on, or to every foe. `Double` doubles a foe's Burn;
/// `RiseToHighest` lifts every foe's Burn to the highest among them (then `Amount` more each).
/// </summary>
public record BurnAction : CardStep
{
	public int Amount { get; init; }
	public bool All { get; init; }
	public bool Double { get; init; }
	public bool RiseToHighest { get; init; }

	public override bool NeedsTarget => !All && !RiseToHighest;

	public override string? Refusal(GameState s, int space, bool foeRow) =>
		!NeedsTarget || (foeRow && s.FoeAt(space) is not null) ? null : "Drop it on a foe";

	public override ActionResult Execute(GameState s)
	{
		var foes =
			All || RiseToHighest
				? s.LivingFoes().ToList()
				: [.. s.LivingFoes().Where(f => f.Position == Space)];
		var highest = s.LivingFoes().Select(f => f.Burn).DefaultIfEmpty(0).Max();
		foreach (var foe in foes)
		{
			var burn =
				Double ? foe.Burn * 2
				: RiseToHighest ? Math.Max(foe.Burn, highest) + Amount
				: foe.Burn + Amount;
			s = s.UpdateObject(foe.Id, foe with { Burn = burn });
		}
		return new(s);
	}
}

/// <summary>A foe's Burn ticked — the screen floats it.</summary>
public record FoeBurnedEvent : GameEvent
{
	public int FoeId { get; init; }
	public int Damage { get; init; }
}

// ===== Block that asks for an engine first

/// <summary>
/// **Ember's Block — big, but only once its engine runs** (Shayne: "it does not come easily"):
/// `Amount`, plus so much per Spell Power, per spell cast this turn, or per Burn on their line; or
/// `Bonus` more once `IfSpellsCast` spells have been cast this turn.
/// </summary>
public record EmberBlockAction : CardStep
{
	public int Amount { get; init; }
	public int PerSpellPower { get; init; }
	public int PerSpellThisTurn { get; init; }
	public int PerBurnOnTheirLine { get; init; }
	public int IfSpellsCast { get; init; }
	public int Bonus { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var ally = Target(s);
		var party = s.GetParty();
		var block =
			Amount
			+ PerSpellPower * s.SpellBonus()
			+ PerSpellThisTurn * party.SpellsThisTurn
			+ PerBurnOnTheirLine * s.LivingFoes().Sum(f => f.Burn)
			+ (IfSpellsCast > 0 && party.SpellsThisTurn >= IfSpellsCast ? Bonus : 0);
		s = s.UpdateObject(ally.Id, ally with { Block = ally.Block + block });
		return new ActionResult(s).WithEvent(
			new BlockGainedEvent { AllyId = ally.Id, Amount = block }
		);
	}
}

// ===== CHAINS

/// <summary>Spell Surge: your spells cost this much less for the rest of the turn.</summary>
public record SpellDiscountAction : GameAction
{
	public int Amount { get; init; } = 1;

	public override ActionResult Execute(GameState s)
	{
		var party = s.GetParty();
		return new(
			s.UpdateObject(party.Id, party with { SpellDiscount = party.SpellDiscount + Amount })
		);
	}
}

/// <summary>Fan the Flames: your next `Count` spells this turn are cast twice.</summary>
public record CastTwiceAction : GameAction
{
	public int Count { get; init; } = 2;

	public override ActionResult Execute(GameState s)
	{
		var party = s.GetParty();
		return new(
			s.UpdateObject(party.Id, party with { SpellsTwice = party.SpellsTwice + Count })
		);
	}
}

/// <summary>**Wildfire**: this card costs 0 once you have cast this many spells this turn.</summary>
public record FreeAfterSpells : GameComponent
{
	public int Spells { get; init; } = 2;
}

/// <summary>Kindling: this many copies of a card join your hand.</summary>
public record AddCardsAction : GameAction
{
	public KinCard Card { get; init; } = null!;
	public int Count { get; init; } = 1;

	public override ActionResult Execute(GameState s)
	{
		for (var i = 0; i < Count; i++)
			(s, _) = s.AddObject(Card, s.ZoneId(ZoneType.Hand));
		return new(s);
	}
}

/// <summary>
/// **FIRESTORM: every 0-cost spell in your discard pile is cast again, each at a random foe.** The cards
/// stay where they are. A spell is any card but an attack, so a Heat Surge gives its energy again and
/// a Flicker+ draws again; a step that must be dropped on your own monster (Block) is skipped, and an
/// X card (Meteor) is not cast — it has no energy to spend. Recasts do not count toward the chain.
/// </summary>
public record FirestormAction : GameAction
{
	public override ActionResult Execute(GameState s)
	{
		var events = ImmutableList<GameEvent>.Empty;
		var spells = s.CardsIn(ZoneType.Discard)
			.Where(c => c.Cost == 0 && c.IsSpell() && !c.HasComponent<SpendsAllEnergy>())
			.ToList();
		foreach (var spell in spells)
		{
			var foes = s.LivingFoes().ToList();
			if (foes.Count == 0 || s.GetParty().IsOver)
				break;
			var rng = new Random(s.RngSeed);
			var at = foes[rng.Next(foes.Count)].Position;
			s = s with { RngSeed = rng.Next() };
			foreach (var effect in spell.Effects)
			{
				var step = effect.Template is CardStep aimed
					? aimed with
					{
						Space = at,
						FoeRow = true,
					}
					: effect.Template;
				if (step is CardStep card && card.Refusal(s, at, true) is not null)
					continue;
				var cast = step.Execute(s);
				s = cast.GameState;
				events = events.AddRange(cast.Events);
			}
		}
		return new ActionResult(s).WithEvents(events);
	}
}

// ===== ENERGY

/// <summary>Charge Up: this much more energy when your next turn starts.</summary>
public record EnergyNextTurnAction : GameAction
{
	public int Amount { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var party = s.GetParty();
		return new(
			s.UpdateObject(party.Id, party with { EnergyNextTurn = party.EnergyNextTurn + Amount })
		);
	}
}

public static class PartyEmber
{
	/// <summary>The auras your side holds, for the screen.</summary>
	public static IEnumerable<Aura> Auras(this GameState s) => s.GetParty().GetComponents<Aura>();

	/// <summary>
	/// **BURN ticks as the foes' turn begins**: each burning foe takes its Burn, Block or not, then it
	/// drops by 1 — unless EVERBURN holds.
	/// </summary>
	public static (GameState, ImmutableList<GameEvent>) BurnTick(GameState s)
	{
		var events = ImmutableList<GameEvent>.Empty;
		var everburn = s.GetParty().HasComponent<EverburnAura>();
		foreach (var foe in s.LivingFoes().Where(f => f.Burn > 0).ToList())
		{
			var hurt = foe with
			{
				Hp = Math.Max(0, foe.Hp - foe.Burn),
				Burn = everburn ? foe.Burn : foe.Burn - 1,
			};
			s = s.UpdateObject(foe.Id, hurt);
			events = events.Add(new FoeBurnedEvent { FoeId = foe.Id, Damage = foe.Burn });
		}
		return (s, events);
	}

	/// <summary>SMOULDER: a spell that hit this foe leaves Burn on it.</summary>
	public static GameState Smoulder(GameState s, int foeId)
	{
		var burn = s.LivingAllies().SelectMany(a => a.GetComponents<Smoulder>()).Sum(m => m.Burn);
		return burn > 0 && s.GetObject(foeId) is Foe { IsDead: false } foe
			? s.UpdateObject(foe.Id, foe with { Burn = foe.Burn + burn })
			: s;
	}

	/// <summary>
	/// **A spell was just cast (the count already raised)**: ECHO on the Nth, SPELLWEAVER on the 3rd.
	/// Returns how many EXTRA casts it gets and the state with Spellweaver's draw and energy.
	/// </summary>
	public static (GameState, int Extra) SpellCast(GameState s)
	{
		var party = s.GetParty();
		var nth = party.SpellsThisTurn;
		var extra = s.LivingAllies()
			.SelectMany(a => a.GetComponents<EchoNthSpell>())
			.Count(e => e.Nth == nth);
		if (party.SpellsTwice > 0)
		{
			extra++;
			s = s.UpdateObject(party.Id, party with { SpellsTwice = party.SpellsTwice - 1 });
		}
		if (nth == 3)
			foreach (var weaver in party.GetComponents<SpellweaverAura>())
			{
				var now = s.GetParty();
				s = s.UpdateObject(now.Id, now with { Energy = now.Energy + 1 });
				(s, _) = StartTurnAction.DrawCards(s, 2);
			}
		return (s, extra);
	}
}
