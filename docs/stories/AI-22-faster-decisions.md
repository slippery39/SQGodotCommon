---
id: AI-22
title: Faster AI decisions — sealed runs in minutes, not hours
status: done
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

- [x] AI-22.1 Profile: a sealed run's decisions (pool 4's RGb, a Rok-Tahk board, a Tribble board), by method — done
      2026-11-12 on RGb (findings "AI-22.1"): rollouts 90%, the rollout's attack plans 50%, AI-13.12's free passes 36%,
      the engine's per-step checks ~40%; a Tribble board not yet profiled
- [x] AI-22.2 The levers, ranked by what the profile shows, each measured (suite changed 0, cost per decision)
- [x] AI-22.3 AI-21's steps and AI-18.1 where the profile points at them — moved: AI-21 (attack plans), AI-24 (the rest)
- [x] AI-22.4 The before/after sealed run, in `docs/findings/magic-ai.md` — the engine shortcuts −11%, every game identical
- [x] AI-22.5 → AI-24 (Claude's proposal, 2026-11-12, for Shayne) **A game's cap in engine steps, not minutes** — sealed runs,
      the scan and self-play stop a game past 5 wall-clock minutes, so under load more games stop and the RESULT
      changes (`.claude/rules/two-lanes.md`: quiet-machine tasks). A cap counted in engine steps (the cost line's unit,
      deterministic) makes them "any time" tasks — the same answer however busy the machine is; the minutes stay as a
      backstop against a hang

## Next

**Done (2026-11-13, Shayne closed it):** the engine shortcuts kept — on, no switch — a sealed run ~11% faster with every
move identical. Everything else (shortlist2, freepasses2, the step cap, no-blockers, the state-based audit, mana
caching, twin plans) is AI-24, to go over later.

## Log

- 2026-11-12: created from Shayne's sprint-5 notes.
- 2026-11-12: AI-22.1 started — a profile of pool 4's RGb, 8 opponents on both seats (`dotnet-trace`, thread-time samples). AI-22.5 proposed.
- 2026-11-12: AI-22.1 done (findings): the CardsIn guess tested and reverted — the leaves misattribute.
- 2026-11-12/13: AI-22.2 overnight — four switches tried (shortlist2/1, freepasses2/1), two matched, three ideas costed and not built (findings); `--builds … --ai=NAME`.
- 2026-11-13: the engine shortcuts built behind `MAGIC_SHORTCUTS` (sealed −11%, decisions identical); twin plans tried, reverted (findings).
- 2026-11-13: closed (Shayne). The engine shortcuts made permanent (the switch removed, tests and suite re-run); the rest moved to AI-24.
