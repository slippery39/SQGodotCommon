# MtgSimulator

Class library containing all AI strategies, game runners, deck factories, and reporting for MTG simulation. Referenced by Godot and by `MtgSimulator.Console` (the runnable console entry point). Keeping it a library prevents file-locking conflicts when the console app and Godot are running simultaneously.

## Source Map

| File | Purpose |
|------|---------|
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
| Safety timeout | 300 000 ms wall-clock | **no** | `GameEndReason.TimeLimitReached` — excluded from training, not a draw |
| Unhandled exception | any thrown exception | — | `GameEndReason.UnhandledException` — excluded from training, exception captured |

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
82-card Legacy set has 3 321 pairs (450 KB), the 300-card Hollowmere set has 44 850 (6.2 MB).
Since `synergyWeight` defaults to 0 the pairs are never read at pick time — verified by running
the same evaluation against the full and pairs-free files and getting seat-for-seat identical
win rates. `sim_results/` is gitignored, so keep the full file there for any future synergy
experiment and commit only the stripped copy to `MtgGame/Assets/` (6.2 MB → 63 KB).

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

**Both are kept despite being neutral, and that is not a contradiction.** Each fixes a defect that
is demonstrable at the function level — the discount gives losing positions a gradient instead of
collapsing every line to one number, and fastest-win stopped the AI bolting its own face when two
immediate wins were available. Neither situation is common enough to move a win rate over 1120
games. **Correctness that does not show up in a win rate is still correctness**; what would justify
reverting is a clear loss, not the absence of a gain.

Read "neutral" as "smaller than ~3pp", not "exactly zero".

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

`MtgSimulator.Console/` is the runnable project — it contains only `Program.cs` and references this library. Run that project to launch the simulator interactively. Four modes: 1 = random card pool, 2 = preconstructed decks, 3 = draft, 4 = train draft pickers. `ReadSeed()` is shared by modes 1, 3 and 4 (blank = random, number = literal, word = FNV-1a hashed). `ReadSet()` is shared by modes 3 and 4 and skips its prompt entirely while only one set is registered. Mode 3 auto-loads the selected set's model via `DraftTrainingStore.PathFor` if present and adds the `Trained` picker to the comparison. `ServerGarbageCollection` is enabled here — see the Draft Training section for why. `sim_results/` and `flagged_games/` output folders are written relative to the console app's working directory.

## Key Rules

- Never duplicate legal action generation — always call `MtgActionGenerator.GetLegalActions`. Do not reimplement this in simulator code.
- Never call `BeginGame` from setup code — `GameRunner.Run` is responsible for calling it. Pass a pre-begin `GameState` to `Run`; it calls `BeginGame` internally and captures all resulting events.
- `SimulatorRunner.SetupGame()` uses `MtgGameFactory.Create()` (real mana — players start at 0/0), not `CreateForTesting()`. The simulator tests real land-based mana constraints.
- All state mutation goes through `GameAction`s — no direct state modification in the simulator.
