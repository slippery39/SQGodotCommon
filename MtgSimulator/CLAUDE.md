# MtgSimulator

Class library: AI strategies, game runners, deck factories, reporting. Referenced by Godot and by
`MtgSimulator.Console` (the runnable entry point). Keeping it a library prevents file-locking
conflicts when the console and Godot run at once.

## Where the detail lives

This file is the map. Subsystem detail is in `.claude/rules/`, which loads **only when you open a
matching file** — so read the rule file directly if you are reasoning about a subsystem without
editing it. Measured results are in `docs/findings/`, which never loads on its own.

| Subsystem | Rules (auto-loads on matching files) | Measured results (read on demand) |
|---|---|---|
| AI search, evaluators, card values | `.claude/rules/sim-ai.md` | `docs/findings/ai-strength.md` |
| Draft, card sets, draft training | `.claude/rules/sim-draft.md` | `docs/findings/draft-training.md` |
| Engine discovery (mode 7) | `.claude/rules/sim-discovery.md` | `docs/findings/engine-discovery.md` |
| Constructed evolution (mode 6) | `.claude/rules/sim-evolution.md` | `docs/findings/evolution.md` |
| Scenarios, snapshots, inspector | `.claude/rules/sim-scenarios.md` | — |

**The root `CLAUDE.md` no longer describes MTG** — the repo's active project is DOOMJAM. What used to
live there is now in `docs/mtg/`: `ai-tooling.md` (Space/F6/F7, console modes 5-7),
`measured-tables.md` (regenerating `sim_results/` and the working-directory trap), `card-sets.md`
(the set menu, which has shifted twice), `colours.md` (pips and the manabase tables).

Commands to run any of it: `Commands.md` at the solution root.

**Before proposing an evaluator or scoring change, read the findings file first.** Four evaluator
changes have been measured and all four came back neutral; the pattern is recorded in
`docs/findings/ai-strength.md` and it should temper any fifth.

## Source Map

Non-obvious files only — the rest are named for what they do.

| File | Purpose |
|------|---------|
| `SimulatorRunner.cs` | Orchestrates N games, aggregates, prints all reports |
| `GameRunner.cs` | Runs one game to completion from two `IAiStrategy`s; owns the lifecycle |
| `GameSetup.cs` | `FromDecks` — two built decks → pre-begin `GameState`. Takes deck BUILDERS because owner ids only exist once the game does |
| `CardStatAccumulator.cs` | Games-in-hand counting per card and pair. Shared by `DraftTrainer` and `MetagameEvolver` so the two tables stay comparable |
| `CardPool.cs` | Card pool for random decks; `BuildRandomDeck` samples 40 |
| `StateEvaluator.cs` | Scores a `GameState` from one player's perspective |
| `EvaluationBreakdown.cs` | A score split into its eight terms; `readonly record struct`, allocates nothing |
| `AiDecision.cs` | One captured decision, its ranked candidates, their term breakdowns |
| `CardValueSandbox.cs` | What a card is worth ONCE CASTABLE — cast into a fixed position, rolled forward |
| `FastManaPotentialEvaluator.cs` | Potential signal for beam pruning; preserves fast-mana lines |
| `DrawDiagnostics.cs` | End-of-training draw report: reason split, run-position trend, board medians, per-card lift |
| `GameStateSnapshot.cs` | Human-readable snapshot DTO. **Cannot be loaded back** — see `sim-scenarios.md` |
| `FlaggedGameSaver.cs` | Flagged `GameState` → JSON in `flagged_games/` |
| `Decks/*DeckFactory.cs` | Fixed 60-card precons (Zoo, Goblins, Valakut, Jund, Delver, Dragonstorm, Reanimator, Traditional Storm, Affinity); `DeckRegistry.cs` registers them and exposes `All` / `Build` |
| `Preconstructed*.cs` | Precon round-robin runner, its four stat tables, and the CSV export |
| `Draft/Draft.cs` | `DraftFormat`/`DraftSeat`/`DraftState` + `Create`, `ApplyPicks`, `RunToCompletion`, `BuildDeck` |
| `Draft/DraftPickers.cs` | `DraftPicker` delegate; `Random` and `Curve` pickers |
| `Draft/DraftRunner.cs` | All-AI draft harness: draft a table, build decks, round-robin, print per-seat/per-picker rates |
| `Draft/DraftTournament.cs` | Round-robin standings for a pod with one human seat |
| `Draft/DraftTrainingData.cs` | Count DTOs, `Shrink`, `Merge`; store load/save and `PathFor(setCode)` |
| `Draft/DraftTrainer.cs` | Training mode: N drafts, parallel games, games-in-hand counts |
| `Evolution/Decklist.cs` | `Decklist` + its invariants (60 cards, max 4, 20-26 lands), `Difference`, `Materialize` |
| `Evolution/ConstructedValues.cs` | Card/pair values from constructed games, shrunk toward the draft prior |
| `Evolution/DeckBuilder.cs` | Seeding (anchor + synergy kernel + curve) and the three mutation operators |
| `Evolution/PoolFeatures.cs` | What a card ASKS and which cards ANSWER, read off the cards by reflection — no mechanic-to-meaning table |
| `Evolution/Goldfish.cs` | Solitaire vs an inert opponent. Turns-to-kill is **descriptive only** — measured as the wrong fitness |
| `Evolution/EngineProbe.cs` | **Did the payoff resolve with its support deployed?** One pass over one game's event log |
| `Evolution/PreSimulation.cs` | Uniform-random constructed decks played before evolution; also the pool-conditioned card-value sampler |
| `Evolution/EngineDiscovery.cs` | Mode 7 — probe every concept, rank by assembly, save `sim_results/engines_<set>_<stamp>.json` |
| `Evolution/MetagameEvolver.cs` | Mode 6 — the evolution loop, paired evaluation, culling, report |
| `Evolution/MutationLog.cs` | Every proposal the search considered, not only what survived |
| `Scenarios/StateJson.cs` | Real `GameState` ↔ JSON; reflection-based `$type` discriminators |
| `Scenarios/Scenario.cs` | `Scenario` record + `ScenarioStore` in `scenarios/`; `ScenarioComparer`/`ScenarioConsole` drive mode 5 |

