using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **A TOKEN, as summoned: a simple creature that FADES** (KinJam.md: tokens fade, so free bodies
/// cannot fill the empty columns and undo trainer health). It takes a column, steps and swaps like
/// any monster, cannot be caught or healed, never counts toward losing, and is gone after
/// `FadesIn` of your turn starts.
/// </summary>
public record TokenTemplate(PartyCompanion Creature, int FadesIn);

/// <summary>
/// **Summons tokens at the FRONT of your line** (R11) — MtgCore's `CreateCardAction` shape: a
/// template and a count. Everyone else steps back; a full line takes no more.
/// </summary>
public record SummonTokenAction : CardStep
{
	public TokenTemplate Token { get; init; } = null!;
	public int Count { get; init; } = 1;

	/// <summary>
	/// **Dropped on your FRONT — where the token will stand.** Lighting every place of your line
	/// promised a choice of place the summon does not give.
	/// </summary>
	public override string? Refusal(GameState s, int space, bool foeRow) =>
		foeRow || space != 0 ? "Drop it on your front: that is where it arrives"
		: s.LivingAllies().Count() >= PartyBattle.MaxLine ? "Your line is full"
		: null;

	public override ActionResult Execute(GameState s)
	{
		for (var i = 0; i < Count; i++)
			s = PartySummon.SummonAlly(s, Token);
		return new(s);
	}
}

/// <summary>Swarm: each of your tokens attacks ahead now.</summary>
public record TokensAttackAction : GameAction
{
	public int Amount { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var events = ImmutableList<GameEvent>.Empty;
		foreach (var token in s.LivingAllies().Where(a => a.FadesIn > 0).ToList())
		{
			if (s.GetParty().IsOver || s.GetObject(token.Id) is not Ally { IsDown: false } now)
				continue;
			ImmutableList<GameEvent> hit;
			(s, hit) = PartyState.AttackFoes(s, now, Amount, PartyState.AimAt(s, now, Aim.Front));
			events = events.AddRange(hit);
		}
		return new ActionResult(s).WithEvents(events);
	}
}

/// <summary>Offering: dropped on one of your tokens, it faints — and its faint effects happen.</summary>
public record SacrificeTokenAction : CardStep
{
	public override string? Refusal(GameState s, int space, bool foeRow) =>
		!foeRow && s.AllyAt(space) is { FadesIn: > 0 } ? null : "Drop it on one of your tokens";

	public override ActionResult Execute(GameState s)
	{
		// Settled (out of the line) by the post-processor, like any fall.
		var token = Target(s);
		s = s.UpdateObject(token.Id, token with { Hp = 0 });
		return new ActionResult(PartySummon.TokenFainted(s, token)).WithEvent(
			new AllyKnockedOutEvent { AllyId = token.Id }
		);
	}
}

/// <summary>**When this token faints, the monsters beside it gain Block** — the Sprout.</summary>
public record FaintShield : GameComponent
{
	public int Amount { get; init; }
}

/// <summary>**BACK and HUNT attacks aim at this one** — the Decoy.</summary>
public record Lure : GameComponent;

/// <summary>**Your tokens arrive stronger** — the Howler. Read when a token is summoned.</summary>
public record TokenBoost : GameComponent
{
	public int Hp { get; init; }
	public int Power { get; init; }
}

/// <summary>
/// **TRAMPLE: damage beyond what fells the target carries on into the one BEHIND it** — the
/// Ironhorn's, wild or caught. A token wall in front is paper to it.
/// </summary>
public record Trample : GameComponent;

public static class PartySummon
{
	/// <summary>A token of yours at the FRONT; a full line takes no more.</summary>
	public static GameState SummonAlly(GameState s, TokenTemplate token)
	{
		if (s.LivingAllies().Count() >= PartyBattle.MaxLine)
			return s;
		var boost = s.LivingAllies().SelectMany(a => a.GetComponents<TokenBoost>()).ToList();
		var c = token.Creature;
		var hp = c.Hp + boost.Sum(b => b.Hp);
		var ally = new Ally
		{
			Slot = -1,
			Name = c.Name,
			Hp = hp,
			MaxHp = hp,
			Power = c.Power + boost.Sum(b => b.Power),
			Pattern = c.Moves,
			FadesIn = token.FadesIn,
			Family = c.Family,
			Components = [.. c.Abilities],
		};
		return PartyState.InsertAtFront(s, PartyFamilies.Nurture(s, ally), foes: false);
	}

	/// <summary>A wild creature's brood: an uncatchable foe at the FRONT of their line, fading like any token.</summary>
	public static GameState SummonFoe(GameState s, TokenTemplate token)
	{
		if (s.LivingFoes().Count() >= PartyBattle.MaxLine)
			return s;
		var c = token.Creature;
		var level = s.LivingFoes().Select(f => f.Level).DefaultIfEmpty(PartyLevels.Base).Max();
		return PartyState.InsertAtFront(
			s,
			PartyLevels.Scale(
				new Foe
				{
					Name = c.Name,
					Hp = c.Hp,
					MaxHp = c.Hp,
					Pattern = c.Moves,
					FadesIn = token.FadesIn,
					// FadesIn 0 = a MINION that stays until it is beaten (a boss's band).
					Trait =
						token.FadesIn > 0
							? $"A {c.Name.ToUpperInvariant()}: fades in {token.FadesIn} turns."
							: $"A {c.Name.ToUpperInvariant()}, summoned.",
				},
				level
			),
			foes: true
		);
	}

	/// <summary>A token fell (hit, or offered): the Sprout's shield goes to the ones ahead of and behind it.</summary>
	public static GameState TokenFainted(GameState s, Ally token)
	{
		s = PartyFamilies.TokenFell(s);
		var shield = token.GetComponents<FaintShield>().Sum(f => f.Amount);
		if (shield == 0)
			return s;
		foreach (
			var ally in s.LivingAllies()
				.Where(a => Math.Abs(a.Position - token.Position) == 1)
				.ToList()
		)
			s = s.UpdateObject(ally.Id, ally with { Block = ally.Block + shield });
		return s;
	}

	/// <summary>
	/// **Tokens fade at the start of your turn** — each start takes one off; at 0 it is gone. A
	/// wild brood fading can end the fight — `PartyState.Settle` decides that.
	/// </summary>
	public static GameState Fade(GameState s)
	{
		foreach (
			var c in s.LivingAllies()
				.Cast<Creature>()
				.Concat(s.LivingFoes())
				.Where(c => c.FadesIn > 0)
				.ToList()
		)
			s = s.UpdateObject(
				c.Id,
				c with
				{
					// Never to 0: 0 means a REAL monster, and a real one falling calls the bench.
					FadesIn = Math.Max(1, c.FadesIn - 1),
					Hp = c.FadesIn == 1 ? 0 : c.Hp,
				}
			);
		return s;
	}
}
