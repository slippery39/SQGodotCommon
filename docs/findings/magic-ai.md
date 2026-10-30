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

## The suite regenerated (2026-10-21, TBL-5.3)

`MoveKey` now tells apart two same-named permanents a cost could take, so the old suite's move
tokens no longer replayed; it was regenerated (1½ min) and the oracle re-read it (0.5 min). **The
old suite had already drifted:** regenerated with the OLD `MoveKey`, 23 of 24 games differ from it
(the engine and AI changed since `acdd6c5`; a replay checks the moves still apply, not that the AI
would make them). The `MoveKey` change itself alters 3 of 24 games. On the new suite, Release:

| `--suite=…` | Changed | Agrees with the 4-turn oracle | Given up | Wins missed | Losses walked into |
|---|---|---|---|---|---|
| beam | 0 of 288 | **79.2%** | 0.83 over 172 | 6 | 4 |

- **79.2% is not a regression from 86.1%** — different positions. Compare versions on ONE suite.
- **One decided game swamps the average:** before losses were counted apart, GreedyAi's single walk
  into a loss (−9 000 against an undecided best) read as 49.72 given up a position; it is 2.15.

## The harness proved on AI-3.3 (2026-10-19, AI-6.5)

AI-3.3's question again — does the search lose strength when the libraries it can't know are
shuffled? — asked at both speeds. Release, 16 logical processors (8 cores), server collector.

| Speed | Command | Answer | Time |
|---|---|---|---|
| Quick check | `--suite=beam+shuffle` | 8.3% of decisions changed; agrees with the oracle 81.3% (beam 86.1%) | ~20 s |
| Overnight | `--match=beam+shuffle,beam --until-clear` | **stopped at 288 games: 130–158, 45.1% ± 1.9pp — WORSE by 5pp or more** (log-likelihood: better −10.59, worse 3.40; decided at ±2.94) | 63 min |
| AI-3.3, for reference | 156 games, `--sequential` | 72–84, 46.2% ± 2.7pp — "not shown" (1.4 SE) | 97 min |

- **The overnight run settled what AI-3.3 could not:** the same direction, now a verdict. Its first
  156 games ARE AI-3.3's (seeds 1–13); the next 132 held the same rate.
- **The quick check pointed the same way in 20 s:** shuffle agrees with the oracle less than beam
  does. The oracle reads the real libraries, so it favours beam by construction — whether that
  costs games stays the match's question.
- **Continuous scheduling changes no verdict:** the batched run and the continuous one both stopped at
  288 games, 130–158, with the same log-likelihoods. It is judged on whole batches.
- **Wall clock on this machine drifts ~2× within a day** — `beam` vs `greedy`, 48 games, the
  committed code: 100 s in AI-6.2, 195 s today. Both 288-game runs took 63 min hours apart, so they
  don't compare. Back to back, that fixed 48-game run (no batches, but the same in-order scheduler):
  **159 s, against 195 s with `Parallel.For`'s own chunking** — one sample each.
- **The cores ran half idle:** 42 min into the continuous run it had used 353 CPU-min, ~8.4 of 16
  logical processors — so waiting on batches was never the main cost; the rest is AI-7's question.
- The continuous run's 63 min includes the games still running when the verdict came, played out
  and dropped (the batched run had none). A verdict now abandons them at their next decision.
- **The slowest decision of a parallel run is ~35 s** (one thread, no time limit) — beside a mean
  of ~1.1 s. A lead for AI-7's profile.

## Where a decision's time goes (2026-10-19, AI-7.1)

The suite (`--suite=beam`: 288 decisions, one thread each, 16 logical processors, Release) with the
collector's own counters read at exit (`GC.GetTotalPauseDuration`, collection counts, allocated
bytes), a `dotnet-trace` thread-time sample, and single operations timed on a turn-9 position of
11 permanents (one thread, tiered compilation off).

**1. The garbage collector, made slow by one cache.** `Characteristics.BoardOf` kept each state's
statics in a global `ConditionalWeakTable` — an entry for nearly every state the search makes. Its
lock contended across threads (~6% of samples), its containers' finalizers ran (~6%), and every
collection scanned a weak handle per state. A one-entry cache per thread (the questions about a state
come together) changes no decision — 0 of 288, the same cost count; 122 tests fail if it goes stale:

| `--suite=beam` | Wall | GC pauses | Gen0 collections | CPU | Allocated |
|---|---|---|---|---|---|
| server collector, the weak table | 21.0 s | 5.6 s | 1 177 | 155 s | 36.3 GB |
| server collector, one entry per thread | **14.6 s** | **1.4 s** | 1 190 | 107 s | 36.2 GB |
| workstation collector, the weak table | 56.3 s | **42.8 s (76%)** | 4 363 | 131 s | 36.3 GB |
| workstation collector, one entry per thread | **22.0 s** | 8.6 s | 4 356 | 95 s | 36.3 GB |

MagicCore.Tests went from 1 m 52 s to 1 m 5 s with it.

**2. The table runs on the workstation collector.** `godot-mono` is the editor build: it starts .NET
from its own `GodotPlugins.runtimeconfig.json` (no `System.GC.Server`), never the project's, so a
csproj setting can't reach it. Printed from MagicTable: `IsServerGC` False; with the environment
variable `DOTNET_gcServer=1`, True. The workstation rows above are the table's case.

**3. Allocation: ~126 MB a decision, ~52 KB an engine step** (36.3 GB over 692 784 steps). One
priority pass allocates 31.8 KB, `LegalActions` 14 KB, `Evaluator.Evaluate` 21 KB, a rollout of 193
steps 8.1 MB. Untouched by the cache change; it is what still drives the collections.

**4. One engine step (a priority pass) is ~77 µs**, and the turn structure is most of it:

| Part of `Act(pass)` | µs | Allocated |
|---|---|---|
| the whole step | 77 | 31.8 KB |
| `StateBasedActions.Check` | 39 | 14.2 KB |
| `Triggers.Collect` | 14 | 12.6 KB |

The check is many small zone scans, each a few µs per player: the dying scan (5.9), the token sweep
over graveyard, exile, hand and library — 29 library cards every pass — (5.6), `Attachments.Check`
(4.3), the legend scan (1.7), "walkers survive" (1.3). For scale: `LegalActions` 26 µs, `Evaluate` 32.

**5. Where the search spends it** (sampled, inclusive): rollouts 96%; `GivePriority` 70% (the
state-based check 38%, trigger collection 33%); `Lookahead.Settle` 47%; `Rollout.Answer` — attacks
and blocks judged one creature at a time, each settled — 41% (AI-2.4 measured ~45%); `Evaluate` 5%;
`LegalActions` 5%; mana payment ~3%.

**6. Counted over the suite** (temporary counters): trigger collection ran 680 661 times — **62%
with no staged events, where nothing can trigger**, and only 2.9% found anything. Attack judgments:
13 026, each of 2 or 3 options (don't, the player, a planeswalker); block judgments: 5 239, of 2–6
options. Every option plays the whole combat forward (`Lookahead.Settle`).

**Two traps in measuring this:**
- **65% of the thread-time samples end in `PollGC` under `GameState.GetChildren`, beneath a
  mis-symbolised `Buffer.BulkMoveWithWriteBarrier`** — threads stopped for the sampler's own stack
  walk, not for the collector (it paused 1.4 s of that run). Enumerating a zone costs 120 bytes and
  ~1 µs. Read the callers' inclusive time, never that leaf.
- **Time single operations with tiered compilation off:** the first timings, on tier-0 code, read
  a step at 236 µs against 77.

## Decision-neutral speedups, each measured back to back (2026-10-19, AI-7.3)

Every row: `--suite=beam`, **0 of 288 decisions changed and the cost count identical** (2 405.5 engine
steps, 13.9 rollouts a position), MagicCore.Tests 975 green. Each change is measured against the
tree without it, both runs minutes apart — **this machine's speed drifted ~2× during the day, so only
the pairs compare**. Allocated bytes barely move from run to run; they are the steady measure.

| Change | Allocated | CPU (server / workstation) | Wall (server / workstation) |
|---|---|---|---|
| 7.3a the board cache: one entry per thread, not a weak table | 36.3 → 36.2 GB | 155 → 107 s / 131 → 95 s | 21.0 → 14.6 s / 56.3 → 22.0 s |
| 7.3d-1 trigger collection returns at once with no staged event | 36.4 → **27.0 GB** | 119 → 97 s / 118 → 100 s | 26.4 → 17.7 s / 31.1 → 24.3 s |
| 7.3d-2a `CardsIn` one iterator; zone keys built once | 27.0 → **24.0 GB** | 94 → 84 s / 89 → 75 s | 23.9 → 14.1 s / 21.5 → 18.4 s |
| 7.3d-2b the state-based check: one battlefield read, plain loops | 23.8 → **21.0 GB** | 74 → 72 s / 73 → 70 s | 15.4 → 13.9 s / 17.0 → 17.3 s |
| 7.3c power, toughness and keywords as plain loops (`Characteristics`, `Card`) | 21.0 → **17.9 GB** | 73 → 71 s / 75 → 62 s | 16.1 → 16.6 s / 19.4 → 16.4 s |

- **Whole games are unchanged too:** after 7.3c, AI-3.3's 156-game match (`beam+shuffle` vs
  `beam`) replays exactly — 72–84, the same cost per decision (1 319.6 and 1 245.1 steps) — in
  **272 s**, against 941 s in AI-6.2 and 5 814 s one game at a time (AI-3.3).