## Architecture

`SimulatorRunner` creates a game via `SetupGame()` (`MtgGameFactory.Create()`, random decks from
`CardPool`), injects two `IAiStrategy`s into a `GameRunner`, collects `GameResult`s. All reporting
runs after all games complete.

`GameRunner` owns the full lifecycle: calls `BeginGame` at the start of `Run()`, captures the
begin-game events (initial draws, first turn start) into `AllEvents`/`DrawnCards`, then drives the
loop — pending choices first (`ResolveChoice`), then `MtgActionGenerator.GetLegalActions` and
`SelectAction`. No legal actions left, it auto-fires `EndTurnAction`. This is why no events are
lost to the caller.

## Game Limits (in `GameRunner`)

| Limit | Threshold | Deterministic? | Effect |
|-------|-----------|---|--------|
| Turn limit | 100 turns | yes | `TurnLimitReached` — draw |
| Action warning | 100 actions in one turn | yes | `HadActionWarning = true` — continues |
| Action limit | 200 actions in one turn | yes | `ActionLimitReached` — draw |
| Safety timeout | 1 800 000 ms wall-clock | **no** | `TimeLimitReached` — excluded from training, not a draw. Hang catcher only; must never fire normally |
| Unhandled exception | any | — | `UnhandledException` — excluded, exception captured |

The wall-clock timeout is the one non-deterministic termination, and it is why mode 6 was not
reproducible at `presim 800`. Detail in `.claude/rules/sim-ai.md`.

## CardPool rules

**Damage spells target opponents and opponent creatures only.** The random AI has no targeting
intelligence, so restricting at the card level prevents self-damage. Do not add friendly targets to
simulator cards without also updating the AI strategy.

**EventTriggerCondition migration** — `CardPool` holds duplicate cards, one set on concrete
condition classes and one on generic `EventTriggerCondition` (Grim Watcher, Soul Harvester, War
Drummer, Battlefield Scholar). Both run side by side for validation; remove the concrete versions
once the generic ones are confirmed.

## Reports

`SimulatorRunner` prints four sections after all games complete: **aggregate** (totals, end-reason
breakdown, warnings), **timing** (from `GameResult.GameDurationMs`, measured inside `GameRunner`),
**card win rate when drawn** (per-card P1/P2 draw counts and combined rate, sorted descending), and
**flagged games** (which limits were hit, `[saved]` if a snapshot was written, plus the save
directory and cap status).

## Key Rules

- Never duplicate legal action generation — always call `MtgActionGenerator.GetLegalActions`.
- Never call `BeginGame` from setup code — `GameRunner.Run` does it and captures the resulting events. Pass it a pre-begin `GameState`.
- `SimulatorRunner.SetupGame()` uses `MtgGameFactory.Create()` (real mana, players start at 0/0), not `CreateForTesting()`. The simulator tests real land-based mana constraints.
- All state mutation goes through `GameAction`s — no direct state modification in the simulator.

## Known Issues / Tech Debt

- **`IAiStrategy` is in the `MtgCore` namespace** despite living in `MtgSimulator/`. Should move.
- **EventTriggerCondition migration** — see CardPool rules above.
- **`BeamSearchAiStrategy` does not populate the breakdown.** Only `MultiTurnBeamSearchAiStrategy`
  fills `StateBefore`/`Breakdown`, so the inspector shows bare scores when the plain beam is picked
  in the scenario viewer. Deliberate (MultiTurn is the default) — wire it once the format settles.
- **Neither the F6 overlay nor the F7 toast has been visually verified.** Both compile, the suite is
  green, the layout is unreviewed.

## Console Entry Point

`MtgSimulator.Console/` is `Program.cs` plus a reference to this library. Seven modes: 1 = random
pool, 2 = precons, 3 = draft, 4 = train draft pickers, 5 = inspect a scenario, 6 = evolve a
constructed metagame, 7 = engine discovery.

`ReadSeed()` is shared by 1/3/4/6 (blank = random, number = literal, word = FNV-1a hashed).
`ReadSet()` is shared by 3/4/6 and skips its prompt while only one choice exists; **mode 6 alone
passes `includeCombined: true`**, adding the merged all-sets pool. Mode 5 returns before the
AI-depth prompt; mode 6 defaults that depth to 2 rather than 3, since it plays far more games and
only needs both sides equally strong. Mode 3 auto-loads the set's model via
`DraftTrainingStore.PathFor` if present and adds the `Trained` picker.

`ServerGarbageCollection` is enabled here. `sim_results/` and `flagged_games/` are written relative
to the console's working directory.
