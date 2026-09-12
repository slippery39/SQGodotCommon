---
paths:
  - "MtgSimulator/*Strategy*.cs"
  - "MtgSimulator/*Evaluator*.cs"
  - "MtgSimulator/Evaluation*.cs"
  - "MtgSimulator/AiCardValues.cs"
  - "MtgSimulator/AiDecision.cs"
  - "MtgSimulator/CardValue*.cs"
  - "MtgSimulator/GameRunner.cs"
  - "MtgSimulator/DrawDiagnostics.cs"
  - "MtgSimulator.Tests/*Strength*.cs"
  - "MtgSimulator.Tests/*Beam*.cs"
  - "MtgSimulator.Tests/*Branching*.cs"
  - "MtgSimulator.Tests/*Choice*.cs"
  - "MtgSimulator.Tests/*Evaluator*.cs"
  - "MtgSimulator.Tests/*Lethal*.cs"
---

# MtgSimulator — AI search, evaluation and card values

Loaded when you touch a strategy, an evaluator, or the card-value tables. Measured strength
results live in `docs/findings/ai-strength.md` — read them before proposing an evaluator change.

## The clock was removed from ENDING a game and left in DISCARDING one

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

## The branching cap

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

## A planned action must never be re-found as a different one

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

## Terminal rewards decay with how long they took

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

## The pre-search LETHAL check — and why FindWinner could not cover it

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

## Never compare a rollout score against WinScore

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

## `PruneBeam`'s `seen` set is not transposition detection

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

## Race pressure: board power only matters relative to the life it threatens

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

## Lands in hand are not counted by StateEvaluator

`CardsInHandWeight` counts only NON-land cards. Hand size is a proxy for options, and a land held
is not an option — it is a resource you have failed to deploy.

Counting it made a land drop worth `+2.0` mana minus `1.4` for the card leaving hand: a net
`+0.6`, small enough that the beam would sometimes prefer another line and **skip the land drop
entirely for a turn**, which QA saw as the AI stumbling on its early curve. Skipping an early land
drop is close to the worst play available, so the margin has to be decisive rather than marginal.

Pinned by `MtgSimulator.Tests/AiLandDropTests.cs`, which scores the evaluator directly rather than
racing a time-budgeted beam search.

## Card values in ResolveChoice

`CardValueTable` adds the value of the RESULTING HAND to each option's rollout score, inside
`MultiTurnBeamSearchAiStrategy.ResolveChoice` and nowhere else. Discard, scry, tutor, impulse.
Null table = off, which is the production default today.

**Score the resulting hand, never the option's card.** Direction then falls out of the state:
discarding a bomb leaves a worse hand and scores lower, tutoring one leaves a better hand and
scores higher, with nothing having to know which kind of choice it is looking at.

`ReachDiscount` (0.75/turn) decays a card you cannot cast yet. At two mana an eight-drop is worth
`0.75^6` ≈ 18% of its cast value, which is what makes the AI pitch it and keep a playable two-drop
— pinned by `AnUncastableBombLosesToAPlayableCard_OnTurnOne`.

## Why this is not an evaluator term, measured the hard way

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

## What it fixes, measured

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

## Measured: 18/18 on positions with an obvious answer, and it is ON

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

## A land in hand is worth what it UNLOCKS

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

## The retrain was run as a controlled A/B, and card values do NOT move card valuations

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

## A head-to-head is the wrong instrument for a per-card bug

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

## Four evaluator changes measured, four neutral — read the pattern

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