- **Allocation 36.3 → 17.9 GB (−51%)** over the five. One priority pass on the turn-9 probe position:
  31.8 → 17.9 KB after 7.3d-1 alone.
- **Every `CardsIn` built a new key string, `$"{playerId}.{type}"`, formatting the enum each time**
  — likely the enum-reflection lines in the first profile (`InitializeEnumInfo`, the runtime's type
  cache rebuilt under collection pressure).
- **What still allocates in the state-based check is `Characteristics`:** `IsIndestructible` 736 B a
  call, `Toughness` 288 B — LINQ over `ImmutableArray` components (each boxes the array) and the
  statics iterator. That is 7.3c.
- Revert-checked: lethal damage off by one fails 120 tests; a token sweep that removes nothing, 158.
- **After 7.3c, on the probe position:** a priority pass 31.8 → 8.0 KB, `Evaluate` 21 → 3.1 KB,
  `Toughness` and `IsIndestructible` 0 B (were 288 and 736 — on a board with no P/T statics: an
  applying one still builds its context per read), a 193-step rollout 8.1 → 3.6 MB.
  `LegalActions` is now the largest single allocator (12 KB): every candidate — targets, modes, X —
  through `ValidateAdd`'s payment search.
- **Measured and not kept** (no gain worth the diff): the evaluator as plain loops after 7.3c
  (18.0 → 17.7 GB, CPU unchanged — its allocation had been the characteristic reads); handing the
  check the same state when nothing is staged, so the board cache hits (21.0 → 20.9 GB); each P/T
  static's context built once with its board instead of per read — WORSE, 17.9 → 18.1 GB: every new
  state builds a board, and on these decks few of them are asked a P/T static's value.
- 7.3c revert-checked: a static that grants no keyword fails 95 tests; base P/T from the first
  effect instead of the latest, 1. `HotPathAllocationTests` guards the allocation itself: `HasOwn`
  written back as LINQ allocates 200 B a read and fails it.
- **Attack/block score reuse (7.3e), counted before building:** rollouts made 18 155 combat
  judgments over 45 698 options (each a whole combat played forward); 7 520 judgments directly follow
  another of the same kind by the same player, where one option's line is the one the previous
  judgment already scored — 16% of the simulations, ~6–7% of the search at 41%. Not built.

## The opponent's hand drawn from its unseen cards (2026-10-19, AI-3.4)

`beam+casts+unseen`: the search's casting opponent casts from a SAMPLED hand — its hand cards the
searching player hasn't seen drawn again from those and the cards of its library whose place is
unknown; a card drawn from the library swaps places with the hand card it replaces, so the library
otherwise keeps its real order (Shayne). Measured against `beam+casts` (its real hand) — the hand
matters only to an opponent that casts. Release, 16 logical processors.

| Speed | Result | Cost per decision | Time |
|---|---|---|---|
| Quick check (`--suite=…`) | changed 37 (12.8%; `beam+casts` 35); oracle agreement 76.7% (`beam+casts` 78.8%), wins missed 5 (3) | 3 257.7 steps a position (3 373.5) | ~20 s |
| `--until-clear` | **384 games, 189–195, 49.2% ± 1.6pp — NO difference of 5pp either way** (log-likelihood: better −6.18, worse −3.24) | 1 509.5 steps, 7.0 rollouts (1 533.0, 6.9) | 572 s |

- **Not seeing the opponent's hand costs nothing measurable**, where not seeing the libraries cost
  5pp or more (AI-6.5). A casting opponent's real hand was no advantage worth 5pp.
- **Both sides of either comparison differ in one more way:** the real-hand, real-order search keeps
  the game's own random seed, so its rollouts foresee the real game's next random results (a random
  discard, a random order); any sample gets a fresh seed. So AI-3.4 compares "real hand + that
  foresight" with "sampled hand, none", and AI-3.3 the same for the libraries. Rare in FRA, but not
  separated.
- The oracle can't rank this one: it searches with the board-only opponent, which never casts, so
  the opponent's hand is invisible to it. Its lower agreement is noise for this question.
- By deck, the sampled side: Red-Green 77–51, White-Black 83–45, Jace 29–99 — the same shape as every
  match on this field.

## The opponent's hand guessed from its colours (2026-10-19, AI-3.5)

`beam+casts+guess`: the casting opponent's unseen hand is GUESSED, never its real cards — cards of
the colours it has shown, weighted as a booster weights rarity, a land with the chance of the lands
left in a 40-card, 17-land deck (Shayne); its library stays real. Against `beam+casts` (its real
hand), Release, 16 logical processors:

| Speed | Result | Cost per decision | Time |
|---|---|---|---|
| Quick check | changed 40 (13.9%; `beam+casts` 35, `+unseen` 37) | 3 152.8 steps a position (3 373.5) | ~20 s |
| `--until-clear` | **384 games, 190–194, 49.5% ± 1.6pp — NO difference of 5pp either way** (log-likelihood: better −5.83, worse −3.82) | 1 505.9 steps, 7.0 rollouts (1 588.8, 7.0) | 598 s |

- **An honest guess plays as well as the real hand, and is 5% cheaper** — fewer of its guessed cards
  are castable than real ones, so the simulated opponent does less. Like AI-3.4, it also takes away
  the real game's random seed (foresight of a random discard or order), not separated.
- **A first cut made each guess a new object** — new ids moved every later id away from the real
  game's, so a committed plan naming a new token or target was never found again. Caught in review,
  fixed (a guess keeps its card's id), and that match discarded unfinished.
- By deck, the guessing side: Red-Green 76–52, White-Black 84–44, Jace 30–98.

## The simulated opponent responds at instant speed (2026-10-19, AI-3.6)

`beam+responds`: the casting opponent also answers in OUR turn — while our spell or ability is on the
stack, and in our combat once something attacks — with whichever instant-speed spell beats passing
most (both settled), as often as one does (Shayne). Our own rollout policy never responds. Against
`beam+casts`; the field's decks hold 4–7 instant-speed cards in 40 (White-Black 6, Red-Green 4,
White-Blue Jace 7 — mostly removal). Release, 16 logical processors:

| Speed | Result | Cost per decision | Time |
|---|---|---|---|
| Quick check | changed 36 (12.5%; `beam+casts` 35); oracle agreement 77.4% (78.8%) | 3 481.2 steps a position (3 373.5, +3.2%) | ~20 s |
| `--until-clear` | **288 games, 145–143, 50.3% ± 1.9pp — NO difference of 5pp either way** (log-likelihood: better −3.06, worse −4.04) | 1 593.4 steps, 6.9 rollouts (1 536.6, +3.7%) | 506 s |

- **An opponent that answers our plays changes nothing worth 5pp here**, and costs under 4% — far
  cheaper than expected: most windows hold no castable instant.
- **What the policy can't see:** "beats passing" settles the rest with both sides passing, so each
  response is judged as though it were the only one; with two removal spells in hand each is credited
  with the whole gain. A limit of the rule chosen, not separated.
- A first cut opened the combat window with no attack declared — found in review, that match
  stopped. By deck, the responding side: Red-Green 60–36, White-Black 57–39, Jace 28–68.

## GreedyAi as our rollout policy (2026-10-19, AI-3.7)

`beam+greedyrollout` (run as `beam+greedy`, renamed after — the same code): `GreedyAi` decides every
decision of ours in the rollouts — any play at any speed that strictly beats passing, as many a turn
as do, every choice by its own answer — instead of the cheap policy (the land, then one play a turn
that leaves us no worse, sorcery speed). The opponent keeps its model. Against `beam` (Shayne).
Release, 16 logical processors:

| Speed | Result | Cost per decision | Time |
|---|---|---|---|
| Quick check | changed 40 (13.9%); oracle agreement 77.1% (beam 86.1% — the oracle uses the cheap policy, so it favours beam); walks into a loss 1 | 3 822.5 steps a position (2 405.5, **+59%**) | ~20 s |
| `--until-clear` | **600 games (the cap), 317–283, 52.8% ± 1.4pp — UNDECIDED: worse is ruled out (−13.66), better by 5pp is not reached (0.85)** | 2 041.1 steps, 6.7 rollouts (1 264.6, **+61%**) | 1 085 s |

- **Probably a small gain, under 5pp:** +2.8pp at about 2 standard errors — the only AI-3 model so far
  that leans better — for **61% more cost per decision**. Under AI-6's rule (5pp or more matters;
  smaller is kept or dropped on reasoning) it is AI-3.9's call, with the table's time in the balance.
- **Two changes in one:** GreedyAi plays more a turn AND only what strictly beats passing — the
  cheap policy's "no worse" was chosen so setup plays (which score flat) are made. Not separated.
- By deck, the greedy-rollout side: Red-Green 129–71, White-Black 141–59, Jace 47–153.

