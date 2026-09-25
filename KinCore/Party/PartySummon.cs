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
/// **Summons tokens onto your row** — MtgCore's `CreateCardAction` shape: a template and a count.
/// The first lands on the space it was dropped on, the rest on the nearest empty spaces.
/// </summary>
public record SummonTokenAction : CardStep
{
	public TokenTemplate Token { get; init; } = null!;
	public int Count { get; init; } = 1;

	public override string? Refusal(GameState s, int space, bool foeRow) =>
		!foeRow && space >= 0 && s.AllyAt(space) is null
			? null
			: "Drop it on an empty space of yours";

	public override ActionResult Execute(GameState s)
	{
		for (var i = 0; i < Count; i++)
			if (PartySummon.NearestEmpty(s, Space, ally: true) is { } space)
				s = PartySummon.SummonAlly(s, Token, space);
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
			(s, hit) = PartyState.AttackFoes(s, now, Amount, [now.Space]);
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

/// <summary>**Homing attacks aim at this one** — the Decoy.</summary>
public record Lure : GameComponent;

/// <summary>**Your tokens arrive stronger** — the Howler. Read when a token is summoned.</summary>
public record TokenBoost : GameComponent
{
	public int Hp { get; init; }
	public int Power { get; init; }
}

/// <summary>
/// **TRAMPLE: damage beyond what fells the target carries on** — a foe's to YOU (the Ironhorn,
/// wild), your monster's to a random other foe (caught). One component; the side decides.
/// </summary>
public record Trample : GameComponent;

public static class PartySummon
{
	/// <summary>The drop space if empty, else the nearest empty space on that row; null if full.</summary>
	public static int? NearestEmpty(GameState s, int from, bool ally) =>
		Enumerable
			.Range(0, PartyBattle.Spaces)
			.Where(c => ally ? s.AllyAt(c) is null : s.FoeAt(c) is null)
			.OrderBy(c => Math.Abs(c - from))
			.ThenBy(c => c)
			.Cast<int?>()
			.FirstOrDefault();

	public static GameState SummonAlly(GameState s, TokenTemplate token, int space)
	{
		var boost = s.LivingAllies().SelectMany(a => a.GetComponents<TokenBoost>()).ToList();
		var c = token.Creature;
		var hp = c.Hp + boost.Sum(b => b.Hp);
		(s, _) = s.AddObject(
			new Ally
			{
				Slot = -1,
				Name = c.Name,
				Hp = hp,
				MaxHp = hp,
				Power = c.Power + boost.Sum(b => b.Power),
				Speed = c.Speed,
				Space = space,
				Pattern = c.Moves,
				FadesIn = token.FadesIn,
				Components = [.. c.Abilities],
			},
			s.GetWellKnownId(PartyState.BattleKey)
		);
		return s;
	}

	/// <summary>A wild creature's brood: an uncatchable foe that fades like any token.</summary>
	public static GameState SummonFoe(GameState s, TokenTemplate token, int space)
	{
		var c = token.Creature;
		(s, _) = s.AddObject(
			new Foe
			{
				Name = c.Name,
				Hp = c.Hp,
				MaxHp = c.Hp,
				Speed = c.Speed,
				Space = space,
				Pattern = c.Moves,
				FadesIn = token.FadesIn,
				Catchable = false,
				Trait = $"A {c.Name.ToUpperInvariant()}: fades in {token.FadesIn} turns.",
			},
			s.GetWellKnownId(PartyState.BattleKey)
		);
		return s;
	}

	/// <summary>A token fainted (hit, or offered): the Sprout's shield goes to its neighbours.</summary>
	public static GameState TokenFainted(GameState s, Ally token)
	{
		var shield = token.GetComponents<FaintShield>().Sum(f => f.Amount);
		if (shield == 0)
			return s;
		foreach (
			var ally in s.LivingAllies().Where(a => Math.Abs(a.Space - token.Space) == 1).ToList()
		)
			s = s.UpdateObject(ally.Id, ally with { Block = ally.Block + shield });
		return s;
	}

	/// <summary>
	/// **Tokens fade at the start of your turn** — each start takes one off; at 0 it is gone. A
	/// wild brood fading can end the fight.
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
					FadesIn = c.FadesIn - 1,
					Hp = c.FadesIn == 1 ? 0 : c.Hp,
				}
			);

		if (!s.LivingFoes().Any() && s.GetParty() is { IsOver: false } party)
			s = s.UpdateObject(party.Id, party with { IsOver = true, Won = true });
		return s;
	}
}
