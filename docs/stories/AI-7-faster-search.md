---
id: AI-7
title: A faster search — profiled, then optimised without changing a decision
status: ready
area: AI
created: 2026-10-19
---

## Why

Every measurement and every move on the table pays for the search's speed: ~250 ms a decision,
up to 200 rollouts at ~22 ms each. Measured so far (findings, AI-2.4): attack declarations are ~45%
of a rollout, our policy's checks ~35%, the engine's priority passes ~20% (~0.09 ms each). A faster
search shortens AI-6's runs and the table's thinking alike. Taken up together with AI-6 (Shayne,
2026-10-19).

## Requirements

- [ ] Profiled first: where a decision's time goes — engine, rollout policy, evaluator, allocation —
      recorded in `docs/findings/magic-ai.md`, using AI-6's cost count
- [ ] **A pure optimisation changes no decision** — AI-6's position suite shows zero changed
      decisions against the committed AI (and `GoldenAiReplay` for GreedyAi); anything that does
      change one is a behaviour change, measured like one
- [ ] Each optimisation reports its speedup by the cost count and by wall clock

## Decisions

- 2026-10-19, Shayne: **profile first, decide later** whether optimising the shared
  ImmutableGameObjects engine is worth it (it would be a `CMN-` story, numbered on main and
  cherry-picked); until then MagicCore and its AI only.

Open questions:
- Which optimisations to take, once the profile is in (AI-7.2).

## Tasks

- [ ] AI-7.1 Profile a decision and a rollout over real games; record where the time goes
- [ ] AI-7.2 Choose the optimisations with Shayne from the profile — and whether the shared engine is in
- [ ] AI-7.3 … one task per optimisation, each proven decision-neutral by the position suite

## Next

**Found in AI-6.2: the workstation garbage collector paused every thread** — the server collector
made parallel games 5× faster (findings). Check whether the TABLE gets it: Godot hosts its own .NET
runtime, so `<ServerGarbageCollection>` in a csproj may not reach it; a multi-threaded search on the
table allocates in parallel just the same. Allocation is the likely next lead (a new state per step).

AI-7.1 can start alongside AI-6.1 — the profile wants AI-6.1's count. Leads already measured: attack
declarations settle every option through combat (AI-2.4); `CreatureValue` allocates a keyword array
per creature and reads toughness three times (AI-2.2's review); `Hidden.Sample` rebuilds both
libraries at every decision, even when nothing can move (AI-3.3's review).

## Log

- 2026-10-19: created from an interview with Shayne after AI-3 was paused for slow runs.
