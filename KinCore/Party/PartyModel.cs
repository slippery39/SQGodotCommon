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

	/// <summary>**Snares carried into this battle** — the run's item for catching. Used ones are gone.</summary>
	public int Snares { get; init; }

	/// <summary>
	/// **YOUR health — the trainer's** (KinJam.md "TRAINER HEALTH"). A foe's attack that lands on no
	/// monster hits you, so stepping out of a blow is no longer free: waiting has a price. At 0 the
	/// battle — and the run — is lost.
	/// </summary>
	public int TrainerHp { get; init; }

	/// <summary>
	/// **The gym leader's health; 0 = no leader** (wild creatures have no trainer). The mirror of
	/// yours: your attack that lands on no foe hits the leader, and at 0 the gym is won.
	/// </summary>
	public int LeaderHp { get; init; }

	/// <summary>
	/// **Cards a card or ability discarded this turn** — MtgCore's `SpellsCastThisTurn` pattern:
	/// counted in ONE place (`FirePartyTriggersAction`) from the staged events, so no discard path
	/// can forget it. The end-of-turn discard is not counted. Scrap Hammer reads it.
	/// </summary>
	public int DiscardedThisTurn { get; init; }

	/// <summary>Spell damage dealt this turn, after bonuses and wards — what Overload deals.</summary>
	public int SpellDamageThisTurn { get; init; }

	/// <summary>Focus was played: this turn's dropped spells also hit the foes beside the target.</summary>
	public bool SpellsSplash { get; init; }

	/// <summary>The last spell cast this turn, with where it was dropped — what an Echo repeats.</summary>
	public SpellDamageAction? LastSpell { get; init; }

	// ===== SURGE — energy and cost (PartySurge.cs)

	/// <summary>Borrowed energy (Surge): taken off the NEXT turn's energy.</summary>
	public int EnergyDebt { get; init; }

	/// <summary>Quicken was played: the next card costs 0.</summary>
	public bool NextCardFree { get; init; }

	/// <summary>Cards played this turn — "the first card each turn" (the Hushcap) reads it.</summary>
	public int CardsPlayedThisTurn { get; init; }

	/// <summary>What the last X card paid — its X.</summary>
	public int XPaid { get; init; }

	/// <summary>Foes killed DURING your turn (Battle Cry). The end of the turn does not count.</summary>
	public int FoesDefeatedThisTurn { get; init; }

	/// <summary>
	/// **True while the end of the turn resolves.** A kill then is not "during your turn": its energy
	/// would arrive with nothing left to spend it on.
	/// </summary>
	public bool EndingTurn { get; init; }

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

	/// <summary>
	/// **A TOKEN: your turn starts left before it fades** (`PartySummon.Fade`). 0 = a real
	/// creature, which never fades — and only real monsters keep a battle alive.
	/// </summary>
	public int FadesIn { get; init; }
}

/// <summary>One of YOUR monsters. It fights on its own; your cards move, buff and time it.</summary>
public record Ally : Creature
{
	/// <summary>Its place in the run's team — how the run finds it again after the battle.</summary>
	public int Slot { get; init; }

	/// <summary>Added to this monster's attacks.</summary>
	public int Power { get; init; }

	/// <summary>Power added by cards this turn (Rally). Cleared when your next turn starts.</summary>
	public int BonusPower { get; init; }

	/// <summary>**The free step**: one every turn, more from Dash. A step into an ally swaps the two.</summary>
	public int StepsLeft { get; init; }

	/// <summary>Played its move early this turn (Hasten), so it does not act again at the end.</summary>
	public bool HasActed { get; init; }

	/// <summary>
	/// **On the bench: off the board, waiting.** The first one steps into a fainted monster's space,
	/// free — a fainted monster no longer leaves its column open to hit you for the rest of the fight.
	/// </summary>
	public bool Benched { get; init; }

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

	/// <summary>Attacked since your last turn began (blocked or not) — the Glowmoth's condition.</summary>
	public bool WasHit { get; init; }

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

	/// <summary>False for a boss: an exam is not a catch.</summary>
	public bool Catchable { get; init; } = true;

	/// <summary>**Caught by a Snare** — off the board, beaten, and joining you when the battle is won.</summary>
	public bool Caught { get; init; }

	/// <summary>
	/// **A WILD trait, in words** — what its own abilities (triggers in `Components`) do while it
	/// stands, e.g. the Hoard Drake's Block. "" = none. Shown by the inspector.
	/// </summary>
	public string Trait { get; init; } = "";

	// ===== What it brings when CAUGHT — dormant while wild (KinJam.md: a wild creature shows only
	// its cycle; its deck ability wakes when caught). `PartyRun.FromFoe` copies them.

	public string CaughtPassive { get; init; } = "";
	public string CaughtRule { get; init; } = "";
	public ImmutableList<GameComponent> CaughtAbilities { get; init; } = [];

	/// <summary>Its monster deck, once it is yours.</summary>
	public ImmutableList<KinCard> CaughtCards { get; init; } = [];

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

	/// <summary>
	/// **Repeats the last spell you cast this turn**, on the same drop — the Echo Owl. It needs a
	/// trainer, so a wild one's Echo does nothing.
	/// </summary>
	Echo,

	/// <summary>Summons <see cref="Intent.Summons"/> into the nearest empty space on its own row.</summary>
	Summon,
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

	/// <summary>
	/// **THIEF** (a wild trait): after the attack, a FOE takes the top card of your draw pile, and
	/// holds it until it is beaten or caught. A caught thief loses it (`PartyRun.FromFoe`).
	/// </summary>
	public bool Steals { get; init; }

	/// <summary>What a Summon move brings — the Broodvine's Grub.</summary>
	public TokenTemplate? Summons { get; init; }
}
