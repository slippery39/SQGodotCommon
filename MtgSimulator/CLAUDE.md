# MtgSimulator

Runs N simulated games with configurable AI strategies and reports aggregate stats, timing, per-card win rates, and flagged games.

## Source Map

| File | Purpose |
|------|---------|
| `Program.cs` | Entry point — prompts for game count and AI depth, runs `SimulatorRunner` |
| `SimulatorRunner.cs` | Orchestrates N games, aggregates results, prints all reports |
| `GameRunner.cs` | Runs a single game to completion using two `IAiStrategy` implementations |
| `IAiStrategy.cs` | Interface: `SelectAction` + `ResolveChoice` — all AI implementations conform to this |
| `RandomAiStrategy.cs` | Baseline AI — picks a random legal action; used as playout policy |
| `DepthLimitedAiStrategy.cs` | Greedy depth-limited DFS AI — retained for comparison; not the default |
| `BeamSearchAiStrategy.cs` | Beam search AI — the default strategy; two-bucket pruning (concrete + potential) |
| `IPotentialEvaluator.cs` | Interface for secondary "potential" signals used during beam pruning |
| `FastManaPotentialEvaluator.cs` | Potential evaluator that scores by current available mana (preserves fast-mana lines) |
| `StateEvaluator.cs` | Scores a `GameState` from a given player's perspective (float) |
| `CardPool.cs` | Defines the full card pool; `BuildRandomDeck` samples 40 random cards per game |
| `ZooDeckFactory.cs` | Builds a fixed 60-card Zoo deck (RGW aggro — 24 Plains + 36 spells) for a given player |
| `GoblinsDeckFactory.cs` | Builds a fixed 60-card Goblins deck (red aggro tribal — 24 Plains + 36 spells) for a given player |
| `DeckRegistry.cs` | Registers all named precon decks (`DeckInfo` records); exposes `All` and `Build(name, ownerId)` |
| `PreconstructedStats.cs` | Aggregates precon game results into four stat tables; exposes row records for deck (inc. AvgWinTurn/MinWinTurn/MaxWinTurn), matchup, card GIH WR (inc. AvgCopiesPlayed), and card-per-matchup GIH WR (inc. AvgCopiesPlayed) |
| `PreconstructedSimulatorRunner.cs` | Round-robin precon runner: builds schedule, runs games via `GameRunner`, feeds `PreconstructedStats`, prints console summary, triggers CSV export |
| `PreconstructedCsvExporter.cs` | Writes all four stat tables to `sim_results/precon_<timestamp>.csv`; escapes card names with commas (e.g. "Krenko, Mob Boss") |
| `GameResult.cs` | Record capturing outcome, turn count, actions, drawn/played cards, end reason, duration, all events, exception info |
| `PreconstructedGameResult.cs` | Wraps `GameResult` with deck names and on-play metadata for preconstructed mode |
| `GameStateSnapshot.cs` | Human-readable snapshot DTO — `GameStateSnapshot`, `PlayerSnapshot`, `CreatureSnapshot`, `TurnLog` |
| `FlaggedGameSaver.cs` | Builds a snapshot from a flagged `GameState` and writes it as JSON to `flagged_games/` |

## Architecture

`SimulatorRunner` creates a game via `SetupGame()` (calls `MtgGameFactory.Create()`, builds random decks from `CardPool`), injects two `IAiStrategy` instances into a `GameRunner`, and collects `GameResult`s. All reporting runs after all games complete.

`GameRunner` owns the full game lifecycle: it calls `BeginGame` at the start of `Run()`, captures the begin-game events (initial hand draws, first turn start) into `AllEvents` and `DrawnCards`, then drives the game loop. This ensures no events are lost to the caller. The loop checks for pending choices first (delegates to `activeStrategy.ResolveChoice`), then calls `MtgActionGenerator.GetLegalActions` and `activeStrategy.SelectAction`. When no legal actions remain, it auto-fires `EndTurnAction`.

## Game Limits (in `GameRunner`)

| Limit | Threshold | Effect |
|-------|-----------|--------|
| Time limit | 5 000 ms wall-clock | `GameEndReason.TimeLimitReached` — game ends as draw |
| Turn limit | 100 turns | `GameEndReason.TurnLimitReached` — game ends as draw |
| Action warning | 50 actions in one turn | `HadActionWarning = true` — game continues |
| Action limit | 100 actions in one turn | `GameEndReason.ActionLimitReached` — game ends as draw |
| Unhandled exception | any thrown exception | `GameEndReason.UnhandledException` — game ends as draw, exception captured |

`GameRunner.Run()` wraps the entire game loop in a try/catch. On exception, it terminates with `UnhandledException`, capturing the last known `GameState`, all events up to the crash, and the exception message and stack trace — then returns normally so the run continues with the next game.

`GameRunner.Run()` returns `(GameResult Result, GameState FinalState)` — the final state is passed to `FlaggedGameSaver` by both runners when the result is flagged.

Flagged games (any of the above) are collected separately and printed in the flagged games report. Both `SimulatorRunner` and `PreconstructedSimulatorRunner` track and report flagged games.

## AI Strategies

**`RandomAiStrategy`** — picks a uniformly random legal action. Baseline; also used as the playout policy inside more sophisticated strategies.

**`DepthLimitedAiStrategy`** — greedy depth-first search to a fixed depth. Not minimax — opponent responses during search are not modelled. Retained for comparison; not the current default.

**`BeamSearchAiStrategy`** — beam search (breadth-first) to a fixed depth. At each level, candidates are pruned to two buckets before expanding the next level:
- **Concrete bucket** — top N by `StateEvaluator` score (default: 10)
- **Potential bucket** — top M per `IPotentialEvaluator` (default: 5 slots via `FastManaPotentialEvaluator`)

