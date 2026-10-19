# MagicCore AI — measured results

Numbers from runs. The rules that come from them live in `MagicCore/CLAUDE.md`.

## GreedyAi vs random play (2026-10-07, Reality Fracture, 25 seeds per row)

| AI deck | random deck | before | life uncapped | + lands tapered, no floating-mana credit |
|---|---|---|---|---|
| White-Black | Red-Green | 25/25 | 25/25 | 25/25 |
| White-Black | White-Blue Jace | — | 25/25 | 25/25 |
| Red-Green | White-Black | 19/25 | 21/25 | 22/25 |
| Red-Green | White-Blue Jace | 25/25 | 25/25 | 25/25 |
| White-Blue Jace | White-Black | — | 24/25 | 22/25 |
| White-Blue Jace | Red-Green | 25/25 | 25/25 | 24/25 |

**Read it as noise past the first fix.** At 25 games a row moves ±2 by chance; 145/150 → 143/150
overall is not a change. Against RANDOM play these are correctness fixes, each pinned by a unit
test that fails without it — not strength gains. Strength needs an opponent better than random.

### The life cap (fixed)

Red-Green lost to RANDOM White-Black because the evaluator capped life at 20. Screeching
Soulbreakers' drains pushed the opponent to 26–31, so damage scored zero: in seed 8, attacking with
every Red-Green creature scored exactly the same as not attacking (−4.8 vs −4.8 on turn 21) while all
three Soulbreakers were TAPPED from attacking — an open board. Ties go to "don't attack", so the AI
never raced. Tapping was verified correct (`CombatTests.AnAttacker_StaysTapped…`). Pinned by
`GreedyAiTests.AttacksPastTappedBlockers_EvenWhenTheOpponentIsAboveTwenty`.

**Rule from it: an evaluation term must be monotonic.** A cap turns "more of a good thing" into a
tie, and a tie silently picks the passive move.

### The same bug twice more (fixed)

The review of the life fix found two more non-monotonic terms in the same evaluator, both misread
in the first draft of this file as "one-ply habits":
- **Lands capped at 7** — an 8th land scored less in play than in hand, so the AI hoarded lands
  (the "ends games holding 3–4 lands" habit). Pinned by `GreedyAiTests.PlaysASurplusLand`.
- **Floating mana scored +0.1** — but it empties at the end of the step, which the simulation never
  reaches, so "Add {R}" with nothing to cast looked like a gain.

### What is left (not fixed — one-ply ceilings)

The four remaining Red-Green losses (seeds 1, 3, 10, 14) are long attrition games: White-Black's
fliers and Thalia's lifelink against a deck with few answers to fliers. The AI does attack now.
What remains after the fixes, each needing lookahead rather than a weight: it plays a tapped dual and
an untapped basic as equals (no "do I need the mana this turn"), and it cannot plan around fliers it
has no answer to.

`GreedyAiTests.BeatsRandomPlay_InNearlyEveryGame` asserts 90% overall AND at least 6 of 10 in every
pairing, so a collapse in one matchup cannot hide in the average.

## The strength harness — baselines (2026-10-16, AI-2.3)

`AiMatch` (`dotnet run --project MagicConsole -- --match=A,B --games=N [--sequential]`) plays every
ordered FRA deck pairing with the AIs swapping seats: same decks, seats, seed and coin flip, only the
AI differs.

| Match, 72 games | Result (1 SE over mirror pairs) | Time per decision, `--sequential` |
|---|---|---|
| greedy vs random | **71–1**, 98.6% ± 1.4pp | greedy 1.9 ms, random 0.3 ms |
| greedy vs greedy | **36–36** exactly, ± 0.0pp | 2.2 / 2.2 ms |

- **The two self-checks hold.** An AI against itself scores EXACTLY half with zero error, because
  GreedyAi is deterministic and a mirror pair is the same game with the names swapped —
  `AiMatchTests` asserts it in the normal suite, and breaking the seat swap fails it (revert-checked).
  Greedy beating random proves the harness can see a difference at all —
  `GreedyAiTests.BeatsRandomPlay_InNearlyEveryGame` runs through the harness for that.
- **The standard error is over mirror PAIRS, not games.** The two games of a pair share decks, seed
  and coin flip, so they are not independent; a per-game binomial claimed ±5.9pp for an AI against
  itself, whose true spread is zero.
