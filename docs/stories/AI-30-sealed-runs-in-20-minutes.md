---
id: AI-30
title: Sealed runs in 20 minutes — a pool's question answered while Shayne waits, not in 3 hours
status: ready
area: AI
created: 2026-11-18
---

## Why

Shayne, sprint 5 review (2026-11-18): "the simulations too are still just way too long. We really need to try to find
ways to optimize, either from a direct engine level (which would obviously take modifications to ImmutableGameObjects),
or from AI optmizations. But I would say that is a huge bottleneck that will be a hinderance to many things I want to
do. Like it would be nice if I only had to wait 10-20 minutes for a sealed simulation run results, not 3 hours."

Where it stands (2026-11-18): pool 5's three runs (LIM-1.12) took 52, 51 and 75 min — 200 games each, 50 opponents,
both seats, `--game-minutes=6` — and 41, 37 and 84 of their 200 games were stopped at the cap, giving no result. AI-22
made a sealed run ~11% faster on 2026-11-13, but the quality work since (AI-20's two futures for each unseen draw,
AI-26's and AI-27's pricing) **doubled the cost of a decision**: the position suite 10 594 → 20 078 engine steps a
position, 49 s → 121 s. AI-22.1's profile (before the doubling): the rollouts 90% of a decision, the rollout's attack
plans (`WholeAttacks.Best`) 50%, free passes 36%, the engine's per-step checks the third block.

## Requirements

- [ ] Pool 5's run against the AI field (`ai_reports/sealed-builds-2026-11-18_164356.md`'s command) finishes in
      20 minutes or less on a quiet machine
- [ ] Its play held: the position suite's changed decisions read and accepted, a match no worse
- [ ] Stopped games a small share of a run, not a fifth to two fifths

## Decisions

- 2026-11-18, Shayne (sprint 6 planning): speed first this sprint, before the AI-quality items. A sealed run may also
  stop early once its answer is clear, as `--until-clear` does for matches — "a good stopgap for now".

## Tasks

- [ ] AI-30.1 Re-profile a sealed run at today's default (AI-22.1's method): where the doubling went
- [ ] AI-30.2 The stopped games: what makes them long (boards, decisions), and their share of a run's machine time
- [ ] AI-30.3 Stop a sealed run early once its paired difference is clear (the stopgap)
- [ ] AI-30.4 The levers the profile points at, each measured (suite changed, cost per decision, a sealed run's wall
      time): AI-21's attack plans (AI-21.1–21.5), AI-24's leftovers, the engine's per-step checks in MagicCore and
      ImmutableGameObjects
- [ ] AI-30.5 The before/after sealed run, in `docs/findings/magic-ai.md`

## Next

Start at AI-30.1 on a quiet machine (ask Shayne). Read AI-22 (done) and its findings ("AI-22.1", "AI-22.2" in
`docs/findings/magic-ai.md`), AI-21's Next and AI-24. `dotnet-trace` is in `~/.dotnet/tools`.

## Log

- 2026-11-18: created at the sprint 5 review.
