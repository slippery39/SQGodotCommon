using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **THE COMPANION GAME — THE RELAY (2026-09-25).** One line a side; position 0 is the FRONT. At the
/// end of your turn the lines act in STEPS, back to front, both sides at once. Rules: `KinRelayPlan.md`.
///
/// The root of one battle. Your monsters and the foes are its children; the deck zones hang off it
/// too, under the same well-known keys the old game uses, so the shared draw code still works.
/// </summary>
public record PartyBattle : GameObject
{
	/// <summary>The longest a line can be — three monsters and room for tokens.</summary>
	public const int MaxLine = 5;

	public int TurnNumber { get; init; } = 1;
	public int Energy { get; init; }
	public int MaxEnergy { get; init; } = 3;

	/// <summary>The run's RELICS (`PartyRelics`) — read at the deal and when the fight begins.</summary>
	public ImmutableList<Relic> Relics { get; init; } = [];

	/// <summary>Cards drawn every turn beyond the hand's five (Ancient Lens).</summary>
	public int DrawBonus { get; init; }

	/// <summary>
	/// **Cards a card or ability discarded this turn** — MtgCore's `SpellsCastThisTurn` pattern:
	/// counted in ONE place (`FirePartyTriggersAction`) from the staged events, so no discard path
	/// can forget it. The end-of-turn discard is not counted. Scrap Hammer reads it.
	/// </summary>
	public int DiscardedThisTurn { get; init; }

	/// <summary>Spell damage dealt this turn, after bonuses and wards — what Overload deals.</summary>
	public int SpellDamageThisTurn { get; init; }

	/// <summary>Focus was played: this turn's dropped spells also hit the foe behind the target.</summary>
	public bool SpellsSplash { get; init; }

	/// <summary>The last spell cast this turn, with where it was dropped — what an Echo repeats.</summary>
	public SpellDamageAction? LastSpell { get; init; }

	// ===== EMBER (PartyFamilies.cs)

	/// <summary>
	/// **SPELL POWER for the rest of the fight**, from cards (Stoke, Inner Fire) — added to every spell.
	/// (Kindle, merged into Spell Power: Shayne, 2026-09-28.) `PartySpells.SpellBonus` sums it all.
	/// </summary>
	public int FightSpellPower { get; init; }

	/// <summary>Spell Power for THIS turn, from cards and first-attack bonuses.</summary>
	public int TurnSpellPower { get; init; }

	/// <summary>Fan the Flames: this many of your next spells this turn are cast twice.</summary>
	public int SpellsTwice { get; init; }

	/// <summary>Spell Surge: your spells cost this much less this turn.</summary>
	public int SpellDiscount { get; init; }

	/// <summary>Charge Up: this much more energy when your next turn starts.</summary>
	public int EnergyNextTurn { get; init; }

	/// <summary>Spells played this turn — the Echo Owl's "first spell each turn".</summary>
	public int SpellsThisTurn { get; init; }

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

	/// <summary>
	/// **Your monsters that have acted this round** — the relay's count. Pike's FINISHER reads it, so a
	/// front that acts last cashes everything behind it. Reset when your turn starts; Hasten counts.
	/// </summary>
	public int AlliesActedThisRound { get; init; }

	/// <summary>
	/// **DEPLOY (R2): the fight has not begun.** You see their line and order yours; cards and
	/// END TURN wait until FIGHT (`BeginFightAction`). The hand is already dealt, so the order can
	/// answer it.
	/// </summary>
	public bool Deploying { get; init; }

	/// <summary>The order your line was deployed in, by `Ally.Slot` — the run keeps it for the next fight.</summary>
	public ImmutableList<int> DeployedOrder { get; init; } = [];

	public bool IsOver { get; init; }
	public bool Won { get; init; }
}

/// <summary>
/// **Anything in a line — your monster or a foe. Both play a telegraphed CYCLE of moves on their
/// own**, at the end of your turn, in steps from the back of the lines to the front. A foe's cycle is
/// exactly what it would bring if caught.
/// </summary>
public abstract record Creature : GameObject
{
	public int Hp { get; init; }
	public int MaxHp { get; init; }

	/// <summary>Its LEVEL (`PartyLevels`) — shown; its stats already carry it.</summary>
	public int Level { get; init; } = PartyLevels.Base;

	/// <summary>Its FAMILY (`PartyFamilies`) — a tag for your own synergies; no weakness chart.</summary>
	public Family Family { get; init; }

	/// <summary>
	/// **Its place in its line: 0 is the FRONT.** Kept contiguous by `PartyState.Settle`; −1 once it has
	/// left the line (fallen, caught) or while it waits on the bench.
	/// </summary>
	public int Position { get; init; }

	/// <summary>Soaks damage before HP.</summary>
	public int Block { get; init; }

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

/// <summary>One of YOUR monsters. It fights on its own; your cards order, buff and time it.</summary>
public record Ally : Creature
{
	/// <summary>Its place in the run's team — how the run finds it again after the battle.</summary>
	public int Slot { get; init; }

	/// <summary>
	/// **A TOKEN** — summoned by a card or a bonus, gone after the fight, never keeps a battle alive.
	/// Apart from `FadesIn`: a Sprout stays until it falls (Grove draft 1, 2026-09-29).
	/// </summary>
	public bool IsToken { get; init; }

	/// <summary>Added to this monster's attacks.</summary>
	public int Power { get; init; }

	/// <summary>
	/// **ROOTED Block** (Grove), gained since your last turn start: it survives the NEXT turn start, once
	/// (Shayne, 2026-09-30 — it stacked forever and Grove walled whole acts).
	/// </summary>
	public int Rooted { get; init; }

