---
id: AI-22
title: Faster AI decisions — sealed runs in minutes, not hours
status: ready
area: AI
created: 2026-11-12
---

## Why

Sealed testing (LIM-1.12, `/sealed-test`) is bottlenecked on the search's speed. Shayne (2026-11-12): "I can't wait
hours at a time for answers on a specific deckbuild or 23rd card simulation … even a 2x speedup would be relevant, a
10x speedup would be incredible". Pool 4's runs stopped 18% of games at the 6-minute cap (Rok-Tahk: every creature a mana
source); AI-13.12 cost +52% a decision (`docs/findings/magic-ai.md`); the TRK self-play stalled on Tribble boards (AI-21).
AI-7 profiled and optimised the search once (done) — this is the second pass, aimed at sealed runs.

## Requirements

- [ ] Measured first: a profile of a real sealed run (`--builds … --both-seats`), where the time goes.
- [ ] Each change either decides exactly as before (the position suite's "changed" 0, AI-7's rule) or is its own
      switch, measured by a match and agreed with Shayne.
- [ ] A sealed run of 50 opponents, both seats, on the same machine: the time before and after, in findings.
- [ ] Target 2×; 10× is a hope, not a promise.

## Decisions

- 2026-11-12, Shayne: speed goes BEFORE AI-20. Claude's proposal, agreed: only what AI-20 won't rewrite — the engine's
  per-step cost, the rollout's hot loop, AI-13.12's cost, big boards (AI-21) and tap-cost moves (AI-18.1); the search's
  structure waits for AI-20's shape (several guesses multiply its cost).

## Tasks

- [ ] AI-22.1 Profile: a sealed run's decisions (pool 4's RGb, a Rok-Tahk board, a Tribble board), by method
- [ ] AI-22.2 The levers, ranked by what the profile shows, each measured (suite changed 0, cost per decision)
- [ ] AI-22.3 AI-21's steps and AI-18.1 where the profile points at them
- [ ] AI-22.4 The before/after sealed run, in `docs/findings/magic-ai.md`

## Next

AI-22.1. Read AI-7's story and its findings first (what was already done). Cost is deterministic (engine steps,
rollouts — `--suite=beam`'s "cost per position"); wall clock only for the sealed run itself.

## Log

- 2026-11-12: created from Shayne's sprint-5 notes.