Potential evaluators preserve setup lines (fast mana, etc.) that score poorly on the main evaluator but may enable a win condition deeper in the tree. The final action is always chosen by concrete score at the leaf level. Falls back to `EndTurnAction` as a tiebreaker (to avoid neutral attacks or pointless spells), then random among remaining ties. Current default: depth 3, concreteSlots 10, with `FastManaPotentialEvaluator` injected by default.

**Land-first override**: before entering beam search, `SelectAction` plays any available `PlayLandAction` immediately. Permanent mana is the highest-priority resource; no search is needed for this decision.

**`IPotentialEvaluator`** — pluggable interface for secondary beam-pruning signals. Implement to add new potential heuristics (graveyard value, storm count, etc.) without touching the search logic.

**`IAiStrategy`** — swap implementations freely; `GameRunner` and `SimulatorRunner` only depend on the interface.

## StateEvaluator

Scores a non-terminal state as a weighted sum. Terminal states short-circuit.

| Factor | Weight |
|--------|--------|
| Life difference (player − opponent) | 0.4 |
| Creature count difference | 3.0 |
| Total effective Power difference (permanent power only) | 2.0 |
| Cards in hand difference | 1.1 |
| Player's own permanent mana (`MaxMana` only — temporary fast mana excluded) | 2.0 |
| Win (opponent has lost) | +10000 |
| Loss (player has lost) | −10000 |

`MaxMana` weight is high (2.0) because in the land system permanent mana is the primary resource — a land behind means fewer spells castable every turn for the rest of the game. Power uses permanent power only (`GetEffectivePermanentPower`); `UntilEndOfTurn` buffs like Giant Growth are excluded since they evaporate next turn.

Zone IDs are read directly from `MtgGameIds` to avoid child-list scans on every evaluation call.

When tuning weights: changes here affect `BeamSearchAiStrategy` and `DepthLimitedAiStrategy`. `RandomAiStrategy` ignores evaluation.

## CardPool

Defines all cards available for random deck generation. `BuildRandomDeck(ownerId, deckSize = 40, landCount = 17)` builds a 40-card limited deck: 17 Plains + 23 non-land cards sampled without replacement from the pool. Land cards in the pool are excluded from the non-land draw.

**Targeting restriction**: damage spells target opponents and opponent creatures only. The random AI has no targeting intelligence, so restricting targets at the card level prevents self-damage. This is intentional — do not add friendly targets to simulator cards without also updating the AI strategy.

**EventTriggerCondition migration**: `CardPool` contains duplicate cards — one set using concrete condition classes (`CreatureDiesCondition`, `CreatureAttacksCondition`, etc.) and one set using the generic `EventTriggerCondition` system (Grim Watcher, Soul Harvester, War Drummer, Battlefield Scholar). Both run side by side for validation. Once the `EventTriggerCondition` versions are confirmed correct, the old concrete-condition versions should be removed.

## Reports

`SimulatorRunner` prints four sections after all games complete:

1. **Aggregate report** — total games, P1/P2 wins, draws, avg turns, avg actions, end reason breakdown (including time limit), action warnings.
2. **Timing report** — total time, avg/min/max ms per game, games per second. Duration comes from `GameResult.GameDurationMs` (measured inside `GameRunner`).
3. **Card win rate when drawn** — per-card P1 draw count + win%, P2 draw count + win%, combined win rate. Sorted descending by combined win rate.
4. **Flagged games** — game number, winner, turn count, total actions, wall-clock time, which limits were hit, and `[saved]` if a snapshot was written. Lists the save directory and cap status.

## Flagged Game Snapshots

When a game is flagged, both runners call `FlaggedGameSaver.TrySave()` immediately after the game ends. Snapshots are written to `flagged_games/` (next to the binary) as indented JSON. Naming: `game_{number}_{reason}_{timestamp_ms}.json`. Saves are capped at `FlaggedGameSaver.MaxSaves` (25) per run — if a run produces more flagged games the first 25 are sufficient to diagnose the cause.

Each snapshot includes:
- Final board state: per-player life, mana, hand card names, library count, graveyard, battlefield creatures (with P/T, damage, sickness, attacked flags)
- Stack contents at termination
- Turn-by-turn event log (`TurnLogs`): one `TurnLog` per half-turn, each containing human-readable event strings (e.g. "Player 1 cast Lightning Bolt", "Goblin attacked", "Player 2 took 3 damage")
- Exception message and stack trace when `EndReason` is `UnhandledException`

`FlaggedGameSaver.TrySave` no longer takes `MtgGameIds` — it resolves all IDs it needs directly from `GameState` via `GetWellKnownId`.

## Known Issues / Tech Debt

- **`IAiStrategy` is in the `MtgCore` namespace** despite its file living in `MtgSimulator/`. Should be moved to the `MtgSimulator` namespace for correctness.
- **EventTriggerCondition migration** — see CardPool section above.

## Key Rules

- Never duplicate legal action generation — always call `MtgActionGenerator.GetLegalActions`. Do not reimplement this in simulator code.
- Never call `BeginGame` from setup code — `GameRunner.Run` is responsible for calling it. Pass a pre-begin `GameState` to `Run`; it calls `BeginGame` internally and captures all resulting events.
- `SimulatorRunner.SetupGame()` uses `MtgGameFactory.Create()` (real mana — players start at 0/0), not `CreateForTesting()`. The simulator tests real land-based mana constraints.
- All state mutation goes through `GameAction`s — no direct state modification in the simulator.
