# Handoff — THE COMPANION GAME: auto-battle, catching, the map, ten regions on a curve

**Read this, then the top of `KinJam.md` (its newest sections come first), then
`docs/design-principles.md` (what we learned, with the evidence), then the `design-card` skill before
touching any card, creature or foe.** Earlier `HANDOFF-Kin*.md` files describe games
that no longer exist — read only their scars sections.

State at handoff (2026-09-24): branch **`kin-pivot` at `de65db9`**, **242 tests green**, solution
and Godot project build, **nothing pushed**. `SQGodotCommon/project.godot` shows as modified — that
predates this work (a headless import strips two comment lines); **leave it out of commits.**

---

## 0. Where the game is, in one paragraph

KIN is a **monster-collecting roguelike deckbuilder** (Pokemon / Monster Rancher × Slay the Spire ×
a dash of Monster Train). **Your monsters fight on their own** — every creature, yours and the foes',
plays a telegraphed cycle of moves at the end of the turn, in Speed order. **You are the trainer**: a
deck of generic cards dropped ON a monster or a foe (Guard, Rally, Dash, Hasten, Stagger, Gust…), and
one free step per monster per turn to aim and dodge. **A foe's attack that finds no monster hits YOU**
(30 health for the run). You start with one monster and **catch** the rest with Snares. A run is
**ten regions** — town → one of two wild areas → a gym — tuned so the bot wins **25%**.

## 1. What this session did, in order