- **Parallel games inflate time per decision 7–8x, unevenly** (greedy 13.1 ms parallel vs 1.9 ms
  sequential). Only `--sequential` timings are evidence.
- **The White-Blue Jace deck goes 2–22 in GreedyAi's hands; White-Black 21–3, Red-Green 13–11**
  (greedy vs greedy, by deck). Either the deck is weak or one-ply play cannot pilot it (planeswalker
  loyalty, Empower Jace). A baseline to compare the search AI's per-deck record against.
- GreedyAi averages ~55 decisions per player per game (AutoPass skips the forced passes) over ~21 turns.

## What one rollout costs (2026-10-16, AI-2.4)

`Rollout.Score` finishes the current turn and plays two more (our cheap greedy policy, the opponent
`BoardOnly`), then evaluates. Measured at every GreedyAi decision of six real games, one per ordered
deck pairing (`RolloutTests.Cost_OfOneRollout`, `[Explicit]`), single-threaded:

| Build | Rollouts | mean | p50 | p90 | p99 | max |
|---|---|---|---|---|---|---|
| Debug | 384 | 24.2 ms | 20.8 | 43.9 | 73.2 | 124.5 |
| Release | 384 | 22.1 ms | 15.8 | 42.5 | 87.5 | 104.4 |
| Release, after AI-2.4's review fixes (blocks judged like attacks; spent plays skipped) | 384 | 21.5 ms | 16.2 | 43.1 | 89.0 | 137.1 |

**So a 1-second move buys ~45 rollouts on one thread** — MtgSimulator's default budget is 400 (on
an engine with no stack or priority). Release barely helps: the cost is work, not debug overhead.

Where it goes, per rollout (a temporary probe, Release, not committed):

| Part | Count | Time |
|---|---|---|
| choices answered — 4.2 of them attack declarations, each option settled through combat | 5.8 | **9.8 ms** |
| our policy's checks (most return at once: not our sorcery-speed window) | 27.8 | 7.7 ms |
| priority passes and plays — the engine stepping through the turn | 53.3 | 4.8 ms |

**Attack declarations are the first lever** if rollouts must get cheaper: ~45% of the time for ~4
decisions. The passes are the engine's own floor (~0.09 ms each).

## The search AI against GreedyAi (2026-10-16, AI-2.5)

`BeamSearchAi` at its defaults (depth 3, beam 4, branching 16 / 5, budget 200 rollouts, plans
committed), Release, `--match=beam,greedy --games=72 --sequential` — the search before held values;
since AI-1.9 `beam` holds cards by value, so reproduce this with `--match=beam-plain,greedy` (games one at a time; each
decision's rollouts still run in parallel across 16 cores):

| Build | Result (1 SE over mirror pairs) | Time per decision: mean / slowest | By the search's deck (W-L) |
|---|---|---|---|
| first cut | **55–17, 76.4% ± 4.2pp** | 206 ms / 4 695 ms | White-Black 22–2, Red-Green 18–6, Jace 15–9 |
| after /pre-commit's fixes | **54–18, 75.0% ± 4.2pp** | **177 ms / 3 426 ms** | White-Black 22–2, Red-Green 18–6, Jace 14–10 |

- **The search is clearly stronger** — six standard errors above even. The fixes (plans kept in step
  with the real game, plans dropped when an opponent's spell is on the stack, one ply per priority
  window on their turn) left strength the same within noise and cut time by 14%.
- **The Jace deck wins 14–10 in the search's hands** against GreedyAi, where GreedyAi piloting it
  against itself went 2–22: one-ply play was the problem, not (only) the deck.
- **The mean fits a 1-second move; the slowest does not** (3.4 s). The rollout budget is checked
  between levels, so one wide level can overrun it. On the table this needs a wall-clock cap (AI-2.7);
  sim runs stay on the deterministic budget.
- The search makes ~65 decisions a game to GreedyAi's ~52: it reaches more decisions (it keeps more
  creatures and spells in play), not more forced ones — AutoPass skips those for both.

## The card-value sandbox: what to measure (2026-10-16, AI-1.6)

`CardValues.Measure` casts each card into a fixed board (a 1/1–5/5 vanilla ladder on each side) and
compares with the same board without it. MtgSimulator's sandbox rolls **four turns** forward; on
MagicCore that measured combat, not the card. Probed over all 284 playable FRA cards (the rollouts
took ~19 s; the final measurement takes ~1 s):