## The rollout's combat: all-in attacks and owner-chosen targets (2026-10-19, AI-3.8)

Two settings, each measured alone against `beam` (Shayne). `+allin`: at an attack declaration the
rollout also settles "this creature and every one after it attack the player" and takes it if it
beats the best judged alone. `+targets`: a target choice in the rollout takes its owner's best (each
legal target settled) instead of the first legal one. Release, 16 logical processors:

| Setting | Quick check | `--until-clear` | Cost per decision | Time |
|---|---|---|---|---|
| `+allin` | 8 changed (2.8%), oracle 83.3% | **96 games, 39–57, 40.6% ± 2.8pp — WORSE by 5pp or more** (worse 4.24) | 1 529.0 steps (beam 1 278.0, +20%) | 186 s |
| `+targets` | 2 changed (0.7%), oracle 86.1% | **192 games, 96–96, 50.0% ± 1.8pp — NO difference of 5pp** | 1 299.0 steps (1 293.4, +0.4%) | 242 s |

- **All-in attacking made the search clearly worse**, settled at the first possible check. The likely
  cause was named in review before the run: the look-ahead's defender (`QuickAnswer`) blocks only
  what it survives and never chump-blocks, so an all-in line scores as lethal where the rollout's own
  defender (which judges blocks and does chump) survives — the search swings too eagerly and eats the
  crack-back. By deck the all-in side went Red-Green 17–15, White-Black 19–13, **Jace 3–29**. Not
  confirmed by a position; the upgrade (settle the all-in line with judged blocks) is in the code's
  ponytail.
- **Targets by their owner's answer change nothing worth 5pp** at no cost: on this field the first
  legal target is rarely the wrong one (2 decisions in 288).

## AI-3's matrix, and the defaults chosen (2026-10-19, AI-3.9)

Every simulation model, each measured alone by the early-stop match (5pp either way, 5% error per
direction) against the search without it. Cost is engine steps per decision in that match.

| Model (`beam+…`) | Against | Result | Cost |
|---|---|---|---|
| `shuffle` — libraries honest (AI-3.3) | real order | **WORSE by 5pp**, 288 games, 130–158 | +9% |
| `casts` — opponent casts from hand (AI-3.1) | `beam` | not shown stronger (156 games, 84–72, before early stop) | +21% |
| `unseen` — its hand redrawn from unseen cards (AI-3.4) | `casts` | no difference, 384 games, 189–195 | −2% |
| `guess` — its hand guessed from its colours (AI-3.5) | `casts` | no difference, 384 games, 190–194 | −5% |
| `responds` — answers at instant speed (AI-3.6) | `casts` | no difference, 288 games, 145–143 | +4% |
| `greedyrollout` — GreedyAi plays our side (AI-3.7) | `beam` | **+2.8pp, undecided** at the 600 cap, 317–283 | +61% |
| `allin` — attacks judged all-in too (AI-3.8) | `beam` | **WORSE by 5pp**, 96 games, 39–57 | +20% |
| `targets` — targets by the owner's best (AI-3.8) | `beam` | no difference, 192 games, 96–96 | +0.4% |

**Defaults (Shayne):** the library **honest** — `ShuffleUnknown` became the default; the real order
is `beam+realorder` (5pp+ stronger, but it sees its own draws) — the board-only opponent, the cheap
rollout policy, each attacker alone, the first legal target. The suite's committed answers moved to
the honest search (24 of 288 changed — the same 24 `beam+shuffle` changed); its oracle reads the real
order, so it now favours `beam+realorder` (86.1% against 81.3%).

- **What sees more costs more and, here, rarely plays better.** Only knowledge of the real library
  order — a cheat — was worth 5pp; the opponent's real hand, its casts and its responses were not.
- **Combinations were not run** (Shayne: each model alone is enough to choose). A richer opponent with
  GreedyAi rollouts, or all-in with judged blocks, are costed in DesignNotes.md.

## The threat term: a clock that counts blockers (2026-10-20, AI-5.2)

`Evaluator.Pressure`, in the shared evaluator at half weight: for each side, 20 ÷ the turns its board
needs to kill the other — life over the lasting power that gets past the defender's blockers in the
next attack (fliers blocked only by fliers and reach, a tapped blocker counted only if it untaps
first, the dying and defenders left out). Symmetric and uncapped. The card-value sandbox measures
without it (Shayne): its creature ladder would otherwise give almost any card a flat bonus.

**The blunder it fixes** (`GreedyAiTests.StaysHome_WhenAttackingLeavesItDeadToTheSwingBack`): GreedyAi
at 3 life, its 3/3 facing their 3/3 — it went face, tapping its only blocker, and the swing back was
lethal. The search did not (its rollouts play the swing back). With the term GreedyAi stays home.

| `beam` (with it) vs `beam+nothreat` | Result |
|---|---|
| Quick check | 27 of 288 decisions changed (9.4%); 2 624.7 steps a position (2 436.7, +7.7%) |
| `--until-clear` | **600 games (the cap), 312–288, 52.0% ± 1.4pp — UNDECIDED: worse ruled out (−11.96), better by 5pp not reached (−1.33)**; 1 324.3 steps a decision (1 276.7, +3.7%); 783 s |

- **Kept** by the rule Shayne set: no measured loss, and a recorded blunder fixed. It leans ~+2pp.
- **Half weight** keeps a forecast from outweighing real damage at a healthy life (a free attack for
  2 at 29 life tied with the 1 power it leaves unblocked at full weight — a tie takes the passive
  move); near death it dominates (a lethal swing-back at 3 life scores −10).
- **Review caught four defects before the measurement** — pumps until end of turn counted as threat,
  dying creatures as attackers and blockers, defender and unblockable ignored, and a cap at 40 that
  broke the monotonic rule — and the sandbox measuring its own fixture. The first match was stopped.
- The card-value table is unchanged (measured without the term); GreedyAi's 12 recorded games moved.

## Six more keywords valued (2026-10-20, AI-5.3)

Vigilance, haste, reach, hexproof, indestructible and ward join a creature's valued keywords at the
0.8 the other seven get (Shayne); haste only while the creature is new, and a printed keyword counts
even when a pump grants it again. The card-value sandbox can't value a keyword — it scores the board
with the evaluator, so it would echo the weight — so it measures without them, and the six were
measured by a match instead.

**The blunder it fixes** (`GreedyAiTests.Keywords_OfTwoEqualCreatures_CastsTheOneWithHaste`): of two
equal 2/2s and the mana for one, GreedyAi cast whichever came first; now the one with haste.

| `beam` vs `beam+nokeywords` | Result |
|---|---|
| Quick check | 29 decisions changed against 27 without them; 2 637.2 steps a position (2 625.4) |
| `--until-clear` | **192 games, 95–97, 49.5% ± 1.7pp — NO difference of 5pp**; 1 374.5 steps a decision (1 371.7, +0.2%); 377 s |

- **Kept** by the rule: no difference, and a recorded blunder fixed. As MtgSimulator found, static
  terms add little once rollouts play the game out — but GreedyAi has no rollouts.
- After AI-5 the suite's committed answers moved to the current search (29 of 288); its oracle still
  reads the real library order and the evaluator before AI-5.


## The look-ahead's safe block counts double strike (2026-10-22, AI-8.3)

From Shayne's sealed game (kept: `MagicCore.Tests/Records/DuelistWontTrade.json`, move #332): at 22
life the AI would not trade Divining Duelist (4/3) for a 4/4 Beast — the oracle scored the block
−8145.06 (exactly −10 000 × 0.95⁴: a loss on turn 18) against −75 for no block. Traced through the
oracle's rollouts: in the no-block line the simulated opponent blocked the Duelist (double strike
under Yuriko, attacking alone) with its Craftwork Crusher 7/5 and lost it. `Lookahead.QuickAnswer`
lets a look-ahead blocker take only blocks it survives — and counted a double striker's damage once
(4 < 5). Now `Lookahead.Survives`: damage twice for double strike, deathtouch fatal, marked damage
counted. The simulated opponent chumps with its 1/1, as Shayne did.

| | Result |
|---|---|
| Quick check | 4 of 288 decisions changed (1.4%); 2 830.5 steps a position |
| `beam` vs `beam+oldblocks`, `--until-clear` | **288 games, 155–133, 53.8% ± 1.3pp — BETTER by 5pp or more** (log-likelihood: better 3.84, worse −18.38; decided at ±2.94); 1 398.4 steps a decision (1 425.6); 647 s |
| Suite after `--accept` and a fresh oracle | beam agrees 78.8%, `beam+oldblocks` 78.1% |

- **Kept** — better by AI-5's rule. The recorded GreedyAi games moved by one (`GoldenAiReplay.txt`);
  the card-value table is unchanged.
- After the match, review: a first-striking blocker that kills a double striker in the first-strike
  step takes one hit, not two (the measured version counted two). It moves no suite answer and no
  recorded game, so the match stands.
- **The reported play is NOT changed by it:** the search still declines, now for the horizon —
  blocking dies on turn 18, inside the look-ahead (−8145); not blocking is alive at 3 life on turn 19
  (−17.91), lost a turn later. Both lines lose.