| Commit | What |
|---|---|
| `c46496b` | **AUTO-BATTLE v1** — owned cards replaced: move cycles in Speed order, a trainer's deck of generic cards, a free step (into an ally = swap) |
| `b09171b` | **The hover inspector** — hover any creature: stats, when it acts, passive, its whole cycle in words |
| `244641c` | **Catching v1** — the Snare (an item, not a card; a third of HP or less, certain, never a boss); the bench between battles |
| `bf757dc` | **THE MAP v1** — towns (full heal, a shop), two areas per region each with its OWN pool, trails (2 fights, a find, an optional deeper path with the area's rare), gyms, gold. Five new creatures |
| `229d380` | Portraits for Mosshell, Briar Viper, Cinder Newt, Bog Toad, Old Mire, Old Tusker |
| `d9d1b08` | **Trainer health** (an attack that lands on no monster hits you), **the gym leader** (the mirror: your swing into an empty column hits them), **the in-battle bench** (first benched steps in when one faints) |
| `dba8115` | **`party-sim`** — a beam-search bot that plays whole runs, reports per region |
| `de65db9` | **Ten regions tuned to Shayne's curve** via a difficulty-tier table |

Every design decision, its reason and Shayne's words are in `KinJam.md`, newest first: "THE CURVE",
"TRAINER HEALTH…", "THE MAP v1", "CATCHING v1", "AUTO-BATTLE v1". Why each exists is there — read it
before changing one.

## 2. The rules as built

- **Board:** a row of 5 spaces a side, column N faces column N. Attacks fire straight ahead (a shape
  of offsets), or home on the lowest HP.
- **A creature** (`Creature` → `Ally`, `Foe`): HP, Block, Speed, a `Pattern` (its cycle). Allies add
  Power and a passive (Thorns / Momentum / Off-Balance). A caught foe becomes an ally with exactly its
  cycle, Power 0, at the HP it was caught at, **and its region's scaling**.
- **The turn:** play cards (3 energy, hand of 5), take free steps, throw Snares — then END TURN: every
  creature acts once, fastest first, ties to you. Order and targets are fixed before anyone acts.
- **Your health:** 30, healed only in towns. A foe attack that lands on NO monster hits you once. 0 =
  the run is lost. All monsters (board and bench) down = lost.
- **A gym:** a leader with health (90 × the region's HP tier); your swing that lands on no foe hits
  them; 0 wins. No gym creature can be caught.
- **The run:** `PartyRun.Phase` — Town → ChooseArea → Trail (Battle, Battle, Find, Deep?) → Gym → next
  region's Town (everyone healed). Gold from wins buys Snares (30), cards (50), removals (40).

## 3. Where things are

- **Engine — `KinCore/Party/`:** `PartyModel` (Creature/Ally/Foe/Intent/PartyBattle), `PartyActions`
  (play a card, step, Snare, end/start turn, every `CardStep`, events), `PartyState` (THE API the
  board reads: `ActingOrder`, `IntentTargets`, `ForecastIfTurnEndsNow`, `AimsAtTrainer/Leader`,
  `StepRefusal`, `CatchRefusal`; `Act` resolves a move), `PartyContent` (starters, starter deck,
  rewards, original foes, practice scenarios, `PartyBattleFactory`), `PartyWorld` (new creatures,
  areas, gyms, **`Tiers` — THE TUNING TABLE**, `Regions`, `Trail`), `PartyRun` (the run, outside
  GameState), `PartyBot` + `PartySim` (the sim).
- **Tests — `KinCore.Tests/`:** `PartyTests` (battle rules), `PartyRunTests` (the run, the map, the
  tiers), `PartyBotTests` (the bot fires). Inline definitions only; real content only in the
  "every real area and gym builds a battle" check.
- **Screen — `SQGodotCommon/KinGame/`:** `KinPartyBoard` (the battle; the run flow is `Continue()`,
  dispatching on `PartyRun.Phase`), `KinPartyCell`, `KinPartyInspector`, `KinPartyRunScreens`
  (starters, after a battle, the end) + `KinPartyRunScreens.Map.cs` (town/shop, areas, find, deeper
  path, gym). Screen rules: `KinUI.md` "THE COMPANION GAME screen".
- **Console — `KinConsole/PartySimCommand.cs`.** Numbers: `docs/findings/companion-balance.md`.
- **Art — `SQGodotCommon/KinGame/Art/*.png`**, generated locally (ComfyUI at
  `D:\AI\ComfyUI_windows_portable`, DreamShaper XL Turbo; the `generate-card-art` skill). Cards have
  NO art yet — every card face is a placeholder silhouette.

## 4. Tools

```
dotnet run --project KinConsole -c Release -- party-sim 300       # ~4 min; per-region table vs the curve
dotnet run --project KinConsole -c Release -- party-sim trace 7   # one run, every play and turn
./Run-Godot.ps1 KinGame/kin_party.tscn -Capture shots/x -Seconds 1.2 -GameArgs '--starter=1','--screen=gymfight'
```
Capture flags (all in `Commands.md`): `--scenario`, `--click-space`, `--focus`, `--play=N[@S|@fS]`,
`--inspect`, `--snare`, `--end-turn`, `--starter` + `--screen=areas|find|deep|gym|gymfight|between|over`.

## 5. The curve, as measured (bot baseline — Shayne's call)

Target 90% through region 3 / 75% through 5 / 25% win. **Bot: 94% / 70% / 25.3%** (300 runs).
Per-region survival and what it took: `docs/findings/companion-balance.md` (c). The one-line lesson:
**HP and damage scaling did nothing — catches scale with their region. More foes than monsters (up to
five) is the lever**, and every death since is your health on the trail. Known: **region 5 spikes**
(77% vs 91%, the Ridge+Crags pools); **gyms barely kill**; racing the leader wins 42–82% of gyms.

## 6. Scars worth not re-earning

1. **Python text mode writes CRLF on Windows.** Patch scripts using `open(p, 'w')` turned LF files
   CRLF and a commit rewrote every line. The repo MIXES endings per file (`CLAUDE.md` is CRLF): read
   with `newline=''`, normalise, write back in the file's own ending. Check `git diff --stat` for
   absurd counts before committing.
2. **Splitting a mixed tree into commits:** the pre-commit hook runs CSharpier on each staged `.cs` and
   re-adds it WHOLE — partial staging cannot survive it. What works: build each intermediate state as
   files (forward-apply from HEAD, or reverse the later patches), commit them in turn in a scratch
   `git worktree`, build + test each, then `git reset --mixed <tip>` in the main tree, run
   `dotnet-csharpier` on the changed files and confirm `git diff` is empty (bar `project.godot`).
3. **CSharpier reformats on commit**, so a patch anchored on pre-commit text stops matching. Read the
   current text first.
4. **Bash heredocs feeding Python break on quotes** — put scripts in files (scratchpad) and run them.
   A `cat > file` with no input waits forever.
5. **A capture cannot hover, drag, or click the FOE row.** `--focus`, `--inspect`, `--snare`, `--play`,
   `--screen` stand in; they show the look, not the input path.
6. **An unwrapped Label is as wide as its text** and drags its whole container with it — the hint strip
   pushed END TURN off screen. `AutowrapMode` + `CustomMinimumSize.X = 1`. A wrapped label measured
   before layout takes a word per line — the inspector re-fits every frame.
7. **New art is not imported by a plain run:** `godot-mono --path SQGodotCommon --headless --import`.
8. **Generated art:** the subject noun decides the picture; cull at 500px (two-headed vipers, four
   tusks). Three candidates a creature.
9. **The sim is a floor:** a one-move bot measured ITSELF (67%, all "hard"); the beam-search bot said
   99%. Trace a run before believing a number. The bot sees what Dash will draw — a small peek.

## 7. Not built / open

- **In-battle bench choice** — the first benched steps in; the player does not choose.
- **Caught monsters have no passive and no colour** of their own (they show as slate).
- **Card art**, and signature cards per monster (allowed, rare — none exist).
- **Regions 3–10 reuse four areas and two gyms**; new creatures come once the curve settles.
- **The lane game still exists** (DESCEND on the menu) — deleting it is a deliberate later step.
- **Items and events that edit a monster's cycle** (Monster Rancher training) — designed, not built.

## 8. What comes next — agreed with Shayne

1. **Shayne plays a full ten-region run** and compares himself to the bot. If he loses early where
   the bot clears 94%, set the bot's targets above the player's (the curve is a PLAYER curve).
2. **New creatures and areas** for regions 3–10, now that the curve holds (re-run `party-sim` after).
3. Smooth **region 5**, and even out **the leader race** (a real choice, not a default).
4. Passives / colours for caught monsters; choosing who steps in from the bench.
5. When the pivot is certain: delete the lane game and fix the root `CLAUDE.md` solution map.
