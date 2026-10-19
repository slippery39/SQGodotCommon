---
id: AI-6
title: Measure an AI change in minutes, or overnight
status: ready
area: AI
created: 2026-10-19
---

## Why

AI-3 stalled on its own measurements: a search-vs-search match is 156 games and 1.5–2 hours
(2+ with the casting opponent), and each model needed two or three. Worse, that wait bought little
— at 156 games the standard error is ~±3pp, so only effects of ~6pp+ show, and every AI-3 result
landed at 1.3–2 SE ("not shown"). Waiting two hours to test a change is too much (Shayne,
2026-10-18). The faster AI itself is AI-7; this story is the harness.

## Requirements

- [ ] **Two speeds:** a quick check that takes minutes while working, and an overnight run before
      anything becomes a default
- [ ] **The quick check is a position suite**, run on demand from the console: positions from the
      search's self-play; each version decides in each; it reports (a) how many decisions differ
      from the committed AI, and where, and (b) how often it agrees with an ORACLE — a much deeper
      search, its answers computed once and stored — and how much score its choice gives up
- [ ] Positions are stored as seed + decks + the moves made, replayed in milliseconds (MagicCore has
      no state serializer, and doesn't need one for this); regenerated when the engine changes the
      order of legal moves — and a stale file says so rather than replaying wrong positions
- [ ] **The overnight run stops early** once a difference of 5pp or more — or its absence — is
      settled (a sequential test on mirror pairs), capped at ~600 games; it may use every core
- [ ] **Cost is a deterministic count** — rollouts and engine steps per decision — reported beside
      the result, identical on every run and machine; wall clock only as an occasional check
- [ ] Win/loss from games played in parallel, proven identical to `--sequential` (the results are
      deterministic; only time needed one game at a time, and the count replaces that)

## Decisions

2026-10-19, Shayne (interview):
- **Two speeds** — a minutes-long quick check plus an overnight run before a default.
- **Only big effects matter: 5pp or more.** Smaller changes are kept or dropped on reasoning.
- **Time is a deterministic count** (rollouts, engine steps), wall clock checked now and then.
- **Faster measurement and a faster AI together** — this story and AI-7, in parallel.
- **The quick check is a position suite**, comparing against BOTH the committed AI (what changed)
  and an oracle computed once (better or worse).
- **Positions from search self-play**, stored as seed + decks + moves.
- **The overnight run stops early, capped** (~600 games).
- **Runs may use every core.**
- **The quick check runs on demand** (a console command), not in /pre-commit.

Defaults, not asked: the early stop tests "no difference" against "+5pp" at 5% error each way; the
oracle is the search at several times the rollout budget and depth — sized once it is measured.

## Tasks

- [x] AI-6.1 The cost count: rollouts and engine steps per decision, reported by `--match`; checked
      once against wall clock (the two must rank the AIs alike) — they do; it undercounts evaluation
- [x] AI-6.2 Strength from parallel games: confirm a parallel run's win/loss equals `--sequential`'s
      on the same seeds, measure the wall-clock gain, and make it the default for search runs —
      identical games, 97 min → 16 min (6.2×) with the server garbage collector
- [ ] AI-6.3 Early stopping for the overnight run: the sequential test on mirror pairs, the cap,
      the reason it stopped in the report
- [ ] AI-6.4 The position suite: generate from self-play (seed + decks + moves), replay, the oracle's
      answers stored; report changed-vs-committed and agreement/score given up against the oracle
- [ ] AI-6.5 Prove it on AI-3: re-run one AI-3.3 comparison through the new harness; Commands.md says
      how to run each speed

## Next

**AI-6.2 done; next AI-6.3** — early stopping for the overnight run: a sequential test on mirror
pairs ("no difference" against "+5pp", 5% error each way), capped at ~600 games, the reason it
stopped in the report. A 156-game search-vs-search match is now ~16 min (findings, AI-6.2), so
the cap is ~1 h. Games run in parallel by default, one thread per search, one game per core, on
the server garbage collector — keep `<ServerGarbageCollection>` in MagicConsole and the tests.
The count: `MagicRules.CountingSteps(() => ai.Decide(s))` and `BeamSearchAi.Rollouts`. Read `docs/findings/magic-ai.md` (AI-2.3 on the harness, the AI-3
sections for what a run costs).

## Log

- 2026-10-19: created from an interview with Shayne after AI-3 was paused for slow runs.
- 2026-10-19: AI-6.1 — engine steps counted in `MagicRules.Act`/`Choose`; `--match` prints "cost
  per decision (deterministic)" (steps, and `BeamSearchAi.Rollouts`). `Settings.Threads` added for
  the test and AI-6.2. Tests: one decision costs exactly the same on every core and on one thread;
  a GreedyAi mirror costs alike from both seats. **Repeating the same decision ten times did NOT
  catch an accounting mistake** — the threads split the work alike each time; comparing against a
  single thread does. Checked against the clock (findings): same ranking on three pairs;
  evaluations are not counted (casts: +24% by steps, +35% by time).
- 2026-10-19: /pre-commit fixes to AI-6.1 — the first cut was a thread-static count plus the
  search adding its workers' counts by thread id: any AI or future parallel code outside that one
  loop would have gone uncounted. Now `MagicRules.CountingSteps` opens a count that flows with the
  execution context into `Parallel.For` (checked: 64 of 64 across 17 threads), for any AI.
  `Threads: 1` is a plain loop (Parallel.For hands even one thread's work to the pool); `Threads`
  below 1 is every core, not a crash; the cost fields are required; `Choose` counts only a valid
  choice; the test reuses its probe. Revert-checked: suppressing the context's flow, and not
  counting `Act`, each fail it. MagicCore.Tests 948 green.
- 2026-10-19: AI-6.2 — parallel games run each search on one thread (`AiMatch.Create`'s
  `searchThreads`; every search name now resolves to its settings in one place), one game per core.
  Proven identical to `--sequential`: `ParallelGames_PlayTheSameGames_AsOneAtATime` (12 games) and AI-3.3's 156-game match reproduced exactly four times. **Found: the workstation garbage
  collector was the bottleneck** — capping games at the cores made it SLOWER (69 min against 31
  uncapped), because threads waited on collections; the server collector made the same 48 games 5×
  faster and the full match 16 min (6.2× AI-3.3's). Server GC on in MagicConsole and the tests
  (suite 59 → 36 s). Parallel timings are labelled "not evidence" in the report.
- 2026-10-19: /pre-commit fixes to AI-6.2 — the equality test listed twelve fields by hand and
  missed `Finished`, lands and spells (it now compares whole games, the clock zeroed, so a new field
  is compared without being listed); the normal suite now checks that every kind of search name
  takes the thread count (`EverySearch_TakesTheThreadCount`); `IsKnown` reads names only, so a
  broken search can't throw from it; the report's parallel flag is required.
