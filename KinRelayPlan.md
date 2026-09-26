# THE RELAY — implementation plan and checklist

**Decided (Shayne, 2026-09-25):** lane combat is retired; the new battle is **THE RELAY** — one line a
side, the front holds, and the line acts **back to front**. Monsters are not cards; one level of
play; **no trainer health, no lanes, no moving monsters every turn.** The UI is redone: creatures are
**sprites standing in a line**, not cards in cells (placeholder sprites for now). Why:
`KinJam.md` (top) and `docs/research/combat-systems.md`.

This file is the ORDER OF WORK, the files each step touches, and the traps found by reading the code
first. Tick items as they land. **Exploring, not tuning: tests prove each rule FIRES; no sims.**

---

## 1. The rules (confirmed by Shayne, 2026-09-25, except where marked)

| # | Rule |
|---|---|
| R1 | **Two lines facing each other.** Position 0 is the FRONT. Up to 5 a side (3 monsters + tokens). The bench waits behind, not in the line. |
| R2 | **DEPLOY before each fight:** you see their line, then order yours (drag). It starts in last fight's order. The only free reordering there is. |
| R3 | **A round:** your turn (3 energy, hand of 5, play cards) → END TURN → **the lines act in STEPS, back to front, BOTH SIDES AT ONCE** (Shayne): everything in position 4 acts together, then 3, then 2, then 1, then the fronts clash last. Why: either side going first forces one side's HP to be inflated to survive it; simultaneous steps need neither. **Within a step:** every actor picks its target from the state at the START of the step; braces and other set-up land before blows, on BOTH sides (so a same-step Brace meets the blow it braced for, whoever's it is); all their effects land, THEN faints and step-ups happen — so a creature that falls in a step still lands its own blow (a trade), and a buff given "ahead" is in place for the next step. **Consequences to watch:** a back-liner acts in an earlier step than the fronts, so a foe's sniper can drop your finisher before it swings; and a longer line gets early steps alone (a 5-long line's position 4 acts with nobody opposite). |
| R4 | **Who a move hits** (replaces columns, shapes and homing): FRONT (default) · BACK · PIERCE (front two) · SWEEP (all) · HUNT (lowest HP) · AHEAD (an ally's move on the one ahead of it) · SELF. **Resolved at the start of each STEP** (R3), not when the turn ends: if your front fell in an earlier step, this step's blows land on the new front. |
| R5 | **The relay:** because the line acts back to front, anything a monster gives "the one ahead" lands before that one acts. Setup in the back, the finisher in front — where the blows land. |
| R6 | **A faint:** it leaves the line, everyone behind steps up, the first benched monster joins at the BACK. The battle is lost when line and bench are all down; **a lost battle ends the run.** |
| R7 | **Mid-fight reordering is only by cards and abilities** (Swap, Rally to the front, Retreat to the back) — and by foes (Shove, Pull). |
| R8 | **Speed is gone** — the order IS the position. |
| R9 | **A Snare reaches only their FRONT** (Shayne): catching a back-liner means pulling it forward first. |
| R10 | **A gym is a tougher line**, won by beating it — the leader's health and the "race the leader" rule are gone. Leader RULES (the elite idea) come later. |
| R11 | **A summoned token enters at the FRONT** of your line, as fodder. |

### The starters, re-kitted for the Relay (Shayne: "we can start with that")
- **Bramble, the Wall — wants the FRONT.** HP 30, Power 2. Bash 4 · Brace (+6 Block). THORNS 2 (unchanged).
  Deck: Thornhide, Bristle.
- **Pike, the Finisher — wants the front, and is fragile there.** HP 18, Power 3. Jab 2 · Jab 2 · Flurry
  (Sweep 1). **FINISHER: +2 for each ally that acted before it this round** (replaces Momentum). Deck: two
  new cards — *Charge* (send it to the front, +2 Power this turn) and *Hold the Line* (Swap two).
- **Gale, the Controller — wants the BACK.** HP 22, Power 2. Buffet (Sweep 1) · Gust (swaps THEIR front two).
  OFF-BALANCE +2 (a foe moved this round takes 2 more from every hit). Deck: Gust, Tailwind (reworded to
  "swap their front two").

### What each creature's moves become (mechanical, no redesign)
3-wide → SWEEP · 2-wide (Stonebeak's Dive, Viper's Strike) → PIERCE · homing (Wisp, Hushcap, Bog Toad's
Tongue) → HUNT · everything else → FRONT · Old Mire's Deluge → SWEEP · Move intents (Drift, Slither, Hop,
Flutter) → cut or SHOVE · Ironhorn's TRAMPLE → the rest goes to the one BEHIND (both sides) · Magpie's steal,
Echo, Brood, the four strategies' triggers → unchanged · Focus and Meteor's "beside" → "and the one behind" ·
the Sprout's shield → the ones ahead and behind it · the Decoy → "HUNT and BACK attacks aim at it".

