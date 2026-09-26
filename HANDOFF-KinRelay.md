# Handoff — THE RELAY: two lines, steps from the back, sprites on a field

**Read this, then `KinRelayPlan.md` (the rules R1–R11, the checklist, the traps), then the top of
`KinJam.md` (newest first), then `KinUI.md` "THE RELAY screen".** `HANDOFF-KinCompanionGame.md` and
every older `HANDOFF-Kin*.md` describe games that no longer exist — read only their scars sections.
The `design-card` skill is STALE (owned cards, Speed, columns) — trust `KinJam.md` and the plan.

## State at handoff (2026-09-25)

- Branch **`kin-pivot`**, nothing pushed. **264 KinCore + 108 engine tests green**; solution and Godot
  project build.
- Last commit **`fdae2a1` (deploy)**. **Phase 4 — the sprite screen — is BUILT BUT NOT COMMITTED**:
  `KinRelayField.cs`, `KinRelayCreature.cs` (new), `KinPartyBoard.cs`, `KinAnimator.cs`,
  `KinPartyInspector.cs`, `KinPartyRunScreens.cs`, `KinPartyCell.cs(.uid)` deleted (staged), plus
  `PartyActions/State/Summon.cs` (hit events carry `AttackerId`; a summon drops on your front),
  `PartyTests.cs`, `KinRelayPlan.md`, `KinUI.md`, `Commands.md`, this file and `CLAUDE.md`.
  Commit it before new work — **leave `SQGodotCommon/project.godot` out** (a headless import strips
  two comment lines; it predates all of this).
- **Shayne is PLAYTESTING the Relay now** (Phase 5). Nothing below the rules is settled until he has.

## 1. The game now, in one paragraph

A monster-collecting roguelike deckbuilder. **Your caught monsters stand in a LINE facing the foes'
line; position 0 is the FRONT, and the fronts meet in the middle of the screen.** Before each fight
you DEPLOY (drag your line into order, then FIGHT). Your turn: 3 energy, 5 cards, played ON a
monster or a foe. At END TURN the lines act in **STEPS from the back, BOTH SIDES AT ONCE** — the
backs first, the fronts clash last. Moves hit by role (front, back, front two, all, weakest, the one
ahead). A fall is settled at the end of its step: the line closes up and the bench joins at the
back. No trainer health, no lanes, no moving every turn; a lost battle ends the run. Each monster
brings a small MONSTER DECK and may change how your cards play (spells +2, discard pays, energy).

## 2. What this session did (commits, oldest first)

| Commit | What |
|---|---|
| `5175476` | **The MTG rule reversed**: copying tested MtgCore/MtgGame code is EXPECTED — look there first; never modify MTG |
| `d2453e4` | **Triggers lifted into the engine** (`ImmutableGameObjects/Triggers.cs`: `Trigger`, `TriggerRule`, `FireTriggers`) |
| `eb92219` | **Monster decks + round one's four strategies** — Discard+Draw, Spellcraft, Surge, Summon — 12 creatures, ~25 cards, practice scenarios |
| `b1705bf` | Round one's design and every MTG lift recorded (`KinJam.md` "Engine findings") |
| `fa35a47` | **Lane combat retired; THE RELAY chosen** — `docs/research/combat-systems.md`, `KinRelayPlan.md` |
| `b130759` `3863be6` | **The Relay engine**: lines, steps, `Settle`; starters re-kitted; every foe converted |
| `fdae2a1` | **DEPLOY** |
| *(uncommitted)* | **The sprite screen** — Phase 4 |

The design's why is in `KinJam.md` (top sections) and `docs/paper/round-one-synergies.md` (every
round-one card and creature, classified by role · strategy · power band).

## 3. Where things are

- **Engine — `KinCore/Party/`**: `PartyModel` (Creature.Position, Aim, IntentType, PartyBattle's
  per-turn fields), `PartyState` (THE API: lines, `IntentTargets`/`AimAt`, `ActingSteps`, `Act`,
  `Settle`, `CostOf`, `ForecastIfTurnEndsNow`), `PartyActions` (play, Snare, the stepped END TURN,
  card steps incl. Swap/Rally/Retreat/Gust), `PartyDeploy`, `PartyTriggers` (the post-processor:
  Settle, then triggers), `PartyDiscard`, `PartySpells`, `PartySurge`, `PartySummon`, `PartyCards`
  (shared cards — dependency-free), `PartyContent` (starters, basic deck, rewards, scenarios, the
  factory), `PartyWorld` (creatures, areas, gyms, tiers), `PartyRun`, `PartyBot`/`PartySim`.
