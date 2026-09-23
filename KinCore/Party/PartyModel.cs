using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **THE COMPANION GAME — the first slice (2026-09-23).** Built BESIDE the lane/unit game rather
/// than over it, so the old game keeps running until this one proves out. Rules: "THE COMPANION
/// GAME" at the top of `KinJam.md`; the scenario is `docs/paper/companion-slice.md`.
///
/// The root of one battle. Your companions and the foes are its children; the deck zones hang off
/// it too, under the same well-known keys the old game uses, so the shared draw code still works.
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

	/// <summary>
	/// The companion that played Draw Fire this turn, or 0. Single-target attacks on a companion
	/// BESIDE it hit it instead. Cleared when your next turn starts.
	/// </summary>
	public int DrawFireAllyId { get; init; }
}

/// <summary>
/// One of YOUR companions. **Companions do nothing without cards** — every action they take is a
/// card they own, except the free move.
/// </summary>
public record Ally : GameObject
{
	public int Hp { get; init; }
	public int MaxHp { get; init; }

	/// <summary>Added to this companion's attack cards.</summary>
	public int Power { get; init; }

	/// <summary>
	/// **The move cooldown, capped at 3.** Speed 3 moves every turn, 2 every other turn, 1 every
	/// third. See <see cref="MoveAllyAction"/>.
	/// </summary>
	public int Speed { get; init; }

	public int Space { get; init; }

	/// <summary>Soaks damage before HP. Cleared when your next turn starts.</summary>
	public int Block { get; init; }

	/// <summary>Turns until the free move is ready. 0 = ready now.</summary>
	public int MoveReadyIn { get; init; }

	/// <summary>The passive, as the player reads it. Rules live in the fields below, never here.</summary>
	public string Passive { get; init; } = "";

	/// <summary>The passive's rule in a sentence — shown when the companion is clicked.</summary>
	public string PassiveRule { get; init; } = "";

	/// <summary>
	/// **Thorns — a foe that ATTACKS this companion takes this much back**, blocked or not. Bramble's
	/// passive: it pays only when she is struck, so it ENDS fights rather than stalling them.
	/// </summary>
	public int Thorns { get; init; }

	/// <summary>Thorns added by cards this turn. Cleared when your next turn starts.</summary>
	public int BonusThorns { get; init; }

	/// <summary>
	/// **Momentum — each step this companion takes adds this to its NEXT attack this turn.** Pike's
	/// passive: the decision is the route. Free moves and card steps both count.
	/// </summary>
	public int MomentumPerStep { get; init; }

	/// <summary>Built by steps, spent by the next attack, cleared when your next turn starts.</summary>
	public int Momentum { get; init; }

	/// <summary>
	/// **Off-Balance — a foe this companion MOVES takes this much extra from every hit this turn.**
	/// Gale's passive: the Controller sets up the others' hits, so the ORDER of plays is the decision.
	/// </summary>
	public int Unbalances { get; init; }

	public int TotalThorns => Thorns + BonusThorns;

	public bool IsKnockedOut => Hp <= 0;

	/// <summary>One step to a space — the ONE place a step builds Momentum, card or free move.</summary>
	public Ally SteppedTo(int space) =>
		this with
		{
			Space = space,
			Momentum = Momentum + MomentumPerStep,
		};
}

public enum IntentType
{
	/// <summary>Damage to the spaces its shape covers, or to one companion if it homes.</summary>
	Attack,

	/// <summary>Gains Block, which holds through your next turn.</summary>
	Block,

	/// <summary>Steps <see cref="Intent.Amount"/> spaces (negative = left), if the space is free.</summary>
	Move,
}

/// <summary>
/// One telegraphed action. **Foes never play from a deck** — they cycle a fixed pattern of these,
/// and the next one is always visible.
/// </summary>
public record Intent
{
	public string Name { get; init; } = "";
	public IntentType Kind { get; init; }
	public int Amount { get; init; }

	/// <summary>
	/// Columns hit, relative to the foe's own. [0] is straight ahead; [-1, 0, 1] is three wide —
	/// and the middle of a three-wide attack cannot step out of it in one move.
	/// </summary>
	public ImmutableList<int> Offsets { get; init; } = [0];

	/// <summary>Ignores the shape: hits the companion with the LOWEST HP, wherever it stands.</summary>
	public bool Homing { get; init; }
}

public record Foe : GameObject
{
	public int Hp { get; init; }
	public int MaxHp { get; init; }
	public int Block { get; init; }
	public int Space { get; init; }
	public ImmutableList<Intent> Pattern { get; init; } = [];
	public int PatternIndex { get; init; }

	/// <summary>Extra damage this foe takes from every hit, from being moved. Cleared at your turn start.</summary>
	public int OffBalance { get; init; }

	public Intent Current => Pattern[PatternIndex % Pattern.Count];
	public bool IsDead => Hp <= 0;
}

/// <summary>
/// **Which companion a card belongs to.** One combined deck, and only the owner can play its cards
/// — so a knocked-out companion's cards are dead draws. The name rides along for the card face.
/// </summary>
public record OwnedBy : GameComponent
{
	public int AllyId { get; init; }
	public string AllyName { get; init; } = "";
}