| Measured at | Exactly 0 | Negative | Rampart Hunter (3/3 deathtouch) | Artifist Acumen (first strike, a card) |
|---|---|---|---|---|
| rollout, 4 turns | 9 | 2 | **0.0** | −19.1 |
| rollout, 2 / 1 / 0 turns | 10 / 12 / 8 | 2 / 3 / 6 | 0.0 / 0.0 / −1.7 | −14.1 / −14.1 / −11.6 |
| **the cast's turn, no attacks** | 13 (all honest) | **0** | **+10.3** | **+0.3** |

- **Why the rollouts fail here:** with both ladders, the rollout's combat (each attacker judged
  alone; a trigger's target the first legal one) trades the boards away, and the extra card goes with
  them — Rampart Hunter measured EXACTLY the control at every horizon. MtgCore has no blocking, so
  MtgSimulator never met this.
- **Through the turn, not just the resolution:** measured the instant the spell resolved, Face
  Yourself (copies sacrificed at the end step) read 47.5, the top of the set; through the end step it
  reads 0. `CardValuesTests.ACreatureSacrificedAtTheEndStep_IsWorthNothing` pins it (revert-checked).
- **Per-card lands (MtgSimulator's rule):** a basic per coloured pip plus generic plus 3. A flat
  fifteen lands left {G}{G}{G}{G} uncastable and let X run to ~13 (Guiding Hydra 38.5, top 2).
- The honest zeros: pumps until end of turn (Perfected Theory, Blazing Crescendo), cards with nothing
  to work on (Germinate Recruits with no life gained, Loyal Tutor with no planeswalker), Frostbite
  Pyromental (its value is the hasty attack, not measured). **So a held card must keep at least the
  flat hand value** — at 0 the search, whose ties go to acting, would throw zero-valued tricks away.
- **Our own choices must be tried, not declined** (the /pre-commit review): the cheap answer takes the
  fewest picks, so every "you may" and "up to N" was refused, and a trigger's target was the first
  legal one — Ajani's Anguish could burn our own creature. GreedyAi now answers ours (attacks
  excepted); Ajani's Anguish reads 15.5. A cap of 16 casts cut the BEST lines of two-target spells
  (the generator lists our small creatures first) — Compel Brutality and Vigorbloom Charm (14.5, 17.0)
  now get every line. Landcycling no longer counts as a cast.
- Sample (final): Craftwork Crusher 37.3, Emrakul 33.6, Ghalta 26.8, Vraska, the Cutting Glare 25.3,
  **Prophesied End 14.2**, Rampart Hunter 10.3, Last Gasp 9.5, **Rank Rat 4.5**; median 8.0; no
  negatives; twelve honest zeros.

## Held cards at a fraction of their measured value (2026-10-16, AI-1.9)

The search with held cards priced max(flat 1.2, fraction × measured value) — the opponent's hand
blind — against the same search with the flat price. 72 games each, Release:

| Fraction | Result vs the plain search (1 SE over pairs) | Lands played / game (held, plain) | Spells cast / game |
|---|---|---|---|
| 0.25 | 38–34, 52.8% ± 5.2pp | 7.2, 7.4 | 10.4, 10.4 |
| **0.5** | **38–34, 52.8% ± 4.0pp** | 7.4, 7.7 | 10.9, 10.9 |
| 1.0 | **26–46, 36.1% ± 4.7pp** | 7.2, 7.4 | **10.1**, 10.8 |

- **0.25 and 0.5 are neutral within noise; 1.0 loses** by about three standard errors — it hoards:
  fewer spells cast, more decisions. `AiPositionTests.ABigThreat_TheSearch_SpendsItsRemoval` is the
  hoarding check (at 2.0 it fails).
- **Land drops held.** MtgSimulator's hand term made land drops score negative and the AI stopped
  playing lands (24.6%, actions 63 → 50); here the gap is ~0.3 lands a game at every fraction —
  the term is constant per card and never moves with mana.
  (These counts credited only the deciding player; a spell cast during the other's decision — on
  resolution — was missed, for both sides alike. The harness counts both seats since.)
