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
| `DepthLimitedAiStrategy.cs` | Greedy depth-limited search AI — the default strategy |
| `StateEvaluator.cs` | Scores a `GameState` from a given player's perspective (float) |
| `CardPool.cs` | Defines the full card pool; `BuildRandomDeck` samples 40 random cards per game |
| `ZooDeckFactory.cs` | Builds a fixed 40-card Zoo deck (RGW aggro) for a given player |
| `GameResult.cs` | Record capturing outcome, turn count, actions, drawn cards, end reason |

## Architecture

`SimulatorRunner` creates a game via `SetupGame()` (calls `MtgGameFactory.Create()`, builds random decks from `CardPool`, then `BeginGame`), injects two `IAiStrategy` instances into a `GameRunner`, and collects `GameResult`s. All reporting runs after all games complete.

`GameRunner` drives the game loop: checks for pending choices first (delegates to `activeStrategy.ResolveChoice`), then calls `MtgActionGenerator.GetLegalActions` and `activeStrategy.SelectAction`. When no legal actions remain, it auto-fires `EndTurnAction`. Events are tracked throughout for card draw tracking and game-over detection.

## Game Limits (in `GameRunner`)

| Limit | Threshold | Effect |
|-------|-----------|--------|
| Turn limit | 100 turns | `GameEndReason.TurnLimitReached` — game ends as draw |
| Action warning | 50 actions in one turn | `HadActionWarning = true` — game continues |
| Action limit | 100 actions in one turn | `GameEndReason.ActionLimitReached` — game ends as draw |

Flagged games (any of the above) are collected separately and printed in the flagged games report.

## AI Strategies

**`RandomAiStrategy`** — picks a uniformly random legal action. Baseline; also used as the playout policy inside more sophisticated strategies.

**`DepthLimitedAiStrategy`** — greedy best-first search to a fixed depth. Not minimax — opponent responses during search are not modelled (the opponent's turn plays out naturally in the game loop). Falls back to random when all actions score equally. Win cutoff: stops evaluating once a winning score (`>= StateEvaluator.WinScore`) is found at any node — propagates upward.

**`IAiStrategy`** — swap implementations freely; `GameRunner` and `SimulatorRunner` only depend on the interface. Current default: `DepthLimitedAiStrategy` at depth 3 for both players.

## StateEvaluator

Scores a non-terminal state as a weighted sum. Terminal states short-circuit.

| Factor | Weight |
|--------|--------|
| Life difference (player − opponent) | 2.0 |
| Creature count difference | 3.0 |
| Total base Power difference | 1.5 |
| Cards in hand difference | 1.0 |
| Player's own current mana | 0.5 |
| Win (opponent has lost) | +10000 |
| Loss (player has lost) | −10000 |

Zone IDs are read directly from `MtgGameIds` to avoid child-list scans on every evaluation call.

When tuning weights: changes here affect `DepthLimitedAiStrategy` only. `RandomAiStrategy` ignores evaluation.

## CardPool

Defines all cards available for random deck generation. `BuildRandomDeck(ownerId, deckSize = 40)` samples without replacement.

**Targeting restriction**: damage spells target opponents and opponent creatures only. The random AI has no targeting intelligence, so restricting targets at the card level prevents self-damage. This is intentional — do not add friendly targets to simulator cards without also updating the AI strategy.

**EventTriggerCondition migration**: `CardPool` contains duplicate cards — one set using concrete condition classes (`CreatureDiesCondition`, `CreatureAttacksCondition`, etc.) and one set using the generic `EventTriggerCondition` system (Grim Watcher, Soul Harvester, War Drummer, Battlefield Scholar). Both run side by side for validation. Once the `EventTriggerCondition` versions are confirmed correct, the old concrete-condition versions should be removed.

## Reports

`SimulatorRunner` prints four sections after all games complete:

1. **Aggregate report** — total games, P1/P2 wins, draws, avg turns, avg actions, end reason breakdown, action warnings.
2. **Timing report** — total time, avg/min/max ms per game, games per second.
3. **Card win rate when drawn** — per-card P1 draw count + win%, P2 draw count + win%, combined win rate. Sorted descending by combined win rate.
4. **Flagged games** — game number, winner, turn count, total actions, and which limits were hit.

## Known Issues / Tech Debt

- **`StateEvaluator` uses base Power, not effective Power** — reads `CreatureComponent.Power` directly instead of `state.GetEffectivePower(...)`. P/T modifiers (Giant Growth etc.) are invisible to the AI's board evaluation. Should be updated to use `CreatureEvaluator` extension methods to stay consistent with the rest of the engine.
- **`IAiStrategy` is in the `MtgCore` namespace** despite its file living in `MtgSimulator/`. Should be moved to the `MtgSimulator` namespace for correctness.
- **EventTriggerCondition migration** — see CardPool section above.

## Key Rules

- Never duplicate legal action generation — always call `MtgActionGenerator.GetLegalActions`. Do not reimplement this in simulator code.
- Never call `BeginGame` or construct turn actions directly — use `MtgGameStateExtensions.BeginGame` as the entry point.
- `SimulatorRunner.SetupGame()` uses `MtgGameFactory.Create()` (real mana), not `CreateForTesting()`. The simulator tests real mana constraints.
- All state mutation goes through `GameAction`s — no direct state modification in the simulator.
