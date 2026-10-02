# KIN — the ENEMY plan (brainstorm, 2026-10-02)

> **Why this exists.** Playtest, 2026-10-02 (Shayne, two runs): "The enemies do not actually go up in
> power level too much … once you get out of act 1, you have pretty much beaten the game." And one
> loss with nothing to do about it: a CRUSH 10 on floor 3. **Balancing monsters or cards waits until
> the enemies are fleshed out** (Shayne). This is EXPLORING: tests prove each mechanic fires; no sims.

## Why region 3 felt like region 1 — measured from the code

- **Every region draws from the SAME 15 wild species** (`PartyWorld.Wilds`, "mixed uniformly"). Only
  the level changes.
- **The level curve is flat**: stats are base × `0.6 + 0.08 × level`, and the regions climb about a
  level each — region 3's wild foes are ~×1.5 of region 1's (`PartyWorld.Tiers`). The deck grows far
  faster: more cards, upgrades, relics, two more monsters.

## Decided (Shayne, 2026-10-02)

| | |
|---|---|
| **The curve** | **STS-steep**: HP and hits roughly ×2–3 from the first region to the last. AND new kinds of foe each region — both, not one or the other. The exact steps are TUNING, later |
| **Region rosters** | Each region its own species (follows from "new kinds each region"; replaces the uniform `Wilds`) |
| **Junk cards** | YES — they make foes tougher without bigger numbers, and punish a deck that never removes cards |
| **Debuffs on your monsters** | YES, all four: **Weak** (deals less), **Vulnerable** (takes more), **Silence** (no first-attack bonus), **Shaken** (attack cards cannot be played on it) |
| **CRUSH** | **Fragile foes only, small amounts** — "kill it first, or ignore it and take guaranteed damage"; never a run-ender. BUILT: Boar and Hoard Drake lost it (the Boar's Charge is telegraphed by a wind-up instead); new **Rock Mite** (10 HP, CRUSH 3); `TheFirstRegionsCrushersAreFragileAndSmall` holds the rule (region 1: ≤ 12 HP, ≤ 4 crush) |
| **Groups** | All four below — style-punishing elites, the monster angle, clocks, junk cards |

## The ideas — each asks one question

✅ = built from existing rules, 🔧 = needs a new one. Names are placeholders (generic fantasy).

**A. Elites that punish one play style**
- **Null Knight** — +1 Power each time you play a spell 🔧. *Win with attacks?* (Guard is a spell.)
- **Thornback Ogre** — each attack card played on it hurts the attacker 3 🔧. *Win without many small hits?*
- **Hourglass Wraith** — counts your cards; every 7th, it acts at once 🔧. *Win without long chains?*
- **Siege Troll** — gains Power equal to the Block you gained this turn 🔧. *Turtle without feeding it?*
- **Ash Elemental** — Burn heals it 🔧. *Does the Ember deck have a plan B?*

**B. The monster angle — foes that care WHICH monster does what** (seeds for the hook)
- **Goblin Duelist** — challenges one of your monsters; takes half damage from the others 🔧.
- **Hexer** — Silences a monster's first-attack bonus while it lives 🔧 (Silence).
- **Wolf Pack** — hunts your weakest ✅ (`Aim.Hunt`); +3 against anyone below half 🔧.
- **Banshee** — Shakes your front monster for a turn 🔧 (Shaken).
- **Mimic** — copies the first-attack bonus of the last monster that hit it 🔧.
- **Rival Beastmaster** (boss) — her own monsters with first-attack bonuses, buffed by her moves ✅ Summon + 🔧.

**C. Clocks and scaling — kill it now, or handle it later**
- **Powder Goblin** — counts 3, 2, 1, then explodes on your whole line ✅ wind-up telegraph + 🔧 countdown.
- **Cultist** — +2 Power a round ✅ (ENRAGE).
- **Brood Queen** — tanky; summons a weak minion each turn ✅.
- **Troll** — heals 5 a turn unless it is Burning 🔧.
- **Ooze** — at half HP splits into two with what it has left 🔧.
- **Lich** — cannot die while its Phylactery (a minion behind it) lives ✅ Summon + 🔧.

**D. Junk cards**
- **Bog Hag** — 2 **Mire** into your draw pile: unplayable, clogs the hand 🔧.
- **Giant Spider** — **Web** into your hand: costs 1 to clear; while held, line cards cannot be played 🔧.
- **Plague Rat** — **Rot** into your discard: when drawn, your weakest monster takes 2 🔧.
- **Witch** — **Doubt** into your hand each turn: gone at turn end; unplayed, −1 energy next turn 🔧.
- **Goblin Thief** — steals a card from your hand; kill it before it flees ✅ (`CardStolenEvent`).

## The build, in order (proposed)

1. **Region rosters + the curve's shape** — each region its own pool; the tier table steepened.
   Structural, and every foe after it lands in a region.
2. **The debuff system** — the four, as statuses on `Ally`, each a chip with its tip (`KinSymbols`).
3. **The junk-card system** — cards a foe adds for the fight (unplayable, fades at turn end, on-draw).
   Grep MTG first (token cards created into a zone); a fight's junk never reaches the run deck.
4. **Foes that react to your plays** — on spell, on attack, card count. The engine's `Trigger`s
   already drive the Hoard Drake's HOARD, so this is mostly content.
5. **The foes, in batches** — one per group first (the first-build picks: Bog Hag, Plague Rat,
   Hexer, Goblin Duelist, Powder Goblin, Wolf Pack, Null Knight, Thornback Ogre), then the rest.

Open, for the next session: which foes go in which region; the curve's numbers (tuning); art.