- **Rechecked against MtgSimulator** (Shayne): its horizon fixes — the terminal discount, the
  absolute horizon (both ported), the pre-search lethal check (our lethal, not this) — never scored
  "lost one turn past the horizon"; its race pressure was the only signal, and our port counts
  blockers with trample ignored, so at this horizon Yuriko "stops" the trampling Crusher (from the
  code, not measured).

## The threat term counts a trampler's excess (2026-10-22, AI-8.4)

`Evaluator.Pressure` matched each attacker to one blocker and let nothing past a blocked one — at
AI-8.3's horizon Yuriko's 2/3 "stopped" Shayne's 7/5 trampler at 3 life, so a lethal board scored as
no clock. Now a trampler meets the blocker that absorbs most and gets past what that blocker can't
absorb by the next attack — its lasting toughness (pumps and marked damage gone, as power already
is), or 1 against deathtouch; trample granted until end of turn doesn't count.

| | Result |
|---|---|
| Quick check (first version) | 2 of 288 decisions changed (0.7%), both attack declarations |
| `beam` vs `beam+notrample`, `--until-clear` (first version) | 288 games, 141–147, 49.0% ± 1.0pp — no difference of 5pp either way; 421 s |
| **the same, final version** (review: lasting toughness, end-of-turn trample) | **288 games, 141–147, 49.0% ± 1.2pp — NO difference of 5pp either way** (log-likelihood: better −13.34, worse −5.49); 1 416.2 steps a decision (1 425.6); 402 s |
| Suite after `--accept` and a fresh oracle | beam agrees 79.5%, `beam+notrample` 79.2% (1 of 288 differs) |

- **Kept** by AI-5's rule: no difference, and the rebuilt position fixed — at Shayne's turn 14
  (`DuelistWontTrade`, #332) the search and the oracle now TRADE the Duelist for a Beast:

  | at #332 | don't block | block a Beast |
  |---|---|---|
  | search | −45.97 | **−38.51** (`+notrample`: −27.55 / −28.51, declines) |
  | oracle | −65.21 | **−33.71** (`+notrample`: −17.91 / −23.71, declines) |

- So the report needed the threat term, not a horizon change: AI-8.5 (a lethal board at the horizon
  scored as a loss) has no recorded blunder behind it now.
- Review moved 1 suite answer and 2 recorded games after the first match, so the match was run
  again on the final version — same verdict. GreedyAi's recorded games moved (all with Red-Green,
  the deck with tramplers); the card-value table is unchanged (measured without the threat term).

## The simulated opponent uses its planeswalkers (2026-10-22, AI-8.1)

From Shayne's first playtest (TBL-2, rebuilt by hand): the AI's 2/2 went face while his Jace token
at 3 loyalty — able to −3 and draw — sat unattacked. GreedyAi, the search and the oracle all did it,
at 20 life and at 8: face scored +2.11, the Jace +0.70 (2 × 0.35). The look-ahead never saw the
draw: the board-only opponent never used a walker, and a casting one wouldn't, because by the
evaluator cashing the Jace (2 + 1.05) for a card (2.71 to the search) left it worse off.

Not fixed in the evaluator: pricing a walker's unspent abilities would also stop the AI using its
own Jace — no weights satisfy both (the 0.8-per-counter trap, again). Instead `Rollout.TheirLoyalty`
(Shayne: "assume the opponent will use its best planeswalker ability"): every turn it can, whatever
its model, the simulated opponent uses the loyalty ability with the best EFFECT — the walker's own
worth added back. **Checked which ability fired**: judged by the evaluator alone, a Jace at 3
surveiled, and the AI attacked it only to make it surveil itself to death; with the worth added
back it draws at 3 and 6, surveils at 1 and 2. A first match on the surveiling version was stopped.

| | Result |
|---|---|
| Quick check | 14 of 288 decisions changed (4.9%) — removal aimed at Jace, attacks on Jace |
| `beam` vs `beam+idlewalkers`, `--until-clear` | **288 games, 145–143, 50.3% ± 1.6pp — NO difference of 5pp either way** (log-likelihood: better −4.22, worse −5.59); 1 394.1 steps a decision (1 364.5, +2.2%); 385 s |
| Suite after `--accept` and a fresh oracle | beam agrees 80.6%, `beam+idlewalkers` 77.8%; wins missed 7 against 10 — an oracle with the same opponent model, so it leans this way by construction |

- **Kept** by AI-5's rule: no difference, and the rebuilt positions fixed —
  `AiPositionTests.AJaceThatWouldDraw_IsAttacked_OneThatWouldOnlySurveil_IsNot`: a 2/2 attacks a
  Jace at 3 (control: `+idlewalkers` goes face), not one at 1, nor a 5/5 one at 2 (Shayne's cases).
- An asymmetry, chosen: their walkers act by effect, ours by the rollout's "leaves us no worse"
  and the evaluator's count — the other half, in DesignNotes ("values resources by count").
- GreedyAi's recorded games and the card-value table are unchanged (neither uses the rollout).
- After the match, review: under a casting model (`+casts`, `+responds`, not the default) its usual
  play judged the walker first by the evaluator alone, pre-empting the effect choice — now a walker
  is `TheirLoyalty`'s alone while it is on. The board-only default is untouched (the suite reads the
  same both ways), so the match stands.

## A planeswalker priced by what its best ability makes (2026-10-23, AI-8.2)

From Shayne's first playtest (TBL-2, rebuilt by hand; the hand a stand-in): the AI surveiled Way of
the Wildspeaker away with the lands to cast it — "a 4/4 that draws a card" (Jace +7, −4 for a 4/4
Beast, −3 to draw). Traced: priced as loyalty alone, casting Way scored +1.23 and lost to a 2-mana
pump (+1.45), so the one-play look-ahead never cast it and a Way on top was worth a random card;
with cheap spells in hand it was binned by 0.9. The Beast it enables scores +13.40 — as the NEXT play.

`Evaluator.Walker`: **max(2, ½ × what its best affordable ability makes) + 0.35 a counter** — a
creature token's body, a card drawn at a held card's price; anything else unpriced (the old body).
**The first version was not monotonic** (½ × a draw replaced the body of 2 once −3 was affordable, so
empowering Jace 2 → 3 scored as a loss) — caught in review, its match stopped; the floor fixes it,
pinned by `GreedyAiTests.AWalkersValue_NeverFalls_AsItsLoyaltyRises`. The card-value table moved for
4 cards: Way of the Wildspeaker 6.45 → 10.85, Garruk, Curse Breaker 3.75 → 8.15, Way of the Healer
5.75 → 7.25, The Theorist 3.05 → 3.3.

| | Result |
|---|---|
| Quick check | 6 of 288 decisions changed (2.1%) |
| `beam` vs `beam+walkercount`, `--until-clear` | **192 games, 98–94, 51.0% ± 1.8pp — NO difference of 5pp either way** (log-likelihood: better −2.22, worse −5.40); 1 387.9 steps a decision (1 423.8); 395 s |
| Suite after `--accept` and a fresh oracle | beam agrees 81.6%, `beam+walkercount` 80.2%; score given up 0.76 against 0.87 |

