# Handoff — KIN harvest, the other-machine merge, and five reproducibility fixes (2026-10-06)

**State at handoff:** branch `MTG-ImmutableObjects-PlayingAround`, **all 1,530 tests green**
(ImmutableGameObjects 111, MtgCore 846, SQGodotCommon 119, MtgSimulator 454), build clean. **Ahead
of origin, behind 0 — not pushed** (Shayne pushes). One file uncommitted at the time of writing:
`SQGodotCommon/MtgGame/Assets/card_values_csc.json` (the regenerated table, copied into the game).
Shayne reviews and asks for each commit — never commit unasked.

---

## 1. What happened, in order

**Harvested the reusable parts of the `kin-pivot` branch** (a separate game, KIN, built on this
framework). Commits `54ba5b8`..`39d75df`:

- `Common/` fixes: **touch drag** (`DraggableNode2D` arms on press, starts on motion — a finger's
  press is its first contact, so "am I hovered?" was always false), Singleton teardown noise, and
  **GameManager is an autoload** so any scene opens directly.
- `Run-Godot.ps1` (godot-mono, second monitor, capture folder + `.gdignore`) and its docs in
  `Commands.md`; `.claude/rules/godot-frontend.md` (silent Godot traps + the shared card scene).
- Three process principles in root `CLAUDE.md`; `docs/roguelike-deckbuilder/design-principles.md`.
- **Android build**: `Build-Apk.ps1` → `build/mtg.apk`; net9.0 pin on the Godot chain; card art
  imported lossy (145 MB → 22 MB); an `Android MTG` preset in the gitignored, branch-shared
  `export_presets.cfg` beside KIN's `Android`. All five quiet export failures are in `Commands.md`.
- Skills `match-mockup`, `generate-art`, `add-icon` (game-agnostic) + `tools/gen_art.py`,
  `img2img.py`, `pixelate.py`.

**Merged 13 commits pushed from another machine** (`e573106`). This machine had continued from
09-04 without pulling. Kept: ComboProbe, cost inversion, the discard-outlet probe fix, the
exploration/optimisation split + Harvest, `RunningSimulations.md`. The reanimator bug had been fixed
on BOTH machines — one implementation kept. **The merge would have shipped a bug**: Harvest built
decks from `Decklist.Empty`, dropping colour identity; fixed and pinned.

**Then five fixes, each found by measuring rather than reading:**

| commit | fix |
|---|---|
| `6464077` | **Planeswalkers** threw in the activation probe (one loyalty ability per turn; the catch discarded the whole probe). All 19 in DES read as producing nothing. Probe catches now record the exception MESSAGE, not just its type. |
| `ac2f058` | **Leverage was non-reproducible**: the sandbox table had `RngSeed` 0, which the engine reads as "truly random". `CreateForTesting` seeds too. Two mode 7 runs at one seed are now byte-identical. |
| `939cfa3` | Piped console runs ended in an unhandled `ReadKey` exception; every piped recipe re-verified. **Both mode-4 recipes in `Commands.md` had been training on CMB, not CSC**, since Hollowmere retired. |
| `462c471` | `ColorSlotSurvivalTests` wrote into the user's `sim_results/` depending on test order. |
| `e3cafe4` | **Trigger loop moved into the engine** (`ImmutableGameObjects/Triggers.cs`); MtgCore runs on it. |
| `880498c` | Culling's leftovers removed (dead code, an orphaned doc comment, docs describing it as live). |

`card_values_csc.json` was regenerated: **17 of 408 cards changed**, all random-effect cards
(Flames of the Firebrand 0.10 → 12.20, Inferno Titan 43.18 → 60.60). Two sweeps are byte-identical.

---

## 2. Decisions already taken — do not re-litigate

- **KIN is standalone.** A jam project that tested the framework; it will never be merged back or
  synced. Engine changes here need not stay compatible with `KinCore`.
- **The engine uses MTG's trigger names** (`MaxTriggers`, `MaxTriggersPerTurn`, `TriggerCount*`).
- **Components stay pure data.** `TriggeredAbility` holds only caps; a game passes "matches" and
  "spawns" to `FireTriggers<T>` as arguments. No default concrete trigger type until a second game
  needs one. Emblem triggers keep their own loop (no caps, no source card).
- **`install_art.py` and `make_ui.py` stay on kin-pivot** until a game here needs cut-outs or has a
  locked palette. `draw-card-art` (SVG) was not wanted.