---

## 2. Checklist

### Phase 0 — confirm (Shayne)
- [x] R3: simultaneous steps, back to front (Shayne's proposal)
- [x] R9: catching only the front
- [x] The three starter kits above

### Phase 1 — the engine: lines and the relay (`KinCore/Party/`) — DONE 2026-09-25
Faints are settled by `PartyState.Settle` — MtgCore's state-based effects: hits only deal damage;
Settle (the post-processor after every card, and explicitly after every step) removes the fallen,
brings the bench in at the back, closes the lines and decides the battle. 96 party tests, all new
or re-expressed in lines. The OLD board compiles and runs on it (cells = line positions) until Phase 4.
- [x] `PartyModel`: `Creature.Space` → `Position` (0 = front); drop `Speed`, `Ally.StepsLeft`, `Momentum*`,
      `PartyBattle.TrainerHp`/`LeaderHp`; `Intent.Offsets`/`Homing` → `Intent.Target` (R4)
- [x] `PartyState`: `Line(side)`, `Front`, `Back`; **compaction on a faint, at the END of a step** (R3, R6);
      insert at the front (R11); `ActingOrder` = steps by position, back to front, both sides per step (R3)
- [x] `EndPartyTurnAction`: per STEP — targets from the step's starting state, every effect lands, then
      faints and step-ups (R3, R4). Replaces "order and targets fixed before anyone acts"; the forecast
      still simulates the whole round, so the board stays exact
- [x] Delete: `MoveAllyAction`, `StepRefusal`, `DashAction`, `PushAction`, `HitTrainer`/`AimsAtTrainer`,
      `HitLeader`/`AimsAtLeader`
- [x] New card steps: `SwapAction`, `RallyAction` (to front), `RetreatAction`; `GustAction` (their front two)
- [x] `CatchRefusal`: the front only (R9)
- [x] Tests (`PartyTests`): delete the lane tests; add one per rule R1–R11 that FIRES; re-express the
      round-one tests in positions

### Phase 2 — content (`PartyContent`, `PartyWorld`, `PartyCards`) — mostly done with Phase 1 (it had to compile)
- [x] The starters' kits and decks (section 1)
- [x] Every foe's moves migrated by the table above (the Move intents kept: they now move a creature
      through its own line — Wisp's Drift goes back, the Viper's Slither comes forward)
- [x] Round-one cards and creatures reworded where they named spaces (Focus, Meteor, Sprout, Decoy, Trample)
- [ ] The creatures' doc comments still describe columns in places — reword while playtesting
- [ ] `docs/paper/round-one-synergies.md`: mark what changed

### Phase 3 — the run (`PartyRun`, `PartyWorld`) — DONE 2026-09-25
- [x] DEPLOY (R2): the battle opens `Deploying` (`PartyScenario.Deploy`; the run and the practice
      scenarios use it); `DeployMoveAction` reorders freely until `BeginFightAction`; cards, Snares
      and END TURN wait. **The forecast works while deploying** — it plays the first round in the
      order being set, which is what the order is chosen by. The bot keeps the order it is given.
      Stopgap input on the old board: click a monster, then its place; the button reads FIGHT.
- [x] The team's order persists between fights — the DEPLOYED order (`PartyBattle.DeployedOrder`),
      not where the line ended up after swaps and faints
- [x] Gyms without leader health (R10): `Encounter.LeaderHp`, `PartyWorld.LeaderHp`, `Region.Build` scaling
- [x] No trainer health anywhere: towns heal monsters only; a lost battle ends the run (R6)
- [x] `PartyBot` (no steps; reorder cards; score without trainer HP) and `PartySim`/`PartySimCommand` compile
      and run — **not tuned** (exploring)
- [x] `PartyRunTests`, `PartyBotTests` updated