- **Time, `--sequential`, fraction 0.5:** 249 ms per decision against 224 ms (+11%), slowest 5.8 s
  against 4.4 s; 73 decisions a game against 70. The plan's divergence threshold (5.0) was tuned for
  flat prices; held values move the score more per card, so plans drop a little more often.
- **Never compare timings from parallel search-vs-search runs:** each game's search parallelises its
  own rollouts too, so the cores are oversubscribed — 3–4.6 s mean, minutes at the slowest. The
  win/loss columns are identical either way (deterministic); only time needs `--sequential`.
- **Decision: 0.5 is the search's default** (AI-1's plan: default if not worse — it isn't, and it fixes
  a bad play reported from a real game). `--match=beam-plain` is the search before it.

## The search on the table: a 1-second cap (2026-10-16, AI-2.7)

`BeamSearchAi.Settings.Interactive` (a 1 s `TimeLimit`), the search against itself over every
ordered FRA pairing, seeds 1–2 (24 games), one game at a time; per decision. "Deepest level" counts
the searches (plan replays excluded) by the last level that expanded anything — root / 2 / 3:

| Cap | Build | Decisions | Mean | p95 | p99 | Slowest | Over 1.2 s / 1.5 s | Deepest level |
|---|---|---|---|---|---|---|---|---|
| none | Debug | 2 138 | 246 ms | 1.27 s | 2.37 s | 4.61 s | 122 / 70 | |
| elapsed checked between levels | Debug | 1 899 | 322 ms | 1.49 s | 2.09 s | 3.33 s | 168 / 91 | |
| a level started only if all its batches fit | Release | 2 048 | 201 ms | 0.75 s | 1.02 s | 2.24 s | 8 / 3 | |
| none | Release | 2 138 | 325 ms | 1.68 s | 2.74 s | 5.24 s | 195 / 131 | 531 / 126 / 469 |
| **each line expanded only if one batch fits** | Release | 1 913 | **222 ms** | 0.94 s | **1.12 s** | **1.65 s** | **10 / 3** | 488 / 198 / 363 |

- **Checking the elapsed time between levels does not cap anything:** in a real mid-game position one
  level costs 0.4–0.6 s (a parallel batch of rollouts per kept line — far above the ~22 ms one
  rollout costs on a small board), so a level started at 0.7 s ends at 1.3.
- **Estimating a whole level was too cautious** (the /pre-commit review): the first estimate was the
  root's whole time — up to 16 rollouts, where a batch below it has at most 5 (measured: a 0.8 s root,
  then batches of ~0.25 s) — and lines whose turn was over counted as batches though they cost
  nothing. A 0.3 s root read as a 1.5 s level and the search stopped at one level. Now the clock is
  checked before each line is expanded, the first batch estimated from the root's scaled to 5
  rollouts; a line left unexpanded keeps its rollout score, to the same horizon as the rest. **The
  capped search reaches the third level in 363 searches against the uncapped 469.**
- **What is left over 1.2 s is mostly the root:** 16 root moves rolled out take 1.1–2.2 s. Marked
  `ponytail:` in BeamSearchAi and in DesignNotes.
- **On the table** (`MagicTableManager`, search in both seats, Debug, 3 games, the level-estimate
  version): the slowest `StepAi` — which plays the AI's passes up to its next visible move — took
  1.08 s; p95 0.57 s.
- The two Debug rows and the uncapped Release rows show how much one run's means move (246 vs 325 ms
  for the same search): compare the tails, not the means.
- **Strength under the cap is not measured** — a wall clock in a sim run makes it machine-dependent.
  `beam` in `--match` stays uncapped; AI-2.8 measures that.

## The search against GreedyAi, with held values (2026-10-16, AI-2.8)

`--match=beam,greedy --games=156 --sequential`, Release, at 2b9eea2 — `beam` is the search as it
plays since AI-1 (held cards at half their measured value), UNCAPPED (no wall clock in a sim run).
156 games = 13 seeds × 6 ordered deck pairings × both seats: 26 games per pairing, the story's 25+.

| Result (1 SE over mirror pairs) | Time per decision: mean / slowest | Decisions per game (beam, greedy) | By the search's deck (W-L) |
|---|---|---|---|
| **111–45, 71.2% ± 3.1pp** | 239 ms / 6 238 ms (GreedyAi 4.2 ms) | 66, 50 | White-Black 48–4, Red-Green 40–12, Jace 23–29 |