- **Android:** card art is imported LOSSY; one export preset per game, never repurpose another's.
- Design lessons for roguelike deckbuilders live under `docs/roguelike-deckbuilder/`, not as rules.

---

## 3. Scars — each cost real time this session

- **Line endings are per-file in this repo** (CRLF, LF and MIXED, no `.gitattributes` for code).
  Git Bash's `sed -i`, the Edit tool and `cat >` heredocs all normalise a file and turn a one-line
  change into a whole-file diff. Check `git ls-files --eol <file>` before and after, compare with
  `git diff --ignore-cr-at-eol`, and restore by replaying the real line changes onto HEAD's bytes.
- **Git Bash heredocs mangle backslashes.** Python with `\n` inside a `printf` string failed three
  times. Write the script to a file instead.
- **The CSharpier pre-commit hook re-stages whole `.cs` files**, so a partially staged `.cs` file
  gets committed whole. Non-`.cs` files can be staged partially.
- **Piping only a mode number RUNS that mode with defaults** — probing "what's the set menu" with
  `printf '7\n'` ran a full mode 7. Read `Program.cs` for prompt order instead.
- **Any fixture that skips `SetupGameAction` has `RngSeed` 0 = unseeded randomness** unless it seeds
  itself. `CreateForTesting` now does; a new hand-built fixture must.
- **A probe `catch` that records only the exception type hides the bug for weeks.** Record the
  message.
- **A smoke run writes into `sim_results/`.** Back up `constructed_values_*` before a console run,
  checksum after, restore. The presim tables are the expensive ones.
- **Mode 7 is now deterministic, which makes it a free regression check**: run
  `printf '7\n\n4\n10\n8\nsmoke\n'` before and after an engine change and diff the reports (strip
  the PID line). The trigger consolidation was verified this way.
- `Decklist.Identity` is optional on the record — any operator building from `Decklist.Empty` must
  copy it across or the next `ValidateFieldIdentities` throws.

---

## 4. Open items, roughly by value

1. **Push.** Origin is behind by everything above.
2. **Install `build/mtg.apk` on a phone** (`adb install`, or the cloudflared tunnel in `Commands.md`)
   — check the menu and touch drag. Touch problems go to `DesignNotes.md`. The touch half of
   `godot-frontend.md` is unverified until then.
3. **Missing card art.** Several CSC cards show the placeholder dagger (Sylvan Ranger, Radha, Return
   to Nature, Conclave Mentor…) though `_needs_art_csc.txt` lists only two tokens. Count them first;
   `generate-art` can fill the gaps, and that is when porting `install_art.py` becomes worth it.
4. **Numbers recorded before 2026-10-06 need re-measuring before anyone quotes them**: every
   leverage / `supp'd` figure (unseeded noise), and every figure in the merged other-machine docs
   (measured before colour identity; DES was 732 cards then, 429 now).
5. **Planeswalkers are measured on their first legal loyalty ability only** — measuring each from a
   separate branch is not built.
6. **`PoolFeatures`' other `ProcessAllActions` calls** (`ProbeTriggers`, `ProbeChainedTriggers`,
   `ProbeCostDemands`) have not been audited for pausing on a choice — the bug the discard-outlet
   fix found in `ProbeCardProfiles`.
7. Leftover disposable files from this session in `sim_results/`: `engines_leg/des/cmb_20261006_*`
   and today's `metagame_csc_*`.

---

## 5. Where things are

| | |
|---|---|
| How to run every console mode, verified prompt lists | `RunningSimulations.md` |
| Commands, Godot runs, captures, Android build | `Commands.md` |
| Engine discovery, leverage, ComboProbe, cost inversion | `.claude/rules/sim-discovery.md` |
| Evolution, Harvest, exploration/optimisation | `.claude/rules/sim-evolution.md` |
| Godot UI traps | `.claude/rules/godot-frontend.md` |
| The engine trigger loop and the spawn/post-processor order trap | `ImmutableGameObjects/ImmutableGameObjects/CLAUDE.md` |

```
dotnet build
dotnet test                                                              # 1,530, ~2.5 min
dotnet test MtgSimulator.Tests --filter "FullyQualifiedName~CardValueSweep.SweepTheCoreSetCube"
./Run-Godot.ps1 MtgGame/Draft/DraftScene.tscn -Capture shots/draft -Seconds 4
./Build-Apk.ps1
```