- **Screen — `SQGodotCommon/KinGame/`**: `KinPartyBoard` (run flow, hand, status, input, the event
  queue) → `KinRelayField` (the two lines, drops 0–4 yours / 5–9 theirs, slides) →
  `KinRelayCreature` (one view: move, sprite medallion, name, HP, status, note). `KinMoveText`
  (move in words) lives in the field file.
- **Tests — `KinCore.Tests/PartyTests.cs`** (one per Relay rule + every strategy), `PartyRunTests`,
  `PartyBotTests`. Inline definitions only.

## 4. Tools

```
./Run-Godot.ps1 KinGame/kin_party.tscn -Capture shots/x -Seconds 1.4 -GameArgs '--scenario=0','--fight','--play=1'
./Run-Godot.ps1 KinGame/kin_party.tscn -Capture shots/x -Seconds 3.2 -GameArgs '--scenario=0','--end-turn','--end-turn'
```
Practice scenarios OPEN DEPLOYING: `--fight` presses FIGHT at 0.3s (needed before `--play`,
`--focus`, `--snare`). `--click-space=a,b` clicks your line's places (deploy: a monster, then its
place). `--end-turn` twice = FIGHT then END TURN. `--inspect=N` hovers drop N. Scenario 0 is the
intro (Bramble, Pike, Gale); 3–6 the four strategies. All flags: `Commands.md`.

## 5. Scars worth not re-earning (this session)

1. **Look in MTG FIRST, and copy it.** Triggers, discard choice (pipeline + `ChoiceAction` + MTG's
   `ChoicePanel` used unchanged), CostEngine, X costs, temporary mana, ReplacementEngine-style damage
   modifiers, token creation, state-based effects — all were there, tested. Never modify MTG itself;
   engine names must not collide with MtgCore's (`Trigger`, not `TriggeredAbilityComponent`).
2. **Spawned actions run BEFORE the post-processor.** Triggers from the end-of-turn resolution land
   after the NEXT turn starts. Stamp events (`FoeDefeatedEvent.DuringYourTurn`), and read effects
   that must matter DURING the round as components at the point of use, not as triggers.
3. **Simultaneous means deferred faints.** Hits only deal damage; `Settle` (the post-processor after
   every card, and explicitly after every step) handles falls. Within a step, braces land before
   blows on BOTH sides, or a step is not symmetric.
4. **Track who has ACTED, not which position is next** — a token summoned at the front pushes
   everyone back, and a loop over positions skips whoever moved.
5. **The forecast must work in every state the board can show.** It simulates END TURN, which deploy
   refuses — the first deploy capture was a blank screen. It now presses FIGHT on its copy first.
6. **A lit drop must be a real choice.** Summon lit five places though it always lands in front;
   fixed in the ENGINE's refusal, not the screen.
7. **Lunge the sprite, never the whole view** — the name and HP bar went into the neighbour's slot.
8. **CSharpier's parser is older than the compiler**: `c ? [.. a] : [.. b]` compiles but CSharpier
   rejects it and the hook leaves the whole file unformatted. Spread over a conditional instead.
9. **The pre-commit hook re-stages whole files** — split commits by file groups that each build.
10. **Long Python patch scripts go in scratchpad FILES**, not Bash heredocs; match indentation from the
    file (a tab too many and nothing is written).

## 6. Not built / open

- **Phase 5 — playtest** (now) and **Phase 6 — close**: some creature doc comments still describe
  columns; the `design-card` skill needs rewriting for the Relay.
- **The bot keeps the order it is given** (no deploy search), answers choices naively, and the sim is
  **not tuned** — the curve numbers are invalid since the lanes died. Exploring: no sims.
- **Later (in the plan, section 4):** gym-leader rules (elites, previewed from the town — designed in
  `docs/paper/round-one-synergies.md`), kill-order layers (reinforcements joining their back), clocks,
  a relay chain meter, held items, training cycles in towns, monster-deck changes in towns, evolution.
- **Art**: the twelve round-one creatures and the tokens are silhouettes; real sprites need
  TRANSPARENT backgrounds (the `generate-card-art` skill assumes opaque squares).
- **Types: undecided** (Shayne: not "just because").
- The lane game still exists (DESCEND on the menu).

## 7. Next

1. Commit Phase 4 (see State).
2. **Shayne playtests the Relay** — the intro scenario, the four strategy scenarios, a run. Watch for:
   does the order feel like the decision? Is the back-to-front step readable? Is deploy enough
   positioning, with no moving mid-fight?
3. Act on what the playtest says before any new layer.