- **Still clearly stronger** — seven standard errors above even. Against the plain search's 54–18
  (75.0% ± 4.2pp, 72 games) the difference is within noise, as AI-1.9 measured held values neutral.
- **The Jace deck is the search's weak seat** (23–29); GreedyAi piloting it went 2–22 against itself.
  **Open:** the plain search went 14–10 with it (seeds 1–6), the held one 23–29 (seeds 1–13) — within
  noise (~±10 and ±7pp), but held values may make Jace's cards too dear to spend. Settle it on the
  same seeds: `--match=beam,beam-plain --sequential`, read the by-deck line (~45 min). Not run.
- Per game: turns 20.2; lands played 6.8 (search) vs 7.5, spells cast 9.6 vs 9.7.
- **Decision: the search is the default AI** on MagicTable and the console (the story's rule:
  default once measured better). `--greedy` picks GreedyAi.

## The opponent casts from its hand (2026-10-17, AI-3.1)

`beam-casts`: the search whose simulated opponent (`Rollout.OpponentModel.CastsFromHand`) also plays
a land, up to `ActionsPerTurn` (1) spells and its planeswalkers' loyalty abilities in its own turn,
by our own cheap policy, from its REAL hand — against `beam` (`BoardOnly`, the default). 156 games
each, `--sequential`, Release, at 74978b0 plus the change; the same seeds as AI-2.8.

| Match | Result (1 SE over mirror pairs) | Time per decision: mean / slowest | beam-casts by its deck (W-L) |
|---|---|---|---|
| beam vs greedy (AI-2.8, for reference) | 111–45, 71.2% ± 3.1pp | 239 ms / 6 238 ms | White-Black 48–4, Red-Green 40–12, Jace 23–29 |
| **beam-casts vs greedy** | **123–33, 78.8% ± 2.8pp** | 276 ms / 9 128 ms | White-Black 50–2, Red-Green 43–9, Jace 30–22 |
| **beam-casts vs beam** | **84–72, 53.8% ± 3.0pp** | **321 ms** / 9 351 ms (beam 228 / 8 915) | White-Black 35–17, Red-Green 36–16, Jace 13–39 |

- **Not shown to be stronger.** Head-to-head +3.8pp is 1.3 standard errors; against GreedyAi +7.6pp
  over the baseline is ~1.8 combined. Both point the same way — not a result yet.
