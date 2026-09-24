using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **THE COMPANION GAME — AUTO-BATTLE v1 (2026-09-24).** Built BESIDE the lane/unit game rather
/// than over it, so the old game keeps running until this one proves out. Rules: "AUTO-BATTLE v1" at
/// the top of `KinJam.md`.
///
/// The root of one battle. Your monsters and the foes are its children; the deck zones hang off it
/// too, under the same well-known keys the old game uses, so the shared draw code still works.
/// </summary>
public record PartyBattle : GameObject
{
	/// <summary>Spaces per side. Column N faces column N.</summary>
	public const int Spaces = 5;

	public int TurnNumber { get; init; } = 1;
	public int Energy { get; init; }
	public int MaxEnergy { get; init; } = 3;
	public bool IsOver { get; init; }
	public bool Won { get; init; }
}

/// <summary>
/// **Anything on the board — your monster or a foe. Both play a telegraphed CYCLE of moves on their
/// own**, at the end of your turn, one creature at a time in Speed order. A foe's cycle is exactly
/// what it would bring if caught.
/// </summary>
public abstract record Creature : GameObject
{
	public int Hp { get; init; }
	public int MaxHp { get; init; }
	public int Space { get; init; }

	/// <summary>Soaks damage before HP.</summary>
	public int Block { get; init; }

	/// <summary>**Turn order**: the fastest acts first, both sides interleaved. Ties: yours first.</summary>
	public int Speed { get; init; }

	public ImmutableList<Intent> Pattern { get; init; } = [];
	public int PatternIndex { get; init; }

	/// <summary>The move it plays at the end of this turn — the telegraph.</summary>
	public Intent Current => Pattern[PatternIndex % Pattern.Count];

	public bool IsDown => Hp <= 0;
}

/// <summary>One of YOUR monsters. It fights on its own; your cards move, buff and time it.</summary>
public record Ally : Creature
{
	/// <summary>Added to this monster's attacks.</summary>
	public int Power { get; init; }

	/// <summary>Power added by cards this turn (Rally). Cleared when your next turn starts.</summary>
	public int BonusPower { get; init; }

	/// <summary>**The free step**: one every turn, more from Dash. A step into an ally swaps the two.</summary>
	public int StepsLeft { get; init; }

	/// <summary>Played its move early this turn (Hasten), so it does not act again at the end.</summary>
	public bool HasActed { get; init; }

	/// <summary>The passive, as the player reads it. Rules live in the fields below, never here.</summary>
	public string Passive { get; init; } = "";

	/// <summary>The passive's rule in a sentence — shown when the monster is clicked.</summary>
	public string PassiveRule { get; init; } = "";

	/// <summary>
	/// **Thorns — a foe that ATTACKS this monster takes this much back**, blocked or not. Bramble's
	/// passive: it pays only when she is struck, so it ENDS fights rather than stalling them.
	/// </summary>
	public int Thorns { get; init; }

	/// <summary>Thorns added by cards this turn. Cleared when your next turn starts.</summary>
	public int BonusThorns { get; init; }

	/// <summary>
	/// **Momentum — each step this monster takes adds this to its NEXT attack.** Pike's passive: the
	/// free step is damage, so where Pike ends the turn is chosen, not drifted into.
	/// </summary>
	public int MomentumPerStep { get; init; }

	/// <summary>Built by steps, spent by the next attack, cleared when your next turn starts.</summary>
	public int Momentum { get; init; }

	/// <summary>
	/// **Off-Balance — while this monster stands, any foe you move takes this much extra from every
	/// hit that turn.** Gale's passive: a push is set-up for everyone, so ORDER matters — Gale's own
	/// Gust helps only the monsters slower than it.
	/// </summary>
	public int Unbalances { get; init; }

	public int TotalThorns => Thorns + BonusThorns;

	public bool IsKnockedOut => IsDown;

	/// <summary>What one of its attacks deals: the move's amount plus Power, Rally and Momentum.</summary>
	public int AttackFor(int amount) => amount + Power + BonusPower + Momentum;
}

public record Foe : Creature
{
	/// <summary>Extra damage this foe takes from every hit, from being moved. Cleared at your turn start.</summary>
	public int OffBalance { get; init; }

	/// <summary>Stagger: it loses its next move (the cycle still advances).</summary>
	public bool Staggered { get; init; }

	public bool IsDead => IsDown;
}

public enum IntentType
{
	/// <summary>Damage to the spaces its shape covers on the OTHER row, or to one creature if it homes.</summary>
	Attack,

	/// <summary>Gains Block. A foe's holds through your next turn.</summary>
	Block,

	/// <summary>Steps <see cref="Intent.Amount"/> spaces (negative = left), if the space is free.</summary>
	Move,

	/// <summary>Pushes the foe AHEAD <see cref="Intent.Amount"/> columns (negative = left) — Gale's Gust.</summary>
	Push,
}

/// <summary>One telegraphed move. **Nothing on the board plays from a deck but you.**</summary>
public record Intent
{
	public string Name { get; init; } = "";
	public IntentType Kind { get; init; }
	public int Amount { get; init; }

	/// <summary>
	/// Columns hit, relative to the creature's own. [0] is straight ahead; [-1, 0, 1] is three wide —
	/// and the middle of a three-wide attack cannot step out of it in one move.
	/// </summary>
	public ImmutableList<int> Offsets { get; init; } = [0];

	/// <summary>Ignores the shape: hits the creature with the LOWEST HP on the other side.</summary>
	public bool Homing { get; init; }
}
