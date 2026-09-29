using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **SPELL = a card that deals damage by itself** (docs/paper/round-one-synergies.md) — not a
/// monster's attack, so it needs no aim: the answer to homing and moving foes. Strike ("it attacks
/// now") stays Combat. Every spell's damage goes through <see cref="PartySpells.SpellDamageTo"/>.
/// </summary>
public enum SpellTarget
{
	/// <summary>Dropped on a foe.</summary>
	Foe,

	/// <summary>Every living foe.</summary>
	All,

	/// <summary>A random living foe (a TOSS has nowhere to be dropped).</summary>
	Random,

	/// <summary>The foe behind the one it was dropped on — Meteor's splash.</summary>
	Behind,
}

public record SpellDamageAction : CardStep
{
	public int Amount { get; init; }
	public SpellTarget Target { get; init; }

	/// <summary>Overload: the amount is all the spell damage already dealt this turn.</summary>
	public bool FromSpellDamageThisTurn { get; init; }

	public override string? Refusal(GameState s, int space, bool foeRow) =>
		Target != SpellTarget.Foe || (foeRow && s.FoeAt(space) is not null)
			? null
			: "Drop it on a foe";

	public override ActionResult Execute(GameState s)
	{
		var party = s.GetParty();
		var amount = FromSpellDamageThisTurn ? party.SpellDamageThisTurn : Amount;

		List<Foe> targets = Target switch
		{
			SpellTarget.All => [.. s.LivingFoes()],
			SpellTarget.Random => RandomFoe(ref s),
			SpellTarget.Behind => [.. s.LivingFoes().Where(f => f.Position == Space + 1)],
			// Focus: this turn, a dropped spell also hits the foe behind its target.
			_ when s.FoeAt(Space) is { } foe => party.SpellsSplash
				?
				[
					.. s.LivingFoes()
						.Where(f => f.Position == foe.Position || f.Position == foe.Position + 1),
				]
				: [foe],
			_ => [],
		};

		// Remembered for the Echo Owl, with the drop it was played on. A splash is not a spell of
		// its own: Meteor's echo is the Meteor.
		party = s.GetParty();
		if (Target != SpellTarget.Behind)
			s = s.UpdateObject(party.Id, party with { LastSpell = this });

		var events = new List<GameEvent>();
		foreach (var foe in targets)
		{
			if (s.GetParty().IsOver || ((Foe)s.GetObject(foe.Id)).IsDead)
				continue;
			var damage = s.SpellDamageTo((Foe)s.GetObject(foe.Id), amount);
			var (after, hit) = PartyState.HitFoe(s, (Foe)s.GetObject(foe.Id), damage);
			var counted = after.GetParty();
			s = after.UpdateObject(
				counted.Id,
				counted with
				{
					SpellDamageThisTurn = counted.SpellDamageThisTurn + damage,
				}
			);
			events.AddRange(hit);
		}
		return new ActionResult(s).WithEvents(events);
	}

	private static List<Foe> RandomFoe(ref GameState s)
	{
		var foes = s.LivingFoes().ToList();
		if (foes.Count == 0)
			return [];
		var rng = new Random(s.RngSeed);
		var pick = foes[rng.Next(foes.Count)];
		s = s with { RngSeed = rng.Next() };
		return [pick];
	}
}

/// <summary>Focus: for the rest of the turn, your dropped spells also hit the foe behind the target.</summary>
public record SplashSpellsAction : GameAction
{
	public override ActionResult Execute(GameState s)
	{
		var party = s.GetParty();
		return new(s.UpdateObject(party.Id, party with { SpellsSplash = true }));
	}
}

/// <summary>**Your spells deal +N while this monster stands** — the Emberling.</summary>
public record SpellPower : GameComponent
{
	public int Amount { get; init; }
}

/// <summary>**Spells deal half to this foe** — the Warden's wild trait.</summary>
public record SpellWard : GameComponent;

/// <summary>A card with a spell in it was played. Staged by `PlayPartyCardAction`.</summary>
public record SpellPlayedEvent : GameEvent;

public record OnSpellPlayed : TriggerRule
{
	public override bool Matches(GameEvent e, GameState s, int sourceId) => e is SpellPlayedEvent;
}

public static class PartySpells
{
	/// <summary>
	/// **What a spell deals to this foe** — MtgCore's ReplacementEngine shape: the modifiers are
	/// components, scanned live here at the point of damage, never cached, and clamped at 0.
	/// **Your bonuses first, then the target's ward**, so a Warden halves the Emberling's +2 too
	/// (MtgCore applies multipliers first; the Warden's question needs the other order).
	/// </summary>
	/// <summary>What every spell deals on top, now — before any foe's ward. The hand shows it live.</summary>
	public static int SpellBonus(this GameState s) =>
		s.LivingAllies().Sum(a => a.SpellPower + a.BonusSpellPower)
		+ s.LivingAllies().SelectMany(a => a.GetComponents<SpellPower>()).Sum(p => p.Amount)
		+ s.GetParty().Kindle;

	public static int SpellDamageTo(this GameState s, Foe foe, int amount)
	{
		amount += s.SpellBonus();
		if (foe.HasComponent<SpellWard>())
			amount /= 2;
		return Math.Max(0, amount);
	}

	public static bool IsSpell(this KinCard card) =>
		card.Effects.Any(e => e.Template is SpellDamageAction);
}
