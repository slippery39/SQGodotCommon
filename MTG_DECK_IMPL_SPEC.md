# MTG Classic Decks — Implementation Spec

Living document. Updated as each phase is designed, built, or revised.

---

## Goals

Implement a selection of classic MTG decks (Standard and Modern) as a vehicle for adding new mechanics to the MtgCore library. Cards go in `MtgCore/CardLibrary`, decks go in `MtgSimulator` as factory classes. The simulator is used to validate that the new mechanics interact correctly.

This is a rules/simulation project — not a full game. Some mechanics are simplified or deferred where full fidelity would add disproportionate complexity.

---

## Architecture Decisions

### Mana System
- Current system: Hearthstone-style auto-incrementing mana (1 on turn 1, 2 on turn 2, etc.).
- Fast mana spells (Rite of Flame, Seething Song) add to the current turn's mana pool only — not permanent.
- Phyrexian mana cards are implemented as free spells that cost 2 life instead of a mana payment.
- Color is tracked as a property on cards but **not enforced** in the simulator. This future-proofs the system without adding enforcement complexity now.

### Land System
- Lands are played as one-shot spells (like any other card) — they do not stay on the battlefield as land permanents.
- When played, a land adds mana to the current turn's pool.
- If a land has a secondary ability (e.g., Darksteel Citadel being an artifact), it creates a token that remains on the battlefield.
- Example: Darksteel Citadel → add 1 colorless, create an indestructible artifact token.
- This lets artifact-land synergy decks (Affinity) work via the leftover tokens without needing a full permanent land system.
- The auto-incrementing mana system can be toggled off when land cards are present in a deck.

### Flying / Taunt
- Full MTG blocking rules are not implemented. Combat is Hearthstone-style (attacker chooses target freely).
- **Taunt** (`HasTaunt`): creatures with Taunt must be attacked/targeted before non-Taunt permanents.
- **Flying** (`HasFlying`): Flying creatures bypass Taunt from non-Flying creatures. (They still must attack Taunt creatures that also have Flying.)
- **Reach** (`HasReach`): Reach creatures can "block" Flying — Flying creatures do not bypass their Taunt.
- A full barracks/combat-zone system (where zone membership determines blocking eligibility) is deferred to a future real-game implementation.

### Transform (Double-Faced Cards)
- Cards store two face records directly: `PrimaryFace` and `TransformedFace`.
- `IsTransformed` (bool) on the card determines which face is active.
- All stat/component lookups derive from the active face.
- A `TransformAction` flips `IsTransformed`.
- The transform trigger follows the existing `TriggeredAbilityComponent` pattern.
- Drawback accepted: if a transformed face needs to reference other live game objects by ID, this model is insufficient. Not a concern for current target cards.

### Counterspells
- Full stack interaction is not implemented.
- Counterspell-like cards (Remand, Mana Leak) are implemented as **trap instants**: they fire automatically when the opponent casts a spell if the controller has sufficient mana.
- Remand: auto-counter + return countered spell to owner's hand; controller draws a card.
- Mana Leak: auto-counter unless the casting player pays 3 (simplified to: counter if they have fewer than 3 mana remaining after casting).

### Planeswalkers
- Deferred. Liliana of the Veil (Jund) is excluded from the initial Jund implementation.

