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

- [ ] AI-6.1 The cost count: rollouts and engine steps per decision, reported by `--match`; checked
      once against wall clock (the two must rank the AIs alike)
- [ ] AI-6.2 Strength from parallel games: confirm a parallel run's win/loss equals `--sequential`'s
      on the same seeds, measure the wall-clock gain, and make it the default for search runs
- [ ] AI-6.3 Early stopping for the overnight run: the sequential test on mirror pairs, the cap,
      the reason it stopped in the report
- [ ] AI-6.4 The position suite: generate from self-play (seed + decks + moves), replay, the oracle's
      answers stored; report changed-vs-committed and agreement/score given up against the oracle
- [ ] AI-6.5 Prove it on AI-3: re-run one AI-3.3 comparison through the new harness; Commands.md says
      how to run each speed

## Next

Start with AI-6.1 — `AiMatch` (`MagicCore/Ai/AiMatch.cs`) times decisions with a stopwatch; the
search already counts its rollouts (`BeamSearchAi.rollouts`). AI-7.1's profiling uses the same count,
so the two stories meet here. Read `docs/findings/magic-ai.md` (AI-2.3 on the harness, the AI-3
sections for what a run costs).

## Log

- 2026-10-19: created from an interview with Shayne after AI-3 was paused for slow runs.
