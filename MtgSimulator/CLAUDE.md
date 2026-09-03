# MtgSimulator

Class library containing all AI strategies, game runners, deck factories, and reporting for MTG simulation. Referenced by Godot and by `MtgSimulator.Console` (the runnable console entry point). Keeping it a library prevents file-locking conflicts when the console app and Godot are running simultaneously.

## Source Map

| File | Purpose |
|------|---------|
| `SimulatorRunner.cs` | Orchestrates N games, aggregates results, prints all reports |
| `GameRunner.cs` | Runs a single game to completion using two `IAiStrategy` implementations |
| `GameSetup.cs` | `FromDecks` — loads two built decks into a pre-begin `GameState`. Shared by draft and constructed; takes deck BUILDERS because owner ids only exist once the game does |
| `CardStatAccumulator.cs` | Games-in-hand counting per card and per pair. Shared by `DraftTrainer` and `MetagameEvolver` so the two tables stay comparable |
| `Evolution/Decklist.cs` | `Decklist` (name → copies + land count), its invariants (60 cards, max 4, 20-26 lands), `Difference`, `Materialize`; `MetagameResult` + `DecklistStore` |
| `Evolution/ConstructedValues.cs` | Card/pair values learned from constructed games, shrunk toward the draft model as prior; `Movers`/`SpearmanAgainstDraft` diff the two formats |
| `Evolution/DeckBuilder.cs` | Seeding (anchor + synergy kernel + curve target) and the three mutation operators |
| `Evolution/PoolFeatures.cs` | What each card ASKS of your deck and which cards ANSWER, read off the cards by reflection — no mechanic-to-meaning table. `Satisfaction`, `DeadCards`, `SpellsCastDemand` |
| `Evolution/Goldfish.cs` | Solitaire against an inert opponent. Reports turns-to-kill (**descriptive only** — measured as the wrong fitness) and, given an `EngineProbe`, the assembly reading |
| `Evolution/EngineProbe.cs` | **Did the payoff resolve with its support deployed?** Payoff/enabler sets out of `PoolFeatures`, read off one game's event log in a single pass |
| `Evolution/EngineDiscovery.cs` | Console mode 7 — probe every concept in a pool, rank by whether the engine assembles, save `sim_results/engines_<set>_<stamp>.json` |
| `Evolution/ConstructedGameSetup.cs` | Two decklists → pre-begin `GameState`; the constructed sibling of `DraftGameSetup` |
| `Evolution/MetagameEvolver.cs` | Console mode 6 — the evolution loop, paired evaluation, culling, and the report. `enginesPath` seeds discovered archetypes as pool-locked, cull-exempt slots; `excludedEngines` drops archetypes a previous run measured as dead |
| `Evolution/MutationLog.cs` | Every proposal the search considered — cards added/removed, parent vs candidate rate, generation, outcome. Summary to the console, whole log to `sim_results/mutations_*.csv` |
| `IAiStrategy.cs` | Interface: `SelectAction` + `ResolveChoice` — all AI implementations conform to this |
| `RandomAiStrategy.cs` | Baseline AI — picks a random legal action; used as playout policy |
| `DepthLimitedAiStrategy.cs` | Greedy depth-limited DFS AI — retained for comparison; not the default |
| `BeamSearchAiStrategy.cs` | Beam search AI — the default strategy; two-bucket pruning (concrete + potential) |
| `IPotentialEvaluator.cs` | Interface for secondary "potential" signals used during beam pruning |
| `FastManaPotentialEvaluator.cs` | Potential evaluator that scores by current available mana (preserves fast-mana lines) |
| `StateEvaluator.cs` | Scores a `GameState` from a given player's perspective (float) |
| `CardPool.cs` | Defines the full card pool; `BuildRandomDeck` samples 40 random cards per game |
| `Decks/ZooDeckFactory.cs` | Builds a fixed 60-card Zoo deck (RGW aggro — 24 Plains + 36 spells) for a given player |
| `Decks/GoblinsDeckFactory.cs` | Builds a fixed 60-card Goblins deck (red aggro tribal — 24 Plains + 36 spells) for a given player |
| `Decks/ValakutDeckFactory.cs` | Builds a fixed 60-card Valakut ramp deck (14 Plains + 4 Valakut + 4 Glimmervoid + 4 Field of the Dead + 2 Simic Growth Chamber + ramp/spells) for a given player |
| `Decks/JundDeckFactory.cs` | Builds a 60-card Jund midrange deck (18 Plains + 2 Mox + 2 Sol Ring + hand disruption, removal, threats, Siege Rhino) for a given player |
| `Decks/DelverDeckFactory.cs` | Builds a fixed 60-card Delver deck for a given player |
| `Decks/DragonstormDeckFactory.cs` | Builds a fixed 60-card Dragonstorm combo deck for a given player |
| `Decks/ReanimatorDeckFactory.cs` | Builds a fixed 60-card Reanimator deck for a given player |
| `Decks/TraditionalStormDeckFactory.cs` | Builds a fixed 60-card Traditional Storm combo deck for a given player |
| `Decks/AffinityDeckFactory.cs` | Builds a fixed 60-card Affinity artifact aggro deck for a given player |
| `Decks/DeckRegistry.cs` | Registers all named precon decks (`DeckInfo` records); exposes `All` and `Build(name, ownerId)` |
| `Draft/Draft.cs` | `DraftFormat` / `DraftSeat` / `DraftState` records + `Create`, `ApplyPicks`, `RunToCompletion`, `BuildDeck` |
| `Draft/DraftPickers.cs` | `DraftPicker` delegate; `Random` (baseline) and `Curve` (stats-per-mana heuristic) pickers |
| `Draft/DraftRunner.cs` | All-AI draft harness: drafts a table, builds decks, round-robins via `GameRunner`, prints win rates per seat and per picker; `PlayGame` is the shared single-game helper |
| `Draft/DraftTournament.cs` | Round-robin standings for a drafted pod with one human seat — circle-method pairings, background AI round simulation, `Standing` rows |
| `Draft/DraftGameSetup.cs` | Builds a pre-begin `GameState` from two drafted pools; shared by `DraftRunner` and `DraftTrainer` |
| `Draft/DraftTrainingData.cs` | `CardStat` / `PairStat` / `DraftTrainingData` count DTOs, `Shrink`, `Merge`; `DraftTrainingStore` load/save/merge JSON and `PathFor(setCode)` |
| `Draft/DraftTrainer.cs` | Training mode: N drafts, parallel games, accumulates games-in-hand counts per card and per card pair |
| `PreconstructedStats.cs` | Aggregates precon game results into four stat tables; exposes row records for deck (inc. AvgWinTurn/MinWinTurn/MaxWinTurn), matchup, card GIH WR (inc. AvgCopiesPlayed), and card-per-matchup GIH WR (inc. AvgCopiesPlayed) |
| `PreconstructedSimulatorRunner.cs` | Round-robin precon runner: builds schedule, runs games via `GameRunner`, feeds `PreconstructedStats`, prints console summary, triggers CSV export |
| `PreconstructedCsvExporter.cs` | Writes all four stat tables to `sim_results/precon_<timestamp>.csv`; escapes card names with commas (e.g. "Krenko, Mob Boss") |
| `GameResult.cs` | Record capturing outcome, turn count, actions, drawn/played cards, end reason, duration, all events, exception info |
| `PreconstructedGameResult.cs` | Wraps `GameResult` with deck names and on-play metadata for preconstructed mode |
| `GameStateSnapshot.cs` | Human-readable snapshot DTO — `GameStateSnapshot`, `PlayerSnapshot`, `CreatureSnapshot`, `TurnLog` |
| `FlaggedGameSaver.cs` | Builds a snapshot from a flagged `GameState` and writes it as JSON to `flagged_games/` |
| `DrawDiagnostics.cs` | `BoardSnapshot` capture + the end-of-training draw report: reason split, run-position trend, board medians, per-card lift |
| `EvaluationBreakdown.cs` | A `StateEvaluator` score split into its eight terms; `readonly record struct` so producing it allocates nothing |
| `AiDecision.cs` | `AiDecision` / `AiActionCandidate` — one captured decision, its ranked candidates, and their term breakdowns |
| `CardValueSandbox.cs` | What a card is worth ONCE CASTABLE — casts it into a fixed position and rolls forward; `CardValue` rows + JSON store |
| `Scenarios/StateJson.cs` | Real `GameState` ↔ JSON round-trip; reflection-based `$type` discriminators for every abstract game type |
| `Scenarios/Scenario.cs` | `Scenario` record (name, note, player to move, state) + `ScenarioStore` load/save/list in `scenarios/` |
| `Scenarios/ScenarioComparer.cs` | Runs N strategies against one scenario; `StrategyResult` rows and the fixed-width comparison table |
| `Scenarios/ScenarioConsole.cs` | Console mode 5 — the standalone scenario viewer; owns the named strategy list |

## Architecture

`SimulatorRunner` creates a game via `SetupGame()` (calls `MtgGameFactory.Create()`, builds random decks from `CardPool`), injects two `IAiStrategy` instances into a `GameRunner`, and collects `GameResult`s. All reporting runs after all games complete.

`GameRunner` owns the full game lifecycle: it calls `BeginGame` at the start of `Run()`, captures the begin-game events (initial hand draws, first turn start) into `AllEvents` and `DrawnCards`, then drives the game loop. This ensures no events are lost to the caller. The loop checks for pending choices first (delegates to `activeStrategy.ResolveChoice`), then calls `MtgActionGenerator.GetLegalActions` and `activeStrategy.SelectAction`. When no legal actions remain, it auto-fires `EndTurnAction`.

## Game Limits (in `GameRunner`)

| Limit | Threshold | Deterministic? | Effect |
|-------|-----------|---|--------|
| Turn limit | 100 turns | yes | `GameEndReason.TurnLimitReached` — game ends as draw |
| Action warning | 100 actions in one turn | yes | `HadActionWarning = true` — game continues |
| Action limit | 200 actions in one turn | yes | `GameEndReason.ActionLimitReached` — game ends as draw |
| Safety timeout | 1 800 000 ms wall-clock | **no** | `GameEndReason.TimeLimitReached` — excluded from training, not a draw. Hang catcher only; must never fire in normal operation |
| Unhandled exception | any thrown exception | — | `GameEndReason.UnhandledException` — excluded from training, exception captured |

### The clock was removed from ENDING a game and left in DISCARDING one

**Mode 6 was not reproducible at `presim 800`, and this is why.** `PreSimulation` and
`DraftTrainer` both drop `TimeLimitReached` games from their counts — and that reason comes from
the wall-clock safety net, the one non-deterministic termination in the table above. Two runs at
an **identical seed with byte-identical inputs**:

```
run 1:  9585 games in 9.4m, 1 excluded.   Base rate 50.0%
run 2:  9584 games in 8.9m, 2 excluded.   Base rate 50.0%
```

A different game dropped each time → slightly different card values → different softmax draws at
seeding → a completely different metagame by generation 1. Two full A/B comparisons were run and
read before anyone noticed, because the evidence was one number in a summary line.

**The fix is to WAIT, not to tune.** A game's result is deterministic; only how long we wait for it
is load-dependent, so a game starved by a 9 600-game parallel batch reaches the same outcome later
and including it is strictly more correct than discarding it. `SafetyTimeoutMs` is 1 800 000 —
~1000x a ~1.7s median game, so load cannot reach it, while a genuine hang still cannot wedge a run.
`PreSimulation` now prints a **loud warning** on any exclusion, because a nonzero count means that
run is not reproducible.

Verified: 8 decks / presim 800 / same seed, run twice, **bit-identical** apart from the elapsed-time
column. Localised by elimination first — presim 0 at 8 decks reproduced, presim 800 did not, so the
generation loop was never at fault.

**Diagnostic rule: if two runs at one seed disagree, read the excluded count before reading
anything else.**

**It costs wall-clock, and that is the honest trade.** CSC presim at 800 decks:

```
before:  9585 games in  9.4m, 1 excluded
before:  9584 games in  8.9m, 2 excluded
after:   9586 games in 23.5m, 0 excluded
```

~9m -> 23.5m, well outside the previous run-to-run range. The games the 300s net was cutting are
genuinely slow, and in a parallel batch one straggler dominates the tail once everything else has
drained. **This is not a defect in the fix** — the deterministic bounds allow it: 100 turns x 200
actions is 20 000 actions, and at ~70 ms per `SelectAction` a single legitimate game can run ~23
minutes. The old net was hiding that by discarding the evidence.

If the cost ever matters, the lever is the DETERMINISTIC bound (turn limit or per-turn action
limit), never the clock. Lowering those changes what a game is, which is a real design decision,
but it keeps runs reproducible. Reaching for the wall-clock again reintroduces exactly this bug.

**Wall-clock time must never decide a game.** It used to: a 20 000 ms limit ended the game as a
draw, so the result depended on how fast the machine was running at that moment. Turn and action
limits already bound a game deterministically (100 x 200), so the clock was never load-bearing
for termination — only for cost, and cost is now bounded inside the AI by
`MultiTurnBeamSearchAiStrategy`'s rollout budget. The 300-second net exists only so a genuine
engine hang cannot wedge a run; a game it ends is broken, not drawn.

**Only `Damage` and `LibraryEmpty` draws are real draws** (both players lost at once —
`CheckStateBasedEffectsAction` reports `WinnerPlayerId = -1`). `TurnLimitReached` and
`ActionLimitReached` are honest evidence too — deterministic, and they mean these two decks
could not finish — but they are not the same thing, and `DrawDiagnostics` keeps them apart.

`GameRunner.Run()` wraps the entire game loop in a try/catch. On exception, it terminates with `UnhandledException`, capturing the last known `GameState`, all events up to the crash, and the exception message and stack trace — then returns normally so the run continues with the next game.

`GameRunner.Run()` returns `(GameResult Result, GameState FinalState)` — the final state is passed to `FlaggedGameSaver` by both runners when the result is flagged.

Flagged games (any of the above) are collected separately and printed in the flagged games report. Both `SimulatorRunner` and `PreconstructedSimulatorRunner` track and report flagged games.

## AI Strategies

**`RandomAiStrategy`** — picks a uniformly random legal action. Baseline; also used as the playout policy inside more sophisticated strategies.

**`DepthLimitedAiStrategy`** — greedy depth-first search to a fixed depth. Not minimax — opponent responses during search are not modelled. Retained for comparison; not the current default.

**`BeamSearchAiStrategy`** — beam search (breadth-first) to a fixed depth. At each level, candidates are pruned to two buckets before expanding the next level:
- **Concrete bucket** — top N by `StateEvaluator` score (default: 10)
- **Potential bucket** — top M per `IPotentialEvaluator` (default: 5 slots via `FastManaPotentialEvaluator`)

Potential evaluators preserve setup lines (fast mana, etc.) that score poorly on the main evaluator but may enable a win condition deeper in the tree. The final action is always chosen by concrete score at the leaf level. Falls back to `EndTurnAction` as a tiebreaker (to avoid neutral attacks or pointless spells), then random among remaining ties. Current default: depth 3, **concreteSlots 6**, with `FastManaPotentialEvaluator` (**2 slots**) injected by default.

**Land-first override**: before entering beam search, `SelectAction` plays any available `PlayLandAction` immediately. Permanent mana is the highest-priority resource; no search is needed for this decision.

**`IPotentialEvaluator`** — pluggable interface for secondary beam-pruning signals. Implement to add new potential heuristics (graveyard value, storm count, etc.) without touching the search logic.

**`MultiTurnBeamSearchAiStrategy`** — beam search (same pruning as above) combined with a multi-turn greedy rollout. Each candidate is scored by `ScoreAfterCompletingTurn`, which completes the current turn greedily then runs `MultiTurnGreedyRollout` for N lookahead turns (default 2). The rollout alternates between `PlayGreedyTurn` (our turn) and `SimulateOpponentTurn` (opponent turn). Default opponent mode is `BoardOnly`. Default: depth 3, **concreteSlots 2**, lookaheadTurns 2.

**The beam is far narrower than it looks, and the numbers here were wrong for a long time** — both
strategies were documented as `concreteSlots 10`. The real defaults are 6 for `BeamSearch` and
**2** for `MultiTurnBeamSearch`, which with the `FastManaPotentialEvaluator`'s 2 slots makes the
live beam **≤4 nodes per level**, not 10–15. That matters whenever you reason about search cost:
a level expands `beam x actions`, so the multiplier is 4, and any estimate built on the old figure
overstates the work by 3–4x. Measure the shape before tuning it.

### The branching cap

`DefaultMaxBranching` (16) is the most actions any one level will roll out. Above it,
`NarrowActions` ranks with the cheap `StateEvaluator` — one `ExecuteAction`, no rollout — and keeps
the best, plus a potential bucket mirroring `PruneBeam`. Below it the method returns its input
after a single integer compare, which is the case for ~99% of decisions (measured spread per
decision on CSC: p50 4, p90 8, p99 16, p99.9 24, max 47).

**Pruning here is not refusing to make a play.** `SelectAction` runs afresh after every action, so
a cut action is re-offered from the next state; the search declines to explore it *in this
ordering*. That is what makes the cap cheap in strength terms, and it is why the idea works at all.

Measured on CSC, 28 000 games, against the same build with the cap disabled:

| | No cap | Cap 16 |
|---|---|---|
| Run time | 2 318s | **1 604s** (−31%) |
| Median game | ~8 000 ms | **~1 700 ms** |
| Games hitting the 300 s net | 12 | **4** |
| Base win rate | 50.0% | 50.0% |