### Phase 4 — the screen: sprites in a line (`SQGodotCommon/KinGame/`) — DONE 2026-09-25
`KinRelayField` (the two lines) + `KinRelayCreature` (one creature's view); `KinPartyCell` deleted,
its move text moved to `KinMoveText`. Checked on captures: the intro, a full round (lunges, floats,
the line closing after the blows), deploy by clicks, a summon, a focused card, the inspector, and a
run's gym. Fixed from them: labels touching their neighbours (padding), a lunge dragging the whole
view (only the sprite lunges now), a refused card showing a generic reason (the engine's reason
now), and **Summon lighting all five places** — it always arrives at the front, so the engine now
takes it only on your front.
- [x] **`KinUI.md` first:** the new layout contract. Proposed: **your line on the left facing right, theirs
      on the right facing left, the two FRONTS meeting in the middle**; banner and hand band unchanged;
      the status bar loses YOU and gains nothing
- [x] **A creature view** (a Control, not `CharacterUI`: the board is Controls and hit-tests rects): start from `Common/Core/CharacterUI` (a Sprite2D, a `HealthBar`, a click area, a
      shader `DamageFlashController`) — reuse it if it fits, adapt a copy if not. Shows: the sprite (flipped
      for foes), HP bar, Block, an ORDER badge (1, 2, 3…), the NEXT MOVE above its head with where it lands
      ("→ FRONT", "→ BACK", "→ ALL", "→ WEAKEST"), the passive / "FADES IN N" line, the forecast
- [x] **Placeholder sprites**: the 12 existing portraits (`Art/*.png`) as sprites; `KinArt.Figure` silhouettes
      for the rest (the round-one twelve and the tokens have no art)
- [x] **Card drops onto creatures**: hovering a card lights exactly the creatures the engine accepts (the
      rule that the lit targets and validation never disagree still holds)
- [x] **DEPLOY screen** (on the field itself: press-drag-release, or click then click): drag your creatures to reorder, a FIGHT button; their line visible
- [x] **Animations** (`KinAnimator`; hit events carry `AttackerId` for the lunge): the actor lunges toward its target, the target flashes, floats as now;
      the line slides up after a faint; a swap slides both
- [x] `KinPartyInspector`: targets instead of shapes, no Speed; hovering a sprite opens it
- [x] Delete `KinPartyCell`, the rows, the aim hint, the step input; keep a move-text helper for the
      starter screen (`KinPartyRunScreens` calls `KinPartyCell.Says`)
- [x] `Commands.md`: capture flags (`--fight`) for the new screen (`--play`, `--inspect`, `--deploy`…)
- [x] Captures of every screen, READ (scar 6: an unwrapped label sets its container's width)

### Phase 5 — play it
- [ ] Practice scenarios rebuilt: one intro (the three starters vs three foes) + the four strategies
- [ ] **Shayne playtests.** Nothing below is decided until then.

### Phase 6 — close
- [ ] Dead lane code gone (grep `Space`, `Offsets`, `Homing`, `TrainerHp`, `LeaderHp`, `Step`)
- [ ] `KinJam.md`, `KinUI.md`, `Commands.md`, the handoff, the root `CLAUDE.md` map; the stale
      `design-card` skill rewritten for the Relay
- [ ] A commit per phase, each building and green (scar 2: the formatter re-stages whole files)

---

## 3. Traps found by reading the code

- **`EndPartyTurnAction` fixes every target before anyone acts** — right for columns (a homing blow could not
  re-aim), wrong for a line: when the front falls, the next blow must find the new front. Resolve per STEP
  (R3); the forecast simulates the full round, so the telegraph and the result still agree.
- **Simultaneous means deferred faints.** Today `HitAlly`/`HitFoe` handle a knockout on the spot (the bench
  steps in, the battle can end). Inside a step the damage must land first and the faints be settled at the
  step's end, or the second actor of a step would see a line the first one already changed.
- **Triggers fired by the end of the round land after the next turn starts** (the engine runs spawned
  actions before the post-processor — `KinJam.md`, Engine findings). Anything that must matter DURING the
  round stays a component read at the point of use.
- **`PartyRun.AfterBattle` maps allies back to the run by `Slot`** — keep `Slot` as identity and add
  `Position` as place; do not overload one field with both.
- **Tokens use `Slot = -1`** and must keep not counting toward losing.
- **`KinPartyRunScreens` reuses `KinPartyCell.Says`** for the starter screen — move it before deleting the cell.
- **About 27 references to `PartyBattle.Spaces`** and 18 files naming trainer or leader health, offsets or
  homing — Phase 1 is the bulk of the work, and it is mostly deletion.

## 4. Later — deliberately not in this plan

Kill-order layers (reinforcements joining their back), clocks (heavy hitters every other round), a chain
meter, held items, training cycles in towns, evolution, gym-leader rules, **real sprite art** (sprites need
transparent backgrounds — the `generate-card-art` skill's "no background removal" assumption no longer
holds), tuning with `party-sim`.