- **It costs 41% more time per decision** (head-to-head, both searches in one run — the cleaner
  comparison; +15% against the baseline's run). The casting opponent evaluates every spell in hand
  at each of its main phases.
- **Part of any gain is cheating:** this opponent model reads the opponent's REAL hand, while the
  evaluator prices that hand blind. The hand modes (AI-3.4, 3.5) take that out.
- **Not symmetric in two details:** in the search's own turn our plays count from the rollout's
  start and the opponent's from the turn's start (`OpponentStep` keeps no state; it reads the turn's
  tally), and our side counts a loyalty or other ability as one of its plays.
- **A first cut that never used loyalty abilities** (the /pre-commit review) measured 120–36 against
  GreedyAi and 80–76 head-to-head (Jace 10–42) — within noise of the fix; the numbers above replace it.
- **Decision: `BoardOnly` stays the default** — not stronger by the evidence, and dearer. Re-measured
  with the hand modes and in the final matrix (AI-3.9).
- Search-vs-search games run longer (23 turns, ~74 decisions a side) than against GreedyAi (~20).

## The libraries shuffled where they can't be known (2026-10-18, AI-3.3)

`beam+shuffle`: the search deciding from `Hidden.Sample` — both libraries reshuffled at its root
wherever the deciding player can't know a card's place (`Card.KnownTo`, AI-3.2), known cards kept,
the sample on its own RNG seed — against the search that reads the libraries in their REAL order.
156 games each, `--sequential`, Release, at 0a09d27 plus the change; the same seeds as AI-2.8.

| Match | Result (1 SE over mirror pairs) | Time per decision: mean / slowest | The shuffling side by its deck (W-L) |
|---|---|---|---|
| **beam+shuffle vs beam** | **72–84, 46.2% ± 2.7pp** | 265 ms / 8 790 ms (beam 249 / 6 618) | White-Black 33–19, Red-Green 30–22, Jace 9–43 |
| **beam+casts+shuffle vs beam+casts** | **69–87, 44.2% ± 2.9pp** | 349 ms / 9 969 ms (beam+casts 323 / 7 989) | White-Black 35–17, Red-Green 26–26, Jace 8–44 |
| **beam+shuffle vs greedy** | **106–50, 67.9% ± 3.0pp** | 211 ms / 4 168 ms | White-Black 47–5, Red-Green 37–15, Jace 22–30 |
| beam vs greedy (AI-2.8, for reference) | 111–45, 71.2% ± 3.1pp | 239 ms / 6 238 ms | White-Black 48–4, Red-Green 40–12, Jace 23–29 |

- **Seeing the real library was worth 3–6pp, consistently:** −3.8pp head-to-head with `BoardOnly`
  (1.4 SE), −5.8pp with the casting opponent (2.0 SE), −3.3pp against GreedyAi beside the baseline.
  Every comparison points the same way — the price of no longer seeing its own future.
- **The honest search is still far stronger than GreedyAi:** 67.9%, about six standard errors.
- **Time: +6–8% head-to-head** — the sample is two library rebuilds per searched decision. The run
  against GreedyAi was faster than the baseline's (211 against 239 ms); compare within one run.
- **The Jace deck pays most** (9–43, 8–44 head-to-head): its surveils planned against cards it
  really knew were coming.
- **Decision (Shayne, 2026-10-18): the default is chosen at AI-3.9**, once the hand modes exist —
  `RealOrder` (stronger, cheats) stays the default until then; `ShuffleUnknown` is honest.

## The cost count against the clock (2026-10-19, AI-6.1)

`--match` reports a deterministic cost per decision: engine steps (every action and choice
`MagicRules.Act`/`Choose` applies inside `MagicRules.CountingSteps`, whose count flows with the
execution context to every thread the decision's work reaches) and rollouts. Checked against wall clock on three 12-game `--sequential` Release runs:

| Run | Steps per decision | ms per decision | Steps ratio | Time ratio |
|---|---|---|---|---|
| beam+casts vs beam | 2 231 vs 1 796 | 421 vs 312 | 1.24 | 1.35 |
| beam vs greedy | 1 565 vs 13.8 | 142 vs 1.9 | 113 | 75 |
| beam+shuffle vs beam | 1 608 vs 1 550 | 267 vs 261 | 1.04 | 1.02 |

- **The count ranks every pair as the clock does.**
- **It undercounts evaluation-heavy work:** position evaluations are not steps, and the casting
  opponent's extra cost is mostly evaluating its spells — +24% by steps, +35% by the clock. Count
  evaluations too if a change's cost is mostly evaluation.
- **The clock drifts where the count doesn't:** the same `beam` cost 0.09 ms a step in one run and
  0.17 in another (smaller boards against GreedyAi are part of it).
- ~7.5 rollouts per decision on average: most decisions are plan replays or have one move worth
  searching; the search proper runs 20–200.
- `BeamSearchAi.Settings.Threads` (null = every core) runs a search's rollouts on fewer threads,
  never changing a decision — for AI-6.2's parallel games.

## Strength from parallel games, and the garbage collector (2026-10-19, AI-6.2)

The same match — `beam+shuffle` vs `beam`, 156 games, AI-3.3's — run four ways, Release. Every run
plays the SAME games: 72–84, by deck 30–22 / 33–19 / 9–43, the same decisions per game and the same
cost count (1 319.6 and 1 245.1 engine steps per decision). Only the wall clock differs:

| Games | Each search | Collector | Wall clock | vs AI-3.3 |
|---|---|---|---|---|
| one at a time (`--sequential`, AI-3.3) | every core | workstation | 5 814 s (97 min) | 1× |
| in parallel, uncapped | one thread | workstation | 1 874 s (31 min) | 3.1× |
| in parallel, one per core | one thread | workstation | 4 138 s (69 min) | 1.4× |
| **in parallel, one per core** | **one thread** | **server** | **941 s (16 min)** | **6.2×** |

- **The default (workstation) garbage collector was the bottleneck.** The engine allocates a new
  state at every step on every thread; its collections pause them all. With 16 games on 16 cores a
  decision took 2.7 s; with ~105 games oversubscribing the same cores it cost half as much CPU —
  the threads had been waiting, not computing. On 48 games of `beam` vs `greedy`: 497 s with the
  workstation collector, **100 s with the server one** (97 s uncapped — the cap now costs nothing).
- **Server GC is on for MagicConsole and MagicCore.Tests** (`<ServerGarbageCollection>`); the suite
  went from 59 s to 36 s. **Every earlier timing in this file is under the workstation collector** —
  compare a new `--sequential` timing with them only after re-running the old side.
- `AiMatchTests.ParallelGames_PlayTheSameGames_AsOneAtATime` (`[Explicit]`) proves parallel and
  sequential runs play identical games — winners, turns, decisions, steps, rollouts.
- In a parallel run the time-per-decision line is not evidence (the report says so); the cost line is.
- **One-at-a-time runs gain too:** the equality test's 12 sequential games of `beam` vs `greedy` took
  55 s on the server collector against 204 s on the workstation one — a multi-threaded search
  allocates in parallel just the same. So the table may gain as well, if Godot's runtime takes the
  setting (AI-7).

## The position suite (2026-10-19, AI-6.4a)

`MagicCore/Sets/FRA.positions.txt`: the search's self-play — 24 games (every ordered FRA pairing,
seeds 1–4), 12 positions a game evenly spaced among its real decisions (two options or more) —
answered by a fresh `beam` at each. Generated in 3½ min (`--suite=generate`). Checked on 16 cores,
each position's search on one thread, Release:

| `--suite=…` | Changed from beam's answers | Cost per position | Time |
|---|---|---|---|
| beam | **0 of 288** | 2 405.5 steps, 13.9 rollouts | 20.5 s |
| greedy | 119 (41.3%) | 16.3 steps | 6.0 s |
| beam+shuffle | 24 (8.3%) | 2 436.7 steps, 14.0 rollouts | 21.9 s |

- **A check takes ~20 seconds** — the minutes-long quick check AI-6 asked for, with room to spare.
- **It sees a difference where a match barely did:** `beam+shuffle` changes 8.3% of decisions,
  `greedy` 41%. It says WHAT changed, not whether it wins — that is the oracle's job (AI-6.4b).
- 13.9 rollouts per position against ~7 per decision in a match: the suite's positions are real
  decisions only (two options or more), where a match counts every pass and plan replay.

## The suite's oracle (2026-10-19, AI-6.4b)

The oracle reads each of the 288 positions once: the search with every first move searched, more
lines kept, a level deeper — and, in the kept version, looking four turns ahead instead of two with
a 2 000-rollout budget. Each version's answer is compared with the oracle's; "score given up" is
the score of the oracle's answer less the score of the version's (never below 0), over positions not
yet decided — a win the oracle found that the version misses, and a loss it walks into that the
oracle's answer avoids, are counted apart. Release, 16 cores:

| `--suite=…` | Agrees: 2-turn oracle (0.7 min) | Agrees: **4-turn oracle** (0.9 min) | Given up (4-turn) | Wins missed | Losses walked into |
|---|---|---|---|---|---|
| beam | 97.6% | **86.1%** | 0.43 | 2 | 0 |
| beam+shuffle | 89.6% | 81.3% | 0.50 | 2 | 0 |
| beam-plain | 90.3% | 79.9% | 0.51 | 5 | 0 |
| beam+casts | 86.8% | 78.8% | 0.75 | 3 | 0 |
| greedy | 58.0% | 55.9% | 2.15 | 6 | 1 |

- **Depth alone made a poor oracle:** most turns run out of decisions before the extra levels, so
  the 2-turn oracle was the same search a little wider — the default agreed with it 97.6% of the time.
  Looking further ahead is what made it differ (86.1%).
- **Both oracles rank alike:** beam first, the variants between, GreedyAi far behind.
- **What it can't do:** it shares the default's evaluator, held values, opponent model and real
  library order, so it favours `beam` by construction. It ranks versions of one model — a pure
  optimisation must keep beam's 86.1% exactly — but whether casting or shuffling is better is the
  overnight match's question.
- 180 of the 288 positions are scored for "given up": the rest are decided within four turns.
- **One decided game swamps the average:** before losses were counted apart, GreedyAi's single walk
  into a loss (−9 000 against an undecided best) read as 49.72 given up a position; it is 2.15.