	/// <summary>
	/// **Block kept at your last turn start** — it has had its extra turn and goes at the next one. Hits
	/// spend it first.
	/// </summary>
	public int Carried { get; init; }

	/// <summary>Power added by cards this turn (Rally). Cleared when your next turn starts.</summary>
	public int BonusPower { get; init; }

	/// <summary>
	/// **SPELL POWER — added to every spell, summed across your team** (round 4: a spell is cast by
	/// the team, not a monster). Its base, from the monster.
	/// </summary>
	public int SpellPower { get; init; }

	/// <summary>
	/// **An attack card has been played on it this turn** — so its FIRST-ATTACK bonus is spent
	/// (`PartyMonsters`). Cleared when your next turn starts.
	/// </summary>
	public bool AttackedThisTurn { get; init; }

	/// <summary>Played its move early this turn (Hasten), so it does not act again at the end.</summary>
	public bool HasActed { get; init; }

	/// <summary>The passive, as the player reads it. Rules live in the fields below, never here.</summary>
	public string Passive { get; init; } = "";

	/// <summary>The passive's rule in a sentence — shown when the monster is inspected.</summary>
	public string PassiveRule { get; init; } = "";

	/// <summary>
	/// **Thorns — a foe that ATTACKS this monster takes this much back**, blocked or not. Bramble's
	/// passive: it pays only when she is struck, so she wants the FRONT.
	/// </summary>
	public int Thorns { get; init; }

	/// <summary>Thorns added by cards this turn. Cleared when your next turn starts.</summary>
	public int BonusThorns { get; init; }

	/// <summary>
	/// **FINISHER — this much more damage for each of your monsters that acted before it this round.**
	/// Pike's passive: the relay's payoff, so it wants the front — where the blows land.
	/// </summary>
	public int FinisherPerAlly { get; init; }

	/// <summary>Attacked since your last turn began (blocked or not) — the Glowmoth's condition.</summary>
	public bool WasHit { get; init; }

	/// <summary>
	/// **Off-Balance — while this monster stands, any foe you move takes this much extra from every
	/// hit that round.** Gale's passive: it moves foes from the back, before the front swings.
	/// </summary>
	public int Unbalances { get; init; }

	public int TotalThorns => Thorns + BonusThorns;

	public bool IsKnockedOut => IsDown;

	/// <summary>What one of its attacks deals before the relay: the move's amount plus Power and Rally.</summary>
	public int AttackFor(int amount) => amount + Power + BonusPower;
}

public record Foe : Creature
{
	/// <summary>Extra damage this foe takes from every hit, from being moved. Cleared at your turn start.</summary>
	public int OffBalance { get; init; }

	/// <summary>**BURN** (`PartyEmber`): this much damage as its turn begins, Block or not, then 1 less.</summary>
	public int Burn { get; init; }

	/// <summary>Stagger: it loses its next move (the cycle still advances).</summary>
	public bool Staggered { get; init; }

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

	public bool IsDead => IsDown;
}

public enum IntentType
{
	/// <summary>Damage to the creatures its <see cref="Intent.Target"/> names on the OTHER line.</summary>
	Attack,

	/// <summary>Gains Block — itself, or the one AHEAD. A foe's holds through your next turn.</summary>
	Block,

	/// <summary>Moves itself <see cref="Intent.Amount"/> places toward the BACK (negative = forward).</summary>
	Move,

	/// <summary>**Swaps the OTHER line's front two** — Gale's Gust, or a foe breaking your formation.</summary>
	Shove,

	/// <summary>
	/// **Repeats the last spell you cast this turn**, on the same foe — the Echo Owl. It needs a
	/// trainer, so a wild one's Echo does nothing.
	/// </summary>
	Echo,

	/// <summary>Summons <see cref="Intent.Summons"/> at the FRONT of its own line.</summary>
	Summon,

	/// <summary>
	/// **A WIND-UP: it does nothing now — and its telegraph shows the NEXT move**, so a big blow
	/// always gives you a turn to answer it (`PartyBosses`).
	/// </summary>
	WindUp,

	/// <summary>**TONGUE: drags the other line's BACK creature to its FRONT** (`PartyBosses`).</summary>
	Pull,
}

/// <summary>
/// **Who a move lands on** — replaces columns, shapes and homing (`KinRelayPlan.md` R4). Chosen at the
/// START of the step the creature acts in.
/// </summary>
public enum Aim
{
	/// <summary>The other line's front — the default.</summary>
	Front,

	/// <summary>The other line's back.</summary>
	Back,

	/// <summary>The other line's front two.</summary>
	Pierce,

	/// <summary>Everyone in the other line.</summary>
	Sweep,

	/// <summary>The lowest HP in the other line.</summary>
	Hunt,

	/// <summary>The one ahead of it in its OWN line — how the relay passes a buff forward.</summary>
	Ahead,
}

/// <summary>One telegraphed move. **Nothing in a line plays from a deck but you.**</summary>
public record Intent
{
	public string Name { get; init; } = "";
	public IntentType Kind { get; init; }
	public int Amount { get; init; }

	/// <summary>Who an Attack lands on; a Block with <see cref="Aim.Ahead"/> shields the one ahead.</summary>
	public Aim Target { get; init; }

	/// <summary>
	/// **THIEF** (a wild trait): after the attack, a FOE takes the top card of your draw pile, and
	/// holds it until it is beaten or caught. A caught thief loses it (`PartyRun.FromFoe`).
	/// </summary>
	public bool Steals { get; init; }

	/// <summary>What a Summon move brings — the Broodvine's Grub.</summary>
	public TokenTemplate? Summons { get; init; }
}