### Equipment
- Equipment is a new permanent subtype.
- Has an "Equip" activated ability (pay cost → attach to target creature you control).
- Grants static P/T or keyword bonuses to the equipped creature.
- Dynamic equipment (Runechanter's Pike: +X where X = instants/sorceries in graveyard) follows the `GraveyardCountComponent` pattern.

### Storm
- Needs "spells cast this turn" counter on `MtgGame`.
- Storm spells copy themselves for each prior spell cast this turn.
- Copies are pushed onto the action stack.

### Suspend
- Cards with Suspend sit in exile with time counters (int on the card).
- At the beginning of each upkeep, remove one time counter from each suspended card.
- When the last counter is removed, cast the card for free.
- Simplification accepted: Lotus Bloom can be implemented as a free mana sorcery if Suspend adds too much overhead.

### +1/+1 Counters
- Stored as an int on `CreatureComponent` (or a dedicated counter component).
- Modify P/T at read time: effective power = base + counters, effective toughness = base + counters.
- Modular: when a creature with Modular dies, move its +1/+1 counters to a target artifact creature.

### Cascade
- When a cascade card is cast, exile cards from the top of the library until a nonland card with CMC less than the cascade card is found.
- Cast that card for free.
- Put the exiled cards on the bottom of the library in a random order.
- Needs "CMC" property on all cards (should match mana cost already stored).

### Delve
- Alternative cost: exile any number of cards from your graveyard, each reduces the mana cost by 1.
- Implemented as a new cost type alongside `LifeAdditionalCost`.

### Death's Shadow
- Dynamic P/T: 13/13 − controller's current life total.
- Follows the `GraveyardCountComponent` pattern with a `LifeTotalComponent` instead.
- If effective P/T ≤ 0, the creature does not exist as a legal permanent (handled by state-based effects).

---

## Mechanics Status

| Mechanic | Status | Notes |
|---|---|---|
| Haste | Done | |
| Double Strike | Done | |
| First Strike | Not started | Needed for Delver deck |
| Flying | Done | `HasFlying` on `CreatureComponent`; bypasses non-flying/non-reach Taunt |
| Taunt | Done | `HasTaunt`; `AttackAction.ValidateTauntConstraint` enforces must-attack |
| Reach | Done | `HasReach`; prevents flying bypass of Taunt |
| Hexproof | Not started | Can't be targeted by opponent |
| Trample | Not started | Needed for Jund |
| Flash | Not started | Instant-speed casting for creatures |
| Flashback | Not started | Cast from graveyard at flashback cost |
| Transform | Not started | Dual-face cards, IsTransformed flag |
| Storm | Done | `SpellsCastThisTurn` on `MtgGame`; `DragonStormEffectAction` deploys N dragons |
| Suspend | Not started | Lotus Bloom uses free-mana fallback instead |
| Fast mana | Done | `AddManaAction` boosts `CurrentMana` without touching `MaxMana` |
| Counterspells (trap) | Not started | Auto-fire on opponent spell |
| +1/+1 Counters | Not started | Needed for Affinity |
| Modular | Not started | Needs +1/+1 counters |
| Affinity cost reduction | Not started | Needs artifact type tracking |
| Equipment | Not started | Needed for Delver, Affinity |
| Cascade | Not started | Needed for Jund |
| Delve | Not started | Needed for Grixis Shadow |
| Death's Shadow | Not started | Dynamic P/T via life total |
| Cycling | Not started | Discard + draw (with life variant) |
| Phyrexian mana | Not started | Free + pay 2 life |
| Milling | Not started | Move top N library cards to graveyard |
| Hand disruption | Not started | View/target opponent's hand |
| Bounce | Not started | Return permanent to owner's hand |
| Soft counterspell | Not started | Counter unless opponent pays X |
| Modal spells (choose 2) | Not started | Needed for Kolaghan's Command |
| Lands (one-shot) | Not started | Add mana + optional token |
| Regenerate | Not started | Needed for Affinity (Welding Jar) |
| Ferocious | Not started | Conditional on 4+ power creature |

---

## Deck Implementation Plan

### Phase 1 — Dragonstorm (2006 Standard) ✓ DONE

**Target cards:**

| Card | Simplification | Status |
|---|---|---|
| Bogardan Hellkite | ETB: deal 5 damage to single random opponent target | Done |
| Hunted Dragon | 6/6 Flying Haste only — Knight token ETB omitted | Done |
| Dragonstorm | Storm via `SpellsCastThisTurn`; deploys N dragons from library | Done |
| Gigadrowse | Exhausts target creature (`HasAttacked = true`); Replicate omitted | Done |
| Remand | Not implemented — stack interaction required | Deferred |
| Rite of Flame | Add 2 mana + 1 per Rite in graveyard (`CountCardsWithNameAction`) | Done |
| Seething Song | Add 5 mana (`AddManaAction`) | Done |
| Sleight of Hand | Look at top 2, keep 1, put other on bottom | Done |
| Telling Time | Already implemented | Done |
| Lotus Bloom | Free sorcery — add 3 mana (Suspend 3 omitted) | Done |

**New mechanics introduced:** Flying, Taunt, Reach (combat keywords), Storm, Fast mana, ETB targeted damage, Exhaust

**New actions added:** `AddManaAction`, `ExhaustCreatureAction`, `SearchLibraryAndDeployAction`, `CountCardsWithNameAction`, `DragonStormEffectAction`

**Status:** Complete — `DragonstormDeckFactory` registered in `DeckRegistry`

---

### Phase 2 — Delver (2012 Standard)

**Target cards:**

| Card | Simplification |
|---|---|
| Delver of Secrets | Transform: upkeep trigger, flip if top card is instant/sorcery |
| Geist of Saint Traft | Hexproof; attack trigger: create 4/4 Angel with Flying, sacrifice at end of combat |
| Restoration Angel | Flash; ETB blink a non-Angel creature you control |
| Snapcaster Mage | Flash; ETB give target instant/sorcery in graveyard Flashback until EOT |
| Gitaxian Probe | Phyrexian Blue (free + 2 life): look at opponent's hand, draw |
| Gut Shot | Phyrexian Red (free + 2 life): deal 1 damage to any target |
| Mana Leak | Trap: auto-counter unless caster has 3+ mana remaining |
| Mutagenic Growth | Phyrexian Green (free + 2 life): +2/+2 until EOT |
| Ponder | Look at top 3, put in any order, draw 1 |
| Thought Scour | Target player mills 2, you draw 1 |
| Vapor Snag | Return target creature to owner's hand, that player loses 1 life |
| Runechanter's Pike | Equipment: +X/+1 first strike, X = instants/sorceries in graveyard |

**New mechanics introduced:** Transform, Hexproof, Flash, Flashback, Phyrexian mana, Equipment, First Strike, Milling, Bounce, Look at opponent's hand

**Status:** Not started

---

### Phase 3 — Grixis Shadow (2017 Modern)

**Target cards:**

| Card | Simplification |
|---|---|
| Death's Shadow | 13/13 − controller life; removed by SBE if P/T ≤ 0 |
| Gurmag Angler | Delve: 5/5, exile cards from graveyard to reduce cost |
| Snapcaster Mage | (shared with Delver) |
| Street Wraith | Cycling: pay 2 life → draw a card |
| Tasigur, the Golden Fang | Delve: 4/5; activated: mill 2, opponent chooses card to return to your hand |
| Fatal Push | Destroy creature CMC ≤ 2 (or ≤ 4 if a permanent died this turn — Revolt) |
| Kolaghan's Command | Choose 2 of 4 modes (modal spell) — may defer modal |
| Opt | Look at top 1, may put on bottom, draw |
| Stubborn Denial | Trap counter: noncreature spell; Ferocious mode counters unless pays 3 |
| Temur Battle Rage | Target creature gains Double Strike and Trample until EOT |
| Terminate | Destroy target creature |
| Thought Scour | (shared with Delver) |
| Inquisition of Kozilek | Look at opponent's hand, discard card with CMC ≤ 3 |
| Thoughtseize | Pay 2 life, look at opponent's hand, discard any nonland card |

**New mechanics introduced:** Death's Shadow, Delve, Cycling (life variant), Ferocious, Modal spells, Revolt, Trample, Hand disruption

**Status:** Not started

---

### Phase 4 — Jund Midrange (2012 Modern)

**Target cards:**

| Card | Notes |
|---|---|
| Bloodbraid Elf | Cascade |
| Dark Confidant | Upkeep trigger: reveal top, lose life = CMC, draw |
| Deathrite Shaman | 3 activated abilities targeting graveyards |
| Tarmogoyf | Already implemented |
| Abrupt Decay | Destroy nonland permanent with CMC ≤ 2 (uncounterable) |
| Blightning | Deal 3 damage to player, that player discards 2 |
| Inquisition of Kozilek | (shared with Grixis) |
| Lightning Bolt | Already implemented |
| Maelstrom Pulse | Destroy all permanents with the same name as target |
| Terminate | (shared with Grixis) |
| Thoughtseize | (shared with Grixis) |
| Liliana of the Veil | **Deferred — Planeswalker system not yet implemented** |

**New mechanics introduced:** Cascade, Upkeep draw trigger, Graveyard exile as mana source, Destroy-all-by-name, Deathtouch (if needed)

**Status:** Not started

---

### Phase 5 — Affinity (2004 Standard)

**Blocked on:** Land system (artifact land tokens) and +1/+1 counters.

**Target cards:**

| Card | Notes |
|---|---|
| Arcbound Ravager | Sacrifice artifact → +1/+1 counter; Modular 1 on death |
| Arcbound Worker | 1/1 Modular 1 |
| Atog | Sacrifice artifact → +2/+2 until EOT |
| Disciple of the Vault | Triggered: opponent sacrifices artifact → lose 1 life |
| Frogmite | Affinity for artifacts (cost reduction) |
| Electrostatic Bolt | Deal 2 damage (3 if artifact creature) |
| Shrapnel Blast | Sacrifice artifact → deal 5 damage |
| Thirst for Knowledge | Draw 3, discard 2 unless discard an artifact |
| Thoughtcast | Affinity for artifacts, draw 2 |
| Chrome Mox | Imprint: exile card → tap for 1 mana of that color |
| Skullclamp | Equipment: +1/-1; when equipped creature dies draw 2 |
| Welding Jar | Sacrifice → Regenerate target artifact |
| Artifact lands | Create artifact token variant (see Land System above) |

**New mechanics introduced:** +1/+1 counters, Modular, Affinity cost reduction, Artifact type tracking, Imprint, Regenerate

**Status:** Blocked

---

## Deferred / Out of Scope

| Item | Reason |
|---|---|
| Planeswalkers | Separate permanent type, loyalty system, attack redirection — own phase |
| Full blocking rules | Deferred to real-game implementation (barracks/combat zone system) |
| Color mana enforcement | Tracked but not enforced in simulator |
| Replicate (Gigadrowse) | Skipped — tap effect sufficient without copies |
| Hunted Dragon Knight tokens | Skipped — creature simplified to 6/6 haste |
| Transform via game object identity | Rejected in favor of dual-face record on card |
| Full stack/priority system | Counterspells use trap pattern instead |