**Strength was measured before shipping it, not assumed**: a capped AI played an uncapped one over
224 drafted-deck games, alternating who was on the play, and scored **48.2% (1 SE = 3.3pp)** — even.
`MtgSimulator.Tests/BranchingCapStrengthTests.cs` is that harness, `[Explicit]` because it plays
hundreds of games. Re-run it before changing the cap; the premise it tests ("orderings among
near-identical actions are not worth finding") is a claim about this game, not a general truth.

**`ResolveChoice` is bounded by the same budget, and that is where the worst cost hid.** It pays a
full rollout per option, and for `MinChoices > 1` a full rollout per *combination* via
`GetCombinations` — C(n, k). Its only escape used to be the wall-clock `moveTimeBudget`, which is
null in the simulator, so it never fired. Flagged snapshots showed games with **three permanents on
the board burning five minutes**, and the cost was invisible in every report because
`GameRunner.ProcessChoice` does not increment `TotalActions` — those games read as "13 actions, 327
seconds". Both loops are sequential, so testing the rollout counter there is deterministic.

**When a game looks slow but its action count is low, suspect choice resolution, not the board.**

**That paragraph describes a FIXED bug, and reading it as a live one nearly cost a wasted feature.**
Once the rollout budget bounded it, the cost went away. Measured by `ChoiceCensus` over 224 real
drafted-deck games:

| | Calls | Thread time | Share |
|---|---|---|---|
| `SelectAction` | 14 125 | 966.7s | **98.8%** |
| `ResolveChoice` | 523 | 11.9s | **1.2%** |

2.33 choices per game, **p50 of 2 options** and p90 of 6 — the biggest single line ("Choose a card
to discard", 205 calls) is 0.6% of total. `MinChoices > 1`, the C(n,k) family, is 8 calls and 1.3s.

**So pruning the option list has a ceiling of 1.2% and a realistic value near zero** — you cannot
prune a 2-option choice. A card-value heuristic was about to be wired in to do exactly that, on the
strength of the paragraph above. Re-run `ChoiceCensus` before believing choice cost is a problem
again; it is a decorator over `IAiStrategy` and changes nothing in production.

The quality question is untouched and is the live one: `CardsInHandWeight` scores every hand card
at a flat 1.4, so nothing in the evaluator distinguishes discarding a bomb from discarding a blank.

**`DefaultExpandBranching` (5) caps levels below the root, much tighter than the root's 16.**
Profiling attributes **72% of search cost to `ExpandNode` against 27% to level 0** — expansion
spends `beam (4) x branching` per level, so most of the budget was going to levels that cannot
change *which* action is returned. `SelectAction` always returns a root action; deeper levels only
refine the scores that rank the roots. Measured at 28 000 games: run time **1 593s → 1 260s
(−21%)** with play strength unchanged.

`NarrowActions` takes `min(expandBranching, maxBranching)` so an explicit tight cap is never
widened. **That min is a trap for the strength harness**: passing `maxBranching: int.MaxValue`
alone leaves expansion capped and the comparison measures nothing. Lift both, and sanity-check the
harness by crippling one arm — branching 1 scores 41.1% against 48.2%, which is how you know the
test can still see a difference at all.

### A planned action must never be re-found as a different one

`FindCommittedAction` re-finds each step of a committed chain in a freshly generated legal-action
list, using `ActionsMatch`. That matcher compared the two cast actions **on card id alone**, and
`MtgActionGenerator` enumerates X **ascending** — so a chain that planned "cast this for X=4"
re-found and executed the **X=0** action.

Every `{X}` card in the cube was exposed: Banefire and Earthquake dealing no damage, Mind Spring
drawing nothing, and green's two Hydras arriving as 0/0s that the zero-toughness rule destroys on
arrival. All of them are indistinguishable from a blank card in a win-rate table, which is where
Primordial Hydra was found (39.2%, bottom of the CSC run). `XValue` is now part of the identity of
both cast actions.

**This is pinned at the matcher, not through a game, and that is deliberate.** `SelectAction`
returns the FIRST action of a chain directly — it never goes through `ActionsMatch` — so a
game-level test passes whether or not the bug is present, which is worse than no test. Forcing a
specific card to position ≥1 of a committed chain is not reliably reproducible from a unit test.
`ActionsMatch` is `internal` so `XCostChainReplayTests` can assert the invariant itself.

**The general rule: anything the search chose between must be part of the match.** Targets already
were; X was not. A field that distinguishes two legal actions and is absent here silently
downgrades the AI's decision to whichever variant the generator happens to emit first.

The potential bucket is load-bearing. Ranking on immediate score alone cuts a fast-mana setup line
before it is ever rolled out — the exact failure `IPotentialEvaluator` exists to prevent, and
invisible when it happens, because the action is never explored rather than explored and rejected.

Ranking scores inside a `Parallel.For` into a pre-allocated array and sorts afterwards, ties broken
by original index. Same rule as `RolloutBudgetExhausted`: never let thread completion order reach a
decision.

**Move time budget**: the optional `moveTimeBudget` constructor param caps wall-clock time per `SelectAction`/`ResolveChoice`. When set, the search degrades gracefully once spent — level 0 scores remaining roots with the cheap immediate evaluator instead of a rollout, beam expansion stops, and the best node found so far is returned. **Default is null (unbounded)**, which preserves deterministic simulator behavior; only the interactive Godot path passes a finite budget (the simulator relies on `GameRunner`'s outer limits instead). `ResolveAllChoices` also carries a generous iteration cap (`MaxChoiceResolutionIterations`) so a non-advancing choice can't spin the calling thread forever.

**`OpponentSimulationMode`** — controls how the opponent's turn is simulated in the rollout:
- `PassTurn` — opponent does nothing.
- `Greedy` — opponent plays the single best non-EndTurn action (includes hand cards; exposes hidden information).
- `Random` — opponent plays one random non-EndTurn action.
- `BoardOnly` — opponent iterates all legal `AttackAction` and `ActivateAbilityAction` options greedily in a loop until none remain, then ends turn. No hand cards, so simulation uses only visible information. This is the default. The loop is essential: stopping after a single attack would undercount the opponent's total damage (e.g. missing that two attackers together deal lethal).

**`IAiStrategy`** — swap implementations freely; `GameRunner` and `SimulatorRunner` only depend on the interface.

### Terminal rewards decay with how long they took

A rollout that reaches a win or loss breaks out of `MultiTurnGreedyRollout` early, so unlike every
other rollout it does not describe the state at the full lookahead horizon. Returning the raw
`±WinScore` made winning next half-turn and winning in two score **identically**, and dying next
half-turn and dying in two likewise — so inside the "someone dies within the lookahead" region
every line collapsed to one number and the search fell through to its tiebreak, at the point where
the choice matters most.

`DiscountTerminal(score, halfTurns)` multiplies a terminal by `TerminalDiscount ^ halfTurns`.
Because a loss is negative, decaying moves it **toward zero**, so a later death outscores an
earlier one — playing for the topdeck falls out of the arithmetic instead of needing a rule.
**This is why `LossScore` must stay negative rather than 0**: a zero loss decays to zero and the
survival half of the effect disappears.

λ is not a sensitive parameter — anything in ~0.85–0.99 ranks identically at a 2-turn lookahead.
The only constraint is that a discounted terminal stays far above the largest non-terminal score.
From Cowling, Ward & Powley (2012) §D; their λ = 0.99 is calibrated for rollouts that run to a
terminal over 40–60 turns. Scope is narrow: it only fires when a terminal lands inside the
lookahead, so it does not touch the oscillation or horizon-blindness defects.

### The pre-search LETHAL check — and why FindWinner could not cover it

`MultiTurnBeamSearchAiStrategy.FindLethalAttacks` runs before the beam: if the effective power of
everything you control is at least the opponent's life, it simulates taking legal `AttackAction`s
until they die, and commits that sequence if they do.

**`LethalDetectionTests` originally concluded this was unnecessary, and the reasoning was sound at
the time**: with no blocking, every individual attack scores well on its own, so the beam assembles
lethal one swing per `SelectAction` call — `TwentyAttackersWithExactLethal_Wins` passes without any
special handling.

**That premise holds only while nothing OUTSCORES a swing.** A repeatable free ability is +1 creature
at evaluator weight 3.0 against a point of damage at life weight 0.2, so the incremental assembly
never starts. `FindWinner` cannot rescue it either: it fires only when a node's ROLLOUT reached a
win, and the rollout completes our turn with `PlayGreedyTurn`, which plays a land, **exactly one**
other action, then ends the turn — so a kill needing six swings is never simulated. Note the
asymmetry that leaves: `SimulateOpponentTurn`'s BoardOnly mode LOOPS every attack, so the model gives
the opponent a whole turn and us one action.

Measured on CMB's planted Twin combo, before and after:

| | before | after |
|---|---|---|
| Activations per game | **198** | 10–16 |
| Game end | `ActionLimitReached` (a draw) | `Damage`, **win on turn 3–4** |
| Mode 7 `kill` | **99.0 (never)** | **3.5** |
| MtgSimulator suite runtime | 1m38s | **28s** |

The deck assembled the combo on turn 2 and then held lethal while activating its copier to the
200-action cap. The suite speedup is the same effect everywhere: games that were grinding now end.

Three properties keep it cheap and honest, and all three are load-bearing:

- **A power gate first**, one battlefield walk, deliberately loose — it ignores who can legally
  attack, because a false positive costs only the simulation while a false negative misses the win.
- **The sequence is SIMULATED, not assumed.** Attacks come from `MtgActionGenerator.GetLegalActions`
  each iteration, so summoning sickness, exhaustion, Taunt, Flying and "can't attack" are enforced by
  the generator rather than re-derived — the same rule that forbids a second copy of the combat rules
  in a UI.
- **It must actually kill.** Returns null unless the opponent is dead at the end, so a board that
  merely looks lethal falls through to the ordinary search. `OneShortOfLethal_DoesNotWin` and the
  burn/pump tests (which need the SEARCH, not raw attacks) still pass.

`lethalCheck: false` on the constructor restores the old behaviour so `EvaluatorStrengthTests` can
play it against its own absence. **That measurement has NOT been run** — the case for shipping it is
a fixed defect (a held win never taken), not a demonstrated win rate, which is the same standing this
file gives the terminal discount and fastest-win changes.

#### Never compare a rollout score against WinScore

**A discounted win is 9500 or 9025, so `>= WinScore` is false for every win the search will ever
find.** Use `StateEvaluator.IsWin` / `IsDecisive`, which test against `WinThreshold` (2500) —
comfortably above any board score the weights can produce (~150) and below the most-decayed win at
any sane lookahead (0.95²⁰ ≈ 3585).

This shipped broken and is worth understanding, because the symptom pointed everywhere except the
cause. `FindWinner` silently stopped returning winners and both `ResolveChoice` early-outs stopped
firing, so the search never short-circuited on a found win and burned its full 400-rollout budget
on every move. **The AI stopped taking a winning line the moment it found one.**

It surfaced as an **8.8% wall-time regression**, which was then blamed on the `Evaluate`/`Explain`
refactor and "fixed" twice — sharing the battlefield walk, then restoring the original body
verbatim — neither of which moved it, because neither was the cause.

**What identified it was reading `Avg actions/game` beside the clock.** Actions went *down* while
time went *up*, which is impossible for a per-call cost and pointed straight at the search doing
more work per decision. Wall time alone would have shipped duplicated evaluator logic to work
around a bug introduced two commits earlier.

Pinned by `TerminalDiscountTests`: a win discounted over 0–20 half-turns must still read as a win,
and the threshold must stay above any plausible board score.

**The general rule, now three for three in this project: a wall-clock number names a symptom, not
a culprit.** Same class as the stale-binary trap and the parallel-batch draw disaster — always
check whether the *work* changed before blaming the code you just touched.

Pinned by `MtgSimulator.Tests/TerminalDiscountTests.cs`, at the function rather than through a
game — reaching a terminal inside a 2-turn rollout needs an already-lethal board where the beam
mostly wins either way, so a game-level assertion passes with or without the discount.

### `PruneBeam`'s `seen` set is not transposition detection

It uses `ReferenceEqualityComparer`, and its only job is stopping the same node **object** being
added twice by the concrete bucket and the potential bucket. Nothing in the search compares
`GameState`s, so two orderings of the same actions are rolled out separately even though they
reach an identical board. `GameState` is a record, but `ImmutableDictionary` and `ImmutableArray`
use reference semantics, so the generated `Equals` would not help — a real dedupe needs a
hand-written state key.

**If that is ever built, key on the resulting STATE, never on which actions commute.** A lord that
pumps creatures entering after it makes `lord → creature` and `creature → lord` reach different
states, so they key differently and both survive, with no rule written anywhere about
commutativity. The state is the ground truth about whether order mattered.

Measure the duplicate rate before building it. `DefaultExpandBranching` (5) is already a lossy
version of the same optimisation and measured neutral on strength, so it may be capturing most of
the available benefit.

## StateEvaluator

Scores a non-terminal state as a weighted sum. Terminal states short-circuit.

| Factor | Weight |
|--------|--------|
| Life difference (player − opponent) | 0.2 |
| Creature count difference | 3.0 |
| Total effective Power difference (permanent power only) | 2.0 |
| Creature damage difference (opponent damage − player damage) | 0.1 |
| Non-creature permanent count difference (Mox, Arena, Exploration, etc.) | 1.5 |
| Cards in hand difference | 1.4 |
| Player's own permanent mana (`MaxMana` only — temporary fast mana excluded) | 2.0 |
| Race pressure (whose clock is shorter — see below) | 20.0 / turns-to-kill |
| Win (opponent has lost) | +10000 |
| Loss (player has lost) | −10000 |

`MaxMana` weight is high (2.0) because in the land system permanent mana is the primary resource — a land behind means fewer spells castable every turn for the rest of the game. Power uses permanent power only (`GetEffectivePermanentPower`); `UntilEndOfTurn` buffs like Giant Growth are excluded since they evaporate next turn. Creature damage (weight 0.1) tracks accumulated damage on surviving creatures — a creature with near-lethal damage is far more fragile than a fresh one, and without this factor the evaluator sees a neutral attack (both creatures survive) as free. Non-creature permanents (weight 1.5) are identified by `PermanentComponent && !CreatureComponent`; land cards are excluded automatically since `Plains` carries no `PermanentComponent`.

### Race pressure: board power only matters relative to the life it threatens

Every other term is a flat weight on a difference, which means a point of life is worth the same at
6 as at 20 and a 2/2 is worth the same whether the player facing it is about to die to it or not.
Nothing in the sum knew that the opponent's creatures convert into *your* death.

Reported from a real game: the AI at 6 life with a 3/1 haste, the player at 20 with a 2/2. It
attacked the face for 3 — worth `+0.6` against a player at 20 — instead of trading the 3/1 into
the 2/2, which kills both and removes the clock that was actually killing it. It died two turns
later. Scored on the old weights, going face was **-0.2** and trading was **-2.8**: the trade lost
by 2.6 purely for giving up a power-2 board edge.

`RacePressure(power, lifeThreatened) = 20 / max(life/power, 0.5)` is added for the player's board
and subtracted for the opponent's. It is **symmetric on purpose** — the same term that makes the AI
respect a clock aimed at it makes it press one aimed at the opponent, so this is not a blanket
shift toward defence. At healthy totals it is a mild nudge (2 power against 20 life contributes
2.0); as either player nears death it dominates every board-quality term, which is correct, because
at that point nothing else decides the game.

It counts total power rather than what can legally attack — summoning sickness, Taunt and
"can't attack" are all ignored. It is a heuristic for how fast a board kills; the search covers the
exact lines.

Measured over 300 games at a fixed seed, against the same build without it: avg turn count
**7.3 → 7.1**, avg actions/game **60.5 → 59.2**, avg time/game **213.5ms → 205.0ms**, zero
turn/action/time-limit games and zero flagged games either way. **That measures stability, not
strength** — both seats run the same evaluator, so a mirror match cannot show which is better.
A real strength number needs an old-vs-new head-to-head, which the harness cannot express without
plumbing a second evaluator through `IAiStrategy` (the pattern to copy is
`BranchingCapStrengthTests`).

Zone IDs are read directly from `MtgGameIds` to avoid child-list scans on every evaluation call.

When tuning weights: changes here affect `BeamSearchAiStrategy` and `DepthLimitedAiStrategy`. `RandomAiStrategy` ignores evaluation.

## CardPool

Defines all cards available for random deck generation. `BuildRandomDeck(ownerId, deckSize = 40, landCount = 13)` builds a 40-card limited deck: 13 Plains + 27 non-land cards sampled without replacement from the pool. Land cards in the pool are excluded from the non-land draw.

**Targeting restriction**: damage spells target opponents and opponent creatures only. The random AI has no targeting intelligence, so restricting targets at the card level prevents self-damage. This is intentional — do not add friendly targets to simulator cards without also updating the AI strategy.

**EventTriggerCondition migration**: `CardPool` contains duplicate cards — one set using concrete condition classes (`CreatureDiesCondition`, `CreatureAttacksCondition`, etc.) and one set using the generic `EventTriggerCondition` system (Grim Watcher, Soul Harvester, War Drummer, Battlefield Scholar). Both run side by side for validation. Once the `EventTriggerCondition` versions are confirmed correct, the old concrete-condition versions should be removed.

## Draft

Deck *selection* as a game mode, for human and AI players alike. Lives in `Draft/`, namespace `MtgSimulator` (flat, like `Decks/`).

**Not a `GameState`.** `DraftState` is a plain immutable record graph. Drafting needs none of the action stack, pipelines, choice resolution, or event log, and the cards it holds are owner-agnostic templates that never enter a `GameState`. Do not migrate this to `GameAction`s.

**Two formats, one model.** `DraftFormat.Booster` (15-card packs, pick one, pass, 3 packs) and `DraftFormat.Digital` (offered N cards, pick one, repeat — seats never interact). `Draft.Create` pre-generates every pack and every offer up front into each seat's `Queue`, so the only per-format branch in `ApplyPicks` is *rotate the remainder* vs *open your own next group*. Booster pass direction alternates per pack via `DraftState.Round`.

### Card sets

Which cards get drafted comes from a `CardSet` (`MtgCore/Sets/`), not from `CardLibrary.All`
directly. `DraftRunner` and `DraftTrainer` both take an optional `CardSet set` parameter
defaulting to `SetRegistry.Default`; `DraftScene` has a single `DraftedSet` field. Swapping
sets needed no change to `Draft` itself — `Draft.Create` has always taken an
`IReadOnlyList<Card>` card pool.

**Models are per-set.** `DraftTrainingStore.PathFor(setCode)` gives the model path;
the Legacy set keeps the original unsuffixed `sim_results/draft_training.json` so the existing
model and its Godot asset copy load without migration, and every other set gets
`draft_training_<code>.json`. `DraftScene` derives its `res://` asset filename from the same
`PathFor` call, so the two cannot drift apart.

**A new set must be trained from scratch, never bootstrapped.** `DraftPickers.Trained` is keyed
by card name and scores unknown cards at exactly the prior. Bootstrapping a new set from an old
model would have every new card score 0 against known cards scoring up to +16.8, so they would
be passed over every pick, never make a deck, and never accumulate data — a self-reinforcing
blind spot. A fresh run uses card-agnostic Curve/Random, which samples new cards uniformly.

**Train from scratch after a rules change too, not just a new set.** The stored values describe
how good a card was under the rules it was measured in. When the Flying restriction landed it
changed what ~55 cards were worth, so every value in the model was describing a game that no
longer existed — bootstrapping from it would have drafted decks by obsolete valuations.

**Bootstrapping and merging are separate decisions in mode 4, and conflating them is a trap.**
"Merge into it rather than replace?" governs only the *output file*. Whether the drafters use
the existing model — which decides which cards get sampled, and therefore which get measured at
all — is a different question, and the console now asks it separately: *"Draft with the existing
model? (Y/n — n trains from scratch)"*. Answering "replace" alone still bootstrapped the
drafters, which silently produced a model trained on stale valuations.

**Strip pairs from the shipped Godot asset on a large set.** Pair count is O(cards²): the
82-card Legacy set has 3 321 pairs (450 KB), the 300-card Hollowmere set has 44 850 (6.2 MB), and
CSC now has 82 560 — **11.5 MB against 46.8 KB stripped**. `synergyWeight` defaults to 0 so pairs
are never read at pick time. `sim_results/` is gitignored, so keep the full file there for any
future synergy experiment and commit only the stripped copy to `MtgGame/Assets/`.

**That safety rests on a RUNTIME default sitting a long way from a BUILD-TIME deletion**, so it is
pinned rather than trusted: `StrippedModelTests` asserts a stripped model drafts identically to a
full one over 50 seeds, and — because a vacuous test would pass just as well — a second test raises
`synergyWeight` and confirms the two arms genuinely do diverge. Both use inline data, so neither
depends on anyone having run a training pass.

### Measured: Hollowmere (HLM), 300 cards

600 drafts, 16 800 games, 33 600 deck-games, ~112 deck-games per card (the Legacy model has
410/card off the same run size — pair and card density both fall as the pool grows). Evaluated
over 72 games at 9 seats:

| Picker | Win rate |
|---|---|
| Trained | **75.0%** |
| Curve | 37.5% |
| Random | 37.5% |

Curve does not underperform Random here as it does on the Legacy pool, because Hollowmere's
expensive cards are genuinely castable via the reanimation package rather than being traps.

### Measured: Core Set Cube (CSC), 134 cards

300 drafts, 8 400 games, 16 800 deck-games, **median 1 445 games per card** — an order of
magnitude denser than Hollowmere off half the drafts, purely because the pool is 134 cards rather
than 300. Evaluated over 72 games at 9 seats:

| Picker | Win rate |
|---|---|
| Trained | **85.4%** |
| Random | 39.6% |
| Curve | 25.0% |

Zero draws in evaluation; all 72 games ended by damage.

**13% of training games were flagged, all `TimeLimitReached`, and this is a harness artifact, not
a game bug.** Training plays its games in one parallel batch, so each game gets a fraction of a
core and the 5 000 ms limit is wall-clock. The same decks run sequentially draw 0–2%. Diagnose the
difference by the reason: `TimeLimitReached` under parallel load is expected, whereas
`ActionLimitReached` would mean a genuine loop. Extra turns and counterspell traps — the two loop
risks in blue — produced no action-limit games at all.

**Do not record specific card values in this file — record the query that produces them.** A line
here once read "Wall of Frost tops the model at +16.1pp, 5.7pp clear of second"; after a balance
pass the file on disk had it at **+1.85, rank 167/408**, and the stale figure was used to argue a
design position a session later. Card values move double digits in a day, and prose in a document
loaded into every session is the worst possible place to cache them.

```
python -c "
import json; d=json.load(open('sim_results/draft_training_csc.json'))
prior=d['Wins']/d['Perspectives']; k=25
v=sorted((100*((c['Wins']+k*prior)/(c['Games']+k))-100*prior, c['Name']) for c in d['Cards'])
print(v[:10]); print(v[-10:])"
```

Check the model's mtime against `git log -1` before trusting it — one written before the last
balance commit is describing a game that no longer exists.

Key rules:
- **Picks are indices into `Seat.Offer`, never `Card` values.** `Card` is a record, so two copies of one template in a pack compare equal and picking by value would remove the wrong card.
- **Packs exclude lands** — `Draft.BuildDeck` supplies the mana base by padding to `deckSize` with Plains: 23 spells + **17 lands** in a 40-card deck. See `Draft.DefaultMaxSpells` for why 17 rather than 13. It stamps `OwnerId`/`ControllerId`, so it must be called **per game**, not once per seat.
- **`DraftPicker` is a delegate**, not an interface. The no-delegates serialization rule does not apply because draft state never enters a `GameState` — same reasoning as `DeckInfo.Builder`.
- **Human seats have no picker type.** The caller drives the loop and supplies that seat's index; `RunToCompletion` is for all-AI drafts only. This keeps all presentation (console, Godot) out of the library — a UI renders `Seats[i].Offer` / `.Pool` and needs no library change.
- **Determinism**: `Draft.Create(format, pool, seed, …)` consumes one `Random(seed)` in fixed seat order and fixes the entire draft. `ApplyPicks` and `RunToCompletion` are pure. Only `DraftPickers.Random` holds RNG; `DraftRunner` seeds it as `seed + 100 + seatIndex`, and games as `seed + 1000 + gameIndex * 5` (mirroring `SimulatorRunner`).

### The human seat (Godot)

`SQGodotCommon/MtgGame/Draft/DraftScene.cs` is the caller the "human seats have no picker type" rule anticipated. It renders `Seats[0].Offer`, takes a click, and builds the pick list as `i == humanSeat ? clickedIndex : pickers[i](offer, pool)` before calling `ApplyPicks`. **No library change was needed to make drafting playable** — keep it that way.

The Godot scene loads the trained model through `DraftTrainingStore.FromJson` rather than `Load`, because `System.IO` cannot read a `res://` path inside an exported build. The model is duplicated under `SQGodotCommon/MtgGame/Assets/`; **regenerating the file in `sim_results/` does not update it** — copy it across, or the game keeps drafting against a stale model.

`DraftScene.DraftedSet` selects which set the UI drafts — currently `CoresetCube.Set`, changeable in one line. `ModelPath` is derived from `DraftTrainingStore.PathFor(DraftedSet.Code)`, so the asset filename tracks the set automatically and cannot drift from what the trainer writes.

### Lands in hand are not counted by StateEvaluator

`CardsInHandWeight` counts only NON-land cards. Hand size is a proxy for options, and a land held
is not an option — it is a resource you have failed to deploy.

Counting it made a land drop worth `+2.0` mana minus `1.4` for the card leaving hand: a net
`+0.6`, small enough that the beam would sometimes prefer another line and **skip the land drop
entirely for a turn**, which QA saw as the AI stumbling on its early curve. Skipping an early land
drop is close to the worst play available, so the margin has to be decisive rather than marginal.

Pinned by `MtgSimulator.Tests/AiLandDropTests.cs`, which scores the evaluator directly rather than
racing a time-budgeted beam search.

### Card values in ResolveChoice

`CardValueTable` adds the value of the RESULTING HAND to each option's rollout score, inside
`MultiTurnBeamSearchAiStrategy.ResolveChoice` and nowhere else. Discard, scry, tutor, impulse.
Null table = off, which is the production default today.

**Score the resulting hand, never the option's card.** Direction then falls out of the state:
discarding a bomb leaves a worse hand and scores lower, tutoring one leaves a better hand and
scores higher, with nothing having to know which kind of choice it is looking at.

`ReachDiscount` (0.75/turn) decays a card you cannot cast yet. At two mana an eight-drop is worth
`0.75^6` ≈ 18% of its cast value, which is what makes the AI pitch it and keep a playable two-drop
— pinned by `AnUncastableBombLosesToAPlayableCard_OnTurnOne`.

#### Why this is not an evaluator term, measured the hard way

It was built as one first (`HandQualityWeight`), and that is wrong in a way worth recording,
because the reasoning for it sounded fine. **`IStateEvaluator` scores every position the search
considers**, so a hand term is consulted about land drops and attacks too. The term's size depends
on `MaxMana`, and playing a land raises `MaxMana` — so a land drop shrank the term:

| Land drop, one cost-8 card held | Term contribution |
|---|---|
| 4 → 5 mana | +7.27 |
| 5 → 6 mana | **−0.34** |
| 7 → 8 mana | **−10.50** |

Against `ManaWeight`'s +2.0 a land drop scored NEGATIVE, so the AI stopped playing lands. Measured
over 1120 games per weight: 0.05 → 49.5%, 0.2 → 47.7%, **1.0 → 24.6% with actions/game 63.1 →
50.0**. The action count is what diagnosed it — 24.6% alone says "worse", 63 → 50 says "stopped
playing the game".

**Inside one choice the mana is identical across every option**, so the reach discount is a
constant and can only affect the ranking it exists to inform. That is the whole reason the feature
belongs in `ResolveChoice`. `CardValues_DoNotTouchTheLandDropDecision` pins that the evaluator
knows nothing about card values.

The general rule: **a term that should only inform one decision must live at that decision, not in
the shared position score.**

#### What it fixes, measured

`DiscardQualityTests` asks the AI to pitch one of two creatures:

| Case | discard Bomb | discard Chaff | Spread | AI pitched |
|---|---|---|---|---|
| Both castable (cost 2, mana 10) | 26.253 | 53.933 | **27.68** | Chaff ✔ |
| Bomb unreachable (cost 8, mana 2) | 5.400 | 5.400 | **0.0000** | **Bomb** ✘ |

The rollout casts a reachable card, so it already prices it — case A needs no help and gets none.
Case B is the gap, and it is exact: identical to four decimal places, so the choice falls through
to a tiebreak. With a table supplied it pitches the Chaff.

**Both sandbox arms are kept** (`CardValueSandbox.Lookup(values, underPressure)`) — a passive board
wants the biggest threat, a dangerous one wants the answer. `TryLoad` currently takes the pressure
arm; selecting per-position from the race term is the intended end state and is not built.

#### Measured: 18/18 on positions with an obvious answer, and it is ON

A win rate is the wrong instrument here — 2.33 choices per game means a 1120-game head-to-head
reports 50.0% whatever happens, which it did (0.1 → 50.0%, 0.5 → 50.0%, 2.0 → 49.7%).
`ChoiceAccuracyTests` counts correct answers on positions where the answer is not in doubt:
six same-cost card pairs × discard / scry / tutor, mana set so neither card is castable.

| | Correct |
|---|---|
| Rollout alone | **0/18** |
| With card values | **18/18** |

`ChoiceCensus.HowOftenDoCardValuesChangeAChoice` measures the population it acts on: 503 choices
over 224 games, **86 changed (17.1%)**, and **24.7% of all choices have zero rollout spread** —
the region this exists for, measured rather than assumed.

**Two scenario bugs found while building that scorecard, both of which reported a clean 0/6 —
indistinguishable from "the feature does not work":**

1. `HandValue` read the hand only, so scry was inert: a scry reorders the LIBRARY and never
   touches the hand. Fixed by valuing the top `LibraryTopCounted` (3) library cards with a
   draw-distance discount.
2. The scry fixture filled the library with 20 blanks BEFORE adding its two test cards, so the
   choice was offering filler. **Top of library is the FIRST card in the zone**
   (`DrawCardsAction` takes `GetChildrenIds(libraryId).FirstOrDefault()`); reading from the other
   end values the three cards you draw LAST.

The scorecard now asserts the choice options actually contain the cards under test, so a
malformed scenario fails loudly instead of scoring zero.

**On by default.** `AiCardValues.Current` loads once and is passed by `SimulatorRunner`,
`PreconstructedSimulatorRunner`, `DraftRunner`, `DraftTrainer` and `ScenarioConsole`; Godot passes
`MtgGameManager(setup, cardValues:)` from `MtgGameScene.LoadCardValues()`. **Null is valid and
means the file is missing**, in which case the AI is exactly what it was before — call
`AiCardValues.Describe()` rather than assuming.

#### A land in hand is worth what it UNLOCKS

A land has no entry in the value table and never will — it is not a threat. But it is not worth
zero either, and pricing it at zero had a specific consequence: every non-land carried a positive
value, so **pitching the land always maximised hand value**. The rollout out-argued that at the
shipped weight, but the margin halved (26.13 → 13.70) and at **weight 1.5** the AI started throwing
away land drops — 3x of headroom on the exact defect `AiLandDropTests` exists to prevent, and
invisible to a win rate at 2.33 choices per game.

**The deeper flaw was in the reach discount, not the zero.** `ReachDiscount` priced a four-drop at
three lands as "one turn away" whether or not you were holding the land that gets you there. Those
are very different positions: with the land, next turn's mana is certain; without it you have to
topdeck one.

`TopdeckDiscount` (0.6) splits `turnsAway` into turns you already hold the land for — merely
LATER — and turns you must still draw — also UNCERTAIN. A land is still worth 0 on its own; its
value is entirely the difference it makes to everything else, and that comes out right in every
case without a rule per case:

| Position | A land in hand is worth |
|---|---|
| 3 lands, four-drops stuck in hand | a lot — it unlocks all of them |
| 6 lands, nothing above four | ~nothing — correctly the first pitch |
| hand of one-drops | ~nothing — flood |

Measured on the three-lands-three-four-drops position, pitching the Plains went from the **best**
hand value (43.89) to the **worst** (26.33), and the margin over the correct pitch went 13.70 →
**31.26** — wider than the 26.13 the rollout manages alone, so card values now reinforce the right
answer instead of fighting it. `LandKeepingChoiceTests` covers both directions, because half of it
is a bias rather than a fix:

| Position | Choice | Answer |
|---|---|---|
| 3 lands, hand of four-drops | discard | pitch a spare four-drop, **keep the land** |
| 3 lands, hand of four-drops | tutor | **fetch the land** — it unlocks three cards |
| 4 lands, hand of four-drops | discard | **pitch the land** — it unlocks nothing |
| 5 lands, hand of four-drops | tutor | **fetch the spell** — the fifth land is surplus |

The land is kept at every weight from 0.25 to 3.0 in the first row. **A rule that simply never
pitched lands would pass rows 1–2 and fail rows 3–4**, which is why the surplus cases are asserted
rather than characterised.

**Counting it twice is the trap to avoid here.** A land must not also get a direct value, or it is
paid for once through `landsInHand` and again on its own.

**The Godot asset is a separate copy**, `SQGodotCommon/MtgGame/Assets/card_values_csc.json`.
Regenerating `sim_results/` does not update it — same trap as the draft model.

#### The retrain was run as a controlled A/B, and card values do NOT move card valuations

Two 300-draft runs, **same seed, same binary**, differing only in `MTG_CARD_VALUES=off`. Both
16 798 deck-games, prior exactly 0.5000, 1 draw in 8400, flat across quarters.

| Comparison | Spearman |
|---|---|
| values-ON vs values-OFF (16 798 each) | **0.972** |
| values-OFF vs the older 5 600-deck-game model | **0.599** |

**That second row is the control that matters.** Comparing the new model against the old one gave
0.596 and looked like a large reranking; the same comparison with the feature OFF gives 0.599. The
movement was **entirely sample size** — median games/card went 124 → 362, i.e. per-card 1 SE
±4.48pp → ±2.63pp.

Mean absolute movement between arms is 0.81pp. Tagging the 59 cards whose text raises a choice
(`WithDiscard`/`WithScry`/`WithDig`/`WithSearchLibrary`/`WithImpulseDraw`/`WithTutor`/`WithModes`):

| Group | n | Mean delta |
|---|---|---|
| choice-raising | 59 | **+0.035pp** (1 SE 0.157) |
| everything else | 349 | −0.056pp (1 SE 0.054) |

**No concentration.** Looters appear on both sides of the movement list (Jeskai Elder +3.09,
Rummaging Goblin +2.76 against Merfolk Looter −1.35, Teferi's Tutelage −1.42), which is what noise
looks like. Mean |delta| is mildly higher for choice cards (0.96 vs 0.78) with no directional
effect — consistent with card values changing WHICH card gets pitched, adding variance to those
cards' measured rates without systematically raising them.

**So a retrain is NOT required for a change of this size**, and that is the reusable finding: this
feature alters 17% of choices at 2.33 choices per game out of ~63 actions, and that does not reach
card-level win rates. **Do not read a model comparison without an equal-data control** — the
sample-size effect here was five times the real one.

Both arms are archived: `sim_results/draft_training_csc.cardvalues-{on,off}.json`. The ON model is
live, chosen for consistency with the AI that plays rather than on evidence of superiority.

### Training a set from the command line

Mode 4 is interactive, but the console reads plain `Console.ReadLine()`, so it drives fine from
stdin — no CLI-argument path was added because piping needs no shipped code:

```
printf '4\n\n\n300\n8\n1\n3\nn\ncscfinal\n' | dotnet run --project MtgSimulator.Console -c Release
```

Fields in order: mode, AI depth (blank = 3), format (blank = Booster), drafts, seats, generations,
**set choice** (the index printed by `ReadSet`, which changes as sets are registered — read the
menu, do not hardcode it), **train-from-scratch**, seed.

**The `n` is load-bearing and this command was missing it.** Once a model file exists, the trainer
asks "Draft with the existing model? (Y/n — n trains from scratch)" and then "Merge into it rather
than replace? (Y/n)", and an exhausted stdin answers **Y to both**. Adding a colour and running
the old command therefore merged the new cards into the previous colour's model instead of
retraining — the exact bootstrapping failure the section above warns about, arrived at by
following the documented command. Answering `n` skips the merge prompt entirely and replaces the
file, so the field count differs between the two paths; count the prompts in the output, do not
assume.

The final `Console.ReadKey` throws `InvalidOperationException` when stdin is redirected. It fires
*after* the model is written, so the file is safe; ignore it.

**Verify the console's `bin/` timestamps before trusting a training run.** `dotnet build
MtgSimulator.Console.csproj -c Release` can report success while leaving a stale copy of
`MtgCore.dll` / `MtgSimulator.dll` in `MtgSimulator.Console/bin/Release/net10.0/`, and
`dotnet run --no-build` then measures code that is not in the binary. This cost two full training
runs and three wrong conclusions in one session: a fix was declared ineffective twice when it had
simply never been compiled in. The tell is maddening — unit tests pass (the test projects rebuild
correctly) while the training run disagrees, which reads exactly like a real bug in the fix.

It is the same class as the `sim_results/` trap below, one level down: the thing you are measuring
is not the thing you changed.

```
rm -rf MtgSimulator.Console/bin MtgSimulator.Console/obj MtgSimulator/bin MtgSimulator/obj        MtgCore/bin MtgCore/obj
dotnet build MtgSimulator.Console/MtgSimulator.Console.csproj -c Release --no-incremental
ls -la MtgSimulator.Console/bin/Release/net10.0/MtgCore.dll   # must be newer than your edit
```

**When a training run contradicts a passing unit test, suspect the binary before the diagnosis.**

**`sim_results/` is relative to the SHELL's working directory, not the project's.** `dotnet run`
does not chdir into the project, so running from the repo root writes `./sim_results/` while
running from inside `MtgSimulator.Console/` writes `MtgSimulator.Console/sim_results/`. Two
directories with the same filename in them is how a freshly trained model gets silently
overwritten by a stale one — which happened, and the only symptom was every card's learned value
being byte-identical after a retrain that had clearly produced different summary numbers.

Always run from the repo root, and check `Prior` against the run's reported base win rate before
shipping a model:

```
python -c "import json;d=json.load(open('sim_results/draft_training_csc.json'));print(d['Prior'],d['Perspectives'])"
```

Reference rate, measured: 5 drafts = 140 games = 28s, so ~5.6 games/sec. 300 drafts ≈ 8 400 games
≈ 25 minutes.

**Card text is part of making a set playable, not a cosmetic afterthought.** A pack is read, not glanced at, and `MtgCardMapper.GetRulesText` silently omits any mechanic it does not know — invisible in a screenshot, but it makes the card undraftable. `SQGodotCommon.Tests/MtgGameTests/HollowmereRulesTextTests.cs` and `CoresetCubeRulesTextTests.cs` pin one card per mechanic and assert no card *with a mechanic* renders blank. Extend both when adding a mechanic.

This is not a hypothetical. Wiring the Core Set Cube into the draft UI produced **22 completely
blank card faces** on the first run of that test — every freeze effect, every bounce-to-library,
every prevention, modal and conditional spell — because `GetRulesText` knew none of the actions
they were built from. The engine was correct and fully tested; the cards were simply undraftable.

Two describe paths must both be extended, and missing either leaves a hole:
- `DescribeEffect` — an action used directly as a `CardEffect.ActionTemplate`
- `DescribeStep` — the same action used inside a `PipelineAction`

A **counterspell trap is the worst case**: it is a `SpellComponent` with *no effects at all*, so
nothing in the effect machinery has anything to say about it. It needs its own branch off the
component, which is why `DescribeCounterTrap` exists.

A card face is built from three `MtgCardMapper` calls, not one — each renders to its own element of the card frame:

| Call | Frame element | Notes |
|---|---|---|
| `GetTypeLine(card)` | band across the bottom of the art | Never blank, so it is the "this face rendered something" guarantee. No `"Creature — "` prefix when subtypes exist — the badge already says it. Every spell reads a flat `"Spell"` — see DesignNotes.md. |
| `GetPowerToughness(card, state)` | badge in the bottom-right corner | The **only** P/T source. `state: null` → printed stats, for draft packs. Returns null for non-creatures, which hides the badge. |
| `GetRulesText(card)` | rules box | Deliberately excludes P/T. Legitimately blank for a French-vanilla creature. |

**The card face is a fixed budget, and text is generated, so verbosity is a bug not a style question.** The rules box shrinks its font to fit and then clips at a 14pt readability floor; a clipped card is invisible in a screenshot but stops telling you what it does. Every place that joins rendered fragments goes through `CombineParts`, which squeezes out the two ways generated text repeats itself — a sequence authored twice over (`"take the opponent's best creature, destroy it"` × 2 → `"… — twice"`) and consecutive clauses differing only in their verb (`"Each creature you control gets +2/+2 …"` + `"… gains Flying …"` → one sentence). The clause merge only combines **predicates**: merging noun middles distributed a shared trailing noun and turned four tokens into two. Failing to merge costs a line; merging wrongly misprints the card, so `IsMergeableVerb` is a closed list.

`HollowmereRulesTextTests` pins the budget set-wide (≤6 rendered lines, ≤24-char type lines, no merged noun clauses) without naming cards, so retuning card balance cannot break it.

P/T used to be printed by `GetRulesText` *and* drawn by a `BoardCard` overlay label, from printed and effective stats respectively — so a lord-buffed creature read "2/2" in its box and "4/4" in its corner. Keep it single-sourced.

**Board layout is a budget, not a free-form arrangement.** `BoardUI.tscn` is a `MainColumn` of `[TopBar] [OpponentRow] [PlayerRow] [BottomBar]`, where each row is an `HBox` of `[panel][battlefield zone]`. The player panels live *beside* their rows rather than above them specifically so they stop driving the column's height — that is what allows `BattlefieldZone.CardScale` to be 0.68 instead of 0.45. See DesignNotes.md before adding anything to the column.

The event log is a collapsible overlay on its own `CanvasLayer`, closed by default, with an unread count on its toggle so an AI turn cannot pass unnoticed. Nothing reclaims its space automatically — `BoardUI.SetBoardWidth` moves `MainColumn`'s right anchor between 0.78 and 1.0. The hand's drop target is synced from `BoardUI.GetPlayerBattlefieldRect()` rather than hardcoded, since the board changes width when the log opens.

`MtgCardTheme` colours the frame by card type and the name plate by tribe, via `SelfModulate` so the tint cannot bleed onto the labels. It is the only per-card visual differentiation the engine can support: there is no colour, faction or rarity field, so `Card.Subtypes` and component presence are all there is to key on.

Card art is keyed by a slug of the card name (`CardArtLoader`). Hollowmere has essentially none, which degrades to the card scene's default artwork rather than failing — `Details.ApplyTo` assigns the texture unconditionally and the setter falls back, so a reused node cannot inherit the previous card's art.

`DraftTournament` runs the pod afterwards: circle-method pairings (seat 0 fixed, the rest rotate) give `seats - 1` rounds where every seat plays every other exactly once. The human's game is played in the UI and reported via `RecordHumanResult`; the other pairings run through `DraftRunner.PlayGame` on a background task started *before* the human leaves for their match, so the tables resolve in parallel with them playing. `SimulateRoundAsync` deliberately touches no tournament state — results come back and are folded in by `CompletePendingRoundAsync` on the caller's thread, which is why there is no lock anywhere in the class.

One known asymmetry, marked `ponytail:` in the source: `MtgGameManager` hardcodes the human as Player 1 and passes them first to `BeginGame`, so **the human is always on the play**. `DraftRunner` alternates across its two games per pair; at one game per pair there is nothing to alternate. Fixing it means threading a "plays second" flag through `MtgGameManager`.

`DraftPickers.Curve` scores stats-per-mana with a nudge away from a top-heavy curve. It sees only `ManaCost`, P/T, and creature-or-not, because that is all `Card` carries — there is no rarity and no color. Upgrade path is the GIH win rates in `sim_results/precon_*.csv` (`PreconstructedStats.CardGihRow`).

`DraftRunner` cycles pickers across seats and alternates who is on the play within each pairing, so its report measures picker quality rather than seat order. With no training file that is Curve vs Random (2 pickers); with one, Trained vs Curve vs Random (3). **Use a seat count divisible by the picker count** or the per-picker rates are not comparable — the runner warns when it isn't.

## Draft Training

Mode 4 in the console. Runs N drafts, plays the resulting decks against each other, and records **games-in-hand** counts: a card is credited for a game only if it was actually drawn, and a pair only when both halves were drawn in that same game. Output is `sim_results/draft_training.json`.

Each `CardStat`/`PairStat` also carries `DeckGames` — how often it was in the deck at all, drawn or not. `Games / DeckGames` is P(drawn | in deck), and the picker needs it (see below). Files written before this field existed load with `DeckGames = 0` and fall back to a scale factor of 1, which silently restores the old bias — **regenerate rather than merge into such a file.**

**Counts are stored, never rates.** That lets runs be merged (`DraftTrainingStore.SaveMerged` folds into whatever is on disk) and lets the scoring formula be retuned without re-simulating. Rates are computed at pick time.

**Shrinkage is not optional.** `DraftTrainingData.Shrink(wins, games, prior, k)` pulls every rate toward the base win rate with `k = 25`. Without it a pair seen twice and won twice reads as 100% and dominates every pick it appears in — an unshrunk synergy term is worse than no synergy term. Pair data is the thin part (median ~76 games/pair after 100 drafts), so this is what makes it usable.

`DraftPickers.Trained` scores each card as `cardDelta + synergyWeight * meanPairDelta`, both in **percentage points**, then samples with a softmax. `temperature` is in those same units: 0 = argmax, ~2 = splits near-ties, high = near-random. Cards absent from the model score 0 (exactly average), so an incomplete model degrades gracefully rather than ignoring unseen cards.

### The synergy baseline

A pair is scored against `DraftTrainingData.ExpectedPairRate(rateA, rateB, prior)` — what the two cards would post together if they did not interact, combining each card's effect in **log-odds** space (the correct way to add independent effects on a win/lose outcome). Synergy is the pair's shrunk rate minus that expectation.

Measuring a pair against the *global prior* instead just re-reports card quality: pair a bomb with anything and the pair looks great, so every pair containing that bomb reads as synergy. Measured on real data, switching baselines dropped Ancestral Recall's pairs from a mean of **+12.3 to +0.6** points, and the all-pairs mean from +3.2 to +0.2 (a proper baseline centres on zero). It also surfaced real interactions the old metric buried — e.g. Carnage Tyrant + Sol Ring at +12.1, ramp into an expensive threat.

Pairs shrink toward **that same expected rate**, not the prior. This matters: shrinking toward the prior would drag a thin pair of two strong cards downward and report it as negative synergy purely for lacking data. Shrinking toward the expectation means a pair with no evidence lands on exactly 0 synergy. `pairShrinkK` defaults to 200 — roughly 10x the card `shrinkK` of 25, matching the ~10x gap in data volume.

### The draw-frequency discount

A card's win rate is conditioned on **that card being drawn**; a pair's on **both being drawn**, which is far rarer. Adding them raw compares quantities measured on different events and over-weights synergy. `DraftPickers.PairDrawRatio` measures the gap from the data — median P(pair drawn) / median P(card drawn), **0.19 / 0.44 ≈ 0.44** — and discounts synergy by it:

```
score(X) = cardDelta(X)
         + synergyWeight · pairDrawRatio · Σ over deck-bound pool Y of pairDelta(X,Y)
```

Three things here are deliberate and were each arrived at by fixing a measured regression:

- **One global scalar, not per-card factors.** Per-card P(drawn) is *endogenous*: a card that wins games faster is drawn less often (measured correlation with win rate: −0.18), so scaling by it penalises exactly the best cards.
- **Applied to synergy only, never to the card term.** Scaling both compresses the whole score range, which makes a fixed `temperature` behave far more randomly — that alone cost ~4 points of win rate (82.2% → 78.4%) and masqueraded as a modelling error.
- **Summed, not averaged**, over only the first `Draft.DefaultMaxSpells` (23) pool cards — the ones `BuildDeck` actually plays. Averaging would arbitrarily divide by pool size; including all 45 counts synergies with cards that never make the deck.

### synergyWeight defaults to 0, on evidence

The synergy term is correct and tested, but **costs win rate at every weight measured**, so it is off by default. Measured on a 16 800-game model (600 drafts, median **464 games per pair**), 288 evaluation games per weight:

| synergyWeight | 0 | 0.5 | 1 | 2 | 4 |
|---|---|---|---|---|---|
| Trained win rate | **82.2%** | 80.2% | 74.6% | 62.8% | 54.5% |

This held after 6x more data, after fixing the independence baseline, and after adding the draw-frequency discount. The metric genuinely works — it ranks Faithless Looting + Tarmogoyf, Goblin Grenade + Goblin Matron and Cranial Plating + Frogmite at the top, and Delver of Secrets + Faithless Looting (looting mills the instants Delver needs) at the bottom. Those are real mechanical interactions, found unsupervised.

The problem is aggregation, not measurement. Per-pair synergy has sd ≈ 0.34 points of which roughly 0.31 is still sampling noise at n = 464; summing ~27 of them accumulates noise as √27 while the true signal is small. The card term (sd ≈ 2.16 after the same scaling) is simply a far better-measured signal, and any weight on synergy trades it away.

Two paths if it is worth revisiting, in order of cost:
1. **Confidence-gate the sum** — only count pairs whose evidence clears a threshold, instead of summing all 27. Standard denoising, cheap to try, untested.
2. **More data** — noise falls as 1/√n, so meaningfully beating it needs ~5x again (≈3000 drafts, ~75 min). Training runs merge, so this is additive.

Do not raise the weight on intuition. The sweep is cheap and has disproved the intuition twice.

### Draw diagnostics

`DraftTrainer.Run` prints a `DrawDiagnostics` report after every training run, because "N draws"
on its own is unreadable — it does not say whether the rules produced a draw or the harness ran
out of patience, and those need opposite fixes.

The report answers four questions, in this order:

| Section | Answers |
|---|---|
| By end reason | Real draw or harness limit. Only `Damage`/`LibraryEmpty` are real. Also prints median turn and median duration per reason — a `TimeLimitReached` at turn 10 is a slow AI, at turn 95 it is a loop. |
| By position in the run | Flat means the card pool; rising means the machine degrading (GC, throttling, another process on the cores). This is what separates a game bug from a measurement artifact, and it has already killed one wrong hypothesis. |
| Final board medians | Draw games vs decided games: life, lowest library, hand, permanent count, and whether anyone decked. |
| Card lift tables | P(draw \| card drawn) and P(draw \| permanent on board at end), each against the run's base draw rate, minimum 30 games. |

`BoardSnapshot` is captured for **every** game, not just draws. A board reading at a draw means
nothing on its own — 11 permanents is only interesting next to the 7 that decided games end on.
It is deliberately not a `GameStateSnapshot`: that carries the whole event log and is written one
file per game, which cannot be held for 28 000 games.

**The most useful single column is median turn at a time-limit draw.** Measured on CSC it is
turn 9–11 after 20 seconds, with roughly double the permanents of a decided game — so the draws
are the AI thinking itself to a standstill on a wide board, not games stalling out. `GameRunner`
checks its clock *between* actions, so one slow `SelectAction` is unbounded; the simulator passes
no `moveTimeBudget`.

Read `Prior` in a saved model as a draw check before shipping it: two perspectives per game and
one winner means a draw-free run gives exactly 0.50, so `Prior = 0.34` is a run that drew 32% of
its games.

#### Measured, and fixed: CSC's draws were the machine, not the cards

Before the fix, the draw rate was a function of how many games were in the parallel batch — same
code, same set, same decks, same seeds:

| Games in the batch | Draws | Median game |
|---|---|---|
| 1 120 | 0.4% | — |
| 8 400 | 2.8% | ~2 800 ms |
| 28 000 | **15.2%** | **7 560 ms** |
| 28 000, event logs dropped | 6.0% | ~4 400 ms |
| 1 120, wall-clock removed | **0%** | — |
| 28 000, wall-clock removed | **0.1%** | ~5 400 ms, flat across quarters |

Over 28 000 games, **4 263 of 4 264 draws were `TimeLimitReached` and exactly one was real**.
After the fix the same 28 000 games give 16 draws — 5 genuine turn-limit decking games kept as
data, 11 broken games excluded — and a base win rate of exactly 50.0% against 33.97% before.
Run time cost: +25%, which is the time those games were previously being cut short of.

The chain was: a game ended on wall-clock → wall-clock is dominated by AI search, which is slow
on wide boards → anything slowing the process pushed borderline games over the line → a bigger
batch retained more memory, so every game got slower. `DraftTrainer` held every `GameResult`
including its `AllEvents` log, which it never reads; dropping that alone moved 2 578 outcomes,
which is the proof that the outcomes were never about the cards.

Three changes, all of them worth understanding before touching this again:

1. **`GameRunner` no longer ends a game on the clock** (300 s safety net only). Termination is
   turn- and action-bounded, which is deterministic.
2. **`MultiTurnBeamSearchAiStrategy` bounds its own cost deterministically** via
   `DefaultRolloutBudget`, replacing wall-clock as the thing that stops a runaway search.
3. **`DraftTrainer` excludes machine-decided games** (`TimeLimitReached`, `UnhandledException`)
   from the counts instead of folding them in as draws, and prints how many it dropped.

**Never read a training run's draw count as a signal about the cards without checking the end
reason first.** For a year's worth of runs it measured the machine.

`MtgSimulator.Tests/MachineIndependenceTests.cs` pins this: the same game is played twice, the
second time against CPU contention, and the results must be identical.

**The load must come from dedicated below-normal-priority threads, never from `Task.Run`.**
Written with Tasks the test failed every time, and the fix was not at fault: the beam search
parallelises over the thread pool, so busy Tasks starve it of workers instead of merely slowing
it, and one move stretched past the 300-second safety net — the game ended at turn 1 having taken
a single action. It also made the test take 7 minutes instead of 3 seconds. Contend for cores, do
not take the workers.

A failure now means a wall-clock reading has crept back into a decision. The one honest exception
is the safety net firing, which the test asserts against separately and reports as proving
nothing either way.

Phases in `DraftTrainer.Run` are ordered deliberately: drafts run **sequentially** (cheap, must stay deterministic), all games across all drafts run in **one parallel batch** into a pre-allocated array, then results are folded in **sequentially** so the output never depends on thread scheduling.

### Generational (on-policy) training

Mode 4 asks for a generation count. Above 1 it loops: train → save → evaluate → retrain with the model it just produced as the drafters. The motivation is that card rates measured in Curve/Random decks describe how good a card is *in a badly-drafted deck*; a payoff card can look mediocre there and be strong in a deck that supports it.

- **Generations overwrite by default.** The model should describe the *current* drafting policy, so accumulating would blend old and new policies and dilute exactly the effect being created. The merge prompt only appears for a single-generation run.
- **Every seat at a training table must use a picker of the same strength.** No model → all Curve/Random (equally weak). A model → all Trained (equally strong). See below for why; `DraftTrainer.BuildPickers` carries the same warning.
- **Evaluation uses fixed seats (9) and fixed seeds (111/222/333) every generation**, so the only thing varying across the trend is the model. `DraftRunner.Run(verbose: false)` returns wins/games per picker name for exactly this.

Watch for a confound when reading a trend: if generation 1 bootstraps from a model trained on far more drafts, its first overwrite drops the data volume, so an early dip may be a sample-size effect rather than a policy effect. Compare generations against each other at equal drafts-per-generation, not against the seed model.

#### Measured result: on-policy retraining neither helps nor hurts

Run correctly (all seats Trained, overwrite, 10 generations × 150 drafts, bootstrapped from a 600-draft model), the trend is flat:

| Gen | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Trained win % | 84.7 | 84.7 | 85.4 | 79.9 | 82.6 | 81.9 | 86.1 | 80.6 | 82.6 | 81.9 | 81.9 |
| CardSpread | 4.98 | 3.62 | 4.40 | 4.15 | 4.37 | 4.18 | 4.35 | 3.99 | 3.99 | 3.83 | 4.45 |

Mean of generations 1–10 is 82.8% against a gen-0 baseline of 84.7%, with a per-generation SE of ±3.3pp at 144 games — i.e. no movement. The mechanism was healthy this time, which is what makes the null result trustworthy:

- **CardSpread stayed flat at ~4pp** (contaminated run: exploded to 14.93pp) — no strength bleed, no diversity collapse.
- **corr(starting rate, change) = −0.451** (contaminated run: +0.630). Negative is regression to the mean, the signature of honest re-measurement rather than amplification.
- **Spearman rank correlation gen0 vs gen10 = 0.874**, top-15 overlap 13/15 — the ranking is stable.

Conclusion: the card ranking converges after a single training pass, and self-play on this card pool has nothing further to teach it. Generational training is wired up and correct, but is not a lever worth spending compute on unless the card pool or the game rules change materially.

#### Measured failure: mixed-strength seats poison the data

Mixing a strong picker with weak ones at the same table was tried, on the reasoning that exploration seats preserve deck diversity. **It is actively harmful and must not be reintroduced.**

A card the model likes gets taken by the Trained seats, so it lands in decks that win ~84%; a card it dislikes lands in Curve/Random decks that win ~34%. The card's measured win rate then encodes *which picker drafted it*, not how good it is. Feeding that back compounds it every generation. Over 10 generations × 150 drafts:

| Symptom | Measurement |
|---|---|
| corr(starting rate, change in rate) | **+0.63** — the loop amplified existing preferences |
| Top-20 cards by starting rate | 59.7% → 67.5% (**+7.8pp**) |
| Bottom-20 cards | 46.9% → 37.3% (**−9.5pp**) |
| Card win-rate range | 44–67% → **31–79%** |
| Spread (sd) | 4.98pp → **14.93pp** |
| Actual play strength | 84.7% → **~80%** (slightly worse) |

Sampling was *not* the problem — every card still got 1,701–2,485 deck-games, so this is not starvation or collapse. It is pure drafter-identity contamination.

The opposite failure mode is real too: with all seats on one model, decks can converge until every card appears in winners and losers equally and rates decay toward the base rate. Softmax `temperature` is what prevents it. **Diagnose either failure with the spread of card win rates across generations** — it should stay roughly flat. Exploding means strength contamination; collapsing means diversity died.

**Training is memory-bound, not CPU-bound.** Measured on 16 cores: sequential 25.3s, DOP 4 16.7s, DOP 16 20.0s — extra threads past ~4 add GC contention and make it *slower*. Server GC (enabled in `MtgSimulator.Console.csproj`) flattens this to ~14.1s at any DOP. Do not add a degree-of-parallelism knob; there is nothing to tune. Reference: 2800 games ≈ 4 minutes.

Measured results on the 600-draft model (16 800 games, 33 600 deck-games): Trained **82.2%** vs Curve ~30% vs Random ~35% over 288 evaluation games. Note that `Curve` scores *below* `Random` — its stats-per-mana metric systematically over-drafts big vanilla creatures (Craw Wurm, Mahamoti Djinn rank lowest in the learned data) over efficient spells and card advantage.

Caveat: the model is trained and evaluated on the same card pool. It learns card quality within that pool; it does not generalise to new cards.

## Engine Discovery (mode 7)

**Phase one of the synergy work: what engines does this pool support, and do they assemble?** No
battles, no win rate, no evolution — every viable concept gets a deck built for it by
`DeckBuilder.SeedConcept` and plays solitaire games, and `EngineProbe` reports whether the payoff
went off with its support already deployed. Output is a ranked report you READ, plus
`sim_results/engines_<set>_<stamp>.json` for the evolver to seed from.

**The split exists because win rate cannot judge an engine that is not built yet.** A
half-assembled storm deck loses every game, so hill climbing on win rate walks away from it before
it is finished — which is what every run of mode 6 has done, *correctly*, to the wrong question.
Asking "does it assemble" first and "is it competitive" second is the only ordering where the
first question has an answer.

```bash
export MTG_MIN_LANDS=12
printf '7\n\n4\n10\n8\nengineseed\n' \
  | dotnet run --project MtgSimulator.Console -c Release
```

**`export` it on its own line — `MTG_MIN_LANDS=12 printf ... | dotnet run` sets the variable for
`printf` and NOT for `dotnet run`.** That form was documented here and in the handoff for a whole
session, and it silently runs the discovery at the 20-land default. The tell is in the output: the
console prints `NOTE MinLands is 20` when the floor is above 14, and every number in a run carrying
that line is measuring decks the archetypes cannot legally be.

Fields: mode, AI depth (blank = 2), set index (read the menu, it moves), solitaire games per
engine, engines to highlight, seed. **Set `MTG_MIN_LANDS=12`** — Storm runs 12 lands, Zoo and
Affinity 14, so at the 20 default every one of them is illegal and an engine is being asked to
assemble out of a deck it cannot be. Mode 7 prints a NOTE when the floor is above 14.

### A combo deck can be built correctly, measured correctly, and STILL not assemble

Three separate causes were found for one symptom — a planted two-card combo (CMB's Twin package)
reading `depth 0.0, LIFT 0.0`. The first two were measurement bugs and are fixed; **the third is
not a bug and is the important one.**

1. **`Summarise` medianed depth over ALL games**, including the zeroes from games that never
   assembled, so below 50% assembly `MedianDepth` was pinned to exactly 0 and `Lift` with it. 22 of
   22 engines under 50% read depth 0; 0 of 20 above it did. Fixed — depth now filters to assembled
   games, matching what turn always did. **Engines at depth 0 went 22/42 → 1/43.**
2. **Activating an ability emitted no event naming the card**, so `IsExecution` credited an
   activated-ability payoff when the creature ENTERED PLAY — before the ability could ever be used.
   Fixed by `AbilityActivatedEvent`. Moved the whole table (Mere-Storm depth 7.5 → 22.0).
3. **The deck wins without the combo.** After both fixes the Twin engine still reads
   `assem 20%, depth 1.0` — and the reason is in the same report: **`wins 8 of 10`, goldfish kill
   turn 6.** The flex slots are the format's best cards, the AI curves out and kills the inert
   opponent, and the 4-mana 1/1 copier is simply a worse play than a planeswalker. The combo is
   never needed, so it is never assembled.

**This is the goldfish blindness, one level up.** That criticism is already recorded against
`Goldfish` SPEED as a fitness — *"against an opponent that does nothing the quickest kill is cheap
creatures attacking"* — and `EngineProbe` runs inside that same solitaire game. Its metric is
better; its FIXTURE is the same one, and the game is decided before the engine matters.

**So the open item for combo measurement is the fixture, not the metric.** Three costed directions,
none tried:

- **Ask whether the combo was AVAILABLE, not whether the AI used it.** `LoopDetector` already
  answers this from a position and already finds this exact combo in two actions. Cost needs
  measuring — its published figures are for two-card fixtures, not a mid-game board.
- **Deny the deck its good stuff.** The Twin core's archetype pool is 4 cards, so `Complete` tops
  up from the format; a combo-only fill would force the question. Risks measuring an unplayable
  deck.
- **Give the goldfish opponent a clock**, so curve-out beatdown is not automatically sufficient.

Do not read a low `assem` on a combo core as "the builder failed" — check `Wins` in the same row
first.

### MEASURED: mode 7's GAME columns are not reproducible at a fixed seed

> **FIXED — kept as the diagnosis, not as a live warning.** The cause was
> `StringComparer.Ordinal.GetHashCode`, randomised per process, seeding each candidate's deck build;
> the fix is `EngineDiscovery.StableHash` (FNV-1a), called from `Probe`. **Do not re-derive
> this bug from the section below.** What still holds is the sampling caveat: at 10 games per engine
> the standard error on `assem` is ~16pp, so a single run's LIFT is noisy even now that two runs at
> one seed agree. Raise games-per-engine before ranking on it.

**Three runs, identical seed (`comboseed`), identical binary, DES.** Half the table is deterministic
and half is not:

| Column | Stable? |
|---|---|
| `supp` / `pay` / `enab` — the core's structure | **identical every run** |
| `bare` / `supp'd` — leverage | identical for almost every row |
| `assem` / `depth` / `LIFT` / `cover` / `kill` | **swing wildly** |

```
                     run A                        run B
Blood for Bones      60%  depth 15.0  LIFT +15.0  30%  depth 0.0  LIFT 0.0
Wirewood Herald      50%  depth  1.5  LIFT  +0.5  80%  depth 6.0  LIFT +5.5
Ajani's Pridemate    80%  depth  4.0  LIFT  +4.0  40%  depth 0.0  LIFT  0.0
```

**LIFT is the column the mode exists to produce, and it moved +15.0 → 0.0 for one concept between
two runs of the same seed.** Deck construction and the leverage sandbox are seeded; the solitaire
games are not, or not fully. At 10 games per engine the sampling error alone is ~16pp on `assem`, so
even a correctly seeded run would need far more games before a single number meant anything.

**How to read a mode 7 report until this is fixed:**

- **Trust the structural columns.** `supp`, `pay` and `enab` say what the pool can support and are
  exactly reproducible. So is the core listing underneath.
- **Do not rank on a single run's LIFT**, and do not compare LIFT between runs. A concept that reads
  +15 in one run and 0 in the next has told you nothing.
- **A CONSISTENT zero across runs is still evidence.** Kilnmother Vess reads `depth 0.0, LIFT 0.0` in
  every run — that is a real "never assembles", not noise.

This is the same family as the two non-determinism bugs already recorded here (the parallel-batch
draw disaster and `string.GetHashCode`), and the same diagnostic rule applies: **when two runs at
one seed disagree, find out why before reading either.** Fixing it means threading the run seed into
the solitaire games; raising games-per-engine reduces the noise but does not make runs comparable.

### A candidate is a payoff CARD and its whole demand conjunction, not one demand

`EngineCandidate` used to be keyed on a single `DemandIndex`, and that unit is one level too low.
**Dragonstorm asks two things at once** — `SpellsCastDemand` from `HasStorm`, and a Dragon filter
from its `SelectCardFromLibraryAction` — so a demand-keyed loop probes "spells cast" and "Dragon"
as two unrelated concepts, builds a deck for each, and neither of them is Dragonstorm. The card was
structurally unable to be a candidate.

**Nothing had to be discovered to fix this. `PoolFeatures.DemandsOf("Dragonstorm")` has returned
both demands since the card was written; what was missing is the JOIN.** `DeckCore.For(features,
payoff)` performs it: one payoff slot, then one support slot per informative demand.

It also explains the standing asymmetry in what the search finds. A tribal deck is ONE demand with
a large supplier set, which random mutation stumbles into; an N-card combo is a conjunction of
narrow demands, which it never will. A conjunction has to be constructed, not searched.

Three rules in `DeckCore.For`, each with a reason:

- **The payoff slot takes cards whose demands are a SUBSET of the anchor's.** Redundancy without
  naming anything: Tendrils belongs in a Dragonstorm core (it needs strictly less), Dragonstorm
  does not belong in a Tendrils core (that core promises no Dragon, so it would resolve for
  nothing — the dead-card rule).
- **A payoff never supplies its own slot.** Slots are counted independently, so a card in two of
  them satisfies both off one copy. It is also what gives tribal the right shape: 4 lords in the
  payoff slot and N *other* goblins in the support slot.
- **A thin slot CLAMPS rather than rejecting the core.** A count must not get to decide which
  archetypes exist.

#### Slot minimums are derived, and one family is still wrong

`MinFor` replaced a flat 8 with two rules. **A demand carrying its own number uses it** —
`SpellsCastDemand.Minimum` is harvested off `HasStorm`, and "m spells cast in ONE turn" is a density
question (m−1 others in hand simultaneously), not a draw-one question. **Everything else is
consistency**: the fewest copies giving ≥1 in hand by `TargetTurn` (5) with `TargetProbability`
(0.90), hypergeometric over the SPELL population.

**An opening hand is 7 cards with 3 lands by rule, so it holds only FOUR spells.** Counting seven
overstates every deck's consistency and understates every count here.

Enabler slots are held to `TargetTurn - 1`, because an outlet has to have resolved *before* the
payoff is worth casting. On ALL that gives target 10x against enabler 11x — a small gap, but derived
rather than invented.

**A FETCHED demand is not counted as though it must be drawn**, and getting this right took two
wrong answers worth recording.

Dragonstorm searches its Dragons out of the library, so the consistency rule — "how many copies to
DRAW one by turn 5" — asks a question the deck never has to answer. It produced **11x of the 7
Dragons in the pool**, a deck nobody would build, and was a regression against the old flat 8 for
every tutor.

**"It only needs one to exist" is also wrong, and the reason is the interesting part.** Storm
resolves the spell `Math.Max(SpellsCastThisTurn, 1)` times and Dragonstorm fetches **one Dragon per
copy**, so a storm count of 4 wants four Dragons left in the library. The historical Standard list
ran **six** — 4 Bogardan Hellkite + 2 Hunted Dragon — enough to turn a realistic storm count into
lethal, plus redundancy for copies drawn dead.

`CopiesThatSurvive(need, …)` implements it: the fewest copies leaving at least `need` UNDRAWN at
`TargetProbability`, where `need` comes from the payoff's OTHER demand. **Two demands on one card
that are not independent, and this is the only place the model expresses that.** Dragonstorm's
Dragon slot went 11x → **3x**.

**The floor is deliberately below the historical six, and that is the right relationship.** Deriving
6 is out of reach — it comes from wanting lethal, which is not in the card data. A core is a floor
with slack: `ProtectedIn` locks a slot only AT its minimum, so tuning stays free to find six. A floor
of 3 permits the real list; a floor of 11 forces a fake one. **When a derived count looks low, check
whether it is a floor before treating it as a bug.**

The signal for "fetched" is the recorded origin — `OriginOf` names the action a demand came from
(`SelectCardFromLibraryAction.Subtype`), marked `ponytail:` since a bool recorded at harvest time
would be sturdier. `DemandZone` will NOT serve: a bare subtype spec matches in every zone of the
probe fixture and returns null for exactly this demand.

**Candidates are deduped on the payoff slot, and the dedupe is load-bearing.** On ALL, 783 spells
give 203 cores but only **50 distinct** — ~64 payoffs that merely "target a creature" produce the
byte-identical core, and probing each is 64 runs of one experiment.

The deck built for a candidate is **the core plus good stuff**, which also makes LIFT cleaner than
it was: the control is the same payoffs plus the same good stuff, so the only difference between
the arms is the core's support slots. Depths are consequently much LOWER than the `SeedConcept`
numbers recorded below — that method jammed the whole deck with concept cards, this one guarantees
8 — so read `cover`, not `depth`, and do not compare the two eras' depth columns.

**Still open: broad cores crowd the table.** Concepts with 450+ suppliers ("a creature entered")
still generate cores, and a slot of 8 cards drawn from 465 constrains nothing. LIFT correctly rates
them ~0.0, but they occupy the report.

### LEVERAGE — what a payoff is worth with its demands answered

`CardValueSandbox.MeasureLeverage` is a second arm on the existing sandbox: the same card cast into
the same fixture twice, once bare and once with the demand's best suppliers stocked. **Both arms
subtract their own control**, so the extra permanents cancel and what is left is the card's own
gain from being supported. `LeverageSweepTests` drives it — **11s for all 783 cards**, so this is
cheap enough to run before any discovery session.

The stocking puts suppliers into **battlefield, graveyard, library and hand at once**, deliberately:
a subtype filter reads the battlefield, a reanimation spec the graveyard, Dragonstorm's fetch the
library, a rummage cost the hand — and dispatching per demand kind is the mechanic-to-meaning table
this project refuses to write. `SpellsCastDemand` is the one exception, set on the counter, the same
exception `PoolFeatures` already makes.

Do not record the values here — run the sweep and read them. What is worth carrying is the **shape
of the answer, which is not what was expected**:

```
payoff                     bare  supplied  LEVERAGE
Drogskol Captain          11.67    105.67    +94.00     <- tribal lord
Stromkirk Captain          9.00     77.67    +68.67     <- tribal lord
Krenko, Mob Boss          12.00     50.80    +38.80     <- tribal lord
Goblin Lackey              6.00     40.43    +34.43     <- tribal lord
Dragonstorm                0.00     32.03    +32.03     <- combo payoff
Archangel of Thune        64.31     15.90    -48.41     <- fixture artifact
```

**Raw leverage ranks TRIBAL above COMBO, which is backwards for the problem it was built for.** It
is measuring two different things under one number:

| signature | reads as | example |
|---|---|---|
| the card is a BLANK without support | **bare ≈ 0**, supplied high | Dragonstorm, Zombie Apocalypse, Spectral Tide |
| the card SCALES with support | bare high, supplied higher | every lord in the pool |

Both are real synergy. Only the first is the combo/engine class, and `bare` is what separates them
— Dragonstorm is the only card in the top ten reading exactly 0.00 without its demands. **Sort or
filter on `bare`, not on leverage alone**, and do not collapse them into one score on the strength
of one run; that is the retune-on-intuition failure this file records three times already.

**Large negatives at the bottom are a known fixture artifact, not anti-synergy.** Stocking two
copies into four zones dilutes a hand and a library, and auras and equipment (Archangel of Thune,
Angelic Destiny, Mark of the Vampire) price worse for it. Read the bottom of the table as "the
stocking is wrong for this card", not as a finding.

**Mode 7 now ranks BLANK-FIRST** — `EngineCandidate.BlankFirstKey`, lowest `bare` (clamped at 0),
then most gained, with unmeasured candidates sorted last so a failed measurement cannot masquerade
as a blank. LIFT is kept as a column. Measured at `MTG_MIN_LANDS=12`, 8 games:

```
concept                supp  assem  LIFT  kill    bare   supp'd
Dragonstorm              91    62%  +1.5   6.0    0.00    32.03
Zombie Apocalypse        79     0%   0.0   6.0    0.00    27.20
Entomb                  492    62%   0.0   5.0    0.00     0.00
Flameshadow Conjuring   453    50%   0.0   5.0    1.50    24.50
Goblin Lackey            95    50%  +0.5   5.0    6.00    40.43
Drogskol Captain         53    25%   0.0   5.0   11.67   105.67   <- was rank 1 under leverage
```

Two things to read off it rather than rediscover:

- **`assem` and `bare` answer different questions and the pair is the diagnosis.** Zombie
  Apocalypse is a perfect blank that gains 27 when supported and assembles **0%** of games — a real
  payoff the deck cannot deploy, which is a mana or consistency problem, not a synergy one.
  Dragonstorm is the same shape at 62%.
- **A zero-leverage blank outranks a high-leverage near-blank** (Entomb 0.00/0.00 above Flameshadow
  1.50/24.50). That is the strict lexicographic sort doing what was asked; `bare` is primary by
  design. Fix it by reading the `supp'd` column, not by inventing a combined score on one run.

### Speed was the wrong fitness, and execution is the replacement

`Goldfish` measures turns-to-kill against an inert opponent, and it was built to be the combo
fitness. **Measured on `ComboGradientTest`, the gradient points the wrong way**: dismantling Storm
made it goldfish *faster* (5.0 → 4.0). Against an opponent that does nothing the quickest kill is
cheap creatures attacking; a combo deck has to assemble first, so the instrument rewarded exactly
what the search already converges on unaided. Storm's real edge is that Tendrils damage cannot be
attacked, blocked or answered — **the goldfish removes interaction, and interaction-immunity is
the property that matters**, so it is blind to it by construction.

`EngineProbe` measures execution instead: *the payoff resolved, and when it did, the deck had
already deployed the cards that make it worth resolving*. A pile of the format's best individual
cards contains no payoff for any distinctive demand, so it reads **zero** here where it read best
on the goldfish. `Goldfish` is retained as a description of a deck, never as a fitness.

**Measured on `ComboGradientTest`, ALL pool, `MTG_MIN_LANDS=12`, 10 games per step.** `assem` is
the share of games where a payoff resolved with support down; `depth` is the median number of
enablers already deployed:

| cards swapped for good stuff | Storm assem/depth | Reanimator | Affinity | goldfish (all three) |
|---|---|---|---|---|
| 0 (assembled) | **90% / 12.5** | **80% / 3.5** | **100% / 6.5** | 4.0–5.0 |
| 4 | 0% / 0.0 | 60% / 3.0 | 100% / 6.0 | 4.0–7.0 |
| 8 | 0% / 0.0 | 60% / 2.5 | 100% / 6.5 | 4.0–6.0 |
| 12 | 0% / 0.0 | 20% / 0.0 | 100% / 5.0 | 4.5–5.0 |
| 16 | 0% / 0.0 | 0% / 0.0 | 100% / 3.5 | 4.0–5.5 |
| 24 | — | — | 50% / 1.0 | 4.0–5.0 |
| 32+ (pure pile) | 0% / 0.0 | 0% / 0.0 | 0% / 0.0 | 4.0–5.0 |

**The goldfish column has no trend at all** across three decks and thirty-odd steps — that is the
blindness, measured, not argued. The engine column falls to zero on all three.

**Storm falls off a CLIFF at four cards swapped, and that is a finding rather than a defect.** The
interpolation cuts in `PickWeakest`'s ordering — worst individual card first — and Storm's payoffs
are among the worst-rated cards in the format, so the very first cut takes them. **No local search
on any fitness can walk back to Storm**; its payoff has to be seeded and then protected, which is
exactly what the phase-two core lock is for. Reanimator and Affinity have real gradients and would
be findable by hill climbing if anything measured them.

Three rules inside `EngineProbe.Read`, each of which is silent when wrong and each pinned by a
paired negative case in `EngineProbeTests`:

- **Deployment is read generically, execution is not.** Any event carrying a `CardId` names that
  card happening — being milled or discarded is a perfectly good way to deploy a reanimation
  target — and that is the same positional reflection rule `PoolFeatures` harvests demands with,
  so a new event works the day it is emitted. Execution cannot be generic: a Tendrils pitched to
  Faithless Looting emits a `CardDiscardedEvent` carrying its id, and counting that as "the payoff
  went off" scores a deck for throwing its combo away. `IsExecution` is a closed three-event list
  (resolved, creature entered, permanent entered) and the comment says why.
- **Enablers are distinct card INSTANCES.** One ritual cast, resolved and buried is three events
  and one enabler.
- **A payoff is credited before it is deployed**, so a card that is both — a Goblin lord asks for
  Goblins and is one — never counts as its own support. Another copy landing earlier does, which
  is correct. Same rule as `PoolFeatures.Satisfaction`.

`Reading` carries `Depth` and `Payoffs` separately on purpose. Depth 0 with payoffs 0 means the
deck never cast the card it is built around; depth 0 with payoffs 3 means it cast it into an empty
board three times. Those are different failures, and **collapsing them into one number is how the
goldfish managed to look correct**.

### A targeting spec IS a demand when it aims at your own zones

**Reanimator was a payoff for nothing.** `WithReanimate` builds
`SingleTarget(IsCreatureInOwnGraveyardSpecification)`, so its demand lives on
`TargetingStrategy.Specification` — the one property the harvest rule excluded outright, on the
grounds that "what am I aiming at" is not "what does my deck need". That is right for Lightning
Bolt, whose "any creature" is a question about the opponent's board. It is exactly backwards for
Reanimate, a card that does nothing except ask your deck for creatures in your graveyard.

The two are separated **empirically, not by a type list**: a spec is a demand about your deck when
`GetCandidateIds` returns nothing on a battlefield. The battlefield is the shared board; hand,
graveyard and library are where your own cards live. `ZoneSpecification` already implements this
and composites already delegate to it, so `IsDeckScoped` is one loop and no new vocabulary. Empty
means no — a spec that selects nothing in a fixture holding the whole pool cannot be shown to be
about your deck, and adding fewer demands is the safe direction.

### …and when it aims at cards YOU CONTROL — the zone rule was one proxy too coarse

**"Not on a battlefield" is a proxy for "about your deck", and it missed the first real combo this
engine could express.** CMB's copiers read *"exhaust: create a token copy of target **Illusionist you
control**"*. That is a statement about what you must BUILD — but its candidates are on the
battlefield, so the zone rule classified it with Lightning Bolt and threw it away. Measured, all
four combo pieces harvested **zero** demands, `DeckCore.For` returned null, and a two-card loop that
`LoopDetector` finds in two actions was **invisible to the builder**. Exactly the same class as the
reanimator miss the zone rule was itself introduced to fix, one level down.

**Controlled-by-you is what un-shares the battlefield.** `IsControlScoped` decides it the same
empirical way, with no type list: the same card is placed on BOTH battlefields, and a spec is
control-scoped when it accepts your copy and rejects the opponent's. Nothing names
`IsControlledByYouSpecification`, so a new way of writing "you control" works the day it ships.

The opponent-side copies live in their own map and are deliberately **not** added to `placements` —
that dictionary is what `Matches` walks to compute supply, so an opponent-controlled copy in it
would make every "creature an opponent controls" spec answered by the whole pool and move every
supply number in the file.

**Measured before trusting, because the worry was a flood of "target creature you control" pump
spells:**

| | before | after |
|---|---|---|
| demands total | 63 | **68** |
| demands informative | 43 | **48** |
| cores built / distinct | 77 / 77 | **82 / 82** |

**+5, and the reason is the reusable part: demands dedupe by VALUE pool-wide.** Every pump spell
sharing one `CreatureControlledByYou` spec contributes a single demand between them, not one each —
so widening a *classification* rule costs far less than widening a *card* rule would.
`ComboProvingDiscoveryTests.DumpDemandAndCoreCounts` is the instrument; re-run it on both sides of
any further change here.

The combo's untappers still harvest zero demands, and that is correct: they are the SUPPLY side, not
the payoff. Only the copier asks for anything.

Unlocked **6 new concepts** on ALL, all of them archetypes the mode could not previously express:
instants-in-graveyard (100% assembly), cards-in-hand, creature-in-own-graveyard,
creature-in-any-graveyard. This is the third demand found living somewhere the harvest did not
look; the first two are storm and cast restrictions below.

### …and when it asks about the OPPONENT inside a TRIGGER — the fourth instance of one shape

**"Object-referential" was one proxy too coarse, in exactly the way the zone rule was.**
`ObjectReferentialSpecs` held `IsControlledByOpponentSpecification`, and `IsObjectReferential`
recurses into a trigger condition's `Filter` — so CMB's Sanguine Reciprocity, *"whenever an opponent
loses life"*, **was never harvested as a demand at all.**

That is a different failure from the one recorded for a whole session. The handoff and this file both
said the demand had *no suppliers* and was dropped as uninformative. It did not exist. A demand with
no suppliers is visible in a dump and reads as "nothing answers this"; an unharvested one is
indistinguishable from a card that asks nothing, which is why the wrong diagnosis survived.

The exclusion was right for its actual reason and the reason is entirely about the FIXTURE: the
placement pass deliberately puts no cards on the opponent's battlefield (see `IsControlScoped`), so
*"target creature an opponent controls"* is answered by nothing and every removal spell would read as
a dead card. **A trigger condition is never evaluated against that fixture** — `ProbeTriggers` asks
conditions against real EVENTS — so the reason does not transfer. `TargetingOnlyObjectReferentialSpecs`
is the split, and `underTrigger` is set once on the way down so a composite cannot smuggle the old
behaviour back.

#### The chained trigger probe: some triggers cannot fire alone

Harvesting the demand is only half. `ProbeTriggers` plays each card SOLO, and nothing a card does on
its own makes an opponent lose life — Covenant of Thorns produces that event, but only *after*
something gains you life. `PoolFeatures.ProbeChainedTriggers` is a second pass seeded from the first:
for each card asking a trigger demand that already has suppliers, replay it with one of those
suppliers and record what newly fires. Depth 2, which is what a two-card combo needs.

Three properties, each load-bearing:

- **The subject is played FIRST and the igniter second.** Covenant asks "whenever you gain life"; if
  the life gain already happened when Covenant arrives it fires nothing and the pass measures the
  same zero as before.
- **Attribution is a DIFF against the igniter alone.** This is the whole reason the pass is not the
  trap it was built to avoid: stocking the fixture so the opponent can lose life would make *every*
  card a supplier, push `supply[d].Count` past `UninformativeShare`, and drop the demand anyway —
  same outcome, more code. Same principle as `MeasureLeverage` subtracting its own control arm.
- **One control run per igniter**, cached, not one per subject.

**Measured on DES, and each half is separately necessary:**

| | demands | informative | cores |
|---|---|---|---|
| before | 71 | 52 | 91 |
| chained pass alone | **71** | **52** | **91** — a complete no-op |
| both | 78 | 53 | 93 |

Reciprocity's demand ends with four suppliers — Bloodhunter Bat, Corpse Knight, The Drowned
Archfiend (all drain on ETB, found by the solo pass) and **Covenant of Thorns, which only the chain
finds.** Disabling `ProbeChainedTriggers` drops Covenant and leaves the other three, which is the
pool-level vacuity check; `ChainedTriggerProbeTests` pins the same thing on a three-card fixture,
with `WithoutTheProducingCard_TheDemandStillHasNoSuppliers` as the guard against crediting everything.

**+7 demands for the classification fix is small because demands dedupe by VALUE pool-wide** — the
same reason `IsControlScoped` cost only +5. Widening a *classification* rule is far cheaper than
widening a *card* rule.

ponytail: an igniter whose events depend on the board will differ between the two arms and be
credited to the subject. Over-attribution rather than under, which is the wrong direction for this
file; tighten by comparing event SHAPES rather than satisfied demands if a real archetype is ever
measured gaining a supplier it should not have.

### Storm was structurally undiscoverable, and now is not

`PoolFeatures` harvests a card's own filter objects, and storm has none — it is a `bool` that
`ResolveSpellAction` multiplies by `MtgGame.SpellsCastThisTurn`. Same for
`RequiresSpellsCastThisTurnRestriction`, whose question lives in `CanCast`. So the strongest deck
in the precon round-robin (Traditional Storm, 63.1%) could not be found by a mode built to find
archetypes.

`PoolFeatures.SpellsCastDemand` is **the one demand in that file that is not the card's own filter
object.** Harvested from `HasStorm` (minimum 2 — a storm spell counts itself, so a minimum of 1 is
satisfied by casting nothing) and from the restriction's type. Supplied by every non-land pool card
costing ≤ 2, because there is nothing to evaluate it against a fixture with: "a spell was cast" is
an event in a turn, not a property of a card in a zone.

It is **skipped by `ProbeCostDemands`**, and that guard is load-bearing. Its supplier set would
otherwise pick a representative that is whichever qualifying card sorts first — and if that is an
artifact, four copies on the battlefield move every affinity card's cost and the probe reports the
entire artifact archetype as demanding "cast spells first". The cost probe reads a BOARD; this
demand is about a turn.

#### Supply is NET MANA, and the first attempt at it was pointed the wrong way

Supply was "costs 2 or less" for one run, and it produced a storm deck of **Delver of Secrets,
Llanowar Elves, Imposing Sovereign and Skyknight Vanguard** — cheap *creatures*, which are the
opposite of a storm enabler. A two-mana creature costs you two mana to add one to the count. **The
count is not the resource, the mana is.**

`ProbeManaProfit` measures it: deploy each card into a fixture and read the controller's
`CurrentMana` change minus what it cost. Nothing names an action type, so a mana source written
tomorrow is found the day it exists. Two arms, on the split the engine already makes — a permanent
is put onto the battlefield **and a turn is started**, because every mana rock and dork in this
project produces from an upkeep trigger and a probe without the turn scores all of them zero; a
non-permanent has its effects resolved directly.

| | suppliers on ALL (783 cards) | resulting deck | rank |
|---|---|---|---|
| cost ≤ 2 | **335** | cheap creatures, no rituals | 21st of 39 |
| net mana ≥ 0 | **16** | Lotus Bloom, Mox Pearl, Seething Song, Silt Ritual, Ornithopter Shard + 4 storm payoffs, 13 lands | **1st of 43, 100% assembly, depth 9.5** |

**Cantrips come out negative and are excluded, deliberately.** Preordain genuinely does advance a
storm turn by replacing itself, but counting "draws a card" as mana needs a conversion rate between
two resources — a scoring decision, and nothing in this file scores anything. Understating is the
safe direction for a rule that decides what a deck is built out of.

### LIFT is the only column that tells a synergy from a coincidence

The demand model measures **lexical co-occurrence** — cards that mention the same filter. On ALL,
`EventTriggerCondition{CreatureEnteredBattlefield}` has **453 suppliers** and
`IsCardTypeSpecification{Creature}` has **493**: every creature in the pool. A deck of creatures
plus one ETB payoff satisfies those by accident and ranked near the top of the first reports while
being an ordinary midrange pile. Most of the report was noise, and no amount of tuning the depth
metric fixes that, because the deck really is assembling — it just isn't an archetype.

**Lift is a contrast, and contrast is the only thing that answers it.** `GoodStuffControl` builds
the concept's payoffs, at the same copy counts and land count, into a pile of the format's
highest-`CardDelta` cards; the same probe runs against both under **common random numbers** (same
seeds, so shuffle variance cancels in the difference). `Lift = depth - controlDepth`. If a payoff
executes just as readily surrounded by the best cards in the format as by its own concept, the
concept contributes nothing.

Measured on ALL, and it separates cleanly:

| | n | mean LIFT | mean control depth |
|---|---|---|---|
| broad concepts (>300 suppliers) | 14 | **−0.54** | 3.82 |
| narrow concepts (<100 suppliers) | 19 | **+5.18** | 0.45 |

`IsCardTypeSpecification{Creature}` lands at depth 4.0 against control 4.0 — **lift +0.0**, exactly
the right answer. Ranking is on lift.

#### It was vacuous TWICE before it worked, both times invisibly

Worth recording because the failure looked identical to success both times: a plausible column of
numbers that ranked the same as the thing it was supposed to correct.

1. **The control excluded enablers from its filler**, to stop it "rebuilding the concept". That
   guarantees it cannot deploy one, so control depth is 0 by construction. 18 of 43 controls
   scored exactly 0.0.
2. **`EngineProbe.FromConcept` derived enablers from the concept DECK.** A control holds different
   cards, so it had no enablers under that definition either — same symptom, different cause,
   surviving the first fix. Enablers are now **pool**-derived and payoffs deck-derived, so "depth"
   means *how many demand-answering cards did this deck deploy*, a question any deck can be asked.

`EngineDiscovery.Run` now **warns when no control scores above 2.0**, because a control that never
scores is not a baseline and nothing else in the output says so. That check is the automated form
of "verify a test fails when you break the thing it tests" — it would have caught both.

### Ranking is on coverage, not raw depth

Raw depth ranks broad concepts first for free: a deck with 30 enablers deploys more of them than a
deck with 12 whether or not either is an archetype. `EngineCandidate.Coverage` is depth over the
deck's own enabler copies — "how much of my support was down" — which is scale-free. Same bias
`DeckBuilder.PickConcept`'s inverse weighting exists to correct, one step further down the pipe.

**Every viable concept is probed, not a top slice.** Taking the N most distinctive gives N demands
with exactly `MinConceptSuppliers` suppliers, which is the narrow tail rather than a survey, and
the question this mode exists to answer is what the pool supports.

### `SeedConcept` was building concept decks with no payoff in them

**Found by the first mode 7 run, and it had been live for every `conceptSlots > 0` run before
it.** The jam loop samples the whole candidate set — suppliers *and* askers together — so when a
demand has 400 suppliers and 4 askers the askers are essentially never drawn. On the ALL pool
**17 of 39 viable concepts came back with no payoff at all**, and the failures were systematic:
the broader the demand, the rarer its payoff, and therefore the less likely it was to be built.
That is the "24 artifacts and no Atog" failure the method's own contract rules out.

`SeedConcept` now places askers FIRST, capped at a third of the spell slots, before the jam loop
fills the rest. Measured on the same seed: **39 of 39 concepts build, payoffs per deck went 1–5 to
3–6, and `SpellsCastDemand` went from unbuildable to 80% assembly at depth 6.0** — storm went from
invisible to found in one change. `EngineDiscovery` now also NAMES any concept that produced no
deck, because a report showing fewer rows than it probed is indistinguishable from a broken
builder, which is how this survived its first run.

### Known limitation: the cost probe over-attributes convoke

`ProbeCostDemands` puts four copies of a demand's representative permanent on the battlefield and
records every card whose cost moved. **Convoke reduces cost per creature of ANY kind**, so probing
"Zombie creature you control" with four Zombies makes every convoke card look like a Zombie payoff.
In the ALL report, Stoke the Flames and Devouring Light appear as payoffs under nearly every
creature-subtype concept.

Not fatal — those cards genuinely do want creatures, and the metric still measures a real thing —
but it inflates tribal concepts and dilutes their payoff sets. The fix is a control probe:
re-measure with a representative that does NOT match the demand, and attribute only the difference.
Pre-existing, not introduced by mode 7; mode 7 is just the first thing that made it visible.

### The engine record is a CARD POOL, not a decklist

`EngineCandidate.Payoffs` and `.Enablers` are **pool-wide** — every card in the set that asks the
demand, and every card that answers it. `Deck` is one sample from that pool, built by `SeedConcept`
to take the measurements with; it is evidence the pool supports a deck, not a recommendation.

**Payoffs were deck-derived and that hid most of every archetype.** Affinity listed Frogmite and
not Myr Enforcer, Atog or Thoughtcast — cards asking exactly the same thing, absent purely because
the seeder did not draw them. `PoolFeatures.AskersOf(demandIndex)` is the reverse lookup that was
missing; nothing needed the direction until the report became a pool.

This is also the better phase-2 contract: **lock the pool, not the list.** Evolution retunes freely
inside the archetype instead of being frozen to twelve specific cards, which keeps the identity
without the max-density failure (24.4% for jammed goblins against 55% for the AI's half-built one).

### Four probe corrections, all found by reading a report rather than the code

| Symptom | Cause | Fix |
|---|---|---|
| Banefire, Hangarback Walker read as free storm enablers | `ManaCost` is **0** for every X card; X lives on the cast action | `XCostComponent` ⇒ cost unknowable ⇒ cannot be shown profitable |
| Past in Flames excluded from storm | probe counted cards **in hand**; it makes your GRAVEYARD castable | count castable cards anywhere — and note `IsInCastableZone` does **not** cover flashback, that is `MtgActionGenerator`'s job, so the graveyard clause is explicit |
| Entomb filed as a graveyard PAYOFF | a `SelectCard*Action.Filter` names what the card **fetches**, not a precondition | if resolving a card moves another into the demand's zone, it SUPPLIES and is struck from the askers |
| Stoke the Flames a payoff of every tribe | convoke discounts per creature of ANY kind, so 4 Zombies move its cost | control probe: attribute only if cost moves for the representative and **not** for a non-matching permanent |
| Soul Warden supplied no life gain | `ProbeTriggers` played ONE card onto an EMPTY battlefield, so "whenever ANOTHER creature enters" was structurally unfireable | play a vanilla companion after the subject, stripping the companion's own events so it does not make every card a supplier |

Measured after: Stoke is attributed only to genuinely creature-general demands, all sitting in the
±3 lift band; Soul Warden went 0 → 20 concepts; the storm pool went to 85 cards holding Lotus
Bloom, Mox Pearl, Rite of Flame, Seething Song, Silt Ritual, Faithless Looting, Ancestral Recall
and Past in Flames, at lift **+12.5** and 100% assembly.

**Two verification traps worth naming.** The card is `"Past in Flames"` — lowercase "in" — and
three case-sensitive greps in a row reported the fix as failing when it had worked; `GetByName` is
`OrdinalIgnoreCase` and hides the discrepancy. And `Satisfaction` **sums** across a card's demands,
so Dragonstorm's satisfied storm count masked its starving Dragon count and it read as fully
supported. `WeakestSatisfaction` is the per-demand form; `SupportScore` and `DeadCards` both use it
now, and `EngineCandidate.DeadInDeck` reports what is starving in the sample.

### Supply weighting: worked for reanimator, did NOT work for storm

`SupplyOf` returned a flat 1 for almost everything, which made the candidate set correct and
**unsortable** — every supplier scored the same through `SupportScore`, so `CardDelta` broke every
tie and a 513-card reanimator pool got sampled for its best creatures rather than its dozen discard
outlets. It now carries a magnitude per supply kind (net mana and cards for a spells-cast demand,
cards moved for a graveyard one, bodies for a token maker, flat 1 for a plain filter match) and
`SeedConcept` reads it at `ConceptSupplyWeight` 3.0.

Two predictions were set before running it. **One passed:**

| | before | after |
|---|---|---|
| Reanimator sample | Basri Ket, Crusader of Odric, Knight of Glory, Kytheon, Village Ironsmith | **Tome Dredger, Undead Alchemist, The Mere Gives Up Its Dead**, Despoiler of Souls, Endless Obedience |
| depth | 3.5 | **4.5** |

The self-mill and recursion cards arrived, which is exactly what the weight was built to do, so the
signal demonstrably reaches the sampler.

**One failed: storm still has no rituals.** The pool contains Lotus Bloom, Mox Pearl, Rite of
Flame, Seething Song, Silt Ritual and Past in Flames, and the sample deck picked Rain of
Revelation, Thought Scour, Chorus of Whispers and Uncomfortable Chill instead. Cause: `Mana` and
`Cards` are **summed as interchangeable**, and they are not — for a storm turn mana is the binding
resource, because a card you cannot cast does not advance the count. A draw-3 scores the same as a
ritual and has a far better standalone rate.

**Do not "fix" this by weighting mana above cards on instinct.** The engine metric currently rates
the draw build at 90% assembly and lift **+16.5**, higher than the ritual build ever scored, so the
measurement disagrees with the Magic-player prior and only a real deck-vs-deck result should settle
it. The honest position is that discovery's deliverable is the POOL, the pool is correct, and which
subset actually wins is a win-rate question for phase 2 rather than a softmax question here.

### The live problem: a correct enabler SET, selected from by raw card value

Both remaining failures are one cause, and it is now the highest-value thing to fix.

The demand model is right: storm's enablers are the 69 mana-or-card-positive cards, reanimator's
are the 513 cards that answer "a creature card in your graveyard" (every creature, plus the
movement suppliers that actually fill a graveyard). But `SeedConcept` samples within the candidate
set by `CardDelta + SupportScore`, i.e. **by how good each card is on its own**, and the genuinely
enabling minority is swamped:

| | before | after | what it cost |
|---|---|---|---|
| Storm | Lotus Bloom, Mox Pearl, Seething Song, Silt Ritual | Ancestral Recall, Sphinx of Uthuun, Llanowar Visionary, Sylvan Ranger | card draw arrived, **the rituals left** |
| Reanimator | Reanimate + fat creatures | Reanimate + better creatures | still **no discard outlet**, lift −5.5 |

Reanimator's 513-card enabler set contains perhaps a dozen cards that put a creature in a
graveyard, so a value-ranked sample essentially never draws one. Storm's 69 contains ~16 rituals
and Moxen against ~53 cantrips and card-draw creatures, and the draw cards have far better
standalone rates.

**`SupplyOf` already returns an int and nothing reads it as a weight.** The fix is to make supply
strength mean something — a ritual or a discard outlet supplies a concept far more than a card
that merely qualifies — and to have `SeedConcept`'s softmax include it. That keeps the "features
generate, win rate judges" rule intact, since it changes what gets *proposed*, not what gets
scored.

### Causal supply IS built — this section used to say it was not

**Do not re-derive this.** The movement rule lives in `PoolFeatures.Build` step 3a: `CardProfile`
records how many OTHER cards a card moved into each non-battlefield zone, `DemandZone` finds the
single zone a demand's candidates live in, and a card that moves cards there **supplies** the demand
and is **struck from its askers** — a card does not demand what it creates. No filter match is
required on the moved card, deliberately: a discard outlet is a graveyard enabler whatever it
happened to pitch in one probe.

Measured on ALL, `IsCreatureInOwnGraveyardSpecification`: **513 suppliers, 118 carrying weight above
1**, and the top of the list is exactly the outlets —

```
The Mere Swallows All(7), Abyssal Dredger(5), Drown in the Mere(5), Flood the Vaults(5),
Sunken Chorus(5), Unhallowed Rite(5), Grim Excavation(4), Mere-Drowned Scribe(4)
```

`CausalSupplyTests` dumps this per demand. Run it before believing any claim about what a slot
contains.

### The live limitation: SupplyOf merges channels that mean different things

`SupplyOf` returns **one int** covering every way a card can answer a demand — net mana and cards
for a spells-cast demand, cards moved for a graveyard one, bodies for a token maker, flat 1 for a
plain filter match. They are summed and maxed into a single scalar, so nothing downstream can ask
for one kind specifically.

The cost is visible in the same table: `Grave Titan(5)`, `Hornet Queen(5)`, `Throne of Empires(5)`
sit level with genuine self-mill in the graveyard demand's top twelve. A fatty and a mill spell are
both "enablers" of *a creature card in your graveyard*, and a reanimator deck genuinely wants both
— but it wants them in **different quantities and different roles**, and one merged weight cannot
express that.

**The causal channel is now recorded separately as well as merged** — `CausalSupplyOf` /
`CausalSuppliersOf`, written only by the movement pass, because that is the one channel whose
meaning is *this card CAUSES the thing* rather than *this card IS the thing*. `SupplyOf` is
unchanged, so nothing that read the merged number moved.

`DeckCore.For` splits on it, and the reanimator core is now the deck a human would describe:

```
Angel of Second Rites: 3 slots
   4x Payoff                                             [23]
   8x IsCreatureInOwnGraveyardSpecification             [458]   <- things to reanimate
   8x IsCreatureInOwnGraveyardSpecification [enablers]   [42]   <- things that put them there
```

The two are **disjoint, with a card that does both filed as causal** — slots are counted
independently, so an overlap would let one copy satisfy both, the same rule that strips payoffs from
their own support slot.

**"Causal is the scarcer role" is true for zones you have to work to fill, and NOT for the hand —
now gated by likelihood.** Selecting the demand with the most causal suppliers used to land on *"a
Goblin in your hand"*, where the causal side was **72 cards against 23 declarative**: the movement
rule requires no filter match on the card it moved, so every draw spell counted as putting a Goblin
in your hand, and Lackey's core asked for 13 draw spells against 11 Goblins.

**You choose what you pitch; you do not choose what you draw.** A mover is now credited only if it
is **DIRECTED** — its own card data names this demand, which is what Entomb's
`SelectCardFromZoneAction.Filter` does — or **LIKELY**: `1 - (1 - density)^moved >= 0.5`, i.e. more
often than not at least one card it moved matches, where density is the demand's declarative share
of the pool. Measured on ALL, the effect is surgical:

| core | before | after |
|---|---|---|
| Goblin Lackey | 4 slots, 13x from a **72**-card enabler slot | **3 slots**, 10x from 23 real Goblins |
| Angel of Second Rites (reanimator) | 458 declarative / **42** causal | 458 / **42**, byte-identical |

The probe cannot answer this by inspection — its moved cards are a fixed seven-card filler and
never a Goblin — so the expectation is the honest form. Understating stays the safe direction: a
card cut from the causal channel still sits in the declarative slot if it matches the filter
itself. `ABlindDrawDoesNotEnableANarrowHandDemand_ButAMillStillEnablesTheGraveyard` pins both
halves in one pool, and the mill half is the vacuity guard — switching the channel off entirely
would pass the draw half alone.

**Known ceiling, marked `ponytail:` in the source: density is measured over the POOL where what
matters is density in the DECK.** A built Zombie deck mills its own Zombies far more reliably than
the pool share implies, so a narrow-tribe self-mill can fall below the gate. No deck exists at
harvest time; revisit if a real archetype is measured losing its enablers.

`AGraveyardCoreSeparatesTheTargetsFromTheOutlets` still names the graveyard demand rather than
picking by count — the selector is no longer load-bearing for the Goblin case, but naming it is
still the honest way to ask.

### Settled: this engine cannot contain an infinite combo, so stop looking for one

**Do not rebuild the combo-discovery plan.** It was scoped, the cheap half was run, and the answer
is structural rather than a matter of search budget.

The produce/consume graph needs no new machinery — `A -> B` when A supplies a demand B asks, which
is `SupplyOf` and `DemandsOf`, and a card is struck from the askers of any demand it supplies so
self-loops cannot occur. `CausalSupplyTests.DumpProduceConsumeCycles` enumerates it. On ALL,
restricted to demands with ≤60 suppliers: **16 narrow demands, 38 cards that both give and take,
86 two-card cycles — and every one is a tribal membership loop.**

```
Arms Dealer      <-> Goblin Chieftain    (both ARE Goblins and WANT Goblins)
Atog             <-> Frogmite            (both artifacts, both want artifacts)
Cemetery Reaper  <-> Diregraf Captain    (Zombies)
```

That is not a combo, it is a tribe — the thing `DeckCore.For` already expresses as one slot. The
mechanism is that a lord is in the SAME demand's supplier list and asker list at once (Goblin
Chieftain IS a Goblin and WANTS Goblins), so every pair of lords points both ways automatically;
eight goblin payoffs give 28 pairs that are all one archetype.

**An asymmetry filter was tried and does not discriminate — do not retry it.** "Symmetric on one
demand = tribe, different demands each way = combo" is the right idea and it reports 65/21 on ALL,
but all 21 are false positives: `Subtype{Zombie}` and `And{Subtype{Zombie}, OnBattlefield,
ControlledByYou}` are distinct demand OBJECTS meaning nearly the same thing, so a tribal pair reads
as asymmetric. Same near-duplicate-concept problem this file already records for LIFT-ranked
selection. Canonicalising the demands would fix the false positives and would not change the
answer.

**No graph over this vocabulary can find a combo**, canonicalised or not. The demands are
membership predicates (subtype, card type, zone) and trigger events. There are no OPERATIONS in
them, so the best a cycle can ever mean is "these two cards are in the same category and both like
that category".

**The reason was the engine's action vocabulary, not the cube's card list.** A Splinter Twin combo
trades OPERATIONS: untap, copy, sacrifice. The engine had no untap effect and no copy action —
`ExhaustCreatureAction` only taps, only `StartTurnAction` cleared `IsExhausted`, and storm lives
inside `ResolveSpellAction` rather than as a targetable copy.

> **SUPERSEDED IN PART — `UnexhaustCreatureAction` SHIPPED.** The untap half of that argument is
> gone: an untap effect now exists (see `MtgCore/CLAUDE.md` §"Exhaust"), so a creature with a tap
> ability can be re-used within a turn and untap-trading combos ARE expressible. **Re-run both
> `LoopDetector` sweeps** — single cards and the 241k pair sweep — because this is exactly the
> moment their zero should be able to move, and if it does not, suspect the detector before
> believing the pool. Copy is still absent as a targetable action, though a token template carrying
> `CopyOnEnterComponent` reaches most of the same ground.
>
> The paragraph below remains correct about the **produce/consume graph**: that graph is built from
> membership predicates and has no operations in it, so it still cannot find a combo no matter what
> primitives exist. The simulation-based `LoopDetector` is the instrument, exactly as stated.

Without untap you could not re-use a mana source; without copy you cannot loop a spell. **No
infinite combo was expressible, so none could be discovered.** The verifier, the resource
fingerprint and the dominance-cycle check were all cancelled — they would have been correct code
searching a space that was provably empty.

**And when that happens, the detector to build is the SIMULATION one, not this graph.** A rigged
fixture plus a dominance check — same permanents, every resource ≥, at least one strictly up —
reads real game state, so it needs no vocabulary at all and sees a mechanic the day it ships. That
is also the only version that can answer the BALANCE question, which is a separate and standing
use: *"did this set change accidentally print a two-card loop?"* is worth asking on every set
change, whether or not anyone wants to build a combo deck.

Two rules for building it, both already paid for elsewhere in this file:

- **Plant a combo before trusting a zero.** A detector that reports "no loops in the cube" is
  indistinguishable from a broken one. Assert it FINDS a deliberate two-card loop in a test-only
  card set first, then assert it reports zero on the real pool.
- **Key on a RESOURCE FINGERPRINT, not a `GameState` key.** Two iterations of a loop hold different
  card instance ids, so state equality never fires. Use a multiset of (card name, tapped, counters)
  plus mana, hand count, storm count, life and graveyard. And require reachable lethal inside
  `GameRunner`'s 200-action cap — a loop that needs more iterations than that ends the game as a
  DRAW, not a win.

### LoopDetector — the balance instrument, built and validated

`Evolution/LoopDetector.cs`. Depth-first over the controller's legal actions, comparing every
reached position against every ancestor **on the current path**, and confirming by replay.
`LoopDetectorTests` drives it; the pool sweep is `[Explicit]` and runs **783 cards in 658 ms**.

**Measured: ALL, 783 cards, 0 looping, 0 threw.** That zero is only meaningful because
`APlantedFreeLoop_IsFound` passes — a detector that never fires reports the same thing.

Three defects were found while building it, and each produced a plausible result:

1. **Dominance is not a loop.** Any one-shot gain produces a position that dominates the one before
   it, so a single "gain 1 life" activation was indistinguishable from an unbounded one. `Repeats`
   replays the cycle from where it ended and requires it to dominate *again*. This is the whole
   feature — without it the detector flags every beneficial action.
2. **`Describe` returned a bare type name**, so two vanilla creatures attacking both read
   `"AttackAction"` and the replay of "attack" matched the OTHER creature. A board of two bears
   reported a confirmed loop. Identity now carries `CardId`, `AbilityIndex` and `XValue` by
   reflection — **the same defect as `ActionsMatch` re-finding an X-cost cast as X=0**, and the
   same rule fixes it: anything that distinguishes two legal actions must be in the match.
3. **`HasAttacked` belongs in the fingerprint alongside `IsExhausted`.** A creature that already
   attacked is spent for the purpose of repeating a line.

Four rules in the fingerprint, each pinned by a test:

- **Not a `GameState` key.** Every loop iteration holds new instance ids, so state equality never
  fires and a detector built on it reports zero forever while looking correct. This is why
  `CLAUDE.md`'s deferred "GameState comparison key" is NOT the thing to build here.
- **Dominance, not equality.** A token engine ends each iteration with strictly more permanents, so
  an equality test misses every growing loop.
- **Permanents keyed by name AND spent-state** (`U`/`T`/`A`). A line leaving a creature tapped where
  it started untapped has spent something and is not repeatable.
- **A grown graveyard is never the gain.** Every cast grows it, so counting it would make any two
  casts look like an engine. Checked for non-decrease, excluded from "something increased".

**`ActivatedAbilityComponent.MaxActivationsPerTurn` (default 1) is the engine's existing guard**,
and `TheEnginesOwnActivationCap_ClosesTheLoop` pins that the same card at the default is not a loop.
The realistic bug this catches on a set edit is that cap missing or set high enough not to bind.

**The pair sweep is built and is cheap: 241 173 fixtures in 28 seconds, 0 loops, 0 threw.**

```
ALL: 783 cards, 306153 pairs, 241173 with a repeatable source (64980 pruned), 0 looping
```

The prune is a **necessary condition, not a heuristic**: a loop needs something repeatable, and
casting is not repeatable because the card leaves your hand, so a pair where neither card has an
`ActivatedAbilityComponent` or a `TriggeredAbilityComponent` cannot loop. The pruned count is
printed, because a filter that quietly shrinks the search is how a sweep comes back clean for the
wrong reason.

**The fixture puts permanents on the battlefield and everything else in HAND.** The first version
put every card on the battlefield, which leaves an instant sitting there doing nothing — so the
sweep tested no spell at all and would have reported a clean pool for the wrong reason. Third time
this class of bug appeared in one session; see the `Describe` and dominance-vs-loop entries above.

**Read the zero as a BASELINE, not a proof.** What it is bounded by, all deliberate:

| Bound | Consequence |
|---|---|
| depth 6, branching 8, node cap 4000 | a longer or wider line is missed |
| `Repeats` matches steps by description | a line whose steps cannot be re-identified fails to confirm |
| no counters in the fingerprint | a counter-only loop looks like an identical board |
| two cards, one controller, 99 mana, turn already started | no three-card lines, no opponent interaction |

Given the engine has no untap and no copy, zero is the EXPECTED answer and its value is as a
regression baseline. **Re-run both sweeps the day an untap or copy primitive ships** — that is the
moment the number should be able to move, and if it does not, suspect the detector before believing
the pool.

Nothing feeds `LoopDetector` from mode 6 or 7; it is a test-time instrument today.

### Measured end to end: cores are NOT yet better than what they replaced

`CoreVsConceptChallenge` builds the same payoff two ways — `DeckCore.For` + `Satisfy` + flex fill
against `DeckBuilder.SeedConcept` — and plays both against the nine hand-built precons, 180 games
per arm, 1 SE = 3.7pp. **Two independent samples:**

```
payoff                  CORE   CONCEPT   delta       delta (2nd sample)
Dragonstorm             9.4%     6.1%    +3.3            +0.6
Zombie Apocalypse      11.7%     3.9%    +7.8           +10.6
Goblin Lackey          31.1%    26.1%    +5.0            -0.6
Angel of Second Rites   4.4%     3.9%    +0.6            -2.2
Atog (control)         18.3%    21.1%    -2.8            -8.9
mean                                     +2.8            -0.5
```

**Three of five deltas change sign between samples, so the effect is smaller than this harness can
see.** Only Zombie Apocalypse is consistently positive. Do not quote the mean of either run.

**The two numbers that ARE robust:**

| | |
|---|---|
| Best AI-built deck | 24–31% |
| Worst hand-built precon | **38–41%** (Dragonstorm) |
| Traditional Storm | **61–63%** |

The gap to hand-built is ~25pp and dwarfs anything the builder change moved. **That is the number
worth attacking, not the +3pp.**

**The composition dump says where it goes** (`WhatDoesTheCoreBuilderActuallyBuild`, no games):

```
Dragonstorm            16 core / 26 flex     <- 61% good stuff: Atog, Frogmite, Kird Ape, Myr Enforcer
Angel of Second Rites  36 core /  4 flex     <- derived minimums nearly fill the list; scored 3.9%
```

Two opposite failures from one flex fill. Ranking 26 free slots by standalone `CardDelta` produces
a pile of three unrelated decks — **the good-stuff failure moved out of the core and into the flex
half**, where nothing constrains it.

#### The fix is a POOL LOCK on the flex slots, and cohesion is the metric to read

`DeckCore.Complete` satisfies the core and then fills the rest **from the core's own slot cards**
rather than from the whole format. Measured on Dragonstorm: **38% on-theme → 100%**, and the list
stops being a pile:

```
Dragonstorm — 18 lands, 42 spells, 100% on-theme, 0 dead cards
  4x [0] Inquisition of Kozilek   4x [1] Rite of Flame        4x [5] Thundermaw Hellkite
  2x [0] Lotus Bloom              4x [3] Seething Song        4x [6] Bogardan Hellkite
  4x [1] Ancestral Recall                                     4x [7] Hunted Dragon
  4x [1] Careful Study                                        4x [7] Dragonstorm
  4x [1] Faithless Looting
```

**Nothing was told this is a "mana engine deck".** That phrase is a human label for a relation the
demand model already computes — `ProbeManaProfit` measures net mana and net cards, so storm's
supplier set IS rituals, cantrips, draw and tutors (`Tide of Whispers(4)`, `Ancestral Recall(3)`,
`Lotus Bloom(3)`, `Rain of Revelation(3)`). The archetype's card pool was always the answer to "what
else should this deck play"; it simply was not being asked.

**Do not try to widen the pool by one step of demand closure.** It was tried: for each core card,
add everything supplying a demand it asks. Storm's 85 suppliers each ask demands answered by most of
the pool, so one step returns the whole format and both arms came out byte-identical — a no-op
dressed as a feature.

**Cohesion is the metric, not win rate**, and `WhatDoesTheCoreBuilderActuallyBuild` reports it with
no games: on-theme share, dead cards, payoff present, `Holds`. Win rate is what pulls decks back
toward good stuff, because good stuff is the cheapest way to compete — so read cohesion first and
judge competitiveness separately.

**Still wrong: the fill has no notion of ENOUGH.** It tops up to 60 with the highest-valued on-theme
cards, giving **12 Dragons** where the historical list ran 6 — the slot minimum is a floor with
nothing above it, so a card that qualifies keeps getting added. A storm deck wants those slots on
rituals and cantrips. The missing idea is diminishing returns per slot, not more constraint.

#### Two harness bugs found by running it, both of which faked a result

1. **`Satisfy` filled the payoff slot best-first and left the ANCHOR out.** The Dragonstorm core
   built a deck holding Tendrils of Agony and four Dragons that nothing fetches — and `Holds`
   returned **true** throughout, because the slot was satisfied even though the core was not. Fixed
   by sorting the anchor first; pinned by `SatisfyAlwaysPlaysTheAnchor_NotJustSomethingFromItsSlot`.
2. **`string.GetHashCode` is randomized per process**, so seeding the shuffle from it resampled the
   whole experiment on every run. Caught because the CONCEPT arm — whose code path did not change —
   moved 6.1% → 7.2% between two runs that should have been byte-identical. Same class as the
   wall-clock bug that made mode 6 irreproducible: **when an unchanged arm moves, stop and find out
   why before reading the arm that did change.**

### Phase two: engine slots in mode 6, holding a POOL rather than a decklist

`MetagameEvolver(enginesPath:)` seeds the top-**LIFT** archetypes from a mode 7 report into the
first slots of the field. The console prompts for it: *"Engine file from mode 7? (blank = none)"*.

```bash
export MTG_MIN_LANDS=12
printf '6\n\n4\n8\n25\n3\n6\n20\n0.45\n800\nY\nY\n0\n4\nsim_results/engines_all_<stamp>.json\nmyseed\n' \
  | dotnet run --project MtgSimulator.Console -c Release
```

**A `DeckCore` narrows the card pool `Mutate` draws from.** One clause at the top of `Mutate`
filters `spells` to the core's own slot cards, and every operator — Swap, Recount, Package,
AdjustLands and the `Fill` they share — takes its candidates from that list, so nothing outside the
archetype can enter by any path. No operator needed to learn about engines.

**This paragraph described a `DeckBuilder.EngineIdentity` and an `EngineIdentityTests` for a whole
session, and NEITHER EXISTED** — a grep over every `.cs` in the solution found only prose. The core
protected what could be CUT (`ProtectedIn`) and nothing protected what could be ADDED, so every free
slot was refilled from the format: the good-stuff failure re-entering through the one door the
constraint did not watch. **Check that a documented mechanism exists before reasoning from it.**

#### It shipped first as a 60% quota, and the drift went straight into the allowance

The reasoning was that a deck should be able to pick up metagame answers, so 40% of spells were
left free. Measured on a real run, the storm slot spent all of it:

```
Steppe Lynx x3, Gravecrawler x3, Liliana of the Veil x2, Zombie Horde Leader x2,
Cathartic Reunion x3, Incorrigible Youths x1     — 14 of 43 spells, and Tendrils gone
```

**29 of 43 in-pool = 67%, legal against the 60% floor the whole time**, so nothing reported a
problem. A budget for drift gets spent on drift.

Narrowing the pool is also strictly better mechanically than grading the result:

- **Monotone.** A quota is checked after the fact, so a starting deck already below it freezes the
  slot solid — every proposal rejected, forever, silently. `SeedConcept` tops up from the whole
  pool when a concept cannot fill 36 slots, so impure starts are expected and that failure was
  reachable.
- **No wasted proposals.** A rejected mutant costs a mutation slot for that generation.
- **It converges INWARD.** `PickWeakest` may still cut anything, and nothing outside can return, so
  an impure start cleans itself up. `MetagameEvolver` prints starting purity for exactly this
  reason — the first version's failure was invisible, and a number is what makes it checkable.

Cutting stays unconstrained, so evolution can still discover that a storm deck wants fewer rituals.
It cannot discover that it wants Steppe Lynx.

`MutationNeverDrawsFromOutsideTheCoresPool` chains 60 generations and asserts nothing outside the
pool ever appears, with `WithoutACore_MutationDoesWanderOutsideThatPool` confirming that
unconstrained mutation *does* leave the same card set — without it the test would pass on a mutator
that never changes anything.

**The pool must be wide enough to fill a deck or the control measures the fixture.** At six cards
every swap produces an illegal list, `Mutate` returns null, and "the deck did not move" says nothing
about the mutator. `TheSameMutationsStillExploreInsideThePool` uses 24 and previously asserted that
a core "leaves everything else free" — a claim the pool lock deliberately makes false.

### Measured: an identity SURVIVES optimisation, and dissolves without a core

`IdentityUnderOptimizationTests` hill climbs a requested deck for 20 generations against a fixed
precon and asserts **cohesion**, not win rate. That framing is the point: a Dragonstorm deck that
loses to Zoo is still a Dragonstorm deck, and a pile that wins more while holding no archetype is
the failure the work exists to prevent. Win rate is the local gradient inside an identity; it is
never the judge of whether the identity survived.

```
Dragonstorm         gen 0  66.7%  ->  gen 20  83.3%   cohesion 100% throughout
Tendrils of Agony   gen 0  66.7%  ->  gen 20 100.0%   cohesion 100% throughout

same 60 proposals, no core:  43/43 off-theme, Dragonstorm 0x
```

**Both decks improved by finding real cards.** Dragonstorm added Thundermaw Hellkite beside Hunted
Dragon — **six Dragons, the historical number, reached by `Rebalance` rather than set by anyone**.
Tendrils found 4x Past in Flames, which makes your graveyard castable and is exactly what a storm
deck wants.

Two operators exist only when a core is supplied, so mode 6's unconstrained slots behave exactly as
before and every earlier measurement stays comparable:

- **`SwapWithinSlot`** — a different card for the same role at the same count. `Satisfy` fills a
  slot best-first and one playset usually covers the floor, so a freshly built deck plays ONE of a
  role's options; this is what lets the rest be tried.
- **`Rebalance`** — move one copy between roles, deck size fixed. The "is four Dragons better than
  six" operator. It cuts only from a slot above its floor, so it cannot erode the identity —
  `ProtectedIn` is not consulted because an illegal move cannot be proposed.

`TargetCopies` is deliberately NOT consulted by mutation. The cap is an opening position for the
fill; once a deck exists, what it should hold is a question for measurement, and a cap that also
bound mutation would answer it by assumption.

#### The control was wrong first, and the correction is a finding

The negative control originally hill climbed without a core and expected the deck to dissolve. It
**did not** — 100% on-theme for all 20 generations, Dragonstorm never cut. That is not the pool lock
working, because the lock was off: `SupportScore` already pays `SupportBonus` for a card the deck
answers and `DeadCardPenalty` against one it does not, so `Fill` prefers on-theme cards in a deck
that is already on-theme. **The soft pressure and the hard lock were doing the same job and the
fitness never had to choose.**

The honest claim is narrower: unconstrained mutation *can* leave the archetype, which a random walk
shows (43/43 off-theme) and a fitness-guided climb from a good deck does not. Read the pool lock as
a **guarantee** rather than as the only thing keeping decks together.

### A slot carries a FLOOR and a CAP, and they answer different questions

`CoreSlot.MinCopies` is the identity floor — never cut below, and what `ProtectedIn` locks.
`CoreSlot.TargetCopies` is the cap the fill aims for, defaulting to unbounded.

**Collapsing them produced 12 Dragons where a real list plays six.** With only a floor, the fill ran
to 60 cards best-first inside the archetype pool, so a slot that qualified kept getting topped up.

The cap is set by *what kind of question the demand asks*, which is derived rather than declared:

| Demand | Cap | Why |
|---|---|---|
| a COUNT (storm, artifacts, a tribe) | **unbounded** | a storm deck wants every ritual it can hold |
| ONE OBJECT you FETCH (a Dragon) | **the floor** | past "enough survive to be found", a further copy is a card you did not want to draw |

Measured — Dragonstorm at `MTG_MIN_LANDS=12` went from 12 Dragons to **4**, and the freed slots
filled with Mox Pearl, Lotus Bloom and Consult the Drowned. That is the trade the fill could not
previously make.

**The floor is where the cap STARTS, not where it belongs.** It is the number an optimiser should
move, and deriving it means there is something honest to move away from. Note `Satisfy` overshoots
it slightly — it tops a card to a full playset once chosen, so a floor of 3 yields 4 — which is why
the lists read as playsets rather than as odd counts.

**Engine slots are never culled.** Mode 7 already judged the archetype on whether it ASSEMBLES; the
win rate is here to tune it against the field, not to decide whether it deserves to exist. A
half-built combo deck loses every game, so a viability floor would delete exactly the decks the
feature exists to keep — which is what every unconstrained run has done. The rate is still reported.

**So the only way a dead engine leaves the field is the exclusion list**, and it has to leave,
because it does not merely waste its own slot. Mere-Storm read 8.2% and 0.0% across two CMB runs and
was **every other deck's best matchup** — the field spread, the viable count and every overall rate
were partly measured against a punching bag. `MetagameEvolver(excludedEngines:)` takes concept names
(console: *"Engines to exclude?"*), matched case-insensitively with a leading `Engine-` tolerated so
a deck name pasted out of a previous report works. An excluded slot falls back to a curve deck,
which is the honest control.

Three properties, all deliberate:

- **Excluded BEFORE the tier cut.** Filtering the chosen slots instead would let a dead archetype
  consume one of `usable * TierBreadth` places, so the list would quietly narrow the field it exists
  to widen.
- **Named on the console, not counted.** A misspelt exclusion is indistinguishable from an effective
  one in the final matrix, so unmatched names are warned about by name.
- **The run prints its own next exclusion list** — engine slots that finished below the viability
  floor, formatted ready to paste. Deciding an archetype is dead is still the reader's call; only
  the retyping is removed.

`EngineExclusionTests` pins it, with `WithoutAnExclusionList_EveryEngineIsStillSeeded` as the
vacuity guard — a `LoadEngines` that returned nothing would pass the exclusion assertion alone.
Cross-run persistence of the list is deliberately **not** built; the prompt plus the printed line is
the whole feature.

The last slot is never an engine: it is the permanent wildcard, and replacing the exploration arm
with a fixed archetype removes the only slot that can find something nobody has thought of.

**Known: LIFT-ranked selection can pick near-duplicate concepts.** The first real run took both
`Subtype{Spirit}` and `Spirit ∧ OnBattlefield ∧ ControlledByYou` into adjacent slots. The seeded
field still cleared the diversity floor, so this is untidy rather than broken; a distinctness pass
over chosen concepts is the fix if it ever costs a slot that matters.

**Re-baseline before any phase-two A/B.** Steppe Lynx, Liliana of the Veil and Chandra's Regulator
were nerfed after every number in `HANDOFF-ConstructedEvolution.md` was measured. Phase one does
not depend on this — a stale value table shifts which cards `SeedConcept` picks, not whether an
engine assembles.

## Constructed Metagame Evolution

Mode 6. Eight AI-built 60-card decks play each other, mutate toward better matchups against the
rest of the field, and the run outputs a metagame — decklists plus the matchup matrix. The first
thing in the project that BUILDS a constructed deck rather than measuring a hand-written one.

Each generation: every deck proposes M mutants, every candidate plays the frozen field, the best
improving mutant that stays distinct is accepted, and at most one non-viable deck is culled.
Phases mirror `DraftTrainer` exactly — sequential proposals, one parallel game batch into a
pre-allocated array, sequential fold-in.

### Common random numbers are what make it work at all

A mutation is worth ~1-3pp. An unpaired 42-game evaluation has a standard error near 7pp, so the
accept/reject decision would be a coin flip and thirty generations of it would be a random walk
that looks like evolution.

`MetagameEvolver.Seed(gen, deck, opponent, repeat)` deliberately does **not** take the candidate
index, so a parent and all of its mutants play identical opponents on identical shuffles with
identical AI RNG. Shuffle and search variance is shared between the arms and cancels in the
difference. **If one detail of this mode is ever "simplified", this is the one that silently
invalidates every number it prints.**

### A round-robin averages 50%, so the 40% target is a floor, not a goal

The sum of win rates in a closed field is exactly `50% x N` by construction — "every deck at 40%"
is unreachable arithmetic. It is implemented as a **viability floor**: a deck that cannot clear
40% against the field is a non-viable list and gets replaced. Grace period of 5 generations (a
fresh seed starts bad by definition) and at most one cull per generation (the field has to be
re-measured after any replacement).

The report also prints each deck's BEST matchup, because a deck under the floor that still
counters something is a real archetype and one that beats nothing is not.

### Seeding is a concept, not 36 random cards

Anchor (sampled by card value) → kernel of its best-evidenced synergy partners → curve target →
weighted fill. Archetypes fall out of the curve and the kernel; **nothing is hand-labelled**, and
nothing should be. Both halves already existed in the trained model: per-card rates say what is
good, `PairStat` says what wants to be together, and both were found unsupervised.

Deck 8 is a permanent **wildcard** slot: uniform anchor ignoring card value, double synergy
weight. It is often bad and gets culled, which IS the exploration. Without a slot that ignores
what the prior already believes, the field can only refine cards the prior already liked, and a
combo deck of individually-mediocre pieces is unreachable.

### The synergy gate was set to a value nothing could reach

**`MinPairGames` was 200 and that silently disabled every synergy path in the mode for three
full runs.** The CSC draft table's busiest pair has **166** games (median 53, p99 108), so
nothing cleared it: `SynergyDelta` returned 0 for all 83 028 pairs, `TopPartners` came back
empty so the seeder's kernel never formed, and both fill and cut scoring collapsed to card
quality plus curve.

The output was piles of individually-strong cards with visible anti-synergies — Atog with no
artifacts, a sweeper alongside the deck's own mana dorks. **It was caught by reading the
decklists, not the code**, which is the same lesson as the inert-card audits: a wrong-but-
plausible output is the only symptom a dead code path produces.

Three compounding causes, all worth recognising again:

1. The 200 was copied from `pairShrinkK`, a **shrinkage constant** — a different quantity that
   happens to be a number about pairs.
2. The unit test used inline data with `Games = 4000`, so it passed while no real pair could
   clear the gate. A test measuring nothing, and the second in this file.
3. Nothing reports a synergy term that is uniformly zero. `TopPartners` returning empty is
   indistinguishable from "this card has no partners".

Now 50, calibrated against measured distributions rather than analogy:

| table | pairs | p50 | p99 | max | ≥50 games |
|---|---|---|---|---|---|
| CSC draft | 83 028 | 53 | 108 | 166 | 56% |
| constructed (one run) | 3 484 | 119 | 5 768 | 13 704 | 56% |

At 50, 46 868 CSC pairs clear and the top measured synergies are mechanically real (Siege-Gang
Commander + Volley Veteran, Siege-Gang + Swiftfoot Boots). `RealisticPairEvidence_ClearsTheGate`
and `TopPartners_FindsAKernel_AtRealisticEvidence` use volumes the real data reaches, so raising
the gate back fails a test instead of going quiet.

**Before trusting any synergy work here, check that pairs actually clear the gate on the model
you are using** — the one-liner is in the Draft Training section and takes seconds.

### Per-deck history: synergy-aware cutting

The global pair table cannot answer "is this combination pulling its weight in THIS deck". It is
spread over 83 000 pairs at a median of 53 games, and **on the combined pool 58% of the pair
space is cross-set and can never have data at all**, because sets are drafted separately — Atog
is in LEG and every artifact it wants is in CSC, a pair that has never existed in any draft.

`DeckHistory` answers it per deck slot instead. Within one deck the same question is far better
conditioned: ~15 distinct cards is ~105 pairs rather than 83 000, every one a 4-of drawn in most
games, so pairs reach hundreds to thousands of games within a few generations.

- **Cutting only.** `PickWeakest` adds `KeepScore`; selection stays on overall win rate plus
  curve, because a card not yet in the deck has no history in it.
- **`PairDelta` sums rather than averages**, so a card carrying four winning pairs is harder to
  cut than one carrying a single lucky pair — the linchpin survives an unremarkable solo rate,
  which is exactly what a synergy piece looks like and what pure card quality cuts first.
- **Summing is safe here where it was not globally.** The measured harm was from adding ~27
  thin cross-pool deltas; these are dense, few, about the deck being scored, and carry two
  orders of magnitude more evidence per pair.
- **Reset on cull**, and pairs whose partner has been cut are ignored — a record about a card no
  longer in the deck is not evidence about the deck now.

Baseline is the deck's OWN base rate, not an independence baseline: inside one deck the question
is simply whether games drawing both go better than that deck's average game, and the cards'
individual rates are already carried by `CardDelta`.

### Selection scores ABSOLUTE joint win rate, never deviation from a baseline

`ConstructedValues.JointDelta` (how the pair does against the base rate) is the selection
criterion. `SynergyDelta` (how it does against `ExpectedPairRate`) is a diagnostic and **must not
be used to choose cards**.

The reason is not a preference. For two individually-terrible cards the independence baseline is
catastrophic, so a pair that merely performs badly scores as strong positive synergy — **the
worst cards in a format are the easiest to show synergy for.** Measured:

| Pair | Baseline expects | "Synergy" | Absolute |
|---|---|---|---|
| Dragonstorm + Tendrils of Agony | −35.4pp | **+8.91pp** | **−26.11pp** |
| Thoughtcast + Dragonstorm | −33.7pp | +5.08pp | −28.26pp |

That ranking built a 4x-Dragonstorm storm deck which finished at **8.6%** and sat near-dead for
30 generations, dragging the whole field's numbers with it.

Absolute scoring also removes the need for a separate card-quality gate: a card that is weak
alone but genuinely enables the anchor still qualifies, one that is bad alone AND together
cannot. Both directions are pinned —
`TwoTerribleCards_AreNotSelectedAsPartners_EvenWithHugeSynergy` and
`AGenuineEnabler_IsStillSelected_EvenIfWeakAlone`. Without the second, the fix would silently
become "only ever pair already-good cards".

**`DeckFit` averages, it does not sum.** The summed version was unbounded in deck size: a
10-card, 40-copy deck produced ~+290 against card values in the ±25 range, so card quality became
rounding error and a deck rated Dragonstorm (+261 combined) above Ancestral Recall (+240).
Averaging keeps it in `CardDelta` units so a weight of 1.0 means "these matter equally".

### What per-deck history does NOT fix

The user-visible complaint was **anti-synergy**, and half of it is still open. Two shapes:

| | Example | Expressible as a pair? |
|---|---|---|
| Genuine anti-synergy | Wrath + your own mana dorks | Yes, once measured |
| Missing critical mass | Atog with no artifacts | **No** |

Atog does not want *one* artifact, it wants ~15. That is a threshold on a deck, and no pairwise
statistic expresses it at any sample size. The planned answer is **rules-derived** synergy read
off components, which needs no data and therefore works on cross-set pairs:

- `DestroyCreatureAction` + `AllValid(Creatures())` → negative per creature in your own deck
- a cost or count filtered on a subtype (Atog's `SacrificeAdditionalCost`, tribal lords) →
  positive per matching card

This is **not** the card labelling that was rejected at design time — nobody types "aggro" on a
card. The components already encode the behaviour and cannot rot, because they *are* the card.

**Deferred deliberately. The full plan, with measured numbers, costed options and the trap list,
is `SynergyFeaturePlan.md` at the solution root.** Start there rather than re-deriving it.

### The synergy gate is load-bearing

This document records that `synergyWeight` cost win rate at every value tested. **That finding
does not transfer here, and the distinction is the whole reason this works.** It measured summing
~27 noisy pair deltas to rank one draft pick — noise accumulates as √27 and swamps a small signal.
This mode uses only the top few pairs off a single anchor, gated at `ConstructedValues.MinPairGames`
(200), which is the confidence-gating listed above as untested improvement path #1.

**Drop the gate and this becomes the thing that was disproved.** `ThinPairs_ContributeExactlyZeroSynergy`
pins it, and `WellEvidencedPairs_ReportRealSynergy` pins that it is not so tight nothing fires —
a test asserting only the zero would pass on a synergy term that never works.

### Limited values are not constructed values

The draft model measures a card in a 40-card, 23-spell, near-singleton drafted deck. Constructed
is 60 cards, 4-ofs, curated. Big vanilla creatures and grindy card advantage rate well in limited
and poorly in constructed; narrow combo pieces rate near zero in limited and can define a
constructed format. **Seeding from a limited prior and never correcting it just builds more
consistent draft decks**, which is the failure this mode exists to avoid.

Fitness is already immune — it is the win rate in constructed games. The exposure is
*reachability*: hill climbing from limited-good starting points may never find the combo deck. So
the mode counts its own games into `sim_results/constructed_values_<set>.json` and shrinks toward
what it measures.

**The blend needs no decay schedule.** It is `DraftTrainingData.Shrink` with the draft-derived
rate passed as the prior instead of 0.5: few constructed games ⇒ the draft value, many ⇒ the
measured constructed value. With no draft model at all every card scores 0, seeding is
quality-blind, and the table builds from scratch — a supported mode, offered as `n` at the
"Seed from the draft model?" prompt, and the honest control arm if the prior is ever suspected of
dominating a result.

`CardShrinkK` is 25, matching `DraftPickers`. That means a single game moves a valuation ~2.6pp
and one generation of play (~168 deck-games for a card in a deck) flips it outright. Both are
pinned, in both directions, by `ASingleConstructedGame_BarelyMovesTheValuation` and
`OneGenerationOfEvidence_MovesTheValuationSubstantially`. **Do not retune k on intuition** — the
same rule as `synergyWeight`.

### Two self-checks before trusting any run

| Check | Question | Where |
|---|---|---|
| 1 | Can the fitness measurement see anything? | `SelfCheck_ADeliberatelyTerribleDeckLosesFarBelowTheViabilityFloor` — 1/1s for 5 against 3/3s for 2. **Measured 0/40.** `[Explicit]` |
| 2 | Did it escape the limited prior? | The run's own "Constructed vs limited" section: Spearman + top movers |

Self-check 2 is the acceptance test for the whole limited-vs-constructed concern, and the run
warns on its own output above 0.95:

| Reading | Means |
|---|---|
| Spearman ≈ 1.0, no movers | **Failing** — constructed data is not displacing the prior |
| ~0.5-0.8 with a coherent mover list | Working — the format has its own valuations |
| ≈ 0 early | Noise, not signal — check games/card first |

Sanity-check the mover list by eye: 4-of-dependent and narrow-but-powerful cards rising, big
vanilla creatures and slow card advantage falling.

### The combined pool

`SetRegistry.Combined` ("ALL", 785 cards) is every registered set as one format. It does not
contradict the no-merging rule on `SetRegistry` — that rule protects the trained *picker*, which
would score a whole set at the prior and never pick from it. A merged pool is fine wherever the
model is a starting prior rather than the pick policy. It is offered to mode 6 only, via
`ReadSet(includeCombined: true)`.

**18 names collide** and are resolved last-registered-wins, on the rule that two cards printed
with the same name do the same thing and the most recent printing is current. Every replacement
is logged, because a silent behaviour swap between two same-named cards is otherwise invisible.
Do not prefix names with set codes — that breaks the name keying every per-card table depends on.

### Measured: first real run (CSC, 8 decks x 30 generations)

38 682 games, **0 excluded**, 38.7 minutes, prior exactly 0.5000 (draw-free). All 8 output decks
valid: 60 cards, max 4 copies, 21-26 lands, 13-17 distinct spells. Final field spread 35.7-65.0%,
6/8 above the viability floor, 10 culls.

Do not record specific card values here — run it and read the file. What is worth carrying:

**Diversity decays to the constraint and stops there: 76% at gen 1, 35% at gen 30**, i.e. exactly
the threshold. The constraint is *binding*, not slack — the field wants to converge further and
only the hard constraint prevents it. Read a final diversity equal to `minDifference` as "these
decks are as similar as they were allowed to be", not as a healthy result.

**Spread does not converge.** 33pp at gen 1, 40pp at gen 30, oscillating 24-55pp throughout with
no trend, and 5-8 of 8 mutants accepted every single generation. The field churns rather than
settling. That is not obviously wrong — a real metagame churns too — but it means **the final
matrix is a snapshot of decks at different ages**, some re-seeded two generations earlier and
still climbing. Deck A finished NON-VIABLE at 35.7% having been re-seeded at gen 28.

**Only 182 of 408 cards were ever played, and 146 cleared 100 games.** The other 226 have no
constructed data at all and will keep scoring at their draft prior forever — the same
self-reinforcing blind spot this document warns about for draft bootstrapping, in a new place.
More generations do not obviously fix it; the wildcard slot is the only pressure against it.

**corr(draft rate, movement) = +0.224.** For reference, the poisoned draft run measured +0.63
(amplification) and the healthy one −0.451 (regression to the mean). Positive means card values
are mildly absorbing the strength of the decks that happened to hold them. **That is structural
here and cannot be fixed the way it was for draft training**: the fix there was making every seat
equal strength, and this mode deliberately makes decks unequal — that is the entire point. Read
`constructed_values_*.json` as "how well this card did in the decks that played it", not as a
context-free card rating.

#### The two tables are NOT directly comparable, and the first report said they were

Both are games-in-hand rates, and a card is credited only for games in which it was **drawn**.
Constructed games end faster than limited ones, so the mean games-in-hand win rate differs by
format even at identical priors: **draft 0.5162 against constructed 0.4439, a 7.2pp offset.**

Uncentred, that made *every* card read as "worse in constructed" by ~9pp — the faller list was
the offset and the riser list was whichever cards beat it. It looks like a dramatic finding and
is an artifact. `Movers` now subtracts the median movement and `FormatOffset` reports it; the
run prints the offset on its own line. `RawMove` is kept so the artifact stays visible rather
than being silently absorbed.

The **ranking was never affected** — `SpearmanAgainstDraft` is rank-based, and read 0.597 either
way, squarely in the healthy band. Only the magnitudes were wrong. Pinned by
`Movers_CentresOutTheCrossFormatOffset`.

**The general rule: two games-in-hand tables measured in different formats share no zero point.**
Compare ranks, or centre, but never subtract one pp column from the other and read the result.

### Measured: 100 generations, all sets, synergy-aware cutting

100 002 games, 258.9 minutes, 46 excluded (0.05%). 201 030 constructed deck-games, median **1 420
games/card** over 429 cards, median **472 games/pair** over 4 654 pairs — the per-deck pair data
is dense, which is what makes `DeckHistory` usable.

Against the 30-generation runs, three things improved and one broke.

**Diversity stopped decaying.** 83% at gen 1, never below 60%, 61% at the end — where the first
CSC run went 76% → 35% and sat on its floor. The constraint is no longer the only thing holding
the field apart.

**The real field got tighter, not looser.** The headline spread of 45.7pp is a wildcard artifact;
excluding it the seven real decks span **37.5–58.3%, 20.8pp**, against 29.3pp for the first run
*including* its wildcard.

**Decks brew when left alone.** Four slots reached age 70–95 (Deck F survived 95 generations
uncut) and the decks are visibly more coherent than the earlier piles — the best deck was 8 of 10
distinct cards from one Hollowmere graveyard theme, where a pre-fix deck was Path to Exile +
Tarmogoyf + 4x Past in Flames with no storm enablers.

#### The wildcard slot is structurally broken on a large pool

**13 of 48 culls were the wildcard, and it never once survived** — final rate 17.1%, age 7, and
EVERY other deck's best matchup is "vs Wildcard". It is a permanent punching bag inflating all
seven real decks and both headline numbers ("Viable 7/8", "spread 45.7pp").

The cause is the uniform anchor. On CSC's 408 cards a uniform pick landed on something playable
often enough; over 785 it is a lottery ticket that never wins, so the slot re-seeds, dies, and
re-seeds forever. The exploration it was meant to provide never materialises — a deck that dies
every seven generations contributes nothing to the metagame it is supposed to widen.

**Do not read this as "the wildcard idea was wrong."** It worked on the smaller pool. What is
wrong is *uniform over everything* as the anchor rule. The fix to try is a restricted uniform —
sample the anchor uniformly among cards that have measured synergy partners, or among the top
half by value — so it still ignores what the prior prefers without being a pure lottery.
Untested; do not ship it on intuition.

#### Spearman fell to 0.280 and the reading is genuinely ambiguous

Against 0.597 on the first CSC run, over 392 cards rather than 146. **This is not thin data** —
median 1 420 games/card — so the two honest readings are:

1. Real divergence. A 785-card format values cards very differently from limited, and more cards
   measured means more of that difference is visible.
2. Context narrowing. Decks are now specialised into themes, so a card played in only one deck
   is measured entirely inside that deck's shell, which is the same deck-strength absorption
   already recorded above (`corr(draft rate, movement)`), sharpened by specialisation.

These need separating before the constructed table is trusted as a card rating. The cheap
discriminator is per-card *deck spread*: a card measured in one deck only is a different kind of
number from one measured across five, and nothing currently distinguishes them.

### The pilot is NOT the ceiling — the search operator is

This section used to claim that a 2-turn lookahead cannot pilot combo, so a metagame skewed to
creature aggro/midrange was inevitable. **That is false and it misdirected a whole session.**
The precon decks in `DeckRegistry` — Traditional Storm, Reanimator, Affinity, Goblins,
Dragonstorm — were piloted well enough to win games from the day they were written, and Storm had
to be **balanced down** for being too strong. Not optimal piloting, but nowhere near unable.

What actually produces the midrange piles is the **mutation operator**, and the difference matters
because one is unfixable and the other is a search change:

Real deckbuilding commits to a concept, jams every card in the pool that serves it, measures, and
then prunes *within* the concept — abandoning it only once the pool is out of options. Nobody
arrives at Goblins by adding eight Goblins to a midrange deck one at a time; that tanks the win
rate at every intermediate step, which is exactly what the accept-if-better rule then rejects.

`Mutate` random-walks: it proposes small edits and keeps whatever raises the win rate this
generation. A half-built synergy deck is worse than the midrange pile it came from at every step
of the way, so the climb never reaches it and the pieces get pruned back out. `Package` was the
first attempt at this and is too small — it brings in an anchor plus up to two partners, where a
concept needs its whole critical mass at once and then needs to be **held** while it is refined.

The symptom to recognise: decks that read as halfway to an archetype. A Dragonstorm deck with **no
dragons in it at all** — a literally blank card holding a slot for generations — and Thoughtcast
in decks with no artifacts. A card whose demands are satisfied at zero is dead, and nothing in the
mode currently notices.

### MEASURED: the DES field loses to hand-built references, 32.7%

**10 decks x 12 generations, 21 846 games, 3 references x 20 games each.** The first run in which
the Designed pool had a gauntlet at all.

| | field wins | reference wins |
|---|---|---|
| CMB Twin | 25.0% | **75.0%** |
| CMB Elves | 29.5% | **70.5%** |
| CMB Reanimator | 43.5% | 56.5% |
| **field overall** | **32.7%** | |

Every internal metric read healthy at the same time: **8/10 viable, 47.8pp spread, 44% diversity**
against a 35% floor. That is the closed-round-robin blindness this file already records for ALL
(Zoo 66.2%), now measured on the pool all recent work has used.

**The strongest single result is the controlled one.** `Engine-Wirewood Herald` was SEEDED with the
elf core and evolved for twelve generations; the hand-built elf list beat it **65-35**. Same
archetype, same pool, handed to the builder — and a human list of it wins. That is an optimiser
result, not a card-power one, because the archetype was not something the search had to discover.

#### CONFIRMED with the data gap closed: (c)/(f) is real, and the table measures the WRONG QUANTITY

The presim re-run gave every CMB card real isolation data — the table went 711 to 732 cards — and
**the elf slot still plays none of them.** The gauntlet gap moved 32.7% to 35.8%, i.e. barely.

| card | rate in RANDOM decks | in the builder's deck |
|---|---|---|
| Sylvan Ranger | 66.8% | 4x |
| Radha, Heart of Keld | 63.1% | 4x |
| Nissa, Vastwood Seer | 59.4% | (4x previous run) |
| Wirewood Conduit | **51.6%** | **0x** |
| Wirewood Symbiont | **48.9%** | **0x** |
| Timberwatch Elder | **47.8%** | **0x** |

**The valuations are correct and that is the problem.** `PreSimulation` measures a card in RANDOM
decks, which is value-in-isolation — and a 1/1 that pumps your other Elves genuinely IS mediocre in
a random deck. A synergy card is *defined* by being weak alone and strong in context, so more data
never fixes this: it measures the isolation value more precisely. **This is the cleanest statement
of items (c)/(f) available and it is now measured rather than argued.**

It also rules out the cold-start reading recorded above: the blind spot was real and worth fixing,
but closing it changed almost nothing.

#### Leverage does NOT currently answer this, and it was built for a different question

The obvious candidate is `CardValueSandbox.MeasureLeverage`, which already measures a card bare and
then with its demands' suppliers stocked. **Measured on exactly this case, it does not discriminate:**

```
                     winRate      bare   supplied     gain
Wirewood Conduit      +0.90pp     6.00       2.59    -3.41
Timberwatch Elder     -1.40pp     6.00       2.59    -3.41   <- identical
Wirewood Symbiont     -0.92pp     6.00       2.59    -3.41   <- identical
Poison-Tip Archer     +8.05pp    15.93      14.48    -1.45
Nissa / Radha / Sylvan Ranger / Reclamation Sage    "asks nothing answerable"
```

Three failures, all structural rather than tuning:

- **It is keyed on the DEMAND, not the card.** All three elves ask the same Elf subtype demand and
  get the same two stocked suppliers, so they measure identically by construction.
- **Four of six played cards return 0.00** — "asks nothing answerable" or "nothing in the pool to
  stock" — so the column is blank for most of a deck.
- **The gains are negative**, which is the fixture artifact this file already documents: stocking two
  copies into four zones dilutes a hand and a library, and tribal bodies price worse for it.

**So the machinery is not a drop-in.** It was built to ask "is this payoff a blank without support?"
and answers that adequately for a Dragonstorm shape (bare 0.00, supplied 32.03). What (c)/(f) needs
is a different measurement: **a card's marginal contribution to a REALISTIC deck state** — add it to
a board and hand that look like the archetype, roll forward, subtract the control — rather than to a
bare fixture stocked with two representatives. Same rollout-and-subtract skeleton, different
conditioning.

#### CAUSE OF THE EARLIER READING, kept because it was a real defect

**Every CMB card was ABSENT from the constructed values table.** `constructed_values_des_presim.json`
held 711 cards measured before the set existed; all 21 CMB cards scored exactly **0.00** while the
HLM/CSC elves around them read up to **+17.17**:

```
Nissa, Vastwood Seer   +17.17      Wirewood Conduit     0.00   <- never played
Sylvan Ranger          +15.55      Timberwatch Elder    0.00   <- never played
Radha, Heart of Keld   +14.33      Wirewood Symbiont    0.00   <- never played
Llanowar Elves         +12.90      (all 21 CMB cards absent from the table)
```

The run was launched with **`presim 0`**, so nothing generated isolation data for the new cards
either. A card with no data scores zero on the fill's main term and loses to every measured card;
`ExplorationBonus * Unmeasured(name)` exists to counteract exactly this and is nowhere near enough
against +17.

**This is the self-reinforcing blind spot this file already documents twice** — once for draft
bootstrapping ("every new card would score 0 against known cards scoring up to +16.8, so they would
be passed over every pick, never make a deck, and never accumulate data") and once for constructed
("226 cards have no constructed data at all and will keep scoring at their draft prior forever"). It
is the same failure in a third place, and a run that adds a set without a presim pass walks straight
into it.

**Operational rule: after adding cards to a pool, run mode 6 with `presim > 0` at least once, or the
new cards are unplayable by construction.** Check with the one-liner rather than assuming:

```
python -c "
import json;d=json.load(open('sim_results/constructed_values_<set>_presim.json'))
have={c['Name'] for c in d['Cards']};print(len(have))"
```

**Superseded by the presim re-run above**, which closed this gap and changed almost nothing — so the
blind spot was a real defect but not the cause of the elf failure. Keep the operational rule; drop
the diagnosis.

#### The elf decklists, which are what localised it

Both decks draw from the same 24-card core pool at the same 17 lands; only the choices differ, so
card power is controlled away and the loss is about selection. The builder's output after twelve
generations:

```
4x Dwynen's Elite      4x Llanowar Elves        4x Reclamation Sage
3x Dwynen, Gilt-Leaf   4x Nissa, Vastwood Seer  4x Sylvan Ranger
3x Elvish Archdruid    4x Poison-Tip Archer     4x Wirewood Herald
4x Elvish Mystic       4x Radha, Heart of Keld
1x Elvish Visionary
```

**Zero Wirewood Symbiont, zero Wirewood Conduit, zero Timberwatch Elder** — three of the four engine
pieces of the archetype it was SEEDED with. The one it kept is the anchor, which `ProtectedIn` locks
so it could not be cut. In their place: four Reclamation Sage, a naturalize body that is near-vanilla
against these decks, plus a stray 1x singleton of the sort a random walk leaves behind.

Note the builder's bodies are BIGGER — Radha 3/3, Poison-Tip Archer 2/3, Dwynen 3/4 against a
hand-built list of mostly 1/1s — and the curves are close (2.40 against 2.16). So it is not a curve
failure and not a stats failure. **Those three names are exactly the cards the values table had never
heard of**, which is what pointed at the cause above.

**References built from HLM/CSC only are still worth having**, to separate "cannot assemble a pushed
planted combo" from "builds weak decks generally". Neither is established yet.

Cost: 36.9 minutes at 10 decks, against 31.2 without a gauntlet.

### The mutation log — what the search TRIED, not only what survived

`MutationLog` records every proposal: generation, slot, the cards added and removed, the parent's
rate, the candidate's rate, and why it ended where it did. Printed as a summary plus a per-card
table, and written whole to `sim_results/mutations_<set>_<stamp>.csv`.

**It exists because a final decklist cannot answer the question that keeps being asked of it.**
"Wirewood Conduit is not in the elf deck" has at least two causes with opposite fixes — never
proposed (a selection-heuristic problem) or proposed, played and cut (a survivability problem) —
and this document argued the first from the fill rule for a whole session with nothing measuring
either. The CSV settles it with a sort.

Four things about how it is computed, each of which would otherwise mislead:

- **`ParentRate` is the parent's rate in the SAME generation.** Common random numbers make that a
  paired comparison — the parent and all its mutants played identical opponents on identical
  shuffles — so `Delta` is the mutation's effect with shuffle and search variance cancelled.
  Against the previous generation's number it would be neither paired nor meaningful.
- **`MeanDelta` per card is over PROPOSALS, not over accepted ones.** Acceptance is conditioned on
  beating the parent, so averaging the accepted rows reports every card as positive by
  construction — the same selection artifact this file records for card values measured inside the
  decks that played them. A card tried three times and kept once is a card the search likes and the
  field does not.
- **`too-similar` is a separate outcome from `rejected`.** They are different failures: one is
  losing on fitness, the other is a field pinned by its diversity floor rejecting proposals it
  agreed were improvements. Collapsing them hides the run this file already records whose diversity
  sat exactly on the constraint for all twelve generations while every other number read healthy.
- **A recount is rendered as a change.** `Mutate` moves 3x to 4x far more often than it swaps a card
  in, so a diff over card SETS would make the most-used operator invisible. Land changes likewise,
  as a synthetic `Land` entry that `ByCard` filters back out.

Culls are logged too (`reseeded`), with the full old→new diff — a cull is the largest edit the
search makes, and omitting it makes a card look never-tried when its whole deck was replaced under it.

**Read the rejects first.** The accepted rows only say what worked; the rejects say what the mutator
keeps reaching for and failing with.

#### MEASURED: a slot can spend its whole mutation budget producing nothing

**10 decks x 12 generations on DES, 12 204 games, 6 engine slots, `MTG_MIN_LANDS=12`, seed 7.**

| slot | core pool | real proposals | final |
|---|---|---|---|
| Engine-Watcher of the Spheres | 129 | **32** | 47.2% |
| Engine-Drogskol Captain | 54 | **33** | 46.1% |
| Engine-Wirewood Herald | 24 | 7 | 59.4% |
| Engine-Xathrid Necromancer | 130 | 6 | 62.8% |
| Engine-Master of the Wild Hunt | **2** | **3** | **26.1% NON-VIABLE** |
| Engine-Sanguine Reciprocity | **5** | **2** | **32.2% NON-VIABLE** |

`MutantsFor` gives a deck at or below 45% the **full** budget, and both non-viable slots sat there
all run — so each was offered ~36 mutation attempts and used 3 and 2 of them. **They were frozen at
their seeded list for twelve generations and then reported as non-viable archetypes.** Xathrid and
Wirewood are the other half of the rule: both spent most of the run above `StableRate` (0.60), where
the budget is deliberately **zero**.

This matters directly for the exclusion list: a run would have told you to permanently exclude two
archetypes that were never optimised at all. **Check a slot's `dry` count before excluding it.**

`no-proposal` rows are now logged for exactly this reason. The finding had to be inferred from
missing rows the first time, which is how it nearly went unnoticed.

#### The bug: a mutation budget spent on calls rather than on mutants

**`MutantsFor` returns "how many mutants to EVALUATE" and the loop spent it as "how many times to
call a function that often fails".** `Mutate` rolls ONE operator and returns null whenever that
operator cannot produce a legal, distinct, core-holding, profile-respecting list — and the slot was
consumed either way. No retry.

Null rates per single call, measured by `MutationYieldTests.WhereDoTheNullProposalsComeFrom` over
600 mutations of each deck a real run produced (DES, `MTG_MIN_LANDS=12`):

| condition | null rate |
|---|---|
| no core, profile `Any` | 3–9% |
| `Control` profile, deck below its band | **34%** |
| engine core, 508 cards in pool | 18% |
| engine core, **5** cards in pool | **95%** |

**Two independent causes, and the curve profile was the one nobody suspected.** A `Control` deck
sitting below its band may only move toward it, so roughly every mutation that lowers the curve is
discarded — an 11x multiplier on the null rate with no pool lock involved at all. That is what put
a curve slot at 0 real proposals against 6 dry and made "narrow core pool" look like the whole story.

`TryMutate` re-rolls up to `MutationRetries` (20). Same configuration before and after:

| slot | before real/dry | after real/dry |
|---|---|---|
| Engine-Sanguine Reciprocity (5-card core) | **0 / 6** | 3 / 4 |
| Control-C (no core, Control profile) | **0 / 6** | **5 / 1** |
| Engine-Spirit Bonds (508-card core) | 8 / 1 | 9 / 0 |
| Aggro-D, Midrange-E | 7 / 0, 5 / 0 | unchanged |

20 proposals to 29; dry 13 to 5. Pinned by `ANarrowPoolStillYieldsProposals_RatherThanBurningTheBudgetOnNulls`,
which measures 92/200 at one attempt against 200/200 at twenty.

**It does NOT make a narrow core searchable and must not be read that way.** A five-card pool
genuinely has few distinct legal lists — Sanguine Reciprocity still reads 4 dry — so re-rolling
stops waste, it does not invent options. The `dry` column still reports what is left.

**Runs before and after this are not comparable at a fixed seed.** A re-roll consumes more draws
from the mutation RNG, so every downstream decision shifts. That is a behaviour change, not a
regression; re-baseline rather than diffing across it.

#### Re-baselined: the search now runs, and the verdicts did not move

Same configuration re-run — 10 decks x 12 generations, DES, same engine report, Mere-Storm excluded,
seed 7.

| slot | core pool | real before | real after | rate before | rate after |
|---|---|---|---|---|---|
| Engine-Master of the Wild Hunt | 2 | **3** | **22** | 26.1% | **26.7%** |
| Engine-Sanguine Reciprocity | 5 | **2** | **26** | 32.2% | **35.0%** |
| Engine-Wirewood Herald | 24 | 7 | 18 | 59.4% | 48.3% |
| Engine-Xathrid Necromancer | 130 | 6 | 9 | 62.8% | 67.2% |
| Engine-Drogskol Captain | 54 | 33 | 36 | 46.1% | 40.0% |

**The two frozen slots got a real search and stayed non-viable** — 22 and 26 proposals, and the rates
moved +0.6pp and +2.8pp. So the caveat this file recorded against the exclusion list is **resolved
for these two**: they are genuinely weak in this field, not merely un-optimised, and excluding them
is now a supported decision rather than a guess. The general rule stands — **read the `dry` column
before excluding** — but a slot with dry near zero has had its chance.

**The fix costs time, which is the honest trade**: 12 204 games in 19.3 minutes became 16 956 in
31.2. More real proposals means more games to evaluate them, and that is what the budget always
meant to buy.

**The best deck in the field is a plain curve slot.** `Midrange-H` finished at **77.2%**, clear of
every discovered engine (next best 67.2%), on 4 real proposals — it sat above `StableRate` almost
throughout and was deliberately left alone. That is the good-stuff-beats-archetype tension this
whole mode exists to study, now measured with a search that actually runs.

#### MEASURED: (d) is answered — never proposed, and NOT because of the pool lock

Wirewood Conduit appears in **zero** rows across 12 generations, and it **is** one of the 24 cards in
the Wirewood Herald core's pool — so the pool lock is not what excludes it. The elf slot's seven
proposals touched five distinct cards: Fauna Shaman, Elvish Archdruid, Elvish Visionary, Llanowar
Visionary, Yeva's Forcemage.

So the handoff's "considered but not valued" is **wrong as stated** — it was never considered. The
cause is the two selection points that both rank by standalone value (`Complete`'s fill at seeding and
`Mutate`'s `Fill`). **Survivability is not implicated**: the card was never in a deck to die.

**Confirmed under a working search.** The mutation-budget fix took the elf slot from 7 proposals to
**18**, touching 9 distinct cards instead of 5 — and Wirewood Conduit still appears in **zero** rows
across the whole run. So this is not budget starvation; it is the selection heuristic, and it is the
same problem as items (c) and (f). A 1/1 mana dork never surfaces in a value-ranked fill however many
attempts it gets.

### Deck profiles

Every non-concept, non-wildcard slot cycles through `DeckBuilder.DeckProfile` — **Aggro** (curve
2.0–2.7), **Midrange** (2.4–3.6), **Control** (3.3–4.5) — and is named for it. `Any` is the
pre-existing behaviour (uniform 2.0–4.5) and remains the default, so callers that do not ask are
unaffected.

Three bands of one number that already existed, not a new mechanism: `curveTarget` already drives
land count via `LandsForCurve`, so an aggro deck is a low curve with fewer lands. There are no
colours, and anything richer ("play removal", "play card draw") would be the hand-labelling this
project rejects. `ProfiledSlots_ActuallyBuildToTheirCurveBand` pins both the spell curve and the
land count.

**The labels previously lied** — slots were named `Midrange-A` while drawing uniformly from
2.0–4.5, so a "midrange" slot could come out with an aggro curve and the report still called it
midrange. Profiles are cycled rather than split by a fixed count because deck count is a parameter.

**Synergy slots take no profile**, deliberately: affinity and reanimator have high printed curves
and low real ones, so `SeedConcept` uses the concept's own average cost instead.

### The best decks were ILLEGAL: MinLands was the binding constraint

`Decklist.MinLands` was 20. **Every hand-built deck that beats an evolved field runs fewer lands** —
Traditional Storm 12, Zoo 14, Affinity 14, Goblins 16 — so they were not hard to reach, they were
outside the search space. Evolved decks finish pinned at exactly 20, which is what a binding
constraint looks like.

The constant was justified as "real constructed mana bases", but two rules here break that analogy:
**no colours** (a land is quantity, never fixing) and **every opening hand contains three lands by
rule**, so 20 of 60 on top of a guaranteed three is far more than an aggressive deck wants.

It also explains why Ancestral Recall and Liliana of the Veil appear 4x in nearly every evolved
deck: inside a 20-land shell with a slow clock, card advantage genuinely IS the right plan. The
card values were correct for the deck space they were given.

`MinLands`/`MaxLands` are now overridable via `MTG_MIN_LANDS`/`MTG_MAX_LANDS` (default 20/26) so an
A/B runs both arms on one binary.

### The field has NO external reference, and it is ~15pp weak

**Measured, and it is the most important thing on this page.** Hand-built decks from
`DeckRegistry` against the evolved ALL field, 160 games each:

| Deck | vs the field |
|---|---|
| **Zoo** (plain RGW aggro) | **66.2%** |
| **Traditional Storm** | **63.1%** |
| Dragonstorm / Jund / Goblins / Reanimator / Affinity | 44-51% |

**Zoo is the control and it is the finding.** Storm alone would have said "hill climbing cannot
reach combo". Zoo — 24 Plains and 36 efficient creatures, reachable by one-card steps — beats the
field by MORE. So the field converges on something ~15pp worse than a straightforward aggro deck it
could have built at any point.

**Fitness is measured entirely inside a closed field, and a round-robin averages exactly 50% by
construction.** All eight decks converged onto 4x Steppe Lynx and 4x Liliana of the Veil (8/8 each,
Ancestral Recall 7/8) — ~10 of ~38 spells identical, held apart only by the diversity floor — and
every internal metric still reported health: 8/8 viable, 18.6pp spread. **The mode cannot tell
"my decks are good" from "my decks are equally mediocre".** The acceptance rule inherits it: a
mutant is kept for beating the frozen field, so "better" means "better against weak decks".

**Run `ArchetypeChallenge` after any evolution run.** It is the only external yardstick that
exists. A field that loses to Zoo by 16pp is not a metagame, whatever its spread says.

### ArchetypeChallenge — test the assumption before designing around it

`MtgSimulator.Tests/ArchetypeChallenge.cs`. Builds the most theme-dense legal deck a pool allows
and plays it against a **saved metagame** — the decks an evolution actually produced. One
`[TestCase]` line per hypothesis.

**Built because an assumption was wrong and nothing else would have caught it.** A run finished
with 4x Goblin Chieftain supported by only 3x Frenzied Goblin, read all session as the
"half-built deck" failure. Against that same eight-deck field:

| Deck | Win rate |
|---|---|
| Full goblins — 4x each of the 10 best goblins, 37 goblin cards | **24.4%** (39/160) |
| The half-goblin deck evolution actually built | **55.0%** |

**The AI was right.** Committing to the tribe costs more than the payoff returns — your 10th-best
goblin instead of the format's 10th-best card is a losing trade. CSC has 17 goblins, so it is not a
pool limit. The half package is the optimum.

**Treat "the AI half-built an archetype" as a HYPOTHESIS, not an observation.** Magic intuition
about critical mass does not transfer to an engine with no colours, a small pool and mostly-weak
tribe members. Run this before designing anything around a suspected archetype.

It also vindicates the rule that features only GENERATE proposals while the win rate JUDGES them:
letting deck composition into the fitness function would have driven decks toward exactly the
losing configuration.

Two traps it embeds: it anchors on the **solution file**, because stray `sim_results/` folders
exist under `bin/` and anchoring there loads an empty table where every card reads 0.00pp; and it
**reports rather than asserting a threshold**, since a pass/fail bar would encode the assumption
under test.

### Concept (synergy) deck slots

`conceptSlots` (console prompt, **default 0 = off**) seeds N slots via `DeckBuilder.SeedConcept`
instead of anchor-and-kernel: pick a mechanical demand from `PoolFeatures`, jam every card in the
pool that answers or asks it, then refine. Slots are labelled `Synergy-1..N` / `Midrange-A..` /
`Wildcard`, each synergy slot takes a **distinct** demand, and they seed first (a synergy deck is
the hardest shape to fit past the diversity floor). Concept slots get `ConceptGraceMultiplier` (3)
times the normal cull grace and re-seed on a concept when culled.

**Unproven either way — leave `conceptSlots` at 0.** Two A/B runs were done and **neither is
readable**, because the control was later run against itself and differed from its own repeat by
MORE than it differed from the treatment (distinct cards 49 vs 47, diversity 55% vs 47%, viable
8/8 vs 7/8 — all at an identical seed and byte-identical inputs).

**This mode is chaotic, and a field-level metric from a single run is an anecdote.** One different
game outcome changes which mutant is accepted, which changes the whole field. Common random
numbers protect the *parent vs mutant* decision inside a generation; nothing protects final
coverage or final diversity across runs. Any claim about a change to seeding, mutation or scoring
needs **several seeds per arm and the noise floor measured first**. That floor has never been
measured, so every single-run comparison in this document — csc1 vs csc2 included — is
provisional. See `SynergyFeaturePlan.md` §14.

The engine itself is deterministic at small scale (4 decks / 3 generations / presim 0 and presim
200 both reproduce bit-identically at a fixed seed), so this is chaos plus an unmeasured noise
floor, not a shuffling bug — with a large-scale reproduction check still outstanding.

What IS established, by unit test rather than by a run: `PickConcept` must weight demands by
**inverse** supplier count. Weighted by breadth it drew "creatures you control" (237 of 408) over
Goblins (18) — a creature pile, not an archetype. **38/60 vs 50/60** concept seeds commit to a
tribe, and `ConceptChoice_PrefersDistinctiveDemands_OverBroadOnes` fails under the old weighting.

**Judge this on distinct cards in final decks and on the decklists, never on field spread** — a
synergy slot narrows its own deck on purpose. And never on one run.

### Running it

```
printf '6\n\n3\n8\n30\n3\n6\n20\nY\n<seed>\n' | dotnet run --project MtgSimulator.Console -c Release
```

Fields in order: mode, AI depth (blank = 2), set (the index printed by `ReadSet` — **read the
menu, do not hardcode it**), decks, generations, mutants, games/matchup, final games/matchup,
seed-from-draft-model, seed. Count the prompts in the output rather than trusting that list —
mode 4's documented command was wrong for exactly this reason.

Reference cost: 8 decks x 30 generations x 3 mutants x 6 games = ~1 300 games/generation (a
little under 1 344, because some mutation proposals return null and are not scheduled).

**Measured at ~1 000 games/minute on the default configuration**, i.e. ~1.3 minutes per
generation and **~40 minutes for a 30-generation run**. Constructed games are much faster than
drafted ones — the reference rate elsewhere in this document is 5.6 games/sec for draft training,
and these are ~17/sec, because 60-card decks with a real curve end sooner than 40-card limited
decks and nothing here pays for the draft itself.

Both output files are relative to the **shell's** working directory — run from the repo root, and
verify `bin/` timestamps before trusting a run. Same two traps as draft training.

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

## Interactive Debug Snapshots

`DebugSnapshotBuilder.BuildJson(history, aiDecisions, error)` is the Godot-side counterpart to
`FlaggedGameSaver` — it serialises the whole `MtgGameManager` history plus every AI decision.
`DebugSnapshot.Error` carries crash context; null means a manual export.

`MtgGameScene` writes these to `user://debug_snapshots/`:

| Prefix | Trigger |
|---|---|
| `debug_` | F5, manual |
| `crash_` | An exception in the AI turn loop, or the process-wide unhandled/unobserved handlers |
| `hang_` | The AI turn exceeded `MaxAiStepsPerTurn` without ending |

The AI turn loop is `async void`; an exception escaping it kills the process with nothing logged,
which is why it is wrapped and why `MtgGameManager.ForceEndAiTurn()` exists — it drains any
pending choice (an unresolved `ChoiceAction` blocks the action stack forever) and hands the turn
back rather than leaving the game wedged.

## Strength harness

`MtgSimulator.Tests/StrengthHarness.cs` plays two AI configurations against each other over real
drafted decks and returns a win rate with its standard error. **Every strength claim either comes
from here or is a guess.**

`EvaluatorStrengthTests` drives it, `[Explicit]` because each run plays hundreds of games.
**Run the two self-checks before trusting any number from it:**

| Self-check | Measured | Catches |
|---|---|---|
| `DefaultAgainstItself_IsEven` | 51.3% ± 3.3pp | harness bias — a skew here fakes an effect everywhere |
| `HarnessCanSeeADifference` (branching 1) | 38.8% ± 3.3pp | a harness that measures nothing |

The second matters more than it looks. **A 50% result is indistinguishable from "no effect", which
is the answer most of these tests are hoping for** — so the harness has to prove it can see 38.8%
first. This project has already shipped a comparison that silently measured nothing: raising
`maxBranching` while `expandBranching` stayed capped, which returned a suspiciously exact result.

`Drafts = 40` (1120 games) resolves a **3.0pp** effect at two standard errors. The 8 drafts the
original used gives 224 games and 6.7pp — enough for "did we break it", not "is it better".

Three fixes over `BranchingCapStrengthTests`, which pioneered the shape:
- **Each arm gets its own RNG.** The original shares one `Random` between both strategies, so they
  consume each other's draws — a confound, and why it could not be parallelised.
- **Games run in parallel** into a pre-allocated array, folded sequentially.
- **Arms interleave across the schedule** rather than running in blocks, so neither systematically
  gets a busier machine. From the SCGAI organisers; this project has already lost a measurement to
  exactly that.

**To make a change measurable, give it a constructor parameter that turns it off**
(`terminalDiscount: 1.0f`, `preferFastestWin: false`). Rebuilding an old commit to get an opponent
is not a harness. Production paths always take the defaults.

### Measured

| Change | Result | Verdict |
|---|---|---|
| Terminal discount vs none | 50.2% ± 1.5pp, 1120 games | neutral — kept |
| Fastest-win vs first-win | 50.1% ± 1.5pp, 1120 games | neutral — kept |
| Toughness weight 0.5 | 50.2% ± 1.5pp, 1120 games | neutral — **not shipped** |
| Toughness weight 1.33 | 51.0% ± 1.5pp, 1120 games | neutral — **not shipped** |
| Resolving choices before scoring | 50.2% ± 1.5pp, 1120 games | neutral — kept, see below |
| Keyword term (2/15) vs none | 50.9% ± 1.5pp, 1120 games | neutral — kept |
| Keyword term wall-clock cost | +0.3% time, +1.6% actions/game | free |
| Weights const → init-only property | −0.6% wall time, actions identical | free |

The self-check numbers above were taken **before** the keyword term shipped, so `Default()` is no
longer the AI they were measured on. Re-measured with keywords live, `DefaultAgainstItself_IsEven`
reads **52.7%** — still even within 2 SE, but do not read a 1.4pp move between those two figures as
a finding. **A self-check number is only comparable to one taken against the same `Default`.**

#### The keyword cost run is a worked example of lesson 1

Measured AB — keywords on first, off second — it read **+10.7% time against +1.6% actions**, and
the obvious reading is that the ~9pp gap is the `GetEffectiveStats` allocation, since actions
barely moved. That reading was **wrong**, and it was argued in this project before the check that
disproved it had been run.

Re-run ABBA (on, off, off, on) the two configurations land on **180s each**. The raw sequence was
83 / 89 / 91 / 97 — rising monotonically across the run *regardless of configuration*. It was
machine drift, and the AB ordering handed all of it to whichever arm ran second.

**`AvgActions` is what makes this legible, and it is now on `Outcome` for that reason.** Actions
were 63.0 vs 62.0 in every one of the four runs — deterministic and repeatable — so the workload
difference was real, tiny, and entirely separate from the clock. The rule the project already had
("read actions/game beside the clock") is necessary but not sufficient: when actions and time
disagree, the honest conclusion is *unexplained*, not *therefore the thing I just changed*.

**Mirror-match a configuration against itself rather than rebuilding an old commit.** Both arms
identical within a run makes the run time purely that configuration's cost, and `Keywords(0f)` is
behaviourally identical to the commit before the term existed — the term is appended last in
`Explain`'s sum, so at zero every other term adds to exactly the float it did before, and
`ScanBattlefield` skips the allocation. Same binary, so a stale `bin/` cannot measure code that
was never compiled in.

### A head-to-head is the wrong instrument for a per-card bug

The half-applied-action fix (see `DesignNotes.md`) measured **50.2% ± 1.5pp** — no aggregate
change — despite fixing a case where the search was scoring positions that did not exist, and
despite the engine needing a per-turn equip cap to contain the resulting loop.

That is not evidence the fix is worthless. It is evidence the instrument is wrong for it. The bug
only fired on actions that raise a **choice**, so it mishandled a specific subset of cards. Both
arms draft from the same pool, so a defect concentrated in a few cards is diluted across 1120
games into nothing.

**The right acceptance test for a per-card defect is a retrain**, checking whether the affected
cards move in the model — the same test the resource-model work needs. Cards with end-of-turn
triggers, discard costs and upkeep costs are the population to watch: `Call to the Grave` (−6.21),
`Dark Tutelage` (−5.52) and `Avaricious Dragon` all raise choices or recurring costs, and all sit
near the bottom.

**Before running a strength harness, ask what population the change affects.** If it is a subset of
cards rather than every decision, a win rate over mixed decks will report 50% whatever happens.

### Four evaluator changes measured, four neutral — read the pattern

Nothing tried so far moves the win rate by 3pp. The most likely reason is structural, and it
should temper expectations for any further evaluator work: **this engine already evaluates by
playout.** `ScoreAfterCompletingTurn` finishes the turn and rolls two more, so the rollout
*observes* much of what a static term would approximate — the toughness term tells the evaluator a
5/5 survives better than a 5/1, and the rollout was already finding that out by playing it.

Churchill (AIIDE 2012) measured the same ordering directly: a playout-based leaf evaluation scored
**0.92** against **0.80** for the best static evaluation function they tried. Static terms have
less headroom once playouts are in place.

**The implication for the plan is to prefer rollout work over evaluator work.** `PlayGreedyTurn`
plays exactly ONE spell per simulated turn while `SimulateOpponentTurn`'s BoardOnly mode loops
every attack — so across a whole 2-turn lookahead the model gives us two spells and gives the
opponent every attack, every turn. Fixing that asymmetry changes what the rollout *sees*, which is
the thing carrying the strength.

Toughness is kept at weight 0 rather than shipped at 1.33. The gap it addresses is real — a 5/1
and a 5/5 are otherwise the same creature to the evaluator, including as removal targets — but a
term that cannot be measured to help is complexity with a maintenance cost and no evidence. The
knob stays so it costs nothing to revisit, and it should be revisited if blocking ever lands, since
that changes what toughness is worth.

**Both are kept despite being neutral, and that is not a contradiction.** Each fixes a defect that
is demonstrable at the function level — the discount gives losing positions a gradient instead of
collapsing every line to one number, and fastest-win stopped the AI bolting its own face when two
immediate wins were available. Neither situation is common enough to move a win rate over 1120
games. **Correctness that does not show up in a win rate is still correctness**; what would justify
reverting is a clear loss, not the absence of a gain.

Read "neutral" as "smaller than ~3pp", not "exactly zero".

## CardValueSandbox — value conditioned on castability

`CardValueSandbox.Measure(cards)` casts each card into a fixed position and rolls the game forward
with the same `ScoreAfterCompletingTurn` the live search uses, minus a control rollout of the same
fixture without the card. `CardValueSweep` drives it (`[Explicit]`, ~8s for CSC's 408 cards) and
writes `sim_results/card_values_<code>.json`.

**This is a different axis from the trained draft model and they are EXPECTED to disagree.**
`DraftTrainingData` measures how much a card raises a 40-card deck's win rate, which is dominated
by castability — a 2/2 for 1 rates well partly *because* it is always castable. That is one scalar
per card and cannot answer "at eight lands, tutor the 2/2 or the 6/6 trample". The sandbox holds
castability constant and measures the other half. **Draft picking keeps using the win rates.**

Do not record card values here — run the sweep and read the file:

```
python -c "
import json; d=json.load(open('sim_results/card_values_csc.json'))
ok=[c for c in d if c['NotMeasured'] is None]
v=sorted(ok,key=lambda c:-c['Value']); print(v[:10]); print(v[-10:])"
```

Three things the first run established, each found by reading the table rather than by reasoning:

- **A rollout that reaches a win returns a discounted terminal (~9000 against board scores of ~80),
  and one such card sets the scale for the entire table.** Sublime Archangel did this at 8971.70,
  two orders of magnitude clear of second. Excluded via `IsDecisive` and reported by name — winning
  inside the lookahead says the FIXTURE is decided, not that the card is worth 9000.
- **The generator emits one cast action per TARGET, so taking any single one picks a target
  arbitrarily** — and `PlayersOrCreatures` includes the caster's own face. Every burn spell in the
  cube measured as a blank (Lightning Bolt at −1.66, exactly the cost of the card leaving hand).
  All targets are now rolled out and the best kept; Bolt reads +6.30, Murder +18.00.
- **`PassTurn` is not a neutral baseline — it is a game where the opponent has conceded**, so every
  defensive, symmetric and reactive card prices negative in it. Day of Judgment reads −35.10 there
  and +15.40 under `BoardOnly`. See the open question below.

**The two arms are `PassTurn` and `BoardOnly` off the existing `OpponentSimulationMode` enum**, so
the fragility probe needed no engine change. Their gap sorts cards by what board they need: a large
positive gap means the value assumes a quiet board (Primordial Hydra +54.23, a 0/0 that grows),
a large negative gap means the card is worthless without pressure to answer (Day of Judgment
−50.50).

**A card reading exactly 0.00 in BOTH arms is an inert-card candidate** — this is the standing
version of the "inert cards throw no errors" audit, and the first run surfaced six.

**Open: which arm is the headline.** Currently `Value` is the `PassTurn` arm, which the evidence
above says is wrong for a whole family of cards. `BoardOnly` produces sensible numbers across the
set and is the search's own production opponent model. Both are in the JSON, so this is a one-line
change.

**Known limitation: the board is symmetric, so a symmetric effect prices at ~zero.** Same
limitation `EndTurnCostSweep` carries. An asymmetric fixture is the instrument for that family and
it would move every other card's number too.

## AI Inspection Tooling

Three pieces over one data model. The point of all of them is the **term breakdown**, not the
score: "this action scores 4.52" is not a diagnosis, but `creatures +3.00, power +4.00,
race −3.20, hand −1.40, lands-in-hand +0.00` is — the land-pricing defect is visible on sight in
the second form and invisible in the first.

**`StateEvaluator.Explain` is the implementation and `Evaluate` is a one-line wrapper over its
`Total`.** That direction is deliberate. A second copy of the weighted sum written for display
would drift from the one the search uses, and a panel showing terms that do not sum to the real
score sends you hunting a discrepancy that exists only in the renderer. `EvaluationBreakdownTests`
pins them together. It returns a `readonly record struct`, so the hottest call in the engine
allocates nothing.

**Measured at ~0% over 300 games**, three interleaved rounds (57.75s vs 58.78s, the gap inside
first-run JIT warmup). Three earlier measurements said +11.8%, +8.0% and +8.8% and were all
measuring the win-detection bug above, not this. Do not re-split them on an unmeasured hunch.

**Summation order in `Explain` must not change.** Float addition is not associative and
`DeterminismTests` / `MachineIndependenceTests` compare exact results.

| Piece | Where | Trigger |
|---|---|---|
| Overlay | `SQGodotCommon/MtgGame/Board/AiInspectorPanel.cs` | **F6** in game |
| Scenario capture | `MtgGameScene.SaveScenario` | **F7** in game |
| Standalone viewer | `Scenarios/ScenarioConsole.cs` | console **mode 5** |

`AiDecision.StateBefore` and `AiActionCandidate.Breakdown` are populated only under
`_captureDecisions` — all three `SetLastDecision` call sites are already guarded — so the simulator
and trainer pay nothing for them.

**`AiActionCandidate.Score` is a ROLLOUT score, not the breakdown's total.** It is the evaluation
of a state two turns ahead; the breakdown is the immediate position. They will not agree, and the
gap between them is exactly what the lookahead contributed, which is why both are shown.

### Scenarios are serialized state, not snapshots

`GameStateSnapshot` renders a position for a human and **cannot be loaded back**. `StateJson` is
the other thing: a real `GameState` round-trip, so a saved position can be handed to
`SelectAction`.

Polymorphic types get a `$type` discriminator resolved by **reflection over every abstract type in
the game assemblies**, not `[JsonDerivedType]` attributes. The state holds 133 polymorphic types
across five hierarchies and the set grows with every new card mechanic; annotating each one means a
forgotten attribute silently breaks scenario loading. The first draft named four bases by hand and
the round-trip test immediately found a fifth — `TargetSpecification`, nested two levels inside a
card's effects.

Three things that would otherwise be silent bugs, each pinned by `StateJsonTests`:
- **`GameObject.Children` is dropped.** It is documented as view-only, `ParentToChildren` is the
  source of truth, and `LoadFrom` populates it recursively — so a hydrated state would serialize
  the object graph exponentially.
- **`ImmutableStack` enumerates top-first**, so a converter that pushes in read order inverts the
  action stack.
- **Metadata values carry their own type tag.** `GetMeta<T>` casts, so an `int` returning as a
  boxed `JsonElement` throws at some unrelated call far from the load.

Round-trip is asserted on **behaviour** — same evaluator score, same legal actions, same AI
decision. A state differing in a field the AI never reads is fine; one that scores differently is
a broken scenario.

**Godot writes `user://scenarios/` and the console reads `scenarios/` relative to the shell's cwd.**
They do not meet on their own — same trap as the draft model asset. F7's toast prints the absolute
path so the copy is one command.

`ScenarioConsole.Strategies` is the single place strategies are named. "Construct these AIs by name
and run them" is the same requirement for a one-position diff and for a head-to-head strength
harness; keep it one list.

## Known Issues / Tech Debt

- **`IAiStrategy` is in the `MtgCore` namespace** despite its file living in `MtgSimulator/`. Should be moved to the `MtgSimulator` namespace for correctness.
- **EventTriggerCondition migration** — see CardPool section above.
- **`BeamSearchAiStrategy` does not populate the breakdown.** Only `MultiTurnBeamSearchAiStrategy`
  fills `StateBefore` / `Breakdown`, so the inspector shows bare scores when the plain beam is
  selected in the scenario viewer. Deliberate — MultiTurn is the default — but wire it once the
  display format has settled.
- **Neither the F6 overlay nor the F7 toast has been visually verified.** Both compile and the
  suite is green, but the layout is unreviewed.

## Console Entry Point

`MtgSimulator.Console/` is the runnable project — it contains only `Program.cs` and references this library. Run that project to launch the simulator interactively. Six modes: 1 = random card pool, 2 = preconstructed decks, 3 = draft, 4 = train draft pickers, 5 = inspect a saved scenario, 6 = evolve a constructed metagame. `ReadSeed()` is shared by modes 1, 3, 4 and 6 (blank = random, number = literal, word = FNV-1a hashed). `ReadSet()` is shared by modes 3, 4 and 6 and skips its prompt entirely while only one choice exists; **mode 6 alone passes `includeCombined: true`**, which adds the merged all-sets pool. Mode 5 returns before the AI-depth prompt; mode 6 defaults that depth to 2 rather than 3, since it plays far more games and only needs both sides equally strong. Mode 3 auto-loads the selected set's model via `DraftTrainingStore.PathFor` if present and adds the `Trained` picker to the comparison. `ServerGarbageCollection` is enabled here — see the Draft Training section for why. `sim_results/` and `flagged_games/` output folders are written relative to the console app's working directory.

## Key Rules

- Never duplicate legal action generation — always call `MtgActionGenerator.GetLegalActions`. Do not reimplement this in simulator code.
- Never call `BeginGame` from setup code — `GameRunner.Run` is responsible for calling it. Pass a pre-begin `GameState` to `Run`; it calls `BeginGame` internally and captures all resulting events.
- `SimulatorRunner.SetupGame()` uses `MtgGameFactory.Create()` (real mana — players start at 0/0), not `CreateForTesting()`. The simulator tests real land-based mana constraints.
- All state mutation goes through `GameAction`s — no direct state modification in the simulator.