- **Kept** by AI-5's rule: no difference, and the play fixed — `AiPositionTests.WayOfTheWildspeaker_IsCastOverAPump`
  (+3.43 against the pump's +1.50; control `+walkercount`: the pump). The surveil itself was too
  narrow to keep as a test — the new card values alone flip it.
- Both sides of the match use the new card-value table: it measures the walker price; the table
  moves with it. GreedyAi's recorded games moved by two.
- **Not done, by design**: a Jace whose best is one draw keeps its old value (½ a card < the body),
  so the AI still won't count cashing a 3-loyalty Jace for a card as a gain — monotonic first.

## Shayne's sealed game beside the AI (2026-10-23, TBL-5.6)

`MagicConsole --record=game_records/2026-10-23_230227_seed934349.json --compare` — Release, 16 cores,
16 s (repeatable: a second run printed the same lines). Seed 934349, 835 moves, Shayne won on turn 33.
His 112 `you` moves held 100 real decisions (two options or more):

| | |
|---|---|
| agreed with the default search | 38.0% |
| agreed with the oracle | 38.0% — **63.0%** counting the 25 picks it scores the same (to 2 decimals) |
| the search with the oracle | 85.0% (the position suite: 86.1%) |
| score given up to the oracle | 3.95 a decision, over 68 |
| missed a win the oracle found | 3 (#615 Seed Suture's target, #703, #821) |
| walked into a loss it avoids | 6 (turns 24–26: passes holding Unsummon, and #590's no-block) |

- **Ties are a third of the "disagreements"**: Pass against Play Island before combat, attacks in a
  game already won. The report labels them "a tie"; the agreement is given both ways.
- **Most real differences are instant-speed windows:** the oracle casts Unsummon or Prophesied End at
  the first chance; Shayne held them.
- **"Walks into a loss" is the oracle's reading, not the outcome** — he won. And the oracle (like the
  search, by default) reads the AI's REAL hand (`Hidden.Hand.Real`), so it judges him knowing what he
  couldn't. An honest-hand oracle for a human's game is not built (TBL-5's Next).
- **The real hand barely moves the summary** (2026-10-24, measured by switching both searches to
  `Hidden.Hand.ShuffleUnseen` for one run, then back): agreed with the oracle 37% (67% with ties)
  against 38% (63%), the search with the oracle 82% against 85%, given up 3.12 against 3.95 — and the
  same 3 missed wins and 6 losses walked into, so those are not the oracle peeking at the AI's hand.
  **Single lines are not stable**: about half the listed decisions changed their reading — a
  different sample of hidden cards is a different look-ahead — so a report is read in aggregate, and
  one line is a lead to settle (AI-11), not a verdict.

## A winning AI presses: progress this turn breaks ties among won lines (2026-10-27, AI-10.2)

A won line scored a flat 10,000 × 0.95^turns, so two wins on the same turn tied and the tie picked
the first option, often "Don't attack" (Shayne's game: Gallia stayed home at #545 and #587 while he
was at 5). Shayne's tie-break: a won line also counts the opponent's life lost by the end of the
search's own turn, a point a life (`Rollout.Settings.Progress`).

- **The match:** `--match=beam+progress,beam --until-clear` (the switch's name while it was off;
  now `--match=beam,beam+noprogress`) stopped after 288 games (6 pairings ×
  24 seeds × both seats, 495 s on 16 cores). beam+progress **51.4% ± 1.2pp** (148–140, no draws);
  no 5pp difference either way. Cost per decision unchanged (1384 against 1380 engine steps, 7.3
  rollouts each). By deck: Red-Green 60-36, White-Black 65-31, White-Blue Jace 23-73.
- **The suite:** 5 of 288 decisions change, each at the same oracle score (score given up 0.76
  either way): won positions now press, with an attack, an ability, or Ajani's Anguish for X=4 over a
  land drop. Against the oracle as stored, agreement fell 81.6% → 79.9%: that oracle scored
  without the tie-break and took the first of its own ties. Re-read with it (`--suite=oracle`, the
  oracle is built from the default rollout), agreement is **83.3%**, against 81.6% for
  beam+noprogress. The score given up is 0.76 a position all four ways.
- Made the default; `beam+noprogress` is the search before it.

## A known card in the opponent's hand priced as itself (2026-10-27, AI-9.5)

Every card in the opponent's hand was priced blind (`HeldValues.Unknown`, 2.70). Now one the searcher
knows (`Card.KnownTo`, e.g. a creature it bounced) is priced at its held value (`Evaluator.Terms.KnownHand`;
off is `beam+blindhand`).

- **It fires but is small:** at #547 of Shayne's 10-23 game the bounced Fateshaper Aspirant is
  priced 6.50 instead of 2.70, which moves Unsummon on it −24.1 → −27.9 against Pass −38.5. It doesn't
  flip that position, and neither does an honest casting opponent (`casts+unseen`: the search bounces
  Gallia, −40.0 against −48.4). Shayne's hold there was patience (AI-4.2, AI-13 lesson 4).
- **The match:** `--match=beam,beam+blindhand --until-clear`: beam 49.0% ± 1.0pp over 96 games
  (47–49); no 5pp difference either way.
- **The suite:** 2 of 288 decisions change. At White-Blue Jace vs Red-Green seed 4, move 53, Fatehold
  Charm's bounce takes a Cadet token over Heartstring Puller (a bounced token is gone), which is the
  recorded fix Shayne agreed (`AiPositionTests.ABounce_TakesTheToken_…`). Against the oracle re-read
  with the term, beam agrees 82.6% (score given up 0.87, 4 losses walked into), and beam+blindhand
  83.0% (0.78, 3): slightly worse on these positions, not seen in the match. Kept on the token fix
  (AI-5's keep rule, Shayne).

## An honest casting opponent in the look-ahead, on today's defaults (2026-10-27, AI-10.4)

`beam+casts+unseen` (the simulated opponent casts from its hand; the cards the search hasn't seen are
redrawn from its unseen cards) against `beam` (board-only), on the defaults since AI-3: honest
libraries, AI-10's progress tie-break, AI-9.5's known hand.

| Speed | Result | Cost |
|---|---|---|
| Quick check | changed 70 of 288 (24.3%); oracle agreement 69.4% (82.6%), but the oracle searches with the board-only opponent, so it can't rank this | 3 552 steps a position (2 963, +20%) |
| `--until-clear` | **384 games, 193–191, 50.3% ± 1.8pp: NO difference of 5pp either way** | 1 599 steps, 7.4 rollouts a decision (1 351, 7.2: +18%); 889 s |

- **Still no stronger**, as AI-3.1 found before the honest libraries: a casting look-ahead changes
  a quarter of the decisions and wins as often.
- **It doesn't settle #547** of Shayne's 10-23 game: it bounces Gallia (−40.0 against Pass −48.4).
  His hold there was patience (AI-4.2's holding position, AI-13 lesson 4).
- By deck: Red-Green 79–49, White-Black 85–43, Jace 29–99 — the field's usual shape.

## Board-only, except the cards we know: the default opponent (2026-10-27, AI-10.4)

Shayne's model: the simulated opponent stays board-only, except that it plays the cards in its hand
WE know (`Card.KnownTo`: a bounced creature, a card it revealed, one it returned from a graveyard),
at sorcery speed in its turn and in answer to our spells and combat (`Responds` with
`Rollout.Settings.KnownOnly`). Off is `beam+boardonly`; the whole hand is `beam+casts` / `beam+responds`.

| Speed | Result | Cost |
|---|---|---|
| Quick check | **0 of 288 decisions change**: a known card in hand is rare in self-play, as Shayne expected | 2 992 steps a position (2 963, +1%) |
| `--until-clear` | 96 games, 47–49, 49.0% ± 1.0pp: no difference of 5pp either way | 1 455 steps a decision (1 438, +1%); 234 s |

- **Kept on what it changes, not on the win rate (Shayne: "while win rate is a nice baseline, we
  also want to make sure the plays … make somewhat decent sense").** In a position built by hand
  and agreed with him (our 2/2 and Ferocity of the Hunt; their KNOWN Last Gasp with two Swamps up),
  the board-only search enchants the 2/2 into the removal spell, and this one holds
  (`AiPositionTests.AnAura_IsntCastIntoAKnownRemovalSpell`). With Last Gasp unseen it decides exactly
  as board-only.
- It also makes the token the better bounce at seed 4 move 53 by itself: the bounced creature,
  known, is recast in the look-ahead (AI-9.5's test now runs under `boardonly` to isolate its term).
- The default since. The oracle, built from the default rollout, was re-read (agreement 83.0%).

## Patience with instant-speed cards: AI-13's first rule (2026-10-28, AI-13.2)

Shayne's lessons 1, 4 and 5 as one card-agnostic rule: casting an instant or a flash card at
sorcery timing (our main phase, the stack empty) costs `Evaluator.PatienceCost` (1.0), so casting it
now must beat holding it by that much (`Evaluator.Terms.Patience`; off is `beam+nopatience`). The
search's tree charges it on every such cast in a line, and our rollout policy waits the same way
(`Rollout.Impatience`); a line that ends the game pays nothing.

- **A hold value on the card in hand did nothing (tried first).** The search's best line after
  "Pass" cast the flash card later the same turn, so holding and casting ended the same and still
  tied. The cost has to sit on the cast, on every such cast in a line (a committed plan replays what
  its line did), and in the rollout too: charged in the tree only, it rewarded pushing the cast past
  the tree's edge — a delay, not patience (the /pre-commit review).
- **The position (agreed, Shayne):** our 2/2 and Ferocity of the Hunt against their unseen Last Gasp
  scored a TIE, Pass 5.40 = cast 5.40, and the tie cast it. Now it holds, in the tree and the rollout
  alike (`AFlashAura_IsHeld_NotCastAtTheFirstChance`; without patience, the control casts).
- **#547 of his 10-23 game** (Unsummon held at 5 life) holds since AI-10.4's known-card opponent —
  in their turn, so patience doesn't touch it; kept as a guard (`Unsummon_IsHeld_AtMove547`).
- **Quick check:** 4 of 288 decisions differ from no patience — two Extended Absences on a Jace held,
  Divining Duelist (a 3-mana flash creature) held early, and one Jace activation that becomes a cast
  as the look-ahead's values move. Against the oracle re-read with the rule: 83.0% agreement, score
  given up 0.80 (without: 82.6%, 1.05).
- **Match:** `--match=beam,beam+nopatience --until-clear` ran to its 600-game cap: **53.0% ± 1.0pp,
  318–282** — undecided at 5pp, but "worse" firmly rejected (log-likelihood −26.7) and "better" near
  its bar (2.43 of 2.94). Cost the same (1 342 steps a decision), spells cast a game the same (9.7):
  it waits rather than hoards. (The tree-only first version: 51.0% ± 1.0pp over 96 games.)

## A held instant priced as an option, replacing the patience cost (2026-10-28, AI-13.4)

Shayne: patience "should be about calculating the expected value of playing the spell now vs holding
for a better situation" — the AI-13.2 cost was a timer (the look-ahead spent a held Unsummon in its
next main phase either way). Optimal stopping instead: `CastGains` measures, in 400 self-play games of
the AI's sealed decks (GreedyAi, 70 s), each instant or flash card's OFFER a turn — the best its
casting could gain, settled, or 0; casts that end the game excluded; twenty mid-slice quantiles. A
held one of ours is worth the expected best of the next k offers, k = the sooner of the two clocks,
1 to 4 (`Evaluator.Terms.HoldOption`; off is `beam+nooption`). Every card's median offer is 0: most
turns offer nothing worth casting for.

- **The bounce's value was the real error.** With the option alone, a plain 3/3 still tied: the
  evaluator rated bouncing it a top-10% use of Unsummon, because a creature on their board counted far
  more than the same known card in their hand (half its card value, AI-9.5). Now a known creature in
  their hand is its board value less 0.5 a mana to recast (`RecastPerMana`): a bounce gains tempo.
- **The cap on k, tried on Shayne's positions:** 6 held the Aura'd creature (the bounce 0.8 short);
  4 and 3 got every bounce position right. 4 kept.
- **Positions (agreed, Shayne):** bounce a token, an Aura'd creature, a countered one; hold against a
  plain 3/3 and a vanilla 5-mana 3/4 at 20 life (without the option, the control bounces both); hold
  against Fateshaper Aspirant (a recast repeats its enters); #547 holds. **Ferocity of the Hunt is no
  longer held** (the patience cost held it by taxing every early cast): its risk — their unseen removal
  on the enchanted creature, later — isn't in an offer measured at the cast. It moves to AI-9.4.
- **Quick check:** 20 of 288 decisions change from the option value alone.
- **Match:** `--match=beam,beam+nooption --until-clear` (both with AI-9.2's life taper, since dropped —
  the positions were re-checked without it, all right): 600 games,
  **51.7% ± 1.4pp, 310–290** — undecided at 5pp, "worse" rejected (log-likelihood −11.4); cost +1.6%
  (1 395 against 1 373 steps a decision); spells cast a game the same (10.2).
- **After the /pre-commit review** (the taper dropped; the offers measured with the option value OFF —
  with it on, each run measured the last run's table; each game's last turn kept; the known-creature
  price moved under AI-9.5's `KnownHand`): oracle agreement 82.6% (score given up 1.08) against 78.8%
  without the option value (1.69); 22 of 288 decisions differ without it.
- **Correction: the positions are held by the bounce's VALUE, not the option.** With the option value
  forced off, all twelve position tests still pass — a creature we bounced priced as itself less its
  recast is what holds Unsummon against a plain attacker (the control: `KnownHand` off, it bounces). The
  option value is kept as Shayne's design for "not ASAP, not forever" — no worse in its match, 22 suite
  decisions moved — but no agreed position depends on it yet: Ferocity of the Hunt (AI-9.4) and Ajani's
  Anguish (AI-13.6) are the cases to prove it on.

## Life tapered from 10: tried and dropped (2026-10-28, AI-9.2)

Shayne's pricing (from AI-13.3's positions): a point a life up to 10, half a point to 20, a quarter
above, instead of a point a life to 20 and half above (`Evaluator.Terms.LifeByUse`, the code:
`Taper(Math.Min(life, 20), 10, 1.0, 0.5) + Math.Max(0, life - 20) * 0.25`).

- **It fixed none of his positions:** the plain attackers were bounced for another reason (the bounce's
  value, AI-13.4); with the option value in, every position passes with it off.
- **Match:** `--match=beam,beam+flatlife --until-clear` (both with the option value): 576 games,
  49.3% ± 1.5pp (284–292), no 5pp difference; cost +2.8%.
- **It moves every evaluation, GreedyAi's too:** 16 tests failed with it (GreedyAi's golden games, the
  card-value table, attacking above 20 life, the table's recorded games) — keeping it meant
  regenerating all of those for no measured gain. Dropped before the commit; the formula is above.

## The look-ahead's defender gang-blocks (2026-10-28, AI-15.2)

From Shayne's replay (AI-14, flag 4): the search scored a menace attack into a full board as a WIN —
the look-ahead's blockers were judged one at a time, each blocking only what it survives, so no second
blocker ever joined and a menace attacker was unblockable. Now a blocker joins a block already
declared when, with it, the blockers kill the attacker, or menace needs a second
(`Lookahead.Joins`, `Evaluator.Terms.GangBlocks`; `+nogang` is the old look-ahead).

- **Position** (rebuilt by hand — the game no longer replayed): our 3/3 menace, their two 2/2s, them
  at 3. The attack scored 10000 (a win), not attacking 9022; now 8.69 against 10.80, and it holds —
  Shayne's answer. The control (`+nogang`) attacks.
- **Suite:** 6 of 288 decisions change; cost 3 097 steps a position (3 040, +1.9%). Against the oracle
  as stored (read with the old look-ahead) 81.3% against 82.6%; re-read with the new one, 82.6%
  (score given up 1.09).
- **Match:** `--match=beam,beam+nogang --until-clear`: 576 games, **51.2% ± 1.2pp (295–281), no 5pp
  difference**, "worse" rejected (log-likelihood −13.1); cost 1 488 steps a decision (1 456, +2.1%);
  1 422 s on 16 cores.
- Kept (AI-5's rule: no difference, the agreed position fixed). GreedyAi's golden games moved (4 of
  12) — gang blocks alone, checked by forcing the join off.
- After the review, a menace join needs the two to kill the attacker or the joiner to survive it (no
  chump): 0 of 288 suite decisions and the golden games unchanged, so the match above stands.

## A won line counts the turn's whole evaluation — no more burning our own face (2026-10-29, AI-16)

From AI-14's replay after AI-15 (game 2 seed 1, turn 13, flag 6): Red-Green cast No Admittance at its
own face (20 → 17), White-Blue Jace at 9. Every line burning a face or a Jace won on turn 15 (it did),
scored by AI-10's tie-break `10,000 × 0.95² − their life as our turn ends`: at their face they blocked
everything and stayed at 6 (9019); at ours they took 6 in the attack, 9 → 3 (9022). Our own life never
entered a won line. Counting it too ties (both net 3), and a tie takes the first move — the self-burn.
Now a won line adds our evaluation as the turn ends, squashed under ~214 (`Rollout.ProgressBy.TurnEnd`;
`+lifeprogress` is AI-10's rule, `+noprogress` no tie-break).

- **Position** (kept, `Records/NoAdmittanceAtOwnFace.json` #321): their face 9083, our own face 9052;
  the control (`+lifeprogress`) burns our face. AI-10's Gallia positions attack under both.
- **Suite:** 6 of 288 decisions change (two edicts cast, Yoshimaru's fight activated, Jace −3 over
  Memory Trap, two passes); cost unchanged, 3 097 steps a position. Against the oracle as stored
  80.6% against 82.6%; re-read with the new rule, 82.6% (score given up 1.09).
- **Match:** `--match=beam,beam+lifeprogress --until-clear`: 192 games, **51.6% ± 0.9pp (99–93), no 5pp
  difference**, "worse" rejected (log-likelihood −19.5); cost 1 492 steps a decision (1 514, −1.5%);
  678 s on 16 cores.
- Kept (AI-5's rule: no difference, the recorded blunder fixed). Measured with a ±200 clamp; the review
  replaced it with `cap × tanh(e / cap)`, which keeps every order below the cap — the suite unchanged.

## A lost line keeps the best board (2026-10-29, AI-16.4)

Shayne: "If the AI can only find losing lines, the AI should still try to maximize their board score" —
a later loss first ("it gives your opponent the chance to make a mistake"), then the best board as our
turn ends. Every lost line on one turn used to score −10,000 × the discount exactly, and the tie took the
first move. Now a line lost after our turn adds the turn-end board, squashed under `TurnEndCap` as a won
line's is (`Rollout.Settings.LostLines`; `+nolostlines` is off).

- **Suite:** 0 of 288 decisions change. At the default look-ahead only 2 positions lose on every line
  (the oracle, four turns deep, sees 13); at #119 Pass now outranks spending Academic Ascent (−9505
  against −9557), as it already passed.
- **8 AI games** (`--decisions --games=8 --seed=11`, scanned with the switch off and on): lost positions
  are common in play, and the rule moves many — Academic Ascent and a 0-damage Ajani's Anguish held
  instead of cast for nothing, Jace's −3 to draw, Wrath of the Bloodmane on a Cadet.
- **Position** (kept, `Records/LostLineAnguishForNothing.json` #548): lost on every line, the control
  casts Ajani's Anguish for X = 0; now it keeps it.
- **Match:** `--match=beam,beam+nolostlines --until-clear`: 96 games, **50.0% ± 0.0pp (48–48), no 5pp
  difference** either way, settled at the first check; cost 1 545 steps a decision (1 543, +0.2%);
  275 s on 16 cores. Expected — a better board in a lost position rarely turns the result.
- Kept (Shayne's rule, AI-10.4's: sensible plays count when the match shows no difference).

## "You may" answered by its chooser in the look-ahead (2026-10-29, AI-17.2)

From Shayne's draft game (seed 535739, turn 11): Way of the Deathbringer's Jace drew a card (−3) where
"−2: you may sacrifice a creature. If you do, create a 4/4" with Blessed Ghoul out was the best play. The
look-ahead answered every choice of none-or-one with nothing (`Lookahead.QuickAnswer`: `MinChoices`) —
the sacrifice declined, the −2 scored −48.5 against −20.1 taken — and the plan made as Way of the
Deathbringer was cast drew, replayed without a search. Now `Rollout.Answer` gives such a choice
`GreedyAi.Answer` (none and each option settled, the chooser's best) — `Rollout.Settings.Mays`,
`+nomays` off. `QuickAnswer`, inside every `Settle`, still declines (AI-17's review); a pay-to-keep
keeps the cheap answer (it pays); an optional target takes its chooser's best.

- **Position** (kept, `Records/DeathbringerDrawsNotABeast.json`, one AI from #257 as the table plays):
  Jace −2, the Beast; the control draws.
- **Suite:** 7 of 288 decisions change; 3 103 steps a position (3 097, +0.2%). Against the oracle as
  stored 81.9% against 82.6%; re-read with the new look-ahead, **85.4%** (score given up 1.06, was 1.09).
- **Match:** `--match=beam,beam+nomays --until-clear`: 192 games, **51.0% ± 1.3pp (98–94), no 5pp
  difference**, "worse" rejected (log-likelihood −10.8); 1 509 steps a decision (1 505, +0.2%); 524 s.
- Kept (AI-5's rule: no difference, the recorded blunder fixed).

## Lethal: the early stop on a win this turn, a defender that chumps, targets by their owner (2026-10-29, AI-17.3)

From Shayne's draft game (seed 636312, turn 21): at 3 life with one blocker he wasn't attacked by both
creatures. Any win stopped the search (`Winner` at 2 500), and both root lines won by turn 23; the look-
ahead's cheap defender never chumped, so a lone attacker looked lethal. Three changes, each its switch:

- **(c) `BeamSearchAi.Settings.WinNowOnly`** (`+anywin`): only a win inside the search's own turn stops it
  early. Match 96 games, **50.0% (48–48)**, no 5pp difference; cost +5.2% a decision (1 610 against 1 530).
- **(a) `Evaluator.Terms.ChumpBlocks`** (`+nochump`, `Lookahead.Chumps`): facing lethal, the chumps are
  planned for every blocker still to be asked — the biggest attacker taken by the least valuable that can
  block it, or by the fewest least-valuable ones that kill it together (Shayne: a 4/4 and a 1/1 double-block
  a 5/5; against a 6/6 the 1/1 chumps alone). First version (the blocker asked first chumps): 288 games,
  48.6% ± 1.0 — no 5pp difference but leaning −1.4. Shayne's rule: 96 games, **50.0% ± 1.5 (48–48)**, cost
  unchanged. GreedyAi's golden games moved (5 of 12; the chump rule alone, checked).
- **Targets by their owner's best, the default** (`Rollout.Targets.OwnersBest`; `+firsttarget` the old):
  `--explain` showed #547's bounce looking right because the simulated opponent's Fateshaper Aspirant put
  its +1/+1 counter and indestructible on OUR creature (the first legal target). 288 games, **49.3% ± 1.2
  (142–146)**, no difference (−0.7, half a standard error); +0.4% cost (AI-3.8 measured the same).
- **Positions:** the turn-21 attack is made with either (a) or (c) alone; #547 holds; the least-valuable
  chump's four cases pass.
- **Suite:** 30 of 288 decisions change; 3 531 steps a position (3 103 after AI-17.2, +14% — (c)'s deeper
  search where it already wins). Re-read with the new look-ahead, the oracle **agrees 87.2%** (85.4%) and
  the **score given up halves, 0.59 a position** (1.06); misses a win it found 4 (5).
- **The review** (before the commit): facing lethal the chump plan comes before a block the blocker
  survives (a planned chumper took a safe block elsewhere, leaving the lethal attacker unblocked), and a
  menace attacker with one blocker counts as unblocked. 1 of 288 suite decisions moved (a Cadet no longer
  chumps Massacre Girl), one golden game; re-read, the oracle **87.2%, score given up 0.81** (still under
  1.06). The matches above stand.

## The attack as one decision, in the search's tree (2026-10-29, AI-17.3 (b))

Shayne: "we are launching an attack as one decision". `Ai/WholeAttacks.cs`: at an attack question no
plan decides for, the search's tree offers whole attacks — everyone, no one, each alone, all but one
(each creature at its own best target given the bigger ones), and everyone with one creature at another
target — each rolled out; the chosen one answers the rest of the declaration
(`BeamSearchAi.Settings.WholeAttacks`, `+eachalone` off).

- **Tree only** (Shayne): in the look-ahead too (`Rollout.Attacks.AsOne`, `+asonelookahead`) cost +27.6%
  a suite position (4 513 steps against 3 537); in the tree alone +6.6%.
- **Positions:** the turn-21 attack by whole attacks alone; the all-in pair (two 3/3s into a 4/4 at 3 life)
  found by `AsOne`, not by each alone; AI-10's Gallia attacks with whole attacks alone too. Shayne's walker
  case (Chandra, Torch of Defiance at 4) is a **known defect**, ignored: the whole attack offers the 4/4 at
  Chandra, but the evaluator prices her at 3.40 (body and loyalty, her abilities unpriced — AI-9.3).
- **Suite:** 5 of 288 change; 3 769 steps a position (+6.6%). Re-read, the oracle **87.2%** (unchanged),
  score given up 0.84 (0.81).
- **Match:** `--match=beam,beam+eachalone --until-clear`: 192 games, **49.5% ± 1.7pp (95–97), no 5pp
  difference**; 1 548 steps a decision (1 531, +1.1%); 768 s. Kept (Shayne's positions).

## The review, code: every shortcut in the decision path (2026-10-29, AI-17.4)

Read in full: `BeamSearchAi`, `Rollout`, `Lookahead`, `WholeAttacks`, `Evaluator`, `GreedyAi`, `Hidden`
(~2 600 lines), every `ponytail:` in them, DesignNotes' "MagicCore known ceilings", AI-3's models not
taken, and MtgSimulator's `MultiTurnBeamSearchAiStrategy`. Each shortcut judged by one question: **can it
make the AI miss an obvious play?** Judged from the code; AI-17.5's positions are the test.

**Likely to cause a miss** — each wants a basics position:

1. **`Lookahead.Settle` declines every "you may" and aims every trigger at its first legal target**
   (`QuickAnswer`: `Options.Take(MinChoices)`). AI-17.2/17.3 fixed the rollout's OWN answers, but Settle is
   still the judge of which play is worth making in: `Narrow` (which children get a rollout — 16 at the
   root, **only 5 below it**), our rollout policy (`OurPlay`: the one play a simulated turn makes), the
   simulated opponent's plays, walkers and responses, `GreedyAi.Answer` one choice down, and
   `WholeAttacks`' targets. A creature whose "enters" trigger targets reads as hitting whatever is listed
   first (AI-17.3's Fateshaper Aspirant: our creature); a "you may" play reads as nothing. Below the root,
   where 5 of maybe 10 children survive, such a play is cut before its rollout. Fix: Settle answers a
   single target or a may by each option settled one level down (GreedyAi's way), then measure the cost.
2. **A planeswalker's abilities are unpriced** (AI-9.3, known defect): damage, removal, pumps and mana
   abilities leave the flat body of 2 — so a walker is chipped where it should be killed, and attacking it
   is undervalued. Its own activations are judged by their result in the rollout, so its "best ability"
   choice is likely right; its worth as a target is not.
3. **The simulated opponent judges each attacker alone** (`Rollout.Attacks.EachAlone`; whole attacks are
   in our tree only). An attack of theirs that kills only together — three 2/2s into our one 3/3 at 4 life
   — is never imagined, so the AI can tap out or attack with its last blocker into a lethal swing back
   ("walked into lethal", AI-17.7's second counter). Shayne (AI-17.3): reconsider whole attacks in the
   look-ahead if a test case needs it — this is that case, if a position shows it.
4. **Every priority window the AI holds an instant in costs one of the search's 3 levels.** `AutoPass`
   skips only a lone pass, so with an instant and open mana, main 1 → beginning of combat → the
   declaration is two levels; a land or a precombat spell first puts the whole attack past depth 3, where
   the rollout's each-alone attack judges it. "Removal on the only blocker, then the lethal attack" is the
   play at risk (at the declaration itself the search starts afresh and finds the attack; the miss is
   choosing the precombat play). Fix if a position fails: let `Advance` pass our empty windows (no spell
   on the stack, no combat) without spending a level.
5. **A creature is valued by its stats and keywords only** (AI-9.6): an engine or a lord with a small body
   is "small", so removal goes on the biggest body. A lord's statics show (the board shrinks when it
   dies); a triggered or activated engine shows only through what it makes inside two turns.
6. **A permanent that never untaps is still a creature at full value** — Infinite Coursework ("tap
   enchanted creature … doesn't untap"; FRA common) leaves its target's body counted, and `Through` counts
   it as an attacker in their clock (it reads no "doesn't untap"). So Coursework scores far under a kill
   spell on the same creature, and the AI fears a clock that can't attack.

**Possible** — a position only if one of the above shows it:

7. **Narrow keeps no "potential" lines.** MtgSimulator kept a potential bucket beside the concrete
   score in both its narrowing and its beam (setup plays — fast mana — that score badly at once); here
   both rank by one score, and below the root only 5 children survive. Mostly item 1's cost.
8. **Choices of several picks get one cheap answer, never branched** (`Children` → `GreedyAi.Answer`):
   modes are settled; a discard of N takes the priciest spells unless flooded (`WorstCards`, by mana
   value — a bomb can go); a search for up to N takes the first N; anything else takes the minimum —
   so a surveil of 2 never puts a card in the graveyard. MtgSimulator rolled out every combination.
9. **A committed plan replays while the evaluation stays within 5.0** of what it expected — the plan's
   later moves were judged only by their rollouts at depth, never searched as a root (#262's −3 was
   such a replay). Fixed for mays; the mechanism stays.
10. **The table's 1 s cap leaves lines below the root unexpanded** — a miss seen at the table may not
    reproduce in a match (no cap there). At the declaration the whole attack is the root, so lethal
    on board is found under the cap.
11. **The rollout's own play is one non-land spell a turn, sorcery speed, never a response**; the opponent
    plays only cards we know. Two-spell turns and tricks after the root turn are invisible — symmetric
    enough to rank lines, and measured (AI-3).

**Unlikely to cause an obvious miss** (kept as they are): the threat term's blocker model (menace, first
strike, sizes — the rollout plays the real combat); `Chumps`/`Joins`/`Survives` ignoring trample's excess,
first strike on the attacker's side, lifelink and split damage; a line lost INSIDE the searched turn tying
(AI-16); never responding to our own spell (no FRA line needs it yet); one hidden-card sample per decision;
GreedyAi's mulligan (2–5 lands); a rollout stopped at its decision limit; `TheirLoyalty` taking a minus that
kills its walker; the colour guess's 40/17 deck.

**Compared with MtgSimulator:** its lethal pre-pass (`FindLethalAttacks`) is now the whole attack
"everyone" at the declaration plus `WinNowOnly` (AI-17.3); its land-first override is a move like any
other here (the pass bias and the land's +1.2 take it); its potential bucket (item 7) and its rolled-out
combinations (item 8) were not ported.

## A finished game keeps its board (2026-10-29, AI-17.6)

Shayne's flier (basics position 3): a 3/3 flier and a 2/2 against two 4/4s at 3 life — "only the flyer
attacks". Both attacks won this turn, and `Explain` showed why the 2/2's cost nothing: the look-ahead's
defender, dead whatever it blocks, blocked nothing — every finished game scored a flat ±10 000, so every
block tied and the tie took "no block". Shayne: "it should block anyways … players can always make
mistakes". **`Evaluator.Terms.EndBoard`** (`+flatend`): a finished game is ±WinScore plus its position
squashed under `EndBoardCap` (10), for every chooser; `Winner`'s threshold and `TurnEndCap`'s margin
allow for it (two lines still never cross a turn: 448 against a 475 gap, 406 against 428 at four turns).

- **Positions:** the flier stays home; off, the 2/2 attacks. AI-16's self-burn control switches it off
  (it avoids the self-burn too). A first try — a won-this-turn tie-break by our own side — fixed nothing
  alone and was removed.
- **Golden games:** 6 of 12 moved; all pass with it forced off. Regenerated.
- **Suite:** 3 of 288 change; 3 772 steps a position (+0.1%).
- **Match:** `--match=beam,beam+flatend --until-clear`: 192 games, **49.5% ± 0.9pp (95–97), no 5pp
  difference**; 1 537 steps a decision against 1 545; 872 s. Kept.

## Life near 0, whole attacks in the look-ahead, every combination of a several-pick choice (2026-10-30, AI-17.6)

From Shayne's basics positions (`MagicCore.Tests/Ai/BasicsTests.cs`), each behind its switch:

- **Life risk below 6** (`Evaluator.Terms.LowLifeRisk`, `+flatlowlife`): (6 − life)² ÷ 2 more — 5 → 1 life
  now costs ~20 points, was 8 (Shayne: "As your life total gets lower you take on more risk"). Match: 384
  games, **49.2% ± 1.1pp (189–195), no 5pp difference**; 1 917 steps a decision against 1 900 (+0.9%).
  Golden games: 1 moved (all pass with it forced off).
- **Whole attacks in the look-ahead, the default** (`Rollout.Attacks.AsOne`, `+eachalonelookahead`):
  position 7 (at 5 with a 3/3 against three 2/2s, the blocker over Draw Two) needs BOTH this and the life
  risk — each alone shown not to flip it. Match: 288 games, **49.7% ± 1.6pp (143–145), no 5pp
  difference**; **1 846 steps a decision against 1 504 (+22.7%)** — cost accepted by Shayne.
- **Every combination of a several-pick choice in the tree** (`BeamSearchAi.Settings.BranchPicks`,
  `+onepick`, up to 32): surveil 2 bins two lands when flooded. Match: 96 games, 50.0% (48–48), identical
  games and cost — it never fired in the three decks; kept on its position.
- **Suite** (all three): 21 of 288 change, 4 470 steps a position (+18.5% on 3 772); `+eachalonelookahead`
  alone 8 change and 3 745 steps, `+flatlowlife` 15, `+onepick` 21 (never fires). Accepted; the oracle
  re-read **86.5%, score given up 1.03** (87.2%, 0.84 before AI-17.6) — a little further from the oracle.
- **Tried and reverted:** a share of each line read as our turn ends (Shayne's option A for the Pinger,
  position 9): the Pinger is cast from 0.2, but AI-13.4's agreed Unsummon hold breaks at every share down
  to 0.1 — a uniform "now" can't tell "waiting buys nothing" from "waiting has value". Then Shayne, the position
  spelled out: with a vanilla 2/2 against it, "playing the 3/3 is probably correct" — not a miss; dropped.
- **Positions that already pass:** a one-card tutor fetches the basic the hand needs (landcycling — the
  control, one ply, ties the basics). **Known, ignored:** Chandra at 1 loyalty not attacked (AI-9.3: priced
  2.35, her abilities unpriced).
- **The review (before the commit):** the end-board tie-break was squashed by its own cap (10), and an
  ended game's position is ±60 and more (the dead player's −50 alone), so boards differed by ~1e-5 and past
  ~190 tied exactly again — now squashed over `EndBoardScale` (100). A several-pick choice asking more than
  its options gave no answer at all (the engine allows fewer); interchangeable cards (two Forests) now give
  one answer. 1 of 288 suite decisions moved (Break Under Pressure cast where it passed); accepted, the
  oracle re-read 86.5%, 1.03. AI-16.4's lost-line control now switches the end board off too (it keeps the
  card alone). The `+flatend` match above was run on the saturated version; **re-run on the committed
  one (b817065): 96 games, 51.0% ± 1.0pp (49–47), no 5pp difference**; 1 759 steps a decision against 1 778.

## The cheap look-ahead answers by its chooser's best; a returning graveyard card keeps its value (2026-10-30, AI-17.6)

- **Review item 1, `Evaluator.Terms.SettleChoices`** (`+quicksettle`): `Lookahead.Settle` answers one
  target or a single "you may" by its chooser's best, one level down (two levels tried: not needed). The
  position (`BasicsTests`: a 3-mana "enters, destroy target creature" behind a land drop beside six 1/1s,
  their 5/5): Settle had the destroyer kill ITSELF (ours are listed first), so our rollout never cast it
  and the land's line left the beam. It also needs more than one play a rollout turn (item 11) — from 3 it
  is cast; but from 2 the agreed #547 hold casts Unsummon (the bounce line's imagined next turn casts
  Hortimancer AND returns Proctor), so `ActionsPerTurn` stays 1 (Shayne: item 11 later). Card values:
  Lich's Relic 2 → 16.5, Yoshimaru 4.5 → 16.5 (their "may"/"up to one" now taken); regenerated. Golden
  games 8 moved (all pass with it forced off). Suite: 9 of 288 change, 4 866 steps a position (+8.8%).
  Match: **192 games, 49.5% ± 1.7pp (95–97), no 5pp difference**; 1 894 steps a decision against 1 814
  (+4.4%). Kept.
- **Graveyard access, `Evaluator.Terms.GraveyardAccess`** (`+deadgraveyard`, Shayne: "like paying a cost
  to be able to have access to a card"): a card whose graveyard ability returns it is priced as in hand
  less 0.5 a mana (Blessed Ghoul 2.65 − 1.5 = 1.15). Suite: no decision changes, no cost — it fires on no
  recorded position. It does NOT fix the Deathbringer (Shayne: −2 sacrificing the Ghoul, which comes back):
  fresh at the sacrifice, Tinybones −14.29 against the Ghoul −16.74 — kept, the Ghoul trades with
  Heartstring Puller in the imagined next turn and the Beast then attacks for 4; sacrificed, Tinybones
  blocks a Cadet and the Beast stays home. The graveyard's 1.15 counts in both lines. No match yet.

