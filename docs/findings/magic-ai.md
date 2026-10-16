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
committed), Release, `--match=beam,greedy --games=72 --sequential` (games one at a time; each
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
